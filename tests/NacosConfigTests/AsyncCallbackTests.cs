using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Nacos.Config.Core;
using Nacos.Config.Extensions;

namespace NacosConfigTests;

/// <summary>
///     Tests for async callback support in configuration listening
///     Requires running Nacos server
/// </summary>
public class AsyncCallbackTests : IAsyncLifetime
{
    private INacosConfigService _configService = null!;
    private ILogger<AsyncCallbackTests> _logger = null!;
    private ServiceProvider _serviceProvider = null!;

    public async Task InitializeAsync()
    {
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

        services.AddNacosConfigService(options => { configuration.GetSection("Nacos").Bind(options); });

        _serviceProvider = services.BuildServiceProvider();
        _configService = _serviceProvider.GetRequiredService<INacosConfigService>();
        _logger = _serviceProvider.GetRequiredService<ILogger<AsyncCallbackTests>>();

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
            catch (System.Threading.Channels.ChannelClosedException)
            {
                // Expected during cleanup
            }
        }
    }

    [Fact]
    public async Task BasicAsyncCallback_ShouldReceiveChanges()
    {
        // Arrange
        var dataId = "async-callback-test";
        var group = "DEFAULT_GROUP";
        var initialContent = $"Initial {Guid.NewGuid()}";
        var updatedContent = $"Updated {Guid.NewGuid()}";

        var changeReceived = new TaskCompletionSource<bool>();
        var receivedContent = "";
        var asyncWorkCompleted = false;

        try
        {
            await _configService.PublishConfigAsync(dataId, group, initialContent);
            await Task.Delay(1000);

            // Subscribe with async callback
            var subscription = _configService.Subscribe(dataId, group, async evt =>
            {
                _logger.LogInformation("Async callback started: {Content}", evt.NewContent);
                
                // Simulate async work
                await Task.Delay(50);
                asyncWorkCompleted = true;
                
                receivedContent = evt.NewContent ?? "";
                changeReceived.TrySetResult(true);
                
                _logger.LogInformation("Async callback completed");
            });

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
            Assert.True(received == changeReceived.Task, "Should receive async callback");
            Assert.Equal(updatedContent, receivedContent);
            Assert.True(asyncWorkCompleted, "Async work should complete");

            subscription.Dispose();
        }
        finally
        {
            await _configService.RemoveConfigAsync(dataId, group);
        }
    }

    [Fact]
    public async Task MixedSyncAndAsyncCallbacks_ShouldBothWork()
    {
        // Arrange
        var dataId = "mixed-callback-test";
        var group = "DEFAULT_GROUP";
        var initialContent = "Initial";
        var updatedContent = $"Updated {Guid.NewGuid()}";

        var syncReceived = new TaskCompletionSource<bool>();
        var asyncReceived = new TaskCompletionSource<bool>();
        var syncContent = "";
        var asyncContent = "";

        try
        {
            await _configService.PublishConfigAsync(dataId, group, initialContent);
            await Task.Delay(1000);

            // Subscribe with sync callback
            var syncSub = _configService.Subscribe(dataId, group, evt =>
            {
                _logger.LogInformation("Sync callback received: {Content}", evt.NewContent);
                syncContent = evt.NewContent ?? "";
                syncReceived.TrySetResult(true);
            });

            // Subscribe with async callback
            var asyncSub = _configService.Subscribe(dataId, group, async evt =>
            {
                _logger.LogInformation("Async callback started: {Content}", evt.NewContent);
                await Task.Delay(50); // Simulate async work
                asyncContent = evt.NewContent ?? "";
                asyncReceived.TrySetResult(true);
                _logger.LogInformation("Async callback completed");
            });

            await Task.Delay(2000);

            // Act
            await _configService.PublishConfigAsync(dataId, group, updatedContent);
            await Task.Delay(2000);
            // Wait for both
            var syncTask = await Task.WhenAny(syncReceived.Task, Task.Delay(45000));
            var asyncTask = await Task.WhenAny(asyncReceived.Task, Task.Delay(45000));

            // Assert
            Assert.True(syncTask == syncReceived.Task, "Sync callback should receive");
            Assert.True(asyncTask == asyncReceived.Task, "Async callback should receive");
            Assert.Equal(updatedContent, syncContent);
            Assert.Equal(updatedContent, asyncContent);

            syncSub.Dispose();
            asyncSub.Dispose();
        }
        finally
        {
            await _configService.RemoveConfigAsync(dataId, group);
        }
    }

    [Fact]
    public async Task AsyncCallbackException_ShouldNotCrashListener()
    {
        // Arrange
        var dataId = "async-exception-test";
        var group = "DEFAULT_GROUP";
        var initialContent = "Initial";
        var updatedContent = $"Updated {Guid.NewGuid()}";

        var badCallbackExecuted = new TaskCompletionSource<bool>();
        var goodCallbackReceived = new TaskCompletionSource<bool>();

        try
        {
            await _configService.PublishConfigAsync(dataId, group, initialContent);
            await Task.Delay(1000);

            // Add a bad async callback that throws
            var badSub = _configService.Subscribe(dataId, group, async evt =>
            {
                badCallbackExecuted.TrySetResult(true);
                await Task.Delay(10);
                throw new InvalidOperationException("Test exception in async callback");
            });

            // Add a good async callback
            var goodSub = _configService.Subscribe(dataId, group, async evt =>
            {
                await Task.Delay(10);
                goodCallbackReceived.TrySetResult(true);
                _logger.LogInformation("Good callback executed successfully");
            });

            await Task.Delay(2000);

            // Act
            await _configService.PublishConfigAsync(dataId, group, updatedContent);
            await Task.Delay(2000);
            // Wait
            var badTask = await Task.WhenAny(badCallbackExecuted.Task, Task.Delay(45000));
            var goodTask = await Task.WhenAny(goodCallbackReceived.Task, Task.Delay(45000));

            // Assert - Both callbacks should execute despite one throwing
            Assert.True(badTask == badCallbackExecuted.Task, "Bad callback should execute");
            Assert.True(goodTask == goodCallbackReceived.Task, "Good callback should still execute");

            badSub.Dispose();
            goodSub.Dispose();
        }
        finally
        {
            await _configService.RemoveConfigAsync(dataId, group);
        }
    }

    [Fact]
    public async Task AsyncCallbackDisposal_ShouldStopReceiving()
    {
        // Arrange
        var dataId = "async-disposal-test";
        var group = "DEFAULT_GROUP";
        var initialContent = "Initial";

        var receivedCount = 0;

        try
        {
            await _configService.PublishConfigAsync(dataId, group, initialContent);
            await Task.Delay(1000);

            // Subscribe with async callback
            var subscription = _configService.Subscribe(dataId, group, async evt =>
            {
                await Task.Delay(10);
                receivedCount++;
                _logger.LogInformation("Async callback #{Count}", receivedCount);
            });

            await Task.Delay(2000);

            // First update - should receive
            await _configService.PublishConfigAsync(dataId, group, $"Update 1 {Guid.NewGuid()}");
            await Task.Delay(5000);

            var countAfterFirst = receivedCount;

            // Dispose subscription
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

    [Fact]
    public async Task AsyncCallback_WithLongRunningOperation_ShouldAwait()
    {
        // Arrange
        var dataId = "async-longrunning-test";
        var group = "DEFAULT_GROUP";
        var initialContent = "Initial";
        var updatedContent = $"Updated {Guid.NewGuid()}";

        var operationStarted = new TaskCompletionSource<bool>();
        var operationCompleted = new TaskCompletionSource<bool>();
        var completionTime = DateTimeOffset.MinValue;

        try
        {
            await _configService.PublishConfigAsync(dataId, group, initialContent);
            await Task.Delay(1000);

            var subscription = _configService.Subscribe(dataId, group, async evt =>
            {
                operationStarted.TrySetResult(true);
                _logger.LogInformation("Starting long-running async operation");
                
                // Simulate long-running async operation
                await Task.Delay(500);
                
                completionTime = DateTimeOffset.UtcNow;
                operationCompleted.TrySetResult(true);
                _logger.LogInformation("Long-running async operation completed");
            });

            await Task.Delay(2000);

            // Act
            var triggerTime = DateTimeOffset.UtcNow;
            await _configService.PublishConfigAsync(dataId, group, updatedContent);
            await Task.Delay(2000);
            // Wait for completion
            var started = await Task.WhenAny(operationStarted.Task, Task.Delay(45000));
            var completed = await Task.WhenAny(operationCompleted.Task, Task.Delay(45000));

            // Assert
            Assert.True(started == operationStarted.Task, "Operation should start");
            Assert.True(completed == operationCompleted.Task, "Operation should complete");
            Assert.True(completionTime > triggerTime, "Completion time should be after trigger");
            Assert.True((completionTime - triggerTime).TotalMilliseconds >= 500, 
                "Should wait for async operation to complete");

            subscription.Dispose();
        }
        finally
        {
            await _configService.RemoveConfigAsync(dataId, group);
        }
    }
}
