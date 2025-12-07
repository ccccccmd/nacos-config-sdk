using System.Collections.Concurrent;
using Microsoft.Extensions.Logging;

namespace Nacos.Config.Transport;

/// <summary>
///     Round-robin server selector with health management and cached healthy server list
/// </summary>
public class RoundRobinServerSelector : IServerSelector
{
    private readonly ConcurrentDictionary<string, ServerHealthInfo> _healthMap;
    private readonly ILogger<RoundRobinServerSelector> _logger;
    private readonly IReadOnlyList<string> _servers;
    private readonly object _cacheLock = new();
    
    private int _currentIndex;
    private volatile IReadOnlyList<string>? _cachedHealthyServers;
    private volatile int _cacheVersion;

    public RoundRobinServerSelector(
        IEnumerable<string> serverAddresses,
        ILogger<RoundRobinServerSelector> logger)
    {
        _servers = NormalizeServerAddresses(serverAddresses).ToList();
        _healthMap = new ConcurrentDictionary<string, ServerHealthInfo>();
        _logger = logger;
        _currentIndex = 0;

        if (_servers.Count == 0)
        {
            throw new ArgumentException("Server addresses cannot be empty", nameof(serverAddresses));
        }

        // Initialize health info for all servers
        foreach (var server in _servers)
        {
            _healthMap[server] = new ServerHealthInfo();
        }

        // Initialize cache with all servers (all healthy initially)
        _cachedHealthyServers = _servers;
        _cacheVersion = 0;

        _logger.LogInformation("Initialized server selector with {Count} servers", _servers.Count);
    }

    public string SelectServer()
    {
        var healthyServers = GetHealthyServersFromCache();

        if (healthyServers.Count == 0)
        {
            _logger.LogWarning("No healthy servers available, trying recovery...");
            TryRecoverServers();
            
            // Refresh cache after recovery attempt
            InvalidateCache();
            healthyServers = GetHealthyServersFromCache();

            if (healthyServers.Count == 0)
            {
                // Last resort: return any server
                _logger.LogError("All servers unhealthy, returning first server");
                return _servers[0];
            }
        }

        // Round-robin selection among healthy servers
        // Use modulo with the current list to handle concurrent changes safely
        var index = (uint)Interlocked.Increment(ref _currentIndex) % (uint)healthyServers.Count;
        return healthyServers[(int)index];
    }

    public void MarkServerFailed(string serverAddress)
    {
        if (_healthMap.TryGetValue(serverAddress, out var health))
        {
            var wasHealthy = health.IsHealthy();
            health.MarkFailed();
            
            // Only invalidate cache if health state actually changed
            if (wasHealthy && !health.IsHealthy())
            {
                InvalidateCache();
            }
            
            _logger.LogWarning("Marked server {Server} as failed (failures: {Failures})",
                serverAddress, health.FailureCount);
        }
    }

    public void MarkServerHealthy(string serverAddress)
    {
        if (_healthMap.TryGetValue(serverAddress, out var health))
        {
            var wasUnhealthy = !health.IsHealthy();
            health.MarkHealthy();
            
            // Only invalidate cache if health state actually changed
            if (wasUnhealthy)
            {
                InvalidateCache();
            }
            
            _logger.LogDebug("Marked server {Server} as healthy", serverAddress);
        }
    }

    public IReadOnlyList<string> GetAllServers()
    {
        return _servers;
    }

    public void RefreshServerList()
    {
        InvalidateCache();
        _logger.LogDebug("Server list refresh requested");
    }

    /// <summary>
    ///     Get healthy servers from cache, rebuilding if necessary
    /// </summary>
    private IReadOnlyList<string> GetHealthyServersFromCache()
    {
        var cached = _cachedHealthyServers;
        if (cached != null)
        {
            return cached;
        }

        lock (_cacheLock)
        {
            // Double-check after acquiring lock
            cached = _cachedHealthyServers;
            if (cached != null)
            {
                return cached;
            }

            // Rebuild cache
            var healthyList = new List<string>(_servers.Count);
            foreach (var server in _servers)
            {
                if (_healthMap.TryGetValue(server, out var health) && health.IsHealthy())
                {
                    healthyList.Add(server);
                }
            }

            _cachedHealthyServers = healthyList;
            Interlocked.Increment(ref _cacheVersion);
            
            _logger.LogDebug("Rebuilt healthy server cache: {Count}/{Total} healthy", 
                healthyList.Count, _servers.Count);

            return healthyList;
        }
    }

    /// <summary>
    ///     Invalidate the cached healthy server list
    /// </summary>
    private void InvalidateCache()
    {
        _cachedHealthyServers = null;
    }

    private void TryRecoverServers()
    {
        var now = DateTimeOffset.UtcNow;
        var recovered = false;
        
        foreach (var (server, health) in _healthMap)
        {
            // Try to recover servers that have been unhealthy for more than 10 seconds
            if (!health.IsHealthy() && (now - health.LastFailureTime).TotalSeconds > 10)
            {
                health.Reset();
                recovered = true;
                _logger.LogInformation("Attempting recovery for server {Server}", server);
            }
        }

        if (recovered)
        {
            InvalidateCache();
        }
    }

    private static IEnumerable<string> NormalizeServerAddresses(IEnumerable<string> addresses)
    {
        foreach (var address in addresses)
        {
            var normalized = address.TrimEnd('/');

            // Ensure http:// or https:// prefix
            if (!normalized.StartsWith("http://", StringComparison.OrdinalIgnoreCase) &&
                !normalized.StartsWith("https://", StringComparison.OrdinalIgnoreCase))
            {
                normalized = "http://" + normalized;
            }

            yield return normalized;
        }
    }

    /// <summary>
    ///     Server health information
    /// </summary>
    private class ServerHealthInfo
    {
        private const int FailureThreshold = 3;
        private int _failureCount;

        public int FailureCount => _failureCount;
        public DateTimeOffset LastFailureTime { get; private set; }

        public bool IsHealthy()
        {
            return _failureCount < FailureThreshold;
        }

        public void MarkFailed()
        {
            Interlocked.Increment(ref _failureCount);
            LastFailureTime = DateTimeOffset.UtcNow;
        }

        public void MarkHealthy()
        {
            Interlocked.Exchange(ref _failureCount, 0);
        }

        public void Reset()
        {
            Interlocked.Exchange(ref _failureCount, 0);
        }
    }
}