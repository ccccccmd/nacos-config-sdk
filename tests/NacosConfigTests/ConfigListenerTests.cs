using System.Threading.Channels;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Nacos.Config.Core;
using Nacos.Config.Extensions;

namespace NacosConfigTests;

/// <summary>
///     Integration tests for configuration listening functionality
///     Requires running Nacos server
/// </summary>
public class ConfigListenerTests : IAsyncLifetime
{
    private INacosConfigService _configService = null!;
    private ILogger<ConfigListenerTests> _logger = null!;
    private ServiceProvider _serviceProvider = null!;

    public async Task InitializeAsync()
    {
        // Load configuration from appsettings.Test.json
        var configuration = new ConfigurationBuilder()
            .SetBasePath(Directory.GetCurrentDirectory())
            .AddJsonFile("appsettings.Test.json", false)
            .Build();

        var services = new ServiceCollection();

        services.AddLogging(builder =>
        {
            builder.AddConsole();
            builder.SetMinimumLevel(LogLevel.Information);
        });

        // Add Nacos configuration service from config file
        services.AddNacosConfigService(options => { configuration.GetSection("Nacos").Bind(options); });

        _serviceProvider = services.BuildServiceProvider();
        _configService = _serviceProvider.GetRequiredService<INacosConfigService>();
        _logger = _serviceProvider.GetRequiredService<ILogger<ConfigListenerTests>>();

        await Task.CompletedTask;
    }

    public async Task DisposeAsync()
    {
        if (_serviceProvider != null)
        {
            try
            {
                await _serviceProvider.DisposeAsync();
            }
            catch (ChannelClosedException)
            {
                // Expected during test cleanup when ConfigListeningManager disposes
                // Channel may already be closed, which is fine
            }
        }
    }

    [Fact]
    public async Task BasicListener_ShouldReceiveConfigChanges()
    {
        // Arrange
        var dataId = "listener-test-config";
        var group = "DEFAULT_GROUP";
        var initialContent = $"Initial content {Guid.NewGuid()}";
        var updatedContent = $"Updated content {Guid.NewGuid()}";

        var changeReceived = new TaskCompletionSource<bool>();
        var receivedConfig = "";
        var changeCount = 0;

        try
        {
            // Publish initial config
            await _configService.PublishConfigAsync(dataId, group, initialContent);
            await Task.Delay(1000);

            // Subscribe
            var subscription = _configService.Subscribe(dataId, group, e =>
            {
                _logger.LogInformation("Config changed: {Content}", e.NewContent);
                receivedConfig = e.NewContent ?? "";
                changeReceived.TrySetResult(true);
                changeCount++;
            });

            // Wait for listener to be established
            await Task.Delay(2000);

            // Act - Update config
            await _configService.PublishConfigAsync(dataId, group, updatedContent);

            await Task.Delay(2000);
            // Wait for change notification
            var received = await Task.WhenAny(
                changeReceived.Task,
                Task.Delay(TimeSpan.FromSeconds(45))
            );

            // Assert
            Assert.True(received == changeReceived.Task, "Should receive change notification");
            Assert.Equal(updatedContent, receivedConfig);

            // Cleanup subscription
            subscription.Dispose();
        }
        finally
        {
            await _configService.RemoveConfigAsync(dataId, group);
        }
    }

    [Fact]
    public async Task MultipleListeners_ShouldAllReceiveChanges()
    {
        // Arrange
        var dataId = "multi-listener-config";
        var group = "DEFAULT_GROUP";
        var initialContent = "Initial";
        var updatedContent = $"Updated {Guid.NewGuid()}";

        var listener1Received = new TaskCompletionSource<bool>();
        var listener2Received = new TaskCompletionSource<bool>();

        try
        {
            // Publish initial
            await _configService.PublishConfigAsync(dataId, group, initialContent);
            await Task.Delay(1000);

            // Subscribe with multiple listeners
            var sub1 = _configService.Subscribe(dataId, group, e =>
            {
                _logger.LogInformation("Listener 1 received: {Content}", e.NewContent);
                listener1Received.TrySetResult(true);
            });

            var sub2 = _configService.Subscribe(dataId, group, e =>
            {
                _logger.LogInformation("Listener 2 received: {Content}", e.NewContent);
                listener2Received.TrySetResult(true);
            });

            await Task.Delay(2000);

            // Act - Update
            await _configService.PublishConfigAsync(dataId, group, updatedContent);

            // Wait for both
            var task1 = await Task.WhenAny(listener1Received.Task, Task.Delay(45000));
            var task2 = await Task.WhenAny(listener2Received.Task, Task.Delay(45000));

            // Assert
            Assert.True(task1 == listener1Received.Task, "Listener 1 should receive");
            Assert.True(task2 == listener2Received.Task, "Listener 2 should receive");

            sub1.Dispose();
            sub2.Dispose();
        }
        finally
        {
            await _configService.RemoveConfigAsync(dataId, group);
        }
    }

    [Fact]
    public async Task ListenerRemoval_ShouldStopReceivingChanges()
    {
        // Arrange
        var dataId = "removal-test-config";
        var group = "DEFAULT_GROUP";
        var initialContent = "Initial";

        var receivedCount = 0;

        try
        {
            await _configService.PublishConfigAsync(dataId, group, initialContent);
            await Task.Delay(1000);

            // Subscribe
            var subscription = _configService.Subscribe(dataId, group, e =>
            {
                receivedCount++;
                _logger.LogInformation("Received change #{Count}", receivedCount);
            });

            await Task.Delay(2000);

            // First update - should receive
            await _configService.PublishConfigAsync(dataId, group, $"Update 1 {Guid.NewGuid()}");
            await Task.Delay(5000);

            var countAfterFirst = receivedCount;

            // Remove listener
            subscription.Dispose();
            await Task.Delay(2000);

            // Second update - should NOT receive
            await _configService.PublishConfigAsync(dataId, group, $"Update 2 {Guid.NewGuid()}");
            await Task.Delay(5000);

            // Assert
            Assert.True(countAfterFirst >= 1, "Should receive first change");
            Assert.Equal(countAfterFirst, receivedCount); // Count should not increase
        }
        finally
        {
            await _configService.RemoveConfigAsync(dataId, group);
        }
    }
}