namespace Nacos.V2.Config.Authentication;

/// <summary>
/// Null authentication provider (no authentication)
/// </summary>
public class NullAuthProvider : IAuthenticationProvider
{
    public bool IsEnabled => false;

    public Task InitializeAsync(CancellationToken cancellationToken = default)
    {
        return Task.CompletedTask;
    }

    public Task<bool> EnsureAuthenticatedAsync(CancellationToken cancellationToken = default)
    {
        return Task.FromResult(true);
    }

    public void ApplyAuthentication(HttpRequestMessage request)
    {
        // No authentication to apply
    }

    public void ApplyAuthentication(IDictionary<string, string> parameters)
    {
        // No authentication to apply
    }
}
