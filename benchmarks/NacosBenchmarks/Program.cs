using BenchmarkDotNet.Attributes;
using BenchmarkDotNet.Running;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Nacos.V2.Config.Core;
using Nacos.V2.Config.Extensions;

namespace NacosBenchmarks;

class Program
{
    static void Main(string[] args)
    {
        //var summary = BenchmarkRunner.Run<ConfigOperationBenchmarks>();
       var summary = BenchmarkRunner.Run<ConnectionPoolingBenchmarks>();
    }
}

[MemoryDiagnoser]
[SimpleJob(warmupCount: 3, iterationCount: 10)]
public class ConfigOperationBenchmarks
{
    private ServiceProvider _serviceProvider = null!;
    private INacosConfigService _configService = null!;
    private const string TestDataId = "benchmark-config";
    private const string TestGroup = "BENCHMARK_GROUP";

    [GlobalSetup]
    public void Setup()
    {
        var configuration = new ConfigurationBuilder()
            .SetBasePath(Directory.GetCurrentDirectory())
            .AddJsonFile("appsettings.Benchmark.json", optional: false)
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
        catch (System.Threading.Channels.ChannelClosedException)
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
    private ServiceProvider _serviceProvider = null!;
    private INacosConfigService _configService = null!;

    [GlobalSetup]
    public void Setup()
    {
        var configuration = new ConfigurationBuilder()
            .SetBasePath(Directory.GetCurrentDirectory())
            .AddJsonFile("appsettings.Benchmark.json", optional: false)
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
        catch (System.Threading.Channels.ChannelClosedException)
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

        for (int i = 0; i < requestCount; i++)
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
    private ServiceProvider _serviceProvider = null!;
    private INacosConfigService _configService = null!;
    private const int ConfigCount = 100;

    [Params(true, false)] public bool EnableSnapshot { get; set; }

    [GlobalSetup]
    public void Setup()
    {
        var configuration = new ConfigurationBuilder()
            .SetBasePath(Directory.GetCurrentDirectory())
            .AddJsonFile("appsettings.Benchmark.json", optional: false)
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
        catch (System.Threading.Channels.ChannelClosedException)
        {
            // Expected during cleanup, channel already closed
        }
    }

    [Benchmark]
    public async Task GetMultipleConfigs()
    {
        var tasks = new Task<string?>[ConfigCount];

        for (int i = 0; i < ConfigCount; i++)
        {
            var dataId = $"snapshot-test-{i % 10}";
            tasks[i] = _configService.GetConfigAsync(dataId, "DEFAULT_GROUP");
        }

        await Task.WhenAll(tasks);
    }
}
