using Xunit;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Nacos.V2.Config.Core;
using Nacos.V2.Config.Extensions;

namespace NacosConfigTests;

/// <summary>
/// Integration tests for basic config operations
/// Requires running Nacos server
/// </summary>
public class ConfigOperationsTests : IAsyncLifetime
{
    private ServiceProvider _serviceProvider = null!;
    private INacosConfigService _configService = null!;
    private ILogger<ConfigOperationsTests> _logger = null!;

    public async Task InitializeAsync()
    {
        // Load configuration from appsettings.Test.json
        var configuration = new ConfigurationBuilder()
            .SetBasePath(Directory.GetCurrentDirectory())
            .AddJsonFile("appsettings.Test.json", optional: false)
            .Build();

        var services = new ServiceCollection();

        // Configure logging
        services.AddLogging(builder =>
        {
            builder.AddConsole();
            builder.SetMinimumLevel(LogLevel.Information);
        });

        // Add Nacos configuration service from config file
        services.AddNacosConfigService(options =>
        {
            configuration.GetSection("Nacos").Bind(options);
        });

        _serviceProvider = services.BuildServiceProvider();
        _configService = _serviceProvider.GetRequiredService<INacosConfigService>();
        _logger = _serviceProvider.GetRequiredService<ILogger<ConfigOperationsTests>>();

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
                // Expected during test cleanup when ConfigListeningManager disposes
                // Channel may already be closed, which is fine
            }
        }
    }

    [Fact]
    public async Task PublishConfig_ShouldSucceed()
    {
        // Arrange
        var dataId = "test-config";
        var group = "DEFAULT_GROUP";
        var content = """
            {
                "name": "Test Config",
                "version": "1.0.0"
            }
            """;

        try
        {
            // Act
            var result = await _configService.PublishConfigAsync(dataId, group, content, "json");

            // Assert
            Assert.True(result, "Publish config should succeed");
        }
        finally
        {
            // Cleanup
            await _configService.RemoveConfigAsync(dataId, group);
        }
    }

    [Fact]
    public async Task GetConfig_ShouldReturnPublishedContent()
    {
        // Arrange
        var dataId = "test-get-config";
        var group = "DEFAULT_GROUP";
        var expectedContent = """
            {
                "test": "value"
            }
            """;

        try
        {
            // Act - Publish first
            await _configService.PublishConfigAsync(dataId, group, expectedContent, "json");

            // Small delay to ensure propagation
            await Task.Delay(500);

            // Act - Get
            var retrievedContent = await _configService.GetConfigAsync(dataId, group);

            // Assert
            Assert.NotNull(retrievedContent);
            Assert.Equal(expectedContent.Trim(), retrievedContent!.Trim());
        }
        finally
        {
            // Cleanup
            await _configService.RemoveConfigAsync(dataId, group);
        }
    }

    [Fact]
    public async Task RemoveConfig_ShouldDeleteConfiguration()
    {
        // Arrange
        var dataId = "test-remove-config";
        var group = "DEFAULT_GROUP";
        var content = "test content";

        // Act - Publish first
        await _configService.PublishConfigAsync(dataId, group, content);
        await Task.Delay(500);

        // Act - Remove
        var removeResult = await _configService.RemoveConfigAsync(dataId, group);

        // Assert
        Assert.True(removeResult, "Remove should succeed");

        // Verify it's gone
        await Task.Delay(500);
        var retrievedContent = await _configService.GetConfigAsync(dataId, group);
        Assert.Null(retrievedContent);
    }

    [Fact]
    public async Task UpdateConfig_ShouldOverwriteExistingContent()
    {
        // Arrange
        var dataId = "test-update-config";
        var group = "DEFAULT_GROUP";
        var initialContent = "version 1";
        var updatedContent = "version 2";

        try
        {
            // Act - Publish initial
            await _configService.PublishConfigAsync(dataId, group, initialContent);
            await Task.Delay(500);

            // Act - Update
            await _configService.PublishConfigAsync(dataId, group, updatedContent);
            await Task.Delay(500);

            // Act - Get
            var retrievedContent = await _configService.GetConfigAsync(dataId, group);

            // Assert
            Assert.Equal(updatedContent, retrievedContent);
        }
        finally
        {
            // Cleanup
            await _configService.RemoveConfigAsync(dataId, group);
        }
    }
}
