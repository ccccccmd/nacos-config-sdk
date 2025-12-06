using System.Text;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Nacos.V2.Config.Authentication;
using Nacos.V2.Config.Models;
using Nacos.V2.Config.Transport;
using Nacos.V2.Config.Utils;

namespace Nacos.V2.Config.Client;

/// <summary>
/// Nacos configuration HTTP API client implementation
/// </summary>
public class NacosConfigClient : INacosConfigClient
{
    private readonly IHttpTransport _transport;
    private readonly IAuthenticationProvider _authProvider;
    private readonly NacosConfigOptions _options;
    private readonly ILogger<NacosConfigClient> _logger;

    public NacosConfigClient(
        IHttpTransport transport,
        IAuthenticationProvider authProvider,
        IOptions<NacosConfigOptions> options,
        ILogger<NacosConfigClient> logger)
    {
        _transport = transport;
        _authProvider = authProvider;
        _options = options.Value;
        _logger = logger;
    }

    public async Task<ConfigData?> GetConfigAsync(
        ConfigKey key,
        long timeoutMs,
        CancellationToken cancellationToken = default)
    {
        ConfigValidator.ValidateKey(key.DataId, key.Group);

        await _authProvider.EnsureAuthenticatedAsync(cancellationToken).ConfigureAwait(false);

        var parameters = new Dictionary<string, string>
        {
            ["dataId"] = key.DataId,
            ["group"] = key.Group
        };

        if (!string.IsNullOrEmpty(key.Tenant))
        {
            parameters["tenant"] = key.Tenant;
        }

        // Apply authentication
        _authProvider.ApplyAuthentication(parameters);

        // Build query string
        var queryString = BuildQueryString(parameters);
        var path = $"{NacosConstants.ConfigControllerPath}?{queryString}";

        using var request = new HttpRequestMessage(HttpMethod.Get, path);
        request.Headers.Add("notify", "false");
        AddCommonHeaders(request);

        using var cts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        cts.CancelAfter(TimeSpan.FromMilliseconds(timeoutMs));

        try
        {
            var response = await _transport.SendAsync(request, cts.Token).ConfigureAwait(false);

            if (response.StatusCode == System.Net.HttpStatusCode.OK)
            {
                var content = await response.Content.ReadAsStringAsync().ConfigureAwait(false);

                string? contentType = null;
                if (response.Headers.TryGetValues(NacosConstants.ConfigTypeHeader, out var values))
                {
                    contentType = values.FirstOrDefault();
                }

                var md5 = HashUtils.GetMd5(content);

                _logger.LogInformation("Retrieved config {DataId}/{Group}, MD5: {Md5}",
                    key.DataId, key.Group, md5);

                return new ConfigData(content, contentType, md5);
            }
            else if (response.StatusCode == System.Net.HttpStatusCode.NotFound)
            {
                _logger.LogWarning("Config {DataId}/{Group} not found", key.DataId, key.Group);
                return null;
            }
            else if (response.StatusCode == System.Net.HttpStatusCode.Forbidden)
            {
                var error = await response.Content.ReadAsStringAsync().ConfigureAwait(false);
                _logger.LogError("Access denied for config {DataId}/{Group}: {Error}",
                    key.DataId, key.Group, error);
                throw new UnauthorizedAccessException($"Access denied: {error}");
            }
            else
            {
                var error = await response.Content.ReadAsStringAsync().ConfigureAwait(false);
                _logger.LogError("Failed to get config {DataId}/{Group}: {StatusCode}, {Error}",
                    key.DataId, key.Group, response.StatusCode, error);
                throw new HttpRequestException($"Failed to get config: {response.StatusCode}");
            }
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            _logger.LogInformation("GetConfig cancelled by user");
            throw;
        }
        catch (Exception ex) when (ex is not UnauthorizedAccessException)
        {
            _logger.LogError(ex, "Error getting config {DataId}/{Group}", key.DataId, key.Group);
            throw;
        }
    }

