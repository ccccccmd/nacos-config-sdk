namespace Nacos.V2.Config.Transport;

/// <summary>
/// HTTP transport interface for Nacos communication
/// </summary>
public interface IHttpTransport
{
    /// <summary>
    /// Send HTTP request to Nacos server
    /// </summary>
    Task<HttpResponseMessage> SendAsync(
        HttpRequestMessage request,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Build request URL with path
    /// </summary>
    string BuildRequestUrl(string path);
}
