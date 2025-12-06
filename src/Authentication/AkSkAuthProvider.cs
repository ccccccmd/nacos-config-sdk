using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Nacos.Config.Models;
using Nacos.Config.Utils;

namespace Nacos.Config.Authentication;

/// <summary>
///     AK/SK signature-based authentication provider
/// </summary>
public class AkSkAuthProvider : IAuthenticationProvider
{
    private readonly ILogger<AkSkAuthProvider> _logger;
    private readonly NacosConfigOptions _options;

    public AkSkAuthProvider(
        IOptions<NacosConfigOptions> options,
        ILogger<AkSkAuthProvider> logger)
    {
        _options = options.Value;
        _logger = logger;

        IsEnabled = !string.IsNullOrWhiteSpace(_options.AccessKey) &&
                    !string.IsNullOrWhiteSpace(_options.SecretKey);
    }

    public bool IsEnabled { get; }

    public Task InitializeAsync(CancellationToken cancellationToken = default)
    {
        if (IsEnabled)
        {
            _logger.LogInformation("AK/SK authentication enabled");
        }

        return Task.CompletedTask;
    }

    public Task<bool> EnsureAuthenticatedAsync(CancellationToken cancellationToken = default)
    {
        // AK/SK authentication is stateless, always valid
        return Task.FromResult(IsEnabled);
    }

    public void ApplyAuthentication(HttpRequestMessage request)
    {
        if (!IsEnabled)
        {
            return;
        }

        // Add Spas-AccessKey header
        request.Headers.TryAddWithoutValidation("Spas-AccessKey", _options.AccessKey);

        // Calculate and add signature headers (timestamp and signature)
        var timestamp = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds().ToString();
        request.Headers.TryAddWithoutValidation("Timestamp", timestamp);

        // For HTTP requests, we can't easily access parameters to build resource string
        // So we calculate signature with just timestamp
        var signature = HashUtils.GetHmacSha1(timestamp, _options.SecretKey!);
        request.Headers.TryAddWithoutValidation("Spas-Signature", signature);
    }

    public void ApplyAuthentication(IDictionary<string, string> parameters)
    {
        if (!IsEnabled)
        {
            return;
        }

        // Build resource string from parameters
        var resource = string.Empty;

        if (parameters.TryGetValue("tenant", out var tenant) &&
            parameters.TryGetValue("group", out var group))
        {
            resource = $"{tenant}+{group}";
        }
        else if (parameters.TryGetValue("group", out group))
        {
            resource = group;
        }

        // Calculate signature
        var timestamp = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds().ToString();

        var signatureInput = string.IsNullOrEmpty(resource)
            ? timestamp
            : $"{resource}+{timestamp}";

        var signature = HashUtils.GetHmacSha1(signatureInput, _options.SecretKey!);

        // Add signature parameters (these will be added to request headers by the caller)
        parameters["Spas-AccessKey"] = _options.AccessKey!;
        parameters["Timestamp"] = timestamp;
        parameters["Spas-Signature"] = signature;
    }
}