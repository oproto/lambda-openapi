using Microsoft.Extensions.Logging;
using Moq;
using Oproto.Lambda.OpenApi.Merge;
using Oproto.Lambda.OpenApi.Merge.Lambda.Models;
using Oproto.Lambda.OpenApi.Merge.Lambda.Services;

namespace Oproto.Lambda.OpenApi.Merge.Lambda.Tests;

/// <summary>
/// Unit tests for SourceDiscovery.
/// </summary>
public class SourceDiscoveryTests
{
    private readonly Mock<IS3Service> _mockS3Service;
    private readonly Mock<ILogger<SourceDiscovery>> _mockLogger;
    private readonly SourceDiscovery _sourceDiscovery;

    public SourceDiscoveryTests()
    {
        _mockS3Service = new Mock<IS3Service>();
        _mockLogger = new Mock<ILogger<SourceDiscovery>>();
        _sourceDiscovery = new SourceDiscovery(_mockS3Service.Object, _mockLogger.Object);
    }

    #region Auto-Discover Mode Tests

    [Fact]
    public async Task DiscoverSourcesAsync_AutoDiscover_FindsJsonFiles()
    {
        // Arrange
        var keys = new List<string>
        {
            "api/config.json",
            "api/users.json",
            "api/orders.json",
            "api/products.json"
        };

        _mockS3Service
            .Setup(x => x.ListObjectsAsync("test-bucket", "api/", It.IsAny<CancellationToken>()))
            .ReturnsAsync(keys);

        var config = CreateAutoDiscoverConfig("merged.json");

        // Act
        var result = await _sourceDiscovery.DiscoverSourcesAsync("test-bucket", "api/", config);

        // Assert
        Assert.Equal(3, result.Count);
        Assert.Contains(result, s => s.Key == "api/users.json");
        Assert.Contains(result, s => s.Key == "api/orders.json");
        Assert.Contains(result, s => s.Key == "api/products.json");
    }

    [Fact]
    public async Task DiscoverSourcesAsync_AutoDiscover_ExcludesConfigJson()
    {
        // Arrange
        var keys = new List<string>
        {
            "api/config.json",
            "api/users.json"
        };

        _mockS3Service
            .Setup(x => x.ListObjectsAsync("test-bucket", "api/", It.IsAny<CancellationToken>()))
            .ReturnsAsync(keys);

        var config = CreateAutoDiscoverConfig("merged.json");

        // Act
        var result = await _sourceDiscovery.DiscoverSourcesAsync("test-bucket", "api/", config);

        // Assert
        Assert.Single(result);
        Assert.DoesNotContain(result, s => s.Key.EndsWith("config.json"));
    }


    [Fact]
    public async Task DiscoverSourcesAsync_AutoDiscover_ExcludesOutputFile()
    {
        // Arrange
        var keys = new List<string>
        {
            "api/config.json",
            "api/users.json",
            "api/merged.json"  // This is the output file
        };

        _mockS3Service
            .Setup(x => x.ListObjectsAsync("test-bucket", "api/", It.IsAny<CancellationToken>()))
            .ReturnsAsync(keys);

        var config = CreateAutoDiscoverConfig("merged.json");

        // Act
        var result = await _sourceDiscovery.DiscoverSourcesAsync("test-bucket", "api/", config);

        // Assert
        Assert.Single(result);
        Assert.DoesNotContain(result, s => s.Key == "api/merged.json");
    }

    [Fact]
    public async Task DiscoverSourcesAsync_AutoDiscover_AppliesExcludePatterns()
    {
        // Arrange
        var keys = new List<string>
        {
            "api/config.json",
            "api/users.json",
            "api/users-draft.json",  // Should be excluded by pattern
            "api/orders.json",
            "api/orders-draft.json"  // Should be excluded by pattern
        };

        _mockS3Service
            .Setup(x => x.ListObjectsAsync("test-bucket", "api/", It.IsAny<CancellationToken>()))
            .ReturnsAsync(keys);

        var config = CreateAutoDiscoverConfig("merged.json", new List<string> { "*-draft.json" });

        // Act
        var result = await _sourceDiscovery.DiscoverSourcesAsync("test-bucket", "api/", config);

        // Assert
        Assert.Equal(2, result.Count);
        Assert.Contains(result, s => s.Key == "api/users.json");
        Assert.Contains(result, s => s.Key == "api/orders.json");
        Assert.DoesNotContain(result, s => s.Key.Contains("-draft.json"));
    }

    [Fact]
    public async Task DiscoverSourcesAsync_AutoDiscover_ExcludesNonJsonFiles()
    {
        // Arrange
        var keys = new List<string>
        {
            "api/config.json",
            "api/users.json",
            "api/readme.md",
            "api/config.yaml"
        };

        _mockS3Service
            .Setup(x => x.ListObjectsAsync("test-bucket", "api/", It.IsAny<CancellationToken>()))
            .ReturnsAsync(keys);

        var config = CreateAutoDiscoverConfig("merged.json");

        // Act
        var result = await _sourceDiscovery.DiscoverSourcesAsync("test-bucket", "api/", config);

        // Assert
        Assert.Single(result);
        Assert.All(result, s => Assert.EndsWith(".json", s.Key));
    }

    [Fact]
    public async Task DiscoverSourcesAsync_AutoDiscover_ExcludesNestedDirectories()
    {
        // Arrange
        var keys = new List<string>
        {
            "api/config.json",
            "api/users.json",
            "api/nested/orders.json"  // Should be excluded (nested)
        };

        _mockS3Service
            .Setup(x => x.ListObjectsAsync("test-bucket", "api/", It.IsAny<CancellationToken>()))
            .ReturnsAsync(keys);

        var config = CreateAutoDiscoverConfig("merged.json");

        // Act
        var result = await _sourceDiscovery.DiscoverSourcesAsync("test-bucket", "api/", config);

        // Assert
        Assert.Single(result);
        Assert.Equal("api/users.json", result[0].Key);
    }


