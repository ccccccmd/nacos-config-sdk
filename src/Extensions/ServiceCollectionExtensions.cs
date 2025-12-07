using System.Net;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Nacos.Config.Authentication;
using Nacos.Config.Client;
using Nacos.Config.Core;
using Nacos.Config.Listening;
using Nacos.Config.Models;
using Nacos.Config.Storage;
using Nacos.Config.Transport;
using Polly;
using Polly.Extensions.Http;

namespace Nacos.Config.Extensions;

/// <summary>
///     Service collection extensions for Nacos configuration
/// </summary>
public static class ServiceCollectionExtensions
{
    /// <summary>
    ///     Add Nacos configuration service
    /// </summary>
    public static IServiceCollection AddNacosConfigService(
        this IServiceCollection services,
        Action<NacosConfigOptions> configureOptions)
    {
        if (services == null)
        {
            throw new ArgumentNullException(nameof(services));
        }

        if (configureOptions == null)
        {
            throw new ArgumentNullException(nameof(configureOptions));
        }

        // Configure options
        services.Configure(configureOptions);

        // Validate options and determine authentication type
        var tempOptions = new NacosConfigOptions();
        configureOptions(tempOptions);
        tempOptions.Validate();

        // Register HttpClient for Nacos with Polly retry policies
        services.AddHttpClient("NacosConfigClient")
            .ConfigureHttpClient(client =>
            {
                // Calculate timeout: need to accommodate long-polling requests
                // Long polling timeout + buffer (50%) + safety margin
                var longPollingTimeout =
                    tempOptions.LongPollingTimeoutMs + tempOptions.LongPollingTimeoutMs / 2 + 10000;
                var defaultTimeout = tempOptions.DefaultTimeoutMs + 10000;
                var maxTimeout = Math.Max(longPollingTimeout, defaultTimeout);

                client.Timeout = TimeSpan.FromMilliseconds(maxTimeout);
            })
            .AddPolicyHandler(GetRetryPolicy(tempOptions));

        // Register server selector
        services.TryAddSingleton<IServerSelector>(sp =>
        {
            var logger = sp.GetRequiredService<ILogger<RoundRobinServerSelector>>();
            var options = sp.GetRequiredService<IOptions<NacosConfigOptions>>();
            return new RoundRobinServerSelector(options.Value.ServerAddresses, logger);
        });

        // Register HTTP transport
        services.TryAddSingleton<IHttpTransport, NacosHttpTransport>();

        // Register authentication provider based on configuration
        services.TryAddSingleton<IAuthenticationProvider>(sp =>
        {
            var options = sp.GetRequiredService<IOptions<NacosConfigOptions>>();
            var httpClientFactory = sp.GetRequiredService<IHttpClientFactory>();
            var serverSelector = sp.GetRequiredService<IServerSelector>();

            // Priority: Username/Password > AK/SK > Null
            if (!string.IsNullOrWhiteSpace(options.Value.UserName))
            {
                var logger = sp.GetRequiredService<ILogger<UsernamePasswordAuthProvider>>();
                var provider = new UsernamePasswordAuthProvider(httpClientFactory, serverSelector, options, logger);

                // Initialize authentication
                provider.InitializeAsync().GetAwaiter().GetResult();

                return provider;
            }

            if (!string.IsNullOrWhiteSpace(options.Value.AccessKey) &&
                !string.IsNullOrWhiteSpace(options.Value.SecretKey))
            {
                var logger = sp.GetRequiredService<ILogger<AkSkAuthProvider>>();
                var provider = new AkSkAuthProvider(options, logger);

                provider.InitializeAsync().GetAwaiter().GetResult();

                return provider;
            }

            return new NullAuthProvider();
        });

        // Register client
        services.TryAddSingleton<INacosConfigClient, NacosConfigClient>();

        // Register storage
        services.TryAddSingleton<ILocalConfigStorage, FileLocalConfigStorage>();

        // Register listening manager
        services.TryAddSingleton<IConfigListeningManager, ConfigListeningManager>();

        // Register core service
        services.TryAddSingleton<INacosConfigService, NacosConfigService>();

        return services;
    }

    /// <summary>
    ///     Get Polly retry policy for HTTP requests
    /// </summary>
    private static IAsyncPolicy<HttpResponseMessage> GetRetryPolicy(NacosConfigOptions options)
    {
        return HttpPolicyExtensions
            .HandleTransientHttpError() // Handles 5xx and 408
            .OrResult(msg => msg.StatusCode == HttpStatusCode.TooManyRequests) // 429
            .WaitAndRetryAsync(
                options.MaxRetry,
                retryAttempt =>
                {
                    // Exponential backoff: RetryDelayMs * 2^(retryAttempt-1)
                    // Attempt 1: RetryDelayMs
                    // Attempt 2: RetryDelayMs * 2
                    // Attempt 3: RetryDelayMs * 4
                    var delay = options.RetryDelayMs * Math.Pow(2, retryAttempt - 1);
                    return TimeSpan.FromMilliseconds(delay);
                },
                (outcome, timespan, retryAttempt, context) =>
                {
                    // Log retry attempts - uses Debug.WriteLine for diagnostic output
                    var statusCode = outcome.Result?.StatusCode.ToString() ?? "Exception";
                    System.Diagnostics.Debug.WriteLine(
                        $"[NacosConfigSdk] Retry {retryAttempt}/{options.MaxRetry} after {timespan.TotalSeconds:F1}s due to {statusCode}");
                });
    }
}