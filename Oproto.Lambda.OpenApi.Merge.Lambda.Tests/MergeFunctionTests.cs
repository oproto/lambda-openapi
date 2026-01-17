using Amazon.Lambda.Core;
using Microsoft.Extensions.Logging;
using Moq;
using Oproto.Lambda.OpenApi.Merge;
using Oproto.Lambda.OpenApi.Merge.Lambda.Functions;
using Oproto.Lambda.OpenApi.Merge.Lambda.Models;
using Oproto.Lambda.OpenApi.Merge.Lambda.Services;

namespace Oproto.Lambda.OpenApi.Merge.Lambda.Tests;

/// <summary>
/// Unit tests for MergeFunction.
/// </summary>
public class MergeFunctionTests
{
    private readonly Mock<IS3Service> _mockS3Service;
    private readonly Mock<IConfigLoader> _mockConfigLoader;
    private readonly Mock<ISourceDiscovery> _mockSourceDiscovery;
    private readonly Mock<IConditionalWriter> _mockConditionalWriter;
    private readonly Mock<IMetricsService> _mockMetricsService;
    private readonly Mock<ILogger<MergeFunction>> _mockLogger;
    private readonly Mock<ILambdaContext> _mockContext;
    private readonly MergeFunction _mergeFunction;

    public MergeFunctionTests()
    {
        _mockS3Service = new Mock<IS3Service>();
        _mockConfigLoader = new Mock<IConfigLoader>();
        _mockSourceDiscovery = new Mock<ISourceDiscovery>();
        _mockConditionalWriter = new Mock<IConditionalWriter>();
        _mockMetricsService = new Mock<IMetricsService>();
        _mockLogger = new Mock<ILogger<MergeFunction>>();
        _mockContext = new Mock<ILambdaContext>();

        _mergeFunction = new MergeFunction(
            _mockS3Service.Object,
            _mockConfigLoader.Object,
            _mockSourceDiscovery.Object,
            _mockConditionalWriter.Object,
            _mockMetricsService.Object,
            _mockLogger.Object);
    }

    #region Successful Merge Tests

    [Fact]
    public async Task Merge_SuccessfulMerge_ReturnsSuccessResponse()
    {
        // Arrange
        var request = new MergeRequest("test-bucket", "publicapi/");
        var config = CreateValidConfig();
        var sources = CreateDiscoveredSources();
        var openApiContent = CreateValidOpenApiJson();

        SetupSuccessfulMerge(config, sources, openApiContent);

        // Act
        var response = await _mergeFunction.Merge(request, _mockContext.Object);

        // Assert
        Assert.True(response.Success);
        Assert.Contains("Merge completed successfully", response.Message);
        Assert.NotNull(response.Metrics);
        Assert.Equal(2, response.Metrics.SourceFilesProcessed);
        Assert.True(response.Metrics.OutputWritten);
    }

    [Fact]
    public async Task Merge_OutputUnchanged_ReturnsSuccessWithOutputWrittenFalse()
    {
        // Arrange
        var request = new MergeRequest("test-bucket", "publicapi/");
        var config = CreateValidConfig();
        var sources = CreateDiscoveredSources();
        var openApiContent = CreateValidOpenApiJson();

        SetupSuccessfulMerge(config, sources, openApiContent, outputWritten: false);

        // Act
        var response = await _mergeFunction.Merge(request, _mockContext.Object);

        // Assert
        Assert.True(response.Success);
        Assert.Contains("unchanged", response.Message);
        Assert.False(response.Metrics.OutputWritten);
    }

    [Fact]
    public async Task Merge_WithOutputBucketOverride_UsesOverrideBucket()
    {
        // Arrange
        var request = new MergeRequest("input-bucket", "publicapi/", "output-bucket");
        var config = CreateValidConfig();
        var sources = CreateDiscoveredSources();
        var openApiContent = CreateValidOpenApiJson();

        SetupSuccessfulMerge(config, sources, openApiContent);

        // Act
        var response = await _mergeFunction.Merge(request, _mockContext.Object);

        // Assert
        Assert.True(response.Success);
        _mockConditionalWriter.Verify(
            x => x.WriteIfChangedAsync("output-bucket", It.IsAny<string>(), It.IsAny<string>(), It.IsAny<CancellationToken>()),
            Times.Once);
    }

