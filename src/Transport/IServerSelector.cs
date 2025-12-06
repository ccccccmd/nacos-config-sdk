namespace Nacos.V2.Config.Transport;

/// <summary>
/// Server selector interface for load balancing and failover
/// </summary>
public interface IServerSelector
{
    /// <summary>
    /// Select a server address
    /// </summary>
    string SelectServer();

    /// <summary>
    /// Mark a server as failed
    /// </summary>
    void MarkServerFailed(string serverAddress);

    /// <summary>
    /// Mark a server as healthy
    /// </summary>
    void MarkServerHealthy(string serverAddress);

    /// <summary>
    /// Get all server addresses
    /// </summary>
    IReadOnlyList<string> GetAllServers();

    /// <summary>
    /// Refresh server list (for dynamic server discovery in future)
    /// </summary>
    void RefreshServerList();
}
