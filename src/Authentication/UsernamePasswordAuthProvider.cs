using System.Text.Json;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Nacos.Config.Models;
using Nacos.Config.Transport;
using Nacos.Config.Utils;

namespace Nacos.Config.Authentication;

/// <summary>
///     Username/password authentication provider with automatic token refresh
/// </summary>
public class UsernamePasswordAuthProvider : IAuthenticationProvider, IDisposable
{
    private readonly IHttpClientFactory _httpClientFactory;
    private readonly IServerSelector _serverSelector;
    private readonly NacosConfigOptions _options;
    private readonly ILogger<UsernamePasswordAuthProvider> _logger;
    private readonly SemaphoreSlim _loginLock = new(1, 1);
    private readonly TokenInfo _tokenInfo = new();

#if NET6_0_OR_GREATER
    private PeriodicTimer? _refreshTimer;
#else
    private Timer? _refreshTimer;
#endif
    private CancellationTokenSource? _refreshCts;

    public bool IsEnabled { get; }

    public UsernamePasswordAuthProvider(
        IHttpClientFactory httpClientFactory,
        IServerSelector serverSelector,
        IOptions<NacosConfigOptions> options,
        ILogger<UsernamePasswordAuthProvider> logger)
    {
        _httpClientFactory = httpClientFactory;
        _serverSelector = serverSelector;
        _options = options.Value;
        _logger = logger;

        IsEnabled = !string.IsNullOrWhiteSpace(_options.UserName);
    }

    public async Task InitializeAsync(CancellationToken cancellationToken = default)
    {
        if (!IsEnabled)
        {
            _logger.LogDebug("Username/password authentication is not enabled");
            return;
        }

        await LoginAsync(cancellationToken).ConfigureAwait(false);

        // Start background token refresh
        StartTokenRefreshTimer();
    }

    public async Task<bool> EnsureAuthenticatedAsync(CancellationToken cancellationToken = default)
    {
        if (!IsEnabled)
        {
            return true;
        }

        if (!_tokenInfo.IsValid())
        {
            _logger.LogDebug("Token is invalid or expired, refreshing...");
            return await LoginAsync(cancellationToken).ConfigureAwait(false);
        }

        return true;
    }

    public void ApplyAuthentication(HttpRequestMessage request)
    {
        if (!IsEnabled || string.IsNullOrEmpty(_tokenInfo.AccessToken))
        {
            return;
        }

        // Add access token to headers
        request.Headers.TryAddWithoutValidation(NacosConstants.AccessTokenHeader, _tokenInfo.AccessToken);
    }

    public void ApplyAuthentication(IDictionary<string, string> parameters)
    {
        if (!IsEnabled)
        {
            _logger.LogWarning("ApplyAuthentication called but auth is NOT enabled");
            return;
        }

        if (string.IsNullOrEmpty(_tokenInfo.AccessToken))
        {
            _logger.LogWarning(
                "ApplyAuthentication called but AccessToken is NULL or EMPTY. TokenTtl: {Ttl}, LastRefresh: {LastRefresh}",
                _tokenInfo.TokenTtl, _tokenInfo.LastRefreshTime);
            return;
        }

        // Add access token to parameters
        parameters[NacosConstants.AccessTokenHeader] = _tokenInfo.AccessToken!;
        _logger.LogDebug("Applied accessToken to parameters: {TokenPreview}...",
            _tokenInfo.AccessToken.Length > 20 ? _tokenInfo.AccessToken.Substring(0, 20) : _tokenInfo.AccessToken);
    }

