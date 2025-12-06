using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Nacos.V2.Config.Authentication;
using Nacos.V2.Config.Client;
using Nacos.V2.Config.Listening;
using Nacos.V2.Config.Models;
using Nacos.V2.Config.Storage;
using Nacos.V2.Config.Utils;

namespace Nacos.V2.Config.Core;

/// <summary>
/// Nacos configuration service implementation
/// </summary>
public class NacosConfigService : INacosConfigService, IDisposable
{
    private readonly INacosConfigClient _client;
    private readonly ILocalConfigStorage _localStorage;
    private readonly IAuthenticationProvider _authProvider;
    private readonly IConfigListeningManager _listeningManager;
    private readonly NacosConfigOptions _options;
    private readonly ILogger<NacosConfigService> _logger;
    private bool _started;
    private readonly SemaphoreSlim _startLock = new(1, 1);

    public NacosConfigService(
        INacosConfigClient client,
        ILocalConfigStorage localStorage,
        IAuthenticationProvider authProvider,
        IConfigListeningManager listeningManager,
        IOptions<NacosConfigOptions> options,
        ILogger<NacosConfigService> logger)
    {
        _client = client;
        _localStorage = localStorage;
        _authProvider = authProvider;
        _listeningManager = listeningManager;
        _options = options.Value;
        _logger = logger;
    }

    public async Task<string?> GetConfigAsync(
        string dataId,
        string group,
        CancellationToken cancellationToken = default)
    {
        group = ConfigValidator.NormalizeGroup(group);
        ConfigValidator.ValidateKey(dataId, group);

        var key = new ConfigKey(dataId, group, _options.Namespace);

        // Priority 1: Check failover configuration (manual override)
        var failoverConfig = await _localStorage.GetFailoverConfigAsync(key, cancellationToken).ConfigureAwait(false);
        if (failoverConfig != null && !failoverConfig.IsEmpty)
        {
            _logger.LogWarning("Using failover config for {DataId}/{Group}", dataId, group);
            return failoverConfig.Content;
        }

        // Priority 2: Try to get from server
        try
        {
            await _authProvider.EnsureAuthenticatedAsync(cancellationToken).ConfigureAwait(false);

            var configData = await _client.GetConfigAsync(
                key,
                _options.DefaultTimeoutMs,
                cancellationToken).ConfigureAwait(false);

            if (configData != null && !configData.IsEmpty)
            {
                // Save snapshot for future failover
                await _localStorage.SaveSnapshotAsync(key, configData.Content, cancellationToken).ConfigureAwait(false);

                _logger.LogInformation("Retrieved config {DataId}/{Group} from server, MD5: {Md5}",
                    dataId, group, configData.Md5);

                return configData.Content;
            }

            // Config not found on server
            return null;
        }
        catch (UnauthorizedAccessException)
        {
            // Re-throw authorization errors
            throw;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to get config {DataId}/{Group} from server, trying snapshot", dataId, group);

            // Priority 3: Fallback to snapshot
            var snapshotConfig = await _localStorage.GetSnapshotConfigAsync(key, cancellationToken).ConfigureAwait(false);
            if (snapshotConfig != null && !snapshotConfig.IsEmpty)
            {
                _logger.LogWarning("Using snapshot config for {DataId}/{Group}", dataId, group);
                return snapshotConfig.Content;
            }

            // No snapshot available, re-throw original exception
            throw;
        }
    }

    public async Task<bool> PublishConfigAsync(
        string dataId,
        string group,
        string content,
        string type = "text",
        CancellationToken cancellationToken = default)
    {
        group = ConfigValidator.NormalizeGroup(group);
        ConfigValidator.ValidateKey(dataId, group);
        ConfigValidator.ValidateContent(content);

        var key = new ConfigKey(dataId, group, _options.Namespace);

        await _authProvider.EnsureAuthenticatedAsync(cancellationToken).ConfigureAwait(false);

        var result = await _client.PublishConfigAsync(key, content, type, cancellationToken).ConfigureAwait(false);

        if (result)
        {
            // Update local snapshot
            await _localStorage.SaveSnapshotAsync(key, content, cancellationToken).ConfigureAwait(false);

            _logger.LogInformation("Published config {DataId}/{Group}", dataId, group);
        }

        return result;
    }

    public async Task<bool> RemoveConfigAsync(
        string dataId,
        string group,
        CancellationToken cancellationToken = default)
    {
        group = ConfigValidator.NormalizeGroup(group);
        ConfigValidator.ValidateKey(dataId, group);

        var key = new ConfigKey(dataId, group, _options.Namespace);

        await _authProvider.EnsureAuthenticatedAsync(cancellationToken).ConfigureAwait(false);

        var result = await _client.RemoveConfigAsync(key, cancellationToken).ConfigureAwait(false);

        if (result)
        {
            // Clear local snapshot
            await _localStorage.SaveSnapshotAsync(key, string.Empty, cancellationToken).ConfigureAwait(false);

            _logger.LogInformation("Removed config {DataId}/{Group}", dataId, group);
        }

        return result;
    }

    public IDisposable Subscribe(
        string dataId,
        string group,
        Action<ConfigChangedEvent> callback)
    {
        group = ConfigValidator.NormalizeGroup(group);
        ConfigValidator.ValidateKey(dataId, group);

        if (callback == null)
        {
            throw new ArgumentNullException(nameof(callback));
        }

        var key = new ConfigKey(dataId, group, _options.Namespace);

        // Ensure listening manager is started
        EnsureListeningManagerStartedAsync().GetAwaiter().GetResult();

        // Add listener
        _listeningManager.AddListener(key, callback);

        _logger.LogInformation("Subscribed to {DataId}/{Group}", dataId, group);

        // Return unsubscribe action
        return new DisposableAction(() =>
        {
            _listeningManager.RemoveListener(key, callback);
            _logger.LogInformation("Unsubscribed from {DataId}/{Group}", dataId, group);
        });
    }

    private async Task EnsureListeningManagerStartedAsync()
    {
        if (_started)
        {
            return;
        }

        await _startLock.WaitAsync().ConfigureAwait(false);
        try
        {
            if (!_started)
            {
                await _listeningManager.StartAsync().ConfigureAwait(false);
                _started = true;
                _logger.LogInformation("Listening manager started");
            }
        }
        finally
        {
            _startLock.Release();
        }
    }

    public void Dispose()
    {
        _listeningManager.Dispose();
        _startLock.Dispose();
    }

    private class DisposableAction : IDisposable
    {
        private readonly Action _action;
        private bool _disposed;

        public DisposableAction(Action action)
        {
            _action = action;
        }

        public void Dispose()
        {
            if (!_disposed)
            {
                _action?.Invoke();
                _disposed = true;
            }
        }
    }
}
