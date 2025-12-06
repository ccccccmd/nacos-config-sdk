namespace Nacos.Config.Models;

/// <summary>
///     Configuration key that uniquely identifies a configuration item
/// </summary>
public readonly struct ConfigKey : IEquatable<ConfigKey>
{
    public ConfigKey(string dataId, string group, string? tenant = null)
    {
        if (string.IsNullOrWhiteSpace(dataId))
        {
            throw new ArgumentException("DataId cannot be null or whitespace", nameof(dataId));
        }

        if (string.IsNullOrWhiteSpace(group))
        {
            throw new ArgumentException("Group cannot be null or whitespace", nameof(group));
        }

        DataId = dataId;
        Group = group;
        Tenant = tenant ?? string.Empty;
    }

    /// <summary>
    ///     Configuration data ID
    /// </summary>
    public string DataId { get; }

    /// <summary>
    ///     Configuration group
    /// </summary>
    public string Group { get; }

    /// <summary>
    ///     Tenant (namespace)
    /// </summary>
    public string Tenant { get; }

    /// <summary>
    ///     Get a unique string representation of this key
    /// </summary>
    public string GetKeyString()
    {
        return string.IsNullOrEmpty(Tenant)
            ? $"{DataId}+{Group}"
            : $"{DataId}+{Group}+{Tenant}";
    }

    public bool Equals(ConfigKey other)
    {
        return DataId == other.DataId &&
               Group == other.Group &&
               Tenant == other.Tenant;
    }

    public override bool Equals(object? obj)
    {
        return obj is ConfigKey other && Equals(other);
    }

    public override int GetHashCode()
    {
        unchecked
        {
            var hash = 17;
            hash = hash * 31 + (DataId?.GetHashCode() ?? 0);
            hash = hash * 31 + (Group?.GetHashCode() ?? 0);
            hash = hash * 31 + (Tenant?.GetHashCode() ?? 0);
            return hash;
        }
    }

    public override string ToString()
    {
        return GetKeyString();
    }

    public static bool operator ==(ConfigKey left, ConfigKey right)
    {
        return left.Equals(right);
    }

    public static bool operator !=(ConfigKey left, ConfigKey right)
    {
        return !left.Equals(right);
    }
}