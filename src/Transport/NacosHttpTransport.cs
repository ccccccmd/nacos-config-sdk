using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Nacos.V2.Config.Models;

namespace Nacos.V2.Config.Transport;

/// <summary>
/// HTTP transport implementation using IHttpClientFactory
/// </summary>
public class NacosHttpTransport : IHttpTransport
{
    private readonly IHttpClientFactory _httpClientFactory;
    private readonly IServerSelector _serverSelector;
    private readonly NacosConfigOptions _options;
    private readonly ILogger<NacosHttpTransport> _logger;

    public NacosHttpTransport(
        IHttpClientFactory httpClientFactory,
        IServerSelector serverSelector,
        IOptions<NacosConfigOptions> options,
        ILogger<NacosHttpTransport> logger)
    {
        _httpClientFactory = httpClientFactory ?? throw new ArgumentNullException(nameof(httpClientFactory));
        _serverSelector = serverSelector ?? throw new ArgumentNullException(nameof(serverSelector));
        _options = options?.Value ?? throw new ArgumentNullException(nameof(options));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));

        _options.Validate();
    }

    public async Task<HttpResponseMessage> SendAsync(
        HttpRequestMessage request,
        CancellationToken cancellationToken = default)
    {
        var serverAddress = _serverSelector.SelectServer();

        try
        {
            // Create HttpClient from factory for proper connection pooling
            var httpClient = _httpClientFactory.CreateClient("NacosConfigClient");

            // Build full URL
            var requestUrl = BuildFullUrl(serverAddress, request.RequestUri?.ToString() ?? string.Empty);
            request.RequestUri = new Uri(requestUrl);

            _logger.LogDebug("Sending {Method} request to {Url}", request.Method, requestUrl);

            var response = await httpClient.SendAsync(request, cancellationToken).ConfigureAwait(false);

            // Check if response indicates server error (500, 502, 503)
            if (IsServerError(response))
            {
                _logger.LogWarning("Server error from {Server}: {StatusCode}",
                    serverAddress, response.StatusCode);
                _serverSelector.MarkServerFailed(serverAddress);
            }
            else
            {
                // Success or client error (4xx) - mark server as healthy
                _serverSelector.MarkServerHealthy(serverAddress);
            }

            return response;
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            _logger.LogInformation("Request cancelled by user");
            throw;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Request to {Server} failed", serverAddress);
            _serverSelector.MarkServerFailed(serverAddress);
            throw;
        }
    }

    public string BuildRequestUrl(string path)
    {
        var serverAddress = _serverSelector.SelectServer();
        return BuildFullUrl(serverAddress, path);
    }

    private string BuildFullUrl(string serverAddress, string path)
    {
        var contextPath = _options.ContextPath.Trim('/');
        var cleanPath = path.TrimStart('/');

        return $"{serverAddress}/{contextPath}/{cleanPath}";
    }

    private static bool IsServerError(HttpResponseMessage response)
    {
        var statusCode = (int)response.StatusCode;
        return statusCode == 500 || statusCode == 502 || statusCode == 503;
    }
}
