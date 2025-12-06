using Nacos.Config.Models;

namespace Nacos.Config.Listening;

/// <summary>
///     Configuration listening manager interface
/// </summary>
public interface IConfigListeningManager : IDisposable
{
    /// <summary>
    ///     Add a listener for configuration changes
    /// </summary>
    void AddListener(ConfigKey key, Action<ConfigChangedEvent> callback);

    /// <summary>
    ///     Remove a listener
    /// </summary>
    void RemoveListener(ConfigKey key, Action<ConfigChangedEvent> callback);

    /// <summary>
    ///     Start the listening manager
    /// </summary>
    Task StartAsync(CancellationToken cancellationToken = default);

    /// <summary>
    ///     Stop the listening manager
    /// </summary>
    Task StopAsync(CancellationToken cancellationToken = default);
}