    [Fact]
    public async Task Merge_EmitsSuccessMetrics()
    {
        // Arrange
        var request = new MergeRequest("test-bucket", "publicapi/");
        var config = CreateValidConfig();
        var sources = CreateDiscoveredSources();
        var openApiContent = CreateValidOpenApiJson();

        SetupSuccessfulMerge(config, sources, openApiContent);

        // Act
        await _mergeFunction.Merge(request, _mockContext.Object);

        // Assert
        _mockMetricsService.Verify(
            x => x.EmitSuccessMetricsAsync(
                "publicapi/",
                It.IsAny<long>(),
                2,
                true,
                It.IsAny<CancellationToken>()),
            Times.Once);
    }

    #endregion

    #region Error Handling Tests

    [Fact]
    public async Task Merge_ConfigNotFound_ReturnsErrorResponse()
    {
        // Arrange
        var request = new MergeRequest("test-bucket", "missing/");

        _mockConfigLoader
            .Setup(x => x.LoadConfigAsync("test-bucket", "missing/", It.IsAny<CancellationToken>()))
            .ThrowsAsync(new ConfigNotFoundException("test-bucket", "missing/config.json"));

        // Act
        var response = await _mergeFunction.Merge(request, _mockContext.Object);

        // Assert
        Assert.False(response.Success);
        Assert.Contains("Configuration file not found", response.Error);
    }

    [Fact]
    public async Task Merge_InvalidConfig_ReturnsErrorResponse()
    {
        // Arrange
        var request = new MergeRequest("test-bucket", "api/");

        _mockConfigLoader
            .Setup(x => x.LoadConfigAsync("test-bucket", "api/", It.IsAny<CancellationToken>()))
            .ThrowsAsync(new InvalidConfigException("test-bucket", "api/config.json", "Invalid JSON"));

        // Act
        var response = await _mergeFunction.Merge(request, _mockContext.Object);

        // Assert
        Assert.False(response.Success);
        Assert.Contains("Invalid configuration", response.Error);
    }