    private async Task<bool> LoginAsync(CancellationToken cancellationToken)
    {
        await _loginLock.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            // Double-check after acquiring lock
            if (_tokenInfo.IsValid())
            {
                return true;
            }

            var servers = _serverSelector.GetAllServers();
            foreach (var server in servers)
            {
                if (await TryLoginToServerAsync(server, cancellationToken).ConfigureAwait(false))
                {
                    return true;
                }
            }

            _logger.LogError("Failed to login to all Nacos servers");
            return false;
        }
        finally
        {
            _loginLock.Release();
        }
    }

    private async Task<bool> TryLoginToServerAsync(string serverAddress, CancellationToken cancellationToken)
    {
        try
        {
            var httpClient = _httpClientFactory.CreateClient("NacosConfigClient");
            var contextPath = _options.ContextPath.Trim('/');
            var loginUrl = $"{serverAddress}/{contextPath}{NacosConstants.LoginPath}";

            _logger.LogInformation("Attempting login to {Url}", loginUrl);

            var loginData = new Dictionary<string, string>
            {
                ["username"] = _options.UserName!,
                ["password"] = _options.Password ?? string.Empty
            };

            _logger.LogDebug("Login request - username: {Username}", _options.UserName);

            using var content = new FormUrlEncodedContent(loginData);
            using var cts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
            cts.CancelAfter(TimeSpan.FromMilliseconds(5000)); // 5 second login timeout

            _logger.LogDebug("Sending POST request to login endpoint");
            var response = await httpClient.PostAsync(loginUrl, content, cts.Token).ConfigureAwait(false);

            _logger.LogInformation("Login response status: {StatusCode}", response.StatusCode);

            if (!response.IsSuccessStatusCode)
            {
                var errorContent = await response.Content.ReadAsStringAsync().ConfigureAwait(false);
                _logger.LogWarning("Login failed to {Server}: {StatusCode}, {Error}",
                    serverAddress, response.StatusCode, errorContent);
                return false;
            }

            var responseContent = await response.Content.ReadAsStringAsync().ConfigureAwait(false);
            _logger.LogDebug("Login response content: {Content}", responseContent);

            using var jsonDoc = JsonDocument.Parse(responseContent);
            var root = jsonDoc.RootElement;

            if (root.TryGetProperty(NacosConstants.AccessTokenHeader, out var accessTokenElement) &&
                root.TryGetProperty(NacosConstants.TokenTtlField, out var ttlElement))
            {
                _tokenInfo.AccessToken = accessTokenElement.GetString();
                _tokenInfo.TokenTtl = ttlElement.GetInt64();
                _tokenInfo.LastRefreshTime = DateTimeOffset.UtcNow;

                _logger.LogInformation("Successfully logged in to {Server}, token TTL: {Ttl}s",
                    serverAddress, _tokenInfo.TokenTtl);

                return true;
            }

            _logger.LogWarning("Login response from {Server} missing required fields. Response: {Content}",
                serverAddress, responseContent);
            return false;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex,
                "Exception during login to {Server}. Exception type: {ExceptionType}, Message: {Message}",
                serverAddress, ex.GetType().Name, ex.Message);
            return false;
        }
    }

    /// <summary>
    ///     Calculate the refresh interval based on token TTL.
    ///     Uses 80% of TTL, with a minimum of 30 seconds and maximum of 5 minutes.
    /// </summary>
    private TimeSpan CalculateRefreshInterval()
    {
        const int MinRefreshSeconds = 30;
        const int MaxRefreshSeconds = 300; // 5 minutes
        
        if (_tokenInfo.TokenTtl <= 0)
        {
            return TimeSpan.FromSeconds(MinRefreshSeconds);
        }
        
        // Refresh at 80% of token lifetime
        var refreshSeconds = (long)(_tokenInfo.TokenTtl * 0.8);
        
        // Clamp between min and max
        refreshSeconds = Math.Max(MinRefreshSeconds, Math.Min(MaxRefreshSeconds, refreshSeconds));
        
        return TimeSpan.FromSeconds(refreshSeconds);
    }

    private void StartTokenRefreshTimer()
    {
        var refreshInterval = CalculateRefreshInterval();
        _logger.LogInformation("Starting token refresh timer with interval: {Interval}s (TTL: {Ttl}s)", 
            refreshInterval.TotalSeconds, _tokenInfo.TokenTtl);

#if NET6_0_OR_GREATER
        _refreshCts = new CancellationTokenSource();
        _refreshTimer = new PeriodicTimer(refreshInterval);

        _ = Task.Run(async () =>
        {
            while (await _refreshTimer.WaitForNextTickAsync(_refreshCts.Token).ConfigureAwait(false))
            {
                try
                {
                    await EnsureAuthenticatedAsync(_refreshCts.Token).ConfigureAwait(false);
                    
                    // Note: PeriodicTimer interval cannot be changed after creation,
                    // but EnsureAuthenticatedAsync will handle token refresh when needed
                }
                catch (OperationCanceledException) when (_refreshCts.Token.IsCancellationRequested)
                {
                    break;
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Error during token refresh");
                }
            }
        }, _refreshCts.Token);
#else
        _refreshTimer = new Timer(
            async _ =>
            {
                try
                {
                    await EnsureAuthenticatedAsync().ConfigureAwait(false);
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Error during token refresh");
                }
            },
            null,
            refreshInterval,
            refreshInterval);
#endif

        _logger.LogDebug("Started token refresh timer");
    }

    public void Dispose()
    {
#if NET6_0_OR_GREATER
        _refreshTimer?.Dispose();
        _refreshCts?.Cancel();
        _refreshCts?.Dispose();
#else
        _refreshTimer?.Dispose();
#endif
        _loginLock.Dispose();
    }
}