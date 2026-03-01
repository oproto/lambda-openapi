using Microsoft.Extensions.Logging;
using Moq;
using Oproto.Lambda.OpenApi.Merge.Lambda.Models;
using Oproto.Lambda.OpenApi.Merge.Lambda.Services;

namespace Oproto.Lambda.OpenApi.Merge.Lambda.Tests;

/// <summary>
/// Unit tests for ConfigLoader.
/// </summary>
public class ConfigLoaderTests
{
    private readonly Mock<IS3Service> _mockS3Service;
    private readonly Mock<ILogger<ConfigLoader>> _mockLogger;
    private readonly ConfigLoader _configLoader;

    public ConfigLoaderTests()
    {
        _mockS3Service = new Mock<IS3Service>();
        _mockLogger = new Mock<ILogger<ConfigLoader>>();
        _configLoader = new ConfigLoader(_mockS3Service.Object, _mockLogger.Object);
    }

    #region LoadConfigAsync - Valid Config Tests

    [Fact]
    public async Task LoadConfigAsync_ValidConfig_ReturnsConfig()
    {
        // Arrange
        var validJson = """
        {
            "info": {
                "title": "Test API",
                "version": "1.0.0",
                "description": "A test API"
            },
            "autoDiscover": true,
            "output": "merged.json"
        }
        """;

        _mockS3Service
            .Setup(x => x.ReadTextAsync("test-bucket", "publicapi/config.json", It.IsAny<CancellationToken>()))
            .ReturnsAsync(validJson);

        // Act
        var config = await _configLoader.LoadConfigAsync("test-bucket", "publicapi/");

        // Assert
        Assert.NotNull(config);
        Assert.Equal("Test API", config.Info.Title);
        Assert.Equal("1.0.0", config.Info.Version);
        Assert.Equal("A test API", config.Info.Description);
        Assert.True(config.AutoDiscover);
        Assert.Equal("merged.json", config.Output);
    }

    [Fact]
    public async Task LoadConfigAsync_ValidConfigWithExplicitSources_ReturnsConfig()
    {
        // Arrange
        var validJson = """
        {
            "info": {
                "title": "Test API",
                "version": "1.0.0"
            },
            "autoDiscover": false,
            "sources": [
                {
                    "path": "users.json",
                    "name": "Users"
                }
            ],
            "output": "merged.json"
        }
        """;

        _mockS3Service
            .Setup(x => x.ReadTextAsync("test-bucket", "api/config.json", It.IsAny<CancellationToken>()))
            .ReturnsAsync(validJson);

        // Act
        var config = await _configLoader.LoadConfigAsync("test-bucket", "api/");

        // Assert
        Assert.NotNull(config);
        Assert.False(config.AutoDiscover);
        Assert.Single(config.Sources);
        Assert.Equal("users.json", config.Sources[0].Path);
    }

    [Fact]
    public async Task LoadConfigAsync_PrefixWithoutTrailingSlash_AddsSlash()
    {
        // Arrange
        var validJson = """
        {
            "info": {
                "title": "Test API",
                "version": "1.0.0"
            },
            "autoDiscover": true,
            "output": "merged.json"
        }
        """;

        _mockS3Service
            .Setup(x => x.ReadTextAsync("test-bucket", "publicapi/config.json", It.IsAny<CancellationToken>()))
            .ReturnsAsync(validJson);

        // Act - prefix without trailing slash
        var config = await _configLoader.LoadConfigAsync("test-bucket", "publicapi");

        // Assert
        Assert.NotNull(config);
        _mockS3Service.Verify(
            x => x.ReadTextAsync("test-bucket", "publicapi/config.json", It.IsAny<CancellationToken>()),
            Times.Once);
    }

    #endregion

    #region LoadConfigAsync - Missing Config Tests

    [Fact]
    public async Task LoadConfigAsync_ConfigNotFound_ThrowsConfigNotFoundException()
    {
        // Arrange
        _mockS3Service
            .Setup(x => x.ReadTextAsync("test-bucket", "missing/config.json", It.IsAny<CancellationToken>()))
            .ReturnsAsync((string?)null);

        // Act & Assert
        var exception = await Assert.ThrowsAsync<ConfigNotFoundException>(
            () => _configLoader.LoadConfigAsync("test-bucket", "missing/"));

        Assert.Equal("test-bucket", exception.Bucket);
        Assert.Equal("missing/config.json", exception.Key);
        Assert.Contains("Configuration file not found", exception.Message);
    }

