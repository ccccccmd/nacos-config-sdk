namespace Nacos.Config.Utils;

/// <summary>
///     Configuration validator
/// </summary>
public static class ConfigValidator
{
    /// <summary>
    ///     Validate dataId and group
    /// </summary>
    public static void ValidateKey(string dataId, string group)
    {
        ValidateDataId(dataId);
        ValidateGroup(group);
    }

    /// <summary>
    ///     Validate dataId
    /// </summary>
    public static void ValidateDataId(string dataId)
    {
        if (string.IsNullOrWhiteSpace(dataId))
        {
            throw new ArgumentException("DataId cannot be null or whitespace", nameof(dataId));
        }
    }

    /// <summary>
    ///     Validate group
    /// </summary>
    public static void ValidateGroup(string group)
    {
        if (string.IsNullOrWhiteSpace(group))
        {
            throw new ArgumentException("Group cannot be null or whitespace", nameof(group));
        }
    }

    /// <summary>
    ///     Validate content for publishing
    /// </summary>
    public static void ValidateContent(string? content)
    {
        if (content == null)
        {
            throw new ArgumentNullException(nameof(content));
        }
    }

    /// <summary>
    ///     Normalize group to default if null or whitespace
    /// </summary>
    public static string NormalizeGroup(string? group)
    {
        return string.IsNullOrWhiteSpace(group) ? NacosConstants.DefaultGroup : group;
    }
}