    [Fact]
    public async Task Merge_NoSourcesFound_ReturnsErrorResponse()
    {
        // Arrange
        var request = new MergeRequest("test-bucket", "empty/");
        var config = CreateValidConfig();

        _mockConfigLoader
            .Setup(x => x.LoadConfigAsync("test-bucket", "empty/", It.IsAny<CancellationToken>()))
            .ReturnsAsync(config);

        _mockSourceDiscovery
            .Setup(x => x.DiscoverSourcesAsync("test-bucket", "empty/", config, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<DiscoveredSource>());

        // Act
        var response = await _mergeFunction.Merge(request, _mockContext.Object);

        // Assert
        Assert.False(response.Success);
        Assert.Contains("No valid source files found", response.Error);
    }

    [Fact]
    public async Task Merge_AllSourcesInvalid_ReturnsErrorResponse()
    {
        // Arrange
        var request = new MergeRequest("test-bucket", "api/");
        var config = CreateValidConfig();
        var sources = CreateDiscoveredSources();

        _mockConfigLoader
            .Setup(x => x.LoadConfigAsync("test-bucket", "api/", It.IsAny<CancellationToken>()))
            .ReturnsAsync(config);

        _mockSourceDiscovery
            .Setup(x => x.DiscoverSourcesAsync("test-bucket", "api/", config, It.IsAny<CancellationToken>()))
            .ReturnsAsync(sources);

        // Return null for all source files (simulating invalid/missing files)
        _mockS3Service
            .Setup(x => x.ReadTextAsync("test-bucket", It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((string?)null);

        // Act
        var response = await _mergeFunction.Merge(request, _mockContext.Object);

        // Assert
        Assert.False(response.Success);
        Assert.Contains("No valid OpenAPI documents could be loaded", response.Error);
    }

    [Fact]
    public async Task Merge_ConfigNotFound_EmitsFailureMetrics()
    {
        // Arrange
        var request = new MergeRequest("test-bucket", "missing/");

        _mockConfigLoader
            .Setup(x => x.LoadConfigAsync("test-bucket", "missing/", It.IsAny<CancellationToken>()))
            .ThrowsAsync(new ConfigNotFoundException("test-bucket", "missing/config.json"));

        // Act
        await _mergeFunction.Merge(request, _mockContext.Object);

        // Assert
        _mockMetricsService.Verify(
            x => x.EmitFailureMetricsAsync(
                "missing/",
                It.IsAny<long>(),
                "ConfigNotFound",
                It.IsAny<CancellationToken>()),
            Times.Once);
    }

    [Fact]
    public async Task Merge_InvalidConfig_EmitsFailureMetrics()
    {
        // Arrange
        var request = new MergeRequest("test-bucket", "api/");

        _mockConfigLoader
            .Setup(x => x.LoadConfigAsync("test-bucket", "api/", It.IsAny<CancellationToken>()))
            .ThrowsAsync(new InvalidConfigException("test-bucket", "api/config.json", "Invalid JSON"));

        // Act
        await _mergeFunction.Merge(request, _mockContext.Object);

        // Assert
        _mockMetricsService.Verify(
            x => x.EmitFailureMetricsAsync(
                "api/",
                It.IsAny<long>(),
                "InvalidConfig",
                It.IsAny<CancellationToken>()),
            Times.Once);
    }

    #endregion

    #region Conditional Write Tests

    [Fact]
    public async Task Merge_ContentChanged_WritesOutput()
    {
        // Arrange
        var request = new MergeRequest("test-bucket", "api/");
        var config = CreateValidConfig();
        var sources = CreateDiscoveredSources();
        var openApiContent = CreateValidOpenApiJson();

        SetupSuccessfulMerge(config, sources, openApiContent, outputWritten: true);

        // Act
        var response = await _mergeFunction.Merge(request, _mockContext.Object);

        // Assert
        Assert.True(response.Metrics.OutputWritten);
        _mockConditionalWriter.Verify(
            x => x.WriteIfChangedAsync(
                "test-bucket",
                "api/merged.json",
                It.IsAny<string>(),
                It.IsAny<CancellationToken>()),
            Times.Once);
    }

    [Fact]
    public async Task Merge_ContentUnchanged_SkipsWrite()
    {
        // Arrange
        var request = new MergeRequest("test-bucket", "api/");
        var config = CreateValidConfig();
        var sources = CreateDiscoveredSources();
        var openApiContent = CreateValidOpenApiJson();

        SetupSuccessfulMerge(config, sources, openApiContent, outputWritten: false);

        // Act
        var response = await _mergeFunction.Merge(request, _mockContext.Object);

        // Assert
        Assert.False(response.Metrics.OutputWritten);
    }

    #endregion

    #region Helper Methods

    private LambdaMergeConfig CreateValidConfig()
    {
        return new LambdaMergeConfig
        {
            Info = new MergeInfoConfiguration
            {
                Title = "Test API",
                Version = "1.0.0"
            },
            AutoDiscover = true,
            Output = "merged.json"
        };
    }

    private IReadOnlyList<DiscoveredSource> CreateDiscoveredSources()
    {
        return new List<DiscoveredSource>
        {
            new DiscoveredSource("api/users.json", "users", null),
            new DiscoveredSource("api/products.json", "products", null)
        };
    }

    private string CreateValidOpenApiJson()
    {
        return """
        {
            "openapi": "3.0.0",
            "info": {
                "title": "Test Service",
                "version": "1.0.0"
            },
            "paths": {
                "/test": {
                    "get": {
                        "summary": "Test endpoint",
                        "responses": {
                            "200": {
                                "description": "Success"
                            }
                        }
                    }
                }
            }
        }
        """;
    }

    private void SetupSuccessfulMerge(
        LambdaMergeConfig config,
        IReadOnlyList<DiscoveredSource> sources,
        string openApiContent,
        bool outputWritten = true)
    {
        _mockConfigLoader
            .Setup(x => x.LoadConfigAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(config);

        _mockSourceDiscovery
            .Setup(x => x.DiscoverSourcesAsync(It.IsAny<string>(), It.IsAny<string>(), config, It.IsAny<CancellationToken>()))
            .ReturnsAsync(sources);

        _mockS3Service
            .Setup(x => x.ReadTextAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(openApiContent);

        _mockConditionalWriter
            .Setup(x => x.WriteIfChangedAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new ConditionalWriteResult(
                WasWritten: outputWritten,
                OutputKey: $"{config.Output}",
                Reason: outputWritten ? "Content changed" : "Content unchanged"));

        _mockMetricsService
            .Setup(x => x.EmitSuccessMetricsAsync(It.IsAny<string>(), It.IsAny<long>(), It.IsAny<int>(), It.IsAny<bool>(), It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);

        _mockMetricsService
            .Setup(x => x.EmitFailureMetricsAsync(It.IsAny<string>(), It.IsAny<long>(), It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);
    }

    #endregion
}
