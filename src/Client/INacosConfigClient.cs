using Nacos.V2.Config.Models;

namespace Nacos.V2.Config.Client;

/// <summary>
/// Nacos configuration HTTP API client interface
/// </summary>
public interface INacosConfigClient
{
    /// <summary>
    /// Get configuration from Nacos server
    /// </summary>
    Task<ConfigData?> GetConfigAsync(
        ConfigKey key,
        long timeoutMs,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Publish configuration to Nacos server
    /// </summary>
    Task<bool> PublishConfigAsync(
        ConfigKey key,
        string content,
        string type = "text",
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Remove configuration from Nacos server
    /// </summary>
    Task<bool> RemoveConfigAsync(
        ConfigKey key,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Listen for configuration changes (long polling)
    /// </summary>
    Task<List<ConfigKey>> ListenConfigChangesAsync(
        IEnumerable<(ConfigKey Key, string Md5)> configs,
        long timeoutMs,
        CancellationToken cancellationToken = default);
}
