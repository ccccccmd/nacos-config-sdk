namespace Nacos.V2.Config.Models;

/// <summary>
/// Nacos configuration options
/// </summary>
public class NacosConfigOptions
{
    /// <summary>
    /// Nacos server addresses (e.g., http://localhost:8848)
    /// </summary>
    public List<string> ServerAddresses { get; set; } = new();

    /// <summary>
    /// Default namespace (tenant)
    /// </summary>
    public string Namespace { get; set; } = string.Empty;

    /// <summary>
    /// Context path, default is "nacos"
    /// </summary>
    public string ContextPath { get; set; } = "nacos";

    /// <summary>
    /// Default timeout in milliseconds
    /// </summary>
    public int DefaultTimeoutMs { get; set; } = 15000;

    /// <summary>
    /// Username for authentication (username/password mode)
    /// </summary>
    public string? UserName { get; set; }

    /// <summary>
    /// Password for authentication (username/password mode)
    /// </summary>
    public string? Password { get; set; }

    /// <summary>
    /// Access Key for authentication (AK/SK mode)
    /// </summary>
    public string? AccessKey { get; set; }

    /// <summary>
    /// Secret Key for authentication (AK/SK mode)
    /// </summary>
    public string? SecretKey { get; set; }

    /// <summary>
    /// Long polling interval in milliseconds for configuration listening
    /// </summary>
    public int ListenIntervalMs { get; set; } = 1000;

    /// <summary>
    /// Long polling timeout in milliseconds
    /// </summary>
    public long LongPollingTimeoutMs { get; set; } = 30000;

    /// <summary>
    /// Maximum retry count for HTTP requests
    /// </summary>
    public int MaxRetry { get; set; } = 3;

    /// <summary>
    /// Retry delay in milliseconds
    /// </summary>
    public int RetryDelayMs { get; set; } = 2000;

    /// <summary>
    /// Configuration batch size for long polling (number of configs per polling request)
    /// </summary>
    public int ConfigBatchSize { get; set; } = 3000;

    /// <summary>
    /// Local snapshot directory path
    /// </summary>
    public string? SnapshotPath { get; set; }

    /// <summary>
    /// Enable local snapshot caching
    /// </summary>
    public bool EnableSnapshot { get; set; } = true;

    /// <summary>
    /// Validate configuration options
    /// </summary>
    public void Validate()
    {
        if (ServerAddresses == null || ServerAddresses.Count == 0)
        {
            throw new ArgumentException("ServerAddresses cannot be empty");
        }

        if (DefaultTimeoutMs <= 0)
        {
            throw new ArgumentException("DefaultTimeoutMs must be greater than 0");
        }

        if (MaxRetry < 0)
        {
            throw new ArgumentException("MaxRetry cannot be negative");
        }
    }
}