    [Fact]
    public async Task DiscoverSourcesAsync_AutoDiscover_SetsCorrectName()
    {
        // Arrange
        var keys = new List<string>
        {
            "api/config.json",
            "api/users-service.json"
        };

        _mockS3Service
            .Setup(x => x.ListObjectsAsync("test-bucket", "api/", It.IsAny<CancellationToken>()))
            .ReturnsAsync(keys);

        var config = CreateAutoDiscoverConfig("merged.json");

        // Act
        var result = await _sourceDiscovery.DiscoverSourcesAsync("test-bucket", "api/", config);

        // Assert
        Assert.Single(result);
        Assert.Equal("users-service", result[0].Name);
        Assert.Null(result[0].ExplicitConfig);
    }

    [Fact]
    public async Task DiscoverSourcesAsync_AutoDiscover_EmptyPrefix_Works()
    {
        // Arrange
        var keys = new List<string>
        {
            "config.json",
            "users.json",
            "orders.json"
        };

        _mockS3Service
            .Setup(x => x.ListObjectsAsync("test-bucket", "", It.IsAny<CancellationToken>()))
            .ReturnsAsync(keys);

        var config = CreateAutoDiscoverConfig("merged.json");

        // Act
        var result = await _sourceDiscovery.DiscoverSourcesAsync("test-bucket", "", config);

        // Assert
        Assert.Equal(2, result.Count);
        Assert.Contains(result, s => s.Key == "users.json");
        Assert.Contains(result, s => s.Key == "orders.json");
    }

    #endregion

    #region Explicit Sources Mode Tests

    [Fact]
    public async Task DiscoverSourcesAsync_ExplicitSources_ReturnsConfiguredSources()
    {
        // Arrange
        var config = CreateExplicitSourcesConfig(new List<SourceConfiguration>
        {
            new() { Path = "users.json", Name = "Users" },
            new() { Path = "orders.json", Name = "Orders" }
        });

        // Act
        var result = await _sourceDiscovery.DiscoverSourcesAsync("test-bucket", "api/", config);

        // Assert
        Assert.Equal(2, result.Count);
        Assert.Equal("api/users.json", result[0].Key);
        Assert.Equal("Users", result[0].Name);
        Assert.Equal("api/orders.json", result[1].Key);
        Assert.Equal("Orders", result[1].Name);
    }

    [Fact]
    public async Task DiscoverSourcesAsync_ExplicitSources_PreservesSourceConfiguration()
    {
        // Arrange
        var sourceConfig = new SourceConfiguration
        {
            Path = "users.json",
            Name = "Users",
            PathPrefix = "/users",
            OperationIdPrefix = "User"
        };

        var config = CreateExplicitSourcesConfig(new List<SourceConfiguration> { sourceConfig });

        // Act
        var result = await _sourceDiscovery.DiscoverSourcesAsync("test-bucket", "api/", config);

        // Assert
        Assert.Single(result);
        Assert.NotNull(result[0].ExplicitConfig);
        Assert.Equal("/users", result[0].ExplicitConfig!.PathPrefix);
        Assert.Equal("User", result[0].ExplicitConfig!.OperationIdPrefix);
    }


    [Fact]
    public async Task DiscoverSourcesAsync_ExplicitSources_DerivesNameFromFilename()
    {
        // Arrange
        var config = CreateExplicitSourcesConfig(new List<SourceConfiguration>
        {
            new() { Path = "users-service.json" }  // No explicit name
        });

        // Act
        var result = await _sourceDiscovery.DiscoverSourcesAsync("test-bucket", "api/", config);

        // Assert
        Assert.Single(result);
        Assert.Equal("users-service", result[0].Name);
    }

    [Fact]
    public async Task DiscoverSourcesAsync_ExplicitSources_EmptyPrefix_Works()
    {
        // Arrange
        var config = CreateExplicitSourcesConfig(new List<SourceConfiguration>
        {
            new() { Path = "users.json", Name = "Users" }
        });

        // Act
        var result = await _sourceDiscovery.DiscoverSourcesAsync("test-bucket", "", config);

        // Assert
        Assert.Single(result);
        Assert.Equal("users.json", result[0].Key);
    }

    [Fact]
    public async Task DiscoverSourcesAsync_ExplicitSources_DoesNotCallS3List()
    {
        // Arrange
        var config = CreateExplicitSourcesConfig(new List<SourceConfiguration>
        {
            new() { Path = "users.json", Name = "Users" }
        });

        // Act
        await _sourceDiscovery.DiscoverSourcesAsync("test-bucket", "api/", config);

        // Assert - S3 ListObjects should NOT be called for explicit sources
        _mockS3Service.Verify(
            x => x.ListObjectsAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<CancellationToken>()),
            Times.Never);
    }

    #endregion

    #region Helper Methods

    private static LambdaMergeConfig CreateAutoDiscoverConfig(string output, List<string>? excludePatterns = null)
    {
        return new LambdaMergeConfig
        {
            AutoDiscover = true,
            Output = output,
            ExcludePatterns = excludePatterns ?? new List<string>(),
            Info = new MergeInfoConfiguration
            {
                Title = "Test API",
                Version = "1.0.0"
            }
        };
    }

    private static LambdaMergeConfig CreateExplicitSourcesConfig(List<SourceConfiguration> sources)
    {
        return new LambdaMergeConfig
        {
            AutoDiscover = false,
            Sources = sources,
            Output = "merged.json",
            Info = new MergeInfoConfiguration
            {
                Title = "Test API",
                Version = "1.0.0"
            }
        };
    }

    #endregion
}
