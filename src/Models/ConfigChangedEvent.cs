namespace Nacos.Config.Models;

/// <summary>
///     Event raised when configuration changes
/// </summary>
public class ConfigChangedEvent
{
    public ConfigChangedEvent(ConfigKey key, string newContent, string? oldContent, string? contentType = null)
    {
        Key = key;
        NewContent = newContent ?? string.Empty;
        OldContent = oldContent ?? string.Empty;
        ContentType = contentType ?? "text";
        Timestamp = DateTimeOffset.UtcNow;
    }

    /// <summary>
    ///     Configuration key
    /// </summary>
    public ConfigKey Key { get; }

    /// <summary>
    ///     New configuration content
    /// </summary>
    public string NewContent { get; }

    /// <summary>
    ///     Old configuration content
    /// </summary>
    public string OldContent { get; }

    /// <summary>
    ///     Content type
    /// </summary>
    public string ContentType { get; }

    /// <summary>
    ///     Timestamp when the change was detected
    /// </summary>
    public DateTimeOffset Timestamp { get; }
}