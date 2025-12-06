using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Nacos.V2.Config.Models;

namespace Nacos.V2.Config.Storage;

/// <summary>
/// File-based local configuration storage
/// </summary>
public class FileLocalConfigStorage : ILocalConfigStorage
{
    private readonly NacosConfigOptions _options;
    private readonly ILogger<FileLocalConfigStorage> _logger;
    private readonly Lazy<string> _baseDirectory;

    public FileLocalConfigStorage(
        IOptions<NacosConfigOptions> options,
        ILogger<FileLocalConfigStorage> logger)
    {
        _options = options.Value;
        _logger = logger;

        _baseDirectory = new Lazy<string>(() =>
        {
            var baseDir = string.IsNullOrWhiteSpace(_options.SnapshotPath)
                ? Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "nacos", "config")
                : _options.SnapshotPath;

            if (!Directory.Exists(baseDir))
            {
                Directory.CreateDirectory(baseDir);
                _logger.LogInformation("Created snapshot directory: {Directory}", baseDir);
            }

            return baseDir;
        });
    }

    public async Task<LocalConfigData?> GetFailoverConfigAsync(ConfigKey key, CancellationToken cancellationToken = default)
    {
        if (!_options.EnableSnapshot)
        {
            return null;
        }

        var filePath = GetFailoverFilePath(key);

        if (!File.Exists(filePath))
        {
            return null;
        }

        try
        {
            var content = await File.ReadAllTextAsync(filePath, cancellationToken).ConfigureAwait(false);
            var lastModified = File.GetLastWriteTimeUtc(filePath);

            _logger.LogDebug("Read failover config from {FilePath}", filePath);

            return new LocalConfigData(content, lastModified);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error reading failover config from {FilePath}", filePath);
            return null;
        }
    }

    public async Task<LocalConfigData?> GetSnapshotConfigAsync(ConfigKey key, CancellationToken cancellationToken = default)
    {
        if (!_options.EnableSnapshot)
        {
            return null;
        }

        var filePath = GetSnapshotFilePath(key);

        if (!File.Exists(filePath))
        {
            return null;
        }

        try
        {
            var content = await File.ReadAllTextAsync(filePath, cancellationToken).ConfigureAwait(false);
            var lastModified = File.GetLastWriteTimeUtc(filePath);

            _logger.LogDebug("Read snapshot config from {FilePath}", filePath);

            return new LocalConfigData(content, lastModified);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error reading snapshot config from {FilePath}", filePath);
            return null;
        }
    }

    public async Task SaveSnapshotAsync(ConfigKey key, string content, CancellationToken cancellationToken = default)
    {
        if (!_options.EnableSnapshot)
        {
            return;
        }

        var filePath = GetSnapshotFilePath(key);

        try
        {
            var directory = Path.GetDirectoryName(filePath);
            if (!string.IsNullOrEmpty(directory) && !Directory.Exists(directory))
            {
                Directory.CreateDirectory(directory);
            }

            await File.WriteAllTextAsync(filePath, content, cancellationToken).ConfigureAwait(false);

            _logger.LogDebug("Saved snapshot config to {FilePath}", filePath);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error saving snapshot config to {FilePath}", filePath);
        }
    }

    public bool FailoverExists(ConfigKey key)
    {
        if (!_options.EnableSnapshot)
        {
            return false;
        }

        return File.Exists(GetFailoverFilePath(key));
    }

    public string GetFailoverFilePath(ConfigKey key)
    {
        var tenant = string.IsNullOrEmpty(key.Tenant) ? "public" : key.Tenant;
        return Path.Combine(_baseDirectory.Value, "data", "config-data", tenant, key.Group, key.DataId);
    }

    public string GetSnapshotFilePath(ConfigKey key)
    {
        var tenant = string.IsNullOrEmpty(key.Tenant) ? "public" : key.Tenant;
        return Path.Combine(_baseDirectory.Value, "snapshot", tenant, key.Group, key.DataId);
    }
}
