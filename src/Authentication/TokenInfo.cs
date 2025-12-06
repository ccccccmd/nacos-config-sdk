namespace Nacos.Config.Authentication;

/// <summary>
///     Token information for username/password authentication
/// </summary>
internal class TokenInfo
{
    private string? _accessToken;
    private long _tokenTtl;

    public string? AccessToken
    {
        get => Volatile.Read(ref _accessToken);
        set => Volatile.Write(ref _accessToken, value);
    }

    public long TokenTtl
    {
        get => Interlocked.Read(ref _tokenTtl);
        set => Interlocked.Exchange(ref _tokenTtl, value);
    }

    public DateTimeOffset LastRefreshTime { get; set; }

    public long TokenRefreshWindow => TokenTtl / 10;

    public bool IsValid()
    {
        if (string.IsNullOrEmpty(AccessToken))
        {
            return false;
        }

        var elapsedMs = (DateTimeOffset.UtcNow - LastRefreshTime).TotalMilliseconds;
        var refreshThresholdMs = (TokenTtl - TokenRefreshWindow) * 1000;

        return elapsedMs < refreshThresholdMs;
    }
}