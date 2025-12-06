using System.Collections.Concurrent;
using System.Threading.Channels;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Nacos.Config.Client;
using Nacos.Config.Models;

namespace Nacos.Config.Listening;

/// <summary>
///     Configuration listening manager using Channel-based long polling
/// </summary>
public class ConfigListeningManager : IConfigListeningManager
{
    private readonly Channel<ConfigKey> _changedConfigChannel;
    private readonly INacosConfigClient _client;
    private readonly ConcurrentDictionary<ConfigKey, ConfigCacheEntry> _configCache = new();
    private readonly ILogger<ConfigListeningManager> _logger;
    private readonly NacosConfigOptions _options;

    private CancellationTokenSource? _cts;
    private Task? _longPollingTask;
    private Task? _processingTask;

    public ConfigListeningManager(
        INacosConfigClient client,
        IOptions<NacosConfigOptions> options,
        ILogger<ConfigListeningManager> logger)
    {
        _client = client;
        _options = options.Value;
        _logger = logger;

        // Create unbounded channel for configuration changes
        _changedConfigChannel = Channel.CreateUnbounded<ConfigKey>(new UnboundedChannelOptions
        {
            SingleReader = true,
            SingleWriter = false
        });
    }

    public void AddListener(ConfigKey key, Action<ConfigChangedEvent> callback)
    {
        var entry = _configCache.GetOrAdd(key, k => new ConfigCacheEntry(k, string.Empty, string.Empty));
        entry.AddListenerAsync(callback).GetAwaiter().GetResult();

        _logger.LogInformation("Added listener for {DataId}/{Group}, total listeners: {Count}",
            key.DataId, key.Group, entry.GetListenerCount());
    }

    public void RemoveListener(ConfigKey key, Action<ConfigChangedEvent> callback)
    {
        if (_configCache.TryGetValue(key, out var entry))
        {
            var removed = entry.RemoveListenerAsync(callback).GetAwaiter().GetResult();

            if (removed)
            {
                _logger.LogInformation("Removed listener for {DataId}/{Group}, remaining: {Count}",
                    key.DataId, key.Group, entry.GetListenerCount());

                // Remove entry if no more listeners
                if (!entry.HasListeners())
                {
                    _configCache.TryRemove(key, out _);
                    _logger.LogDebug("Removed cache entry for {DataId}/{Group} (no listeners)",
                        key.DataId, key.Group);
                }
            }
        }
    }

    public Task StartAsync(CancellationToken cancellationToken = default)
    {
        if (_cts != null)
        {
            throw new InvalidOperationException("Listening manager is already started");
        }

        _cts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);

        _logger.LogInformation("Starting configuration listening manager");

        // Start long polling worker
        _longPollingTask = Task.Run(() => LongPollingWorkerAsync(_cts.Token), _cts.Token);

        // Start change processing worker
        _processingTask = Task.Run(() => ProcessChangedConfigsAsync(_cts.Token), _cts.Token);

        return Task.CompletedTask;
    }

    public async Task StopAsync(CancellationToken cancellationToken = default)
    {
        if (_cts == null)
        {
            return;
        }

        _logger.LogInformation("Stopping configuration listening manager");

        _cts.Cancel();

        // Wait for workers to complete
        if (_longPollingTask != null)
        {
            await _longPollingTask.ConfigureAwait(false);
        }

        if (_processingTask != null)
        {
            await _processingTask.ConfigureAwait(false);
        }

        _cts.Dispose();
        _cts = null;
    }

    public void Dispose()
    {
        StopAsync().GetAwaiter().GetResult();
        _changedConfigChannel.Writer.Complete();
    }

    /// <summary>
    ///     Long polling worker - checks for configuration changes
    /// </summary>
    private async Task LongPollingWorkerAsync(CancellationToken cancellationToken)
    {
        _logger.LogInformation("Long polling worker started");

        while (!cancellationToken.IsCancellationRequested)
        {
            try
            {
                // Get all configs with listeners
                var configsToCheck = _configCache
                    .Where(kvp => kvp.Value.HasListeners())
                    .Select(kvp => (kvp.Value.Key, kvp.Value.Md5))
                    .ToList();

                if (configsToCheck.Count == 0)
                {
                    // No configs to monitor, wait a bit
                    await Task.Delay(_options.ListenIntervalMs, cancellationToken).ConfigureAwait(false);
                    continue;
                }

                _logger.LogDebug("Checking {Count} configurations for changes", configsToCheck.Count);

                // Long polling check
                var changedKeys = await _client.ListenConfigChangesAsync(
                    configsToCheck,
                    _options.LongPollingTimeoutMs,
                    cancellationToken).ConfigureAwait(false);

                // Write changed configs to channel
                foreach (var key in changedKeys)
                {
                    await _changedConfigChannel.Writer.WriteAsync(key, cancellationToken).ConfigureAwait(false);
                    _logger.LogInformation("Configuration changed: {DataId}/{Group}", key.DataId, key.Group);
                }

                // Short delay before next poll
                await Task.Delay(100, cancellationToken).ConfigureAwait(false);
            }
            catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
            {
                break;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error in long polling worker");
                await Task.Delay(5000, cancellationToken).ConfigureAwait(false);
            }
        }

        _logger.LogInformation("Long polling worker stopped");
    }

    /// <summary>
    ///     Process changed configurations from channel
    /// </summary>
    private async Task ProcessChangedConfigsAsync(CancellationToken cancellationToken)
    {
        _logger.LogInformation("Change processing worker started");

        try
        {
            await foreach (var key in _changedConfigChannel.Reader.ReadAllAsync(cancellationToken)
                               .ConfigureAwait(false))
            {
                try
                {
                    await ProcessConfigChangeAsync(key, cancellationToken).ConfigureAwait(false);
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Error processing config change for {DataId}/{Group}",
                        key.DataId, key.Group);
                }
            }
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            // Normal shutdown
        }

        _logger.LogInformation("Change processing worker stopped");
    }

    /// <summary>
    ///     Process a single configuration change
    /// </summary>
    private async Task ProcessConfigChangeAsync(ConfigKey key, CancellationToken cancellationToken)
    {
        if (!_configCache.TryGetValue(key, out var entry))
        {
            return;
        }

        // Fetch latest config from server
        var configData = await _client.GetConfigAsync(key, _options.DefaultTimeoutMs, cancellationToken)
            .ConfigureAwait(false);

        if (configData == null)
        {
            _logger.LogWarning("Config {DataId}/{Group} was deleted", key.DataId, key.Group);
            return;
        }

        // Update cache and trigger listeners
        var changed = await entry.UpdateContentAsync(configData.Content, configData.Md5).ConfigureAwait(false);

        if (changed)
        {
            _logger.LogInformation("Triggered listeners for {DataId}/{Group}, new MD5: {Md5}",
                key.DataId, key.Group, configData.Md5);
        }
    }
}