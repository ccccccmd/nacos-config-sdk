using System.Threading.Channels;
using BenchmarkDotNet.Attributes;
using BenchmarkDotNet.Running;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Nacos.Config.Core;
using Nacos.Config.Extensions;

namespace NacosBenchmarks;

internal class Program
{
    private static void Main(string[] args)
    {
        //var summary = BenchmarkRunner.Run<ConfigOperationBenchmarks>();
        var summary = BenchmarkRunner.Run<ConnectionPoolingBenchmarks>();
    }
}

[MemoryDiagnoser]
[SimpleJob(warmupCount: 3, iterationCount: 10)]
public class ConfigOperationBenchmarks
{
    private const string TestDataId = "benchmark-config";
    private const string TestGroup = "BENCHMARK_GROUP";
    private INacosConfigService _configService = null!;
    private ServiceProvider _serviceProvider = null!;

    [GlobalSetup]
    public void Setup()
    {
        var configuration = new ConfigurationBuilder()
            .SetBasePath(Directory.GetCurrentDirectory())
            .AddJsonFile("appsettings.Benchmark.json", false)
            .Build();

        var services = new ServiceCollection();

        services.AddLogging(builder =>
        {
            builder.SetMinimumLevel(LogLevel.Warning); // Reduce noise
        });

        services.AddNacosConfigService(options => { configuration.GetSection("Nacos").Bind(options); });

        _serviceProvider = services.BuildServiceProvider();
        _configService = _serviceProvider.GetRequiredService<INacosConfigService>();

        // Warmup: publish test config
        _configService.PublishConfigAsync(TestDataId, TestGroup, "benchmark test content").GetAwaiter().GetResult();
    }

    [GlobalCleanup]
    public void Cleanup()
    {
        _configService.RemoveConfigAsync(TestDataId, TestGroup).GetAwaiter().GetResult();

        try
        {
            _serviceProvider?.Dispose();
        }
        catch (ChannelClosedException)
        {
            // Expected during cleanup, channel already closed
        }
    }

    [Benchmark]
    public async Task<string?> GetConfig()
    {
        return await _configService.GetConfigAsync(TestDataId, TestGroup);
    }

    [Benchmark]
    public async Task<bool> PublishConfig()
    {
        var content = $"Test content {Guid.NewGuid()}";
        return await _configService.PublishConfigAsync(TestDataId, TestGroup, content);
    }

    [Benchmark]
    public async Task GetAndPublish()
    {
        var config = await _configService.GetConfigAsync(TestDataId, TestGroup);
        await _configService.PublishConfigAsync(TestDataId, TestGroup, config + "_updated");
    }
}

[MemoryDiagnoser]
[SimpleJob(warmupCount: 2, iterationCount: 5)]
public class ConnectionPoolingBenchmarks
{
    private INacosConfigService _configService = null!;
    private ServiceProvider _serviceProvider = null!;

    [GlobalSetup]
    public void Setup()
    {
        var configuration = new ConfigurationBuilder()
            .SetBasePath(Directory.GetCurrentDirectory())
            .AddJsonFile("appsettings.Benchmark.json", false)
            .Build();

        var services = new ServiceCollection();

        services.AddLogging(builder => builder.SetMinimumLevel(LogLevel.Warning));

        services.AddNacosConfigService(options =>
        {
            configuration.GetSection("Nacos").Bind(options);
            options.EnableSnapshot = false; // Disable for benchmarking
        });

        _serviceProvider = services.BuildServiceProvider();
        _configService = _serviceProvider.GetRequiredService<INacosConfigService>();
    }

    [GlobalCleanup]
    public void Cleanup()
    {
        try
        {
            _serviceProvider?.Dispose();
        }
        catch (ChannelClosedException)
        {
            // Expected during cleanup, channel already closed
        }
    }

    [Benchmark]
    [Arguments(10)]
    [Arguments(50)]
    [Arguments(100)]
    public async Task ConcurrentRequests(int requestCount)
    {
        var tasks = new Task[requestCount];

        for (var i = 0; i < requestCount; i++)
        {
            var dataId = $"concurrent-config-{i % 10}"; // Reuse 10 configs
            tasks[i] = _configService.GetConfigAsync(dataId, "DEFAULT_GROUP");
        }

        await Task.WhenAll(tasks);
    }
}

[MemoryDiagnoser]
[SimpleJob(warmupCount: 1, iterationCount: 3)]
public class SnapshotPerformanceBenchmarks
{
    private const int ConfigCount = 100;
    private INacosConfigService _configService = null!;
    private ServiceProvider _serviceProvider = null!;

    [Params(true, false)] public bool EnableSnapshot { get; set; }

    [GlobalSetup]
    public void Setup()
    {
        var configuration = new ConfigurationBuilder()
            .SetBasePath(Directory.GetCurrentDirectory())
            .AddJsonFile("appsettings.Benchmark.json", false)
            .Build();

        var services = new ServiceCollection();

        services.AddLogging(builder => builder.SetMinimumLevel(LogLevel.Warning));

        services.AddNacosConfigService(options =>
        {
            configuration.GetSection("Nacos").Bind(options);
            options.EnableSnapshot = EnableSnapshot;
        });

        _serviceProvider = services.BuildServiceProvider();
        _configService = _serviceProvider.GetRequiredService<INacosConfigService>();
    }

    [GlobalCleanup]
    public void Cleanup()
    {
        try
        {
            _serviceProvider?.Dispose();
        }
        catch (ChannelClosedException)
        {
            // Expected during cleanup, channel already closed
        }
    }

    [Benchmark]
    public async Task GetMultipleConfigs()
    {
        var tasks = new Task<string?>[ConfigCount];

        for (var i = 0; i < ConfigCount; i++)
        {
            var dataId = $"snapshot-test-{i % 10}";
            tasks[i] = _configService.GetConfigAsync(dataId, "DEFAULT_GROUP");
        }

        await Task.WhenAll(tasks);
    }
}