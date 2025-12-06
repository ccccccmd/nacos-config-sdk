using Nacos.V2.Config.Models;

namespace Nacos.V2.Config.Core;

/// <summary>
/// Nacos configuration service interface
/// </summary>
public interface INacosConfigService
{
    /// <summary>
    /// Get configuration
    /// Priority: Failover -> Server -> Snapshot
    /// </summary>
    Task<string?> GetConfigAsync(
        string dataId,
        string group,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Publish configuration to server
    /// </summary>
    Task<bool> PublishConfigAsync(
        string dataId,
        string group,
        string content,
        string type = "text",
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Remove configuration from server
    /// </summary>
    Task<bool> RemoveConfigAsync(
        string dataId,
        string group,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Subscribe to configuration changes
    /// Returns IDisposable to unsubscribe
    /// </summary>
    IDisposable Subscribe(
        string dataId,
        string group,
        Action<ConfigChangedEvent> callback);
}