    public async Task<bool> PublishConfigAsync(
        ConfigKey key,
        string content,
        string type = "text",
        CancellationToken cancellationToken = default)
    {
        ConfigValidator.ValidateKey(key.DataId, key.Group);
        ConfigValidator.ValidateContent(content);

        await _authProvider.EnsureAuthenticatedAsync(cancellationToken).ConfigureAwait(false);

        var parameters = new Dictionary<string, string>
        {
            ["dataId"] = key.DataId,
            ["group"] = key.Group,
            ["content"] = content,
            ["type"] = type
        };

        if (!string.IsNullOrEmpty(key.Tenant))
        {
            parameters["tenant"] = key.Tenant;
        }

        // Apply authentication
        _authProvider.ApplyAuthentication(parameters);

        using var request = new HttpRequestMessage(HttpMethod.Post, NacosConstants.ConfigControllerPath);
        request.Content = new FormUrlEncodedContent(parameters);
        AddCommonHeaders(request);

        using var cts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        cts.CancelAfter(TimeSpan.FromMilliseconds(_options.DefaultTimeoutMs));

        try
        {
            var response = await _transport.SendAsync(request, cts.Token).ConfigureAwait(false);

            if (response.StatusCode == System.Net.HttpStatusCode.OK)
            {
                _logger.LogInformation("Published config {DataId}/{Group}", key.DataId, key.Group);
                return true;
            }
            else if (response.StatusCode == System.Net.HttpStatusCode.Forbidden)
            {
                var error = await response.Content.ReadAsStringAsync().ConfigureAwait(false);
                _logger.LogError("Access denied for publishing config {DataId}/{Group}: {Error}",
                    key.DataId, key.Group, error);
                throw new UnauthorizedAccessException($"Access denied: {error}");
            }
            else
            {
                var error = await response.Content.ReadAsStringAsync().ConfigureAwait(false);
                _logger.LogWarning("Failed to publish config {DataId}/{Group}: {StatusCode}, {Error}",
                    key.DataId, key.Group, response.StatusCode, error);
                return false;
            }
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            _logger.LogInformation("PublishConfig cancelled by user");
            throw;
        }
        catch (Exception ex) when (ex is not UnauthorizedAccessException)
        {
            _logger.LogError(ex, "Error publishing config {DataId}/{Group}", key.DataId, key.Group);
            throw;
        }
    }

    public async Task<bool> RemoveConfigAsync(
        ConfigKey key,
        CancellationToken cancellationToken = default)
    {
        ConfigValidator.ValidateKey(key.DataId, key.Group);

        await _authProvider.EnsureAuthenticatedAsync(cancellationToken).ConfigureAwait(false);

        var parameters = new Dictionary<string, string>
        {
            ["dataId"] = key.DataId,
            ["group"] = key.Group
        };

        if (!string.IsNullOrEmpty(key.Tenant))
        {
            parameters["tenant"] = key.Tenant;
        }

        // Apply authentication
        _authProvider.ApplyAuthentication(parameters);

        var queryString = BuildQueryString(parameters);
        var path = $"{NacosConstants.ConfigControllerPath}?{queryString}";

        using var request = new HttpRequestMessage(HttpMethod.Delete, path);
        AddCommonHeaders(request);

        using var cts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        cts.CancelAfter(TimeSpan.FromMilliseconds(_options.DefaultTimeoutMs));

        try
        {
            var response = await _transport.SendAsync(request, cts.Token).ConfigureAwait(false);

            if (response.StatusCode == System.Net.HttpStatusCode.OK)
            {
                _logger.LogInformation("Removed config {DataId}/{Group}", key.DataId, key.Group);
                return true;
            }
            else if (response.StatusCode == System.Net.HttpStatusCode.Forbidden)
            {
                var error = await response.Content.ReadAsStringAsync().ConfigureAwait(false);
                _logger.LogError("Access denied for removing config {DataId}/{Group}: {Error}",
                    key.DataId, key.Group, error);
                throw new UnauthorizedAccessException($"Access denied: {error}");
            }
            else
            {
                var error = await response.Content.ReadAsStringAsync().ConfigureAwait(false);
                _logger.LogWarning("Failed to remove config {DataId}/{Group}: {StatusCode}, {Error}",
                    key.DataId, key.Group, response.StatusCode, error);
                return false;
            }
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            _logger.LogInformation("RemoveConfig cancelled by user");
            throw;
        }
        catch (Exception ex) when (ex is not UnauthorizedAccessException)
        {
            _logger.LogError(ex, "Error removing config {DataId}/{Group}", key.DataId,key.Group);
            throw;
        }
    }

