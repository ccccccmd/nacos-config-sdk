namespace Nacos.Config.Models;

/// <summary>
///     Configuration data returned from Nacos server
/// </summary>
public class ConfigData
{
    public ConfigData(string content, string? contentType = null, string? md5 = null, string? encryptedDataKey = null)
    {
        Content = content ?? string.Empty;
        ContentType = contentType ?? "text";
        Md5 = md5 ?? string.Empty;
        EncryptedDataKey = encryptedDataKey;
    }

    /// <summary>
    ///     Configuration content
    /// </summary>
    public string Content { get; }

    /// <summary>
    ///     Content type (json, yaml, xml, text, etc.)
    /// </summary>
    public string ContentType { get; }

    /// <summary>
    ///     MD5 hash of the content
    /// </summary>
    public string Md5 { get; }

    /// <summary>
    ///     Encrypted data key (for encrypted configurations)
    /// </summary>
    public string? EncryptedDataKey { get; }

    /// <summary>
    ///     Check if configuration is empty
    /// </summary>
    public bool IsEmpty => string.IsNullOrEmpty(Content);
}