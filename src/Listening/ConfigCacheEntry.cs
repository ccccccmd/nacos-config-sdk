using Nacos.Config.Models;

namespace Nacos.Config.Listening;

/// <summary>
///     Configuration cache entry with listeners
/// </summary>
internal class ConfigCacheEntry
{
    private readonly List<Action<ConfigChangedEvent>> _listeners = new();
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
    ///     Add a listener
    /// </summary>
    public async Task AddListenerAsync(Action<ConfigChangedEvent> callback)
    {
        await _lock.WaitAsync().ConfigureAwait(false);
        try
        {
            if (!_listeners.Contains(callback))
            {
                _listeners.Add(callback);
            }
        }
        finally
        {
            _lock.Release();
        }
    }

    /// <summary>
    ///     Remove a listener
    /// </summary>
    public async Task<bool> RemoveListenerAsync(Action<ConfigChangedEvent> callback)
    {
        await _lock.WaitAsync().ConfigureAwait(false);
        try
        {
            return _listeners.Remove(callback);
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

        Action<ConfigChangedEvent>[] listenersSnapshot;
        string oldContent;

        await _lock.WaitAsync().ConfigureAwait(false);
        try
        {
            oldContent = Content; // Save old content before update
            Content = newContent;
            Md5 = newMd5;

            // Take snapshot of listeners to avoid holding lock during callbacks
            listenersSnapshot = _listeners.ToArray();
        }
        finally
        {
            _lock.Release();
        }

        // Trigger listeners outside of lock
        if (listenersSnapshot.Length > 0)
        {
            var evt = new ConfigChangedEvent(Key, newContent, oldContent, "text");

            foreach (var listener in listenersSnapshot)
            {
                try
                {
                    listener(evt);
                }
                catch
                {
                    // Ignore listener exceptions
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
        return _listeners.Count > 0;
    }

    /// <summary>
    ///     Get listener count
    /// </summary>
    public int GetListenerCount()
    {
        return _listeners.Count;
    }
}