    #endregion

    #region LoadConfigAsync - Invalid JSON Tests

    [Fact]
    public async Task LoadConfigAsync_InvalidJson_ThrowsInvalidConfigException()
    {
        // Arrange
        var invalidJson = "{ not valid json {{{{";

        _mockS3Service
            .Setup(x => x.ReadTextAsync("test-bucket", "api/config.json", It.IsAny<CancellationToken>()))
            .ReturnsAsync(invalidJson);

        // Act & Assert
        var exception = await Assert.ThrowsAsync<InvalidConfigException>(
            () => _configLoader.LoadConfigAsync("test-bucket", "api/"));

        Assert.Equal("test-bucket", exception.Bucket);
        Assert.Equal("api/config.json", exception.Key);
        Assert.Contains("Invalid JSON", exception.Message);
    }

    [Fact]
    public async Task LoadConfigAsync_EmptyJson_ThrowsInvalidConfigException()
    {
        // Arrange - empty object will fail validation for missing required fields
        var emptyJson = "{}";

        _mockS3Service
            .Setup(x => x.ReadTextAsync("test-bucket", "api/config.json", It.IsAny<CancellationToken>()))
            .ReturnsAsync(emptyJson);

        // Act & Assert
        var exception = await Assert.ThrowsAsync<InvalidConfigException>(
            () => _configLoader.LoadConfigAsync("test-bucket", "api/"));

        Assert.Contains("Missing required field", exception.Message);
    }

    #endregion

    #region LoadConfigAsync - Validation Tests

    [Fact]
    public async Task LoadConfigAsync_MissingTitle_ThrowsInvalidConfigException()
    {
        // Arrange
        var json = """
        {
            "info": {
                "version": "1.0.0"
            },
            "autoDiscover": true,
            "output": "merged.json"
        }
        """;

        _mockS3Service
            .Setup(x => x.ReadTextAsync("test-bucket", "api/config.json", It.IsAny<CancellationToken>()))
            .ReturnsAsync(json);

        // Act & Assert
        var exception = await Assert.ThrowsAsync<InvalidConfigException>(
            () => _configLoader.LoadConfigAsync("test-bucket", "api/"));

        Assert.Contains("info.title", exception.Message);
    }

    [Fact]
    public async Task LoadConfigAsync_MissingVersion_ThrowsInvalidConfigException()
    {
        // Arrange
        var json = """
        {
            "info": {
                "title": "Test API"
            },
            "autoDiscover": true,
            "output": "merged.json"
        }
        """;

        _mockS3Service
            .Setup(x => x.ReadTextAsync("test-bucket", "api/config.json", It.IsAny<CancellationToken>()))
            .ReturnsAsync(json);

        // Act & Assert
        var exception = await Assert.ThrowsAsync<InvalidConfigException>(
            () => _configLoader.LoadConfigAsync("test-bucket", "api/"));

        Assert.Contains("info.version", exception.Message);
    }

    [Fact]
    public async Task LoadConfigAsync_NoSourcesAndAutoDiscoverFalse_ThrowsInvalidConfigException()
    {
        // Arrange
        var json = """
        {
            "info": {
                "title": "Test API",
                "version": "1.0.0"
            },
            "autoDiscover": false,
            "sources": [],
            "output": "merged.json"
        }
        """;

        _mockS3Service
            .Setup(x => x.ReadTextAsync("test-bucket", "api/config.json", It.IsAny<CancellationToken>()))
            .ReturnsAsync(json);

        // Act & Assert
        var exception = await Assert.ThrowsAsync<InvalidConfigException>(
            () => _configLoader.LoadConfigAsync("test-bucket", "api/"));

        Assert.Contains("No sources specified and autoDiscover is disabled", exception.Message);
    }

    #endregion

    #region ExtractPrefix Tests

    [Theory]
    [InlineData("publicapi/config.json", "publicapi/")]
    [InlineData("internal/v2/service.json", "internal/v2/")]
    [InlineData("api/users/openapi.json", "api/users/")]
    [InlineData("config.json", "")]
    [InlineData("", "")]
    public void ExtractPrefix_VariousKeys_ReturnsExpectedPrefix(string key, string expectedPrefix)
    {
        // Act
        var result = _configLoader.ExtractPrefix(key);

        // Assert
        Assert.Equal(expectedPrefix, result);
    }

    #endregion
}
