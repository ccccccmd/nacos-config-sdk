namespace Nacos.Config.Storage;

/// <summary>
///     Local configuration data with metadata
/// </summary>
public class LocalConfigData
{
    public LocalConfigData(string content, DateTimeOffset lastModified)
    {
        Content = content;
        LastModified = lastModified;
    }

    /// <summary>
    ///     Configuration content
    /// </summary>
    public string Content { get; }

    /// <summary>
    ///     Last modified timestamp
    /// </summary>
    public DateTimeOffset LastModified { get; }

    /// <summary>
    ///     Check if data is empty
    /// </summary>
    public bool IsEmpty => string.IsNullOrEmpty(Content);
}