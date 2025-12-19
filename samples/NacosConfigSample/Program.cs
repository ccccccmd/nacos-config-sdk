using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Nacos.Config.Core;
using Nacos.Config.Extensions;
using Nacos.Config.Models;

var builder = Host.CreateApplicationBuilder(args);

// Configure logging
builder.Logging.ClearProviders();
builder.Logging.AddConsole();
builder.Logging.SetMinimumLevel(LogLevel.Debug);

// Add Nacos configuration service
// builder.Services.AddNacosConfigService(options => { configuration.GetSection("Nacos").Bind(options); });
builder.Services.AddNacosConfigService(options =>
{
    // Nacos server address
    options.ServerAddresses = new List<string> { "http://192.168.1.55:8848" };

    // Namespace (optional)
    options.Namespace = "1fbbeafb-13be-494a-b0ab-b54f89077a54";

    // Authentication (optional) - uncomment one of the following:

    // Option 1: Username/Password authentication
    options.UserName = "nacos";
    options.Password = "nacos";
    // Option 2: AK/SK authentication
    // options.AccessKey = "your-access-key";
    // options.SecretKey = "your-secret-key";

    // Enable local snapshot
    options.EnableSnapshot = true;

    // Timeout settings
    options.DefaultTimeoutMs = 10000;
});

var host = builder.Build();

// Get the service and logger
var configService = host.Services.GetRequiredService<INacosConfigService>();
var logger = host.Services.GetRequiredService<ILogger<Program>>();

