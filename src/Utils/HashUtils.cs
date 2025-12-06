using System.Security.Cryptography;
using System.Text;

namespace Nacos.Config.Utils;

/// <summary>
///     Hash utility methods for MD5 and HMAC-SHA1
/// </summary>
public static class HashUtils
{
    /// <summary>
    ///     Compute MD5 hash of a string
    /// </summary>
    public static string GetMd5(string input)
    {
        if (string.IsNullOrEmpty(input))
        {
            return string.Empty;
        }

#if NET6_0_OR_GREATER
        var hashBytes = MD5.HashData(Encoding.UTF8.GetBytes(input));
#else
        using var md5 = MD5.Create();
        var hashBytes = md5.ComputeHash(Encoding.UTF8.GetBytes(input));
#endif

        return BitConverter.ToString(hashBytes).Replace("-", "").ToLowerInvariant();
    }

    /// <summary>
    ///     Compute HMAC-SHA1 signature
    /// </summary>
    public static string GetHmacSha1(string input, string key)
    {
        if (string.IsNullOrEmpty(input) || string.IsNullOrEmpty(key))
        {
            return string.Empty;
        }

        var keyBytes = Encoding.UTF8.GetBytes(key);
        var inputBytes = Encoding.UTF8.GetBytes(input);

#if NET6_0_OR_GREATER
        var hashBytes = HMACSHA1.HashData(keyBytes, inputBytes);
#else
        using var hmac = new HMACSHA1(keyBytes);
        var hashBytes = hmac.ComputeHash(inputBytes);
#endif

        return Convert.ToBase64String(hashBytes);
    }
}