using Nacos.V2.Config.Models;

namespace Nacos.V2.Config.Storage;

/// <summary>
/// Local configuration storage interface
/// </summary>
public interface ILocalConfigStorage
{
    /// <summary>
    /// Get failover configuration (highest priority)
    /// </summary>
    Task<LocalConfigData?> GetFailoverConfigAsync(ConfigKey key, CancellationToken cancellationToken = default);

    /// <summary>
    /// Get snapshot configuration (cache from server)
    /// </summary>
    Task<LocalConfigData?> GetSnapshotConfigAsync(ConfigKey key, CancellationToken cancellationToken = default);

    /// <summary>
    /// Save snapshot configuration
    /// </summary>
    Task SaveSnapshotAsync(ConfigKey key, string content, CancellationToken cancellationToken = default);

    /// <summary>
    /// Check if failover file exists
    /// </summary>
    bool FailoverExists(ConfigKey key);

    /// <summary>
    /// Get failover file path
    /// </summary>
    string GetFailoverFilePath(ConfigKey key);

    /// <summary>
    /// Get snapshot file path
    /// </summary>
    string GetSnapshotFilePath(ConfigKey key);
}