try
{
    logger.LogInformation("=== Nacos Config SDK Sample ===\n");

    var testBasicConfig = false;

    if (testBasicConfig)
    {
        // Example 1: Publish configuration
        logger.LogInformation("--- Publishing configuration ---");
        var publishResult = await configService.PublishConfigAsync(
            "example-config",
            "DEFAULT_GROUP",
            """
            {
                "name": "Nacos Config Sample",
                "version": "2.0.0",
                "environment": "development"
            }
            """,
            "json"
        ).ConfigureAwait(false);
        logger.LogInformation("Publish result: {Result}\n", publishResult);


        await configService.RemoveConfigAsync("example-config", "DEFAULT_GROUP").ConfigureAwait(false);

        // Example 2: Get configuration
        logger.LogInformation("--- Getting configuration ---");
        var config = await configService.GetConfigAsync(
            "example-config",
            "DEFAULT_GROUP"
        ).ConfigureAwait(false);
        logger.LogInformation("Retrieved config:\n{Config}\n", config);

        // Example 3: Update configuration
        logger.LogInformation("--- Updating configuration ---");
        var updateResult = await configService.PublishConfigAsync(
            "example-config",
            "DEFAULT_GROUP",
            """
            {
                "name": "Nacos Config Sample",
                "version": "2.1.0",
                "environment": "production",
                "updated": true
            }
            """,
            "json"
        ).ConfigureAwait(false);
        logger.LogInformation("Update result: {Result}\n", updateResult);

        // Example 4: Get updated configuration
        logger.LogInformation("--- Getting updated configuration ---");
        var updatedConfig = await configService.GetConfigAsync(
            "example-config",
            "DEFAULT_GROUP"
        ).ConfigureAwait(false);
        logger.LogInformation("Updated config:\n{Config}\n", updatedConfig);

        // Example 5: Subscribe to configuration changes
        logger.LogInformation("--- Subscribe to configuration changes ---");
        var subscription = configService.Subscribe(
            "example-config",
            "DEFAULT_GROUP",
            evt =>
            {
                logger.LogInformation("Config changed! Old: {Old}, New: {New}",
                    evt.OldContent, evt.NewContent);
            }
        );
        logger.LogInformation("Subscribed to config changes (Note: Listening manager not yet fully implemented)\n");

        // Example 6: Test different groups
        logger.LogInformation("--- Testing different groups ---");
        await configService.PublishConfigAsync(
            "database-config",
            "DATABASE_GROUP",
            """
            {
                "host": "localhost",
                "port": 3306,
                "database": "nacos_demo"
            }
            """,
            "json"
        ).ConfigureAwait(false);

        var dbConfig = await configService.GetConfigAsync(
            "database-config",
            "DATABASE_GROUP"
        ).ConfigureAwait(false);
        logger.LogInformation("Database config:\n{Config}\n", dbConfig);

        // Clean up
        logger.LogInformation("--- Cleaning up ---");
        subscription.Dispose();
        logger.LogInformation("Unsubscribed from config changes");

        // Optional: Remove test configurations
        // Uncomment the following lines if you want to clean up test data
        /*
        await configService.RemoveConfigAsync("example-config", "DEFAULT_GROUP");
        await configService.RemoveConfigAsync("database-config", "DATABASE_GROUP");
        logger.LogInformation("Removed test configurations");
        */
    }
    // ========================================
    // Advanced Listening Manager Tests
    // ========================================

    logger.LogInformation("\\n\\n=== Advanced Configuration Listening Tests ===\\n");

    // Example 7: Basic Configuration Listening Test
    logger.LogInformation("--- Example 7: Basic Configuration Listening ---");
    {
        var changeDetected = new TaskCompletionSource<ConfigChangedEvent>();
        var listenerConfig = "listener-test-config";

        // Subscribe to configuration changes
        var sub = configService.Subscribe(
            listenerConfig,
            "DEFAULT_GROUP",
            evt =>
            {
                logger.LogInformation("[Example 7] Config changed! Old: '{Old}', New: '{New}'",
                    evt.OldContent, evt.NewContent);
                changeDetected.TrySetResult(evt);
            }
        );

        logger.LogInformation("Subscribed to {DataId}/DEFAULT_GROUP", listenerConfig);

        // Give the listening manager time to start and poll
        await Task.Delay(2000).ConfigureAwait(false);


        // Publish initial configuration
        await configService.PublishConfigAsync(
            listenerConfig,
            "DEFAULT_GROUP",
            "Initial Value v1" + Guid.NewGuid(),
            "text"
        ).ConfigureAwait(false);

        logger.LogInformation("Published initial config, waiting for change detection...");

        // Wait for the listener to be triggered (with timeout)
        var changeTask = await Task.WhenAny(
            changeDetected.Task,
            Task.Delay(35000) // Long enough for one polling cycle
        ).ConfigureAwait(false);

        if (changeTask == changeDetected.Task)
        {
            var evt = await changeDetected.Task.ConfigureAwait(false);
            logger.LogInformation("✓ Listener triggered successfully! New content: {Content}\\n", evt.NewContent);
        }
        else
        {
            logger.LogWarning("✗ Listener was not triggered within timeout\\n");
        }

        sub.Dispose();
    }


    // Example 8: Multiple Listeners on Same Configuration
    logger.LogInformation("--- Example 8: Multiple Listeners on Same Configuration ---");
    {
        var listener1Triggered = new TaskCompletionSource<bool>();
        var listener2Triggered = new TaskCompletionSource<bool>();
        var listener3Triggered = new TaskCompletionSource<bool>();
        var multiListenerConfig = "multi-listener-config";

        // Add multiple listeners to the same configuration
        var sub1 = configService.Subscribe(
            multiListenerConfig,
            "DEFAULT_GROUP",
            evt =>
            {
                logger.LogInformation("[Listener 1] Received change: {New}", evt.NewContent);
                listener1Triggered.TrySetResult(true);
            }
        );

        var sub2 = configService.Subscribe(
            multiListenerConfig,
            "DEFAULT_GROUP",
            evt =>
            {
                logger.LogInformation("[Listener 2] Received change: {New}", evt.NewContent);
                listener2Triggered.TrySetResult(true);
            }
        );

        var sub3 = configService.Subscribe(
            multiListenerConfig,
            "DEFAULT_GROUP",
            evt =>
            {
                logger.LogInformation("[Listener 3] Received change: {New}", evt.NewContent);
                listener3Triggered.TrySetResult(true);
            }
        );

        logger.LogInformation("Added 3 listeners to {DataId}/DEFAULT_GROUP", multiListenerConfig);
        await Task.Delay(2000).ConfigureAwait(false);

        // Publish change
        await configService.PublishConfigAsync(
            multiListenerConfig,
            "DEFAULT_GROUP",
            "Shared Config Content" + Guid.NewGuid(),
            "text"
        ).ConfigureAwait(false);

        logger.LogInformation("Published config change, waiting for all listeners...");

        // Wait for all listeners (with timeout)
        var allListenersTask = Task.WhenAll(
            listener1Triggered.Task,
            listener2Triggered.Task,
            listener3Triggered.Task
        );

        var completedTask = await Task.WhenAny(
            allListenersTask,
            Task.Delay(35000)
        ).ConfigureAwait(false);

        if (completedTask == allListenersTask)
        {
            logger.LogInformation("✓ All 3 listeners were triggered successfully!\\n");
        }
        else
        {
            logger.LogWarning("✗ Not all listeners were triggered within timeout\\n");
        }

        sub1.Dispose();
        sub2.Dispose();
        sub3.Dispose();
    }

    // Example 9: Multiple Configurations Listening
    logger.LogInformation("--- Example 9: Multiple Configurations Listening ---");
    {
        var config1Changed = new TaskCompletionSource<string>();
        var config2Changed = new TaskCompletionSource<string>();

        // Subscribe to two different configurations
        var sub1 = configService.Subscribe(
            "app-config",
            "CONFIG_GROUP_1",
            evt =>
            {
                logger.LogInformation("[Config 1] app-config/CONFIG_GROUP_1 changed: {New}", evt.NewContent);
                config1Changed.TrySetResult(evt.NewContent);
            }
        );

        var sub2 = configService.Subscribe(
            "db-config",
            "CONFIG_GROUP_2",
            evt =>
            {
                logger.LogInformation("[Config 2] db-config/CONFIG_GROUP_2 changed: {New}", evt.NewContent);
                config2Changed.TrySetResult(evt.NewContent);
            }
        );

        logger.LogInformation("Subscribed to 2 different configurations");
        await Task.Delay(2000).ConfigureAwait(false);

        // Publish to first config
        await configService.PublishConfigAsync(
            "app-config",
            "CONFIG_GROUP_1",
            "App Config v1" + Guid.NewGuid(),
            "text"
        ).ConfigureAwait(false);

        logger.LogInformation("Published to app-config/CONFIG_GROUP_1");

        // Wait a bit then publish to second config
        await Task.Delay(5000).ConfigureAwait(false);

        await configService.PublishConfigAsync(
            "db-config",
            "CONFIG_GROUP_2",
            "DB Config v1",
            "text"
        ).ConfigureAwait(false);

        logger.LogInformation("Published to db-config/CONFIG_GROUP_2");

        // Wait for both changes (with timeout)
        var bothChangesTask = Task.WhenAll(config1Changed.Task, config2Changed.Task);
        var completedTask = await Task.WhenAny(
            bothChangesTask,
            Task.Delay(40000)
        ).ConfigureAwait(false);

        if (completedTask == bothChangesTask)
        {
            logger.LogInformation("✓ Both configurations were monitored independently!\\n");
        }
        else
        {
            logger.LogWarning("✗ Not all configuration changes were detected\\n");
        }

        sub1.Dispose();
        sub2.Dispose();
    }

    // Example 10: Listener Removal Test
    logger.LogInformation("--- Example 10: Listener Removal Test ---");
    {
        var firstChange = new TaskCompletionSource<bool>();
        var secondChange = new TaskCompletionSource<bool>();
        var removalTestConfig = "removal-test-config";
        var listenerCallCount = 0;

        // Add listener
        var sub = configService.Subscribe(
            removalTestConfig,
            "DEFAULT_GROUP",
            evt =>
            {
                listenerCallCount++;
                logger.LogInformation("[Removal Test] Change #{Count} detected: {New}",
                    listenerCallCount, evt.NewContent);

                if (listenerCallCount == 1)
                {
                    firstChange.TrySetResult(true);
                }
                else if (listenerCallCount == 2)
                {
                    secondChange.TrySetResult(true);
                }
            }
        );

        await Task.Delay(2000).ConfigureAwait(false);

        // First change - should be detected
        logger.LogInformation("Publishing first change (listener active)...");
        await configService.PublishConfigAsync(
            removalTestConfig,
            "DEFAULT_GROUP",
            "First Change" + Guid.NewGuid(),
            "text"
        ).ConfigureAwait(false);

        await Task.WhenAny(firstChange.Task, Task.Delay(35000)).ConfigureAwait(false);

        if (firstChange.Task.IsCompleted)
        {
            logger.LogInformation("✓ First change detected by listener");
        }

        // Remove listener
        logger.LogInformation("Removing listener...");
        sub.Dispose();
        await Task.Delay(2000).ConfigureAwait(false);

        // Second change - should NOT be detected
        logger.LogInformation("Publishing second change (listener removed)...");
        await configService.PublishConfigAsync(
            removalTestConfig,
            "DEFAULT_GROUP",
            "Second Change",
            "text"
        ).ConfigureAwait(false);

        await Task.WhenAny(secondChange.Task, Task.Delay(35000)).ConfigureAwait(false);

        if (!secondChange.Task.IsCompleted)
        {
            logger.LogInformation("✓ Second change was NOT detected (listener properly removed)");
            logger.LogInformation("✓ Listener removal works correctly!\\n");
        }
        else
        {
            logger.LogWarning("✗ Second change was unexpectedly detected\\n");
        }
    }

    // Example 11: Async Handling with Rapid Changes
    logger.LogInformation("--- Example 11: Async Handling with Rapid Changes ---");
    {
        var changesDetected = new List<string>();
        var changeLock = new object();
        var expectedChanges = 3;
        var allChangesDetected = new TaskCompletionSource<bool>();
        var rapidChangeConfig = "rapid-change-config";

        var sub = configService.Subscribe(
            rapidChangeConfig,
            "DEFAULT_GROUP",
            evt =>
            {
                lock (changeLock)
                {
                    changesDetected.Add(evt.NewContent);
                    logger.LogInformation("[Rapid Change] Change {Count}/{Total}: {Content}",
                        changesDetected.Count, expectedChanges, evt.NewContent);

                    if (changesDetected.Count >= expectedChanges)
                    {
                        allChangesDetected.TrySetResult(true);
                    }
                }
            }
        );

        await Task.Delay(2000).ConfigureAwait(false);

        // Publish multiple changes with delays to allow long-polling to detect them
        logger.LogInformation("Publishing 3 configuration changes...");

        await configService.PublishConfigAsync(
            rapidChangeConfig,
            "DEFAULT_GROUP",
            "Change #1",
            "text"
        ).ConfigureAwait(false);

        // Wait for long-polling cycle (30s timeout + processing time)
        logger.LogInformation("Waiting for first change to be detected...");
        await Task.Delay(35000).ConfigureAwait(false);

        await configService.PublishConfigAsync(
            rapidChangeConfig,
            "DEFAULT_GROUP",
            "Change #2",
            "text"
        ).ConfigureAwait(false);

        logger.LogInformation("Waiting for second change to be detected...");
        await Task.Delay(35000).ConfigureAwait(false);

        await configService.PublishConfigAsync(
            rapidChangeConfig,
            "DEFAULT_GROUP",
            "Change #3",
            "text"
        ).ConfigureAwait(false);

        logger.LogInformation("Waiting for third change to be detected...");
        await Task.WhenAny(allChangesDetected.Task, Task.Delay(35000)).ConfigureAwait(false);

        logger.LogInformation("Total changes detected: {Count}/{Expected}", changesDetected.Count, expectedChanges);

        if (changesDetected.Count >= expectedChanges)
        {
            logger.LogInformation("✓ All rapid changes were processed correctly!");
        }
        else
        {
            logger.LogInformation("Note: Detected {Count} out of {Expected} changes (long-polling may take time)",
                changesDetected.Count, expectedChanges);
        }

        logger.LogInformation("Detected values: {Values}\\n", string.Join(", ", changesDetected));

        sub.Dispose();
    }

    logger.LogInformation("\\n=== All Listening Manager tests completed ===");


    logger.LogInformation("\n=== Sample completed successfully ===");
}
catch (Exception ex)
{
    logger.LogError(ex, "Sample failed");
    return 1;
}

return 0;