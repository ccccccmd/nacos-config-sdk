namespace Nacos.V2.Config.Utils;

/// <summary>
/// Nacos constants
/// </summary>
public static class NacosConstants
{
    /// <summary>
    /// Default group name
    /// </summary>
    public const string DefaultGroup = "DEFAULT_GROUP";

    /// <summary>
    /// HTTP protocol prefix
    /// </summary>
    public const string HttpPrefix = "http://";

    /// <summary>
    /// HTTPS protocol prefix
    /// </summary>
    public const string HttpsPrefix = "https://";

    /// <summary>
    /// Configuration controller path
    /// </summary>
    public const string ConfigControllerPath = "/v1/cs/configs";

    /// <summary>
    /// Configuration listener path
    /// </summary>
    public const string ConfigListenerPath = "/v1/cs/configs/listener";

    /// <summary>
    /// Login path
    /// </summary>
    public const string LoginPath = "/v1/auth/users/login";

    /// <summary>
    /// Word separator for long polling
    /// </summary>
    public const string WordSeparator = "\u0002";

    /// <summary>
    /// Line separator for long polling
    /// </summary>
    public const string LineSeparator = "\u0001";

    /// <summary>
    /// Access token header name
    /// </summary>
    public const string AccessTokenHeader = "accessToken";

    /// <summary>
    /// Token TTL field name
    /// </summary>
    public const string TokenTtlField = "tokenTtl";

    /// <summary>
    /// Config type header name
    /// </summary>
    public const string ConfigTypeHeader = "Config-Type";

    /// <summary>
    /// Client version
    /// </summary>
    public const string ClientVersion = "Nacos-CSharp-Client:v2.0.0";

    /// <summary>
    /// Long polling timeout header
    /// </summary>
    public const string LongPollingTimeoutHeader = "Long-Pulling-Timeout";

    /// <summary>
    /// Probe modify request parameter
    /// </summary>
    public const string ProbeModifyRequest = "Listening-Configs";
}
