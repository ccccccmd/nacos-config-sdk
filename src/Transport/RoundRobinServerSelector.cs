using System.Collections.Concurrent;
using Microsoft.Extensions.Logging;

namespace Nacos.V2.Config.Transport;

/// <summary>
/// Round-robin server selector with health management
/// </summary>
public class RoundRobinServerSelector : IServerSelector
{
    private readonly IReadOnlyList<string> _servers;
    private readonly ConcurrentDictionary<string, ServerHealthInfo> _healthMap;
    private readonly ILogger<RoundRobinServerSelector> _logger;
    private int _currentIndex;

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

        _logger.LogInformation("Initialized server selector with {Count} servers", _servers.Count);
    }

    public string SelectServer()
    {
        var healthyServers = _servers
            .Where(s => _healthMap.TryGetValue(s, out var health) && health.IsHealthy())
            .ToList();

        if (healthyServers.Count == 0)
        {
            _logger.LogWarning("No healthy servers available, trying recovery...");
            // Try to recover one server
            TryRecoverServers();

            healthyServers = _servers
                .Where(s => _healthMap.TryGetValue(s, out var health) && health.IsHealthy())
                .ToList();

            if (healthyServers.Count == 0)
            {
                // Last resort: return any server
                _logger.LogError("All servers unhealthy, returning first server");
                return _servers[0];
            }
        }

        // Round-robin selection among healthy servers
        var index = Interlocked.Increment(ref _currentIndex) % healthyServers.Count;
        return healthyServers[index];
    }

    public void MarkServerFailed(string serverAddress)
    {
        if (_healthMap.TryGetValue(serverAddress, out var health))
        {
            health.MarkFailed();
            _logger.LogWarning("Marked server {Server} as failed (failures: {Failures})",
                serverAddress, health.FailureCount);
        }
    }

    public void MarkServerHealthy(string serverAddress)
    {
        if (_healthMap.TryGetValue(serverAddress, out var health))
        {
            health.MarkHealthy();
            _logger.LogInformation("Marked server {Server} as healthy", serverAddress);
        }
    }

    public IReadOnlyList<string> GetAllServers() => _servers;

    public void RefreshServerList()
    {
        // Currently static server list; can be extended for dynamic discovery
        _logger.LogDebug("Server list refresh requested (currently static)");
    }

    private void TryRecoverServers()
    {
        var now = DateTimeOffset.UtcNow;
        foreach (var (server, health) in _healthMap)
        {
            // Try to recover servers that have been unhealthy for more than 10 seconds
            if(!health.IsHealthy() && (now - health.LastFailureTime).TotalSeconds > 10)
            {
                health.Reset();
                _logger.LogInformation("Attempting recovery for server {Server}", server);
            }
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
    /// Server health information
    /// </summary>
    private class ServerHealthInfo
    {
        private const int FailureThreshold = 3;
        private int _failureCount;
        private DateTimeOffset _lastFailureTime;

        public int FailureCount => _failureCount;
        public DateTimeOffset LastFailureTime => _lastFailureTime;

        public bool IsHealthy() => _failureCount < FailureThreshold;

        public void MarkFailed()
        {
            Interlocked.Increment(ref _failureCount);
            _lastFailureTime = DateTimeOffset.UtcNow;
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
