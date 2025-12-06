namespace Nacos.Config.Authentication;

/// <summary>
///     Authentication provider interface
/// </summary>
public interface IAuthenticationProvider
{
    /// <summary>
    ///     Check if authentication is enabled
    /// </summary>
    bool IsEnabled { get; }

    /// <summary>
    ///     Initialize authentication (e.g., initial login)
    /// </summary>
    Task InitializeAsync(CancellationToken cancellationToken = default);

    /// <summary>
    ///     Ensure authentication is valid (refresh if needed)
    /// </summary>
    Task<bool> EnsureAuthenticatedAsync(CancellationToken cancellationToken = default);

    /// <summary>
    ///     Apply authentication to HTTP request
    /// </summary>
    void ApplyAuthentication(HttpRequestMessage request);

    /// <summary>
    ///     Apply authentication to parameters dictionary
    /// </summary>
    void ApplyAuthentication(IDictionary<string, string> parameters);
}