    public async Task<List<ConfigKey>> ListenConfigChangesAsync(
        IEnumerable<(ConfigKey Key, string Md5)> configs,
        long timeoutMs,
        CancellationToken cancellationToken = default)
    {
        await _authProvider.EnsureAuthenticatedAsync(cancellationToken).ConfigureAwait(false);

        // Build listening string format: dataId^2group^2md5^2tenant^1
        var sb = new StringBuilder();
        foreach (var (key, md5) in configs)
        {
            sb.Append(key.DataId).Append(NacosConstants.WordSeparator);
            sb.Append(key.Group).Append(NacosConstants.WordSeparator);
            sb.Append(md5);

            if (!string.IsNullOrEmpty(key.Tenant))
            {
                sb.Append(NacosConstants.WordSeparator).Append(key.Tenant);
            }

            sb.Append(NacosConstants.LineSeparator);
        }

        var listeningConfigs = sb.ToString();

        _logger.LogDebug("Listening configs string: {ListeningConfigs}", listeningConfigs);

        var formParameters = new Dictionary<string, string>
        {
            [NacosConstants.ProbeModifyRequest] = listeningConfigs
        };

        // Build query parameters for authentication and authorization
        // Nacos listener API requires accessToken as query parameter, not in form body
        var queryParameters = new Dictionary<string, string>();
        _authProvider.ApplyAuthentication(queryParameters);

        // Add tenant parameter if namespace is configured - required for server-side authorization
        if (!string.IsNullOrEmpty(_options.Namespace))
        {
            queryParameters["tenant"] = _options.Namespace;
        }

        // Build URL with query string
        var path = NacosConstants.ConfigListenerPath;
        if (queryParameters.Count > 0)
        {
            var queryString = BuildQueryString(queryParameters);
            path = $"{path}?{queryString}";
            _logger.LogDebug("Listener endpoint with auth query: {Path}", path.Replace(queryParameters.GetValueOrDefault(NacosConstants.AccessTokenHeader, ""), "***"));
        }

        using var request = new HttpRequestMessage(HttpMethod.Post, path);
        request.Content = new FormUrlEncodedContent(formParameters);
        request.Headers.Add(NacosConstants.LongPollingTimeoutHeader, timeoutMs.ToString());
        AddCommonHeaders(request);

        // Long polling timeout + buffer
        var requestTimeout = timeoutMs + (timeoutMs / 2);

        using var cts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        cts.CancelAfter(TimeSpan.FromMilliseconds(requestTimeout));

        try
        {
            var response = await _transport.SendAsync(request, cts.Token).ConfigureAwait(false);

            if (response.StatusCode == System.Net.HttpStatusCode.OK)
            {
                var responseContent = await response.Content.ReadAsStringAsync().ConfigureAwait(false);
                return ParseChangedConfigs(responseContent);
            }
            else if (response.StatusCode == System.Net.HttpStatusCode.Forbidden)
            {
                var error = await response.Content.ReadAsStringAsync().ConfigureAwait(false);
                _logger.LogError("Long polling access denied: {Error}", error);
                throw new UnauthorizedAccessException($"Long polling access denied: {error}");
            }
            else
            {
                _logger.LogWarning("Long polling returned {StatusCode}", response.StatusCode);
                return new List<ConfigKey>();
            }
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            // User cancelled, not a timeout
            throw;
        }
        catch (OperationCanceledException)
        {
            // Timeout is normal for long polling
            return new List<ConfigKey>();
        }
        catch (Exception ex) when (ex is not UnauthorizedAccessException)
        {
            _logger.LogError(ex, "Error during long polling");
            throw;
        }
    }

    private List<ConfigKey> ParseChangedConfigs(string response)
    {
        var result = new List<ConfigKey>();

        if (string.IsNullOrEmpty(response))
        {
            return result;
        }

        var decodedResponse = Uri.UnescapeDataString(response);
        var lines = decodedResponse.Split(NacosConstants.LineSeparator);

        foreach (var line in lines)
        {
            if (string.IsNullOrWhiteSpace(line))
            {
                continue;
            }

            var parts = line.Split(NacosConstants.WordSeparator);

            if (parts.Length >= 2)
            {
                var dataId = parts[0];
                var group = parts[1];
                var tenant = parts.Length >= 3 ? parts[2] : string.Empty;

                result.Add(new ConfigKey(dataId, group, tenant));

                _logger.LogInformation("Config changed: {DataId}/{Group}/{Tenant}",
                    dataId, group, tenant);
            }
        }

        return result;
    }

    private string BuildQueryString(Dictionary<string, string> parameters)
    {
        var items = parameters.Select(kvp =>
            $"{Uri.EscapeDataString(kvp.Key)}={Uri.EscapeDataString(kvp.Value)}");

        return string.Join("&", items);
    }

    private void AddCommonHeaders(HttpRequestMessage request)
    {
        var timestamp = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds().ToString();
        var requestId = Guid.NewGuid().ToString("N");

        request.Headers.TryAddWithoutValidation("Client-Version", NacosConstants.ClientVersion);
        request.Headers.TryAddWithoutValidation("Client-RequestTS", timestamp);
        request.Headers.TryAddWithoutValidation("Client-RequestToken", HashUtils.GetMd5(timestamp));
        request.Headers.TryAddWithoutValidation("Request-Id", requestId);
        request.Headers.TryAddWithoutValidation("Accept-Charset", "UTF-8");
        request.Headers.TryAddWithoutValidation("exConfigInfo", "true");
    }
}
