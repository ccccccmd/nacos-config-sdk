using Nacos.Config.Models;

namespace Nacos.Config.Listening;

/// <summary>
///     Configuration cache entry with listeners
/// </summary>
internal class ConfigCacheEntry
{
    // Unified async listener list - sync callbacks are wrapped as async
    private readonly List<Func<ConfigChangedEvent, Task>> _asyncListeners = new();
    
    // Map to track sync callbacks and their async wrappers for proper removal
    // This prevents memory leaks when disposing subscriptions
    private readonly Dictionary<Action<ConfigChangedEvent>, Func<ConfigChangedEvent, Task>> _syncToAsyncMap = new();
    
    private readonly SemaphoreSlim _lock = new(1, 1);

    public ConfigCacheEntry(ConfigKey key, string content, string md5)
    {
        Key = key;
        Content = content;
        Md5 = md5;
    }

    public ConfigKey Key { get; }
    public string Content { get; private set; }
    public string Md5 { get; private set; }

    /// <summary>
    ///     Add a synchronous listener (wrapped as async)
    /// </summary>
    public async Task AddListenerAsync(Action<ConfigChangedEvent> callback)
    {
        await _lock.WaitAsync().ConfigureAwait(false);
        try
        {
            // Prevent duplicate additions (memory leak protection)
            if (_syncToAsyncMap.ContainsKey(callback))
            {
                return;
            }

            // Wrap sync callback as async
            var asyncCallback = new Func<ConfigChangedEvent, Task>(evt =>
            {
                callback(evt);
                return Task.CompletedTask;
            });

            // Store mapping for later removal
            _syncToAsyncMap[callback] = asyncCallback;
            _asyncListeners.Add(asyncCallback);
        }
        finally
        {
            _lock.Release();
        }
    }

    /// <summary>
    ///     Add an asynchronous listener
    /// </summary>
    public async Task AddAsyncListenerAsync(Func<ConfigChangedEvent, Task> asyncCallback)
    {
        await _lock.WaitAsync().ConfigureAwait(false);
        try
        {
            if (!_asyncListeners.Contains(asyncCallback))
            {
                _asyncListeners.Add(asyncCallback);
            }
        }
        finally
        {
            _lock.Release();
        }
    }

    /// <summary>
    ///     Remove a synchronous listener
    /// </summary>
    public async Task<bool> RemoveListenerAsync(Action<ConfigChangedEvent> callback)
    {
        await _lock.WaitAsync().ConfigureAwait(false);
        try
        {
            // Find the wrapped async callback using the mapping dictionary
            if (_syncToAsyncMap.TryGetValue(callback, out var asyncCallback))
            {
                // Remove from both collections
                _asyncListeners.Remove(asyncCallback);
                _syncToAsyncMap.Remove(callback);
                return true;
            }
            return false;
        }
        finally
        {
            _lock.Release();
        }
    }

    /// <summary>
    ///     Remove an asynchronous listener
    /// </summary>
    public async Task<bool> RemoveAsyncListenerAsync(Func<ConfigChangedEvent, Task> asyncCallback)
    {
        await _lock.WaitAsync().ConfigureAwait(false);
        try
        {
            return _asyncListeners.Remove(asyncCallback);
        }
        finally
        {
            _lock.Release();
        }
    }

    /// <summary>
    ///     Update content and trigger listeners if changed
    /// </summary>
    public async Task<bool> UpdateContentAsync(string newContent, string newMd5)
    {
        if (Md5 == newMd5)
        {
            return false; // No change
        }

        Func<ConfigChangedEvent, Task>[] listenersSnapshot;
        string oldContent;

        await _lock.WaitAsync().ConfigureAwait(false);
        try
        {
            oldContent = Content; // Save old content before update
            Content = newContent;
            Md5 = newMd5;

            // Take snapshot of listeners to avoid holding lock during callbacks
            listenersSnapshot = _asyncListeners.ToArray();
        }
        finally
        {
            _lock.Release();
        }

        // Trigger all listeners outside of lock
        if (listenersSnapshot.Length > 0)
        {
            var evt = new ConfigChangedEvent(Key, newContent, oldContent, "text");

            foreach (var listener in listenersSnapshot)
            {
                try
                {
                    // Await each listener sequentially
                    await listener(evt).ConfigureAwait(false);
                }
                catch
                {
                    // Ignore listener exceptions to prevent one bad listener
                    // from breaking the entire notification chain
                }
            }
        }

        return true;
    }

    /// <summary>
    ///     Check if there are any listeners
    /// </summary>
    public bool HasListeners()
    {
        return _asyncListeners.Count > 0;
    }

    /// <summary>
    ///     Get listener count
    /// </summary>
    public int GetListenerCount()
    {
        return _asyncListeners.Count;
    }
}
