using System.Net;
using System.Text;
using System.Text.Json;
using Amazon.S3;
using Amazon.S3.Model;
using Microsoft.Extensions.Logging;
using Moq;
using Oproto.Lambda.OpenApi.Merge.Lambda.Services;

namespace Oproto.Lambda.OpenApi.Merge.Lambda.Tests;

/// <summary>
/// Unit tests for S3Service.
/// </summary>
public class S3ServiceTests
{
    private readonly Mock<IAmazonS3> _mockS3Client;
    private readonly Mock<ILogger<S3Service>> _mockLogger;
    private readonly S3Service _service;

    public S3ServiceTests()
    {
        _mockS3Client = new Mock<IAmazonS3>();
        _mockLogger = new Mock<ILogger<S3Service>>();
        _service = new S3Service(_mockS3Client.Object, _mockLogger.Object);
    }

    #region ReadJsonAsync Tests

    [Fact]
    public async Task ReadJsonAsync_ValidJson_DeserializesCorrectly()
    {
        // Arrange
        var testObject = new TestModel { Name = "Test", Value = 42 };
        var json = JsonSerializer.Serialize(testObject, new JsonSerializerOptions { PropertyNamingPolicy = JsonNamingPolicy.CamelCase });
        SetupGetObjectResponse("test-bucket", "test-key.json", json);

        // Act
        var result = await _service.ReadJsonAsync<TestModel>("test-bucket", "test-key.json");

        // Assert
        Assert.NotNull(result);
        Assert.Equal("Test", result.Name);
        Assert.Equal(42, result.Value);
    }

    [Fact]
    public async Task ReadJsonAsync_ObjectNotFound_ReturnsNull()
    {
        // Arrange
        SetupGetObjectNotFound("test-bucket", "missing-key.json");

        // Act
        var result = await _service.ReadJsonAsync<TestModel>("test-bucket", "missing-key.json");

        // Assert
        Assert.Null(result);
    }


    [Fact]
    public async Task ReadJsonAsync_InvalidJson_ThrowsJsonException()
    {
        // Arrange
        SetupGetObjectResponse("test-bucket", "invalid.json", "not valid json {{{");

        // Act & Assert
        await Assert.ThrowsAsync<JsonException>(() =>
            _service.ReadJsonAsync<TestModel>("test-bucket", "invalid.json"));
    }

    #endregion

    #region ReadTextAsync Tests

    [Fact]
    public async Task ReadTextAsync_ValidObject_ReturnsContent()
    {
        // Arrange
        var content = "Hello, World!";
        SetupGetObjectResponse("test-bucket", "test.txt", content);

        // Act
        var result = await _service.ReadTextAsync("test-bucket", "test.txt");

        // Assert
        Assert.Equal(content, result);
    }

    [Fact]
    public async Task ReadTextAsync_ObjectNotFound_ReturnsNull()
    {
        // Arrange
        SetupGetObjectNotFound("test-bucket", "missing.txt");

        // Act
        var result = await _service.ReadTextAsync("test-bucket", "missing.txt");

        // Assert
        Assert.Null(result);
    }

    #endregion

    #region WriteJsonAsync Tests

    [Fact]
    public async Task WriteJsonAsync_ValidObject_SerializesAndWrites()
    {
        // Arrange
        var testObject = new TestModel { Name = "Test", Value = 42 };
        PutObjectRequest? capturedRequest = null;

        _mockS3Client
            .Setup(x => x.PutObjectAsync(It.IsAny<PutObjectRequest>(), It.IsAny<CancellationToken>()))
            .Callback<PutObjectRequest, CancellationToken>((req, _) => capturedRequest = req)
            .ReturnsAsync(new PutObjectResponse());

        // Act
        await _service.WriteJsonAsync("test-bucket", "output.json", testObject);

        // Assert
        Assert.NotNull(capturedRequest);
        Assert.Equal("test-bucket", capturedRequest.BucketName);
        Assert.Equal("output.json", capturedRequest.Key);
        Assert.Equal("application/json", capturedRequest.ContentType);
        Assert.Contains("\"name\"", capturedRequest.ContentBody);
        Assert.Contains("\"value\"", capturedRequest.ContentBody);
    }

    #endregion

    #region ListObjectsAsync Tests

    [Fact]
    public async Task ListObjectsAsync_ReturnsAllKeys()
    {
        // Arrange
        var response = new ListObjectsV2Response
        {
            S3Objects = new List<S3Object>
            {
                new() { Key = "prefix/file1.json" },
                new() { Key = "prefix/file2.json" },
                new() { Key = "prefix/file3.json" }
            },
            IsTruncated = false
        };

        _mockS3Client
            .Setup(x => x.ListObjectsV2Async(It.IsAny<ListObjectsV2Request>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(response);

        // Act
        var result = await _service.ListObjectsAsync("test-bucket", "prefix/");

        // Assert
        Assert.Equal(3, result.Count);
        Assert.Contains("prefix/file1.json", result);
        Assert.Contains("prefix/file2.json", result);
        Assert.Contains("prefix/file3.json", result);
    }


    [Fact]
    public async Task ListObjectsAsync_HandlesPagination()
    {
        // Arrange
        var firstResponse = new ListObjectsV2Response
        {
            S3Objects = new List<S3Object>
            {
                new() { Key = "prefix/file1.json" },
                new() { Key = "prefix/file2.json" }
            },
            IsTruncated = true,
            NextContinuationToken = "token123"
        };

        var secondResponse = new ListObjectsV2Response
        {
            S3Objects = new List<S3Object>
            {
                new() { Key = "prefix/file3.json" }
            },
            IsTruncated = false
        };

        var callCount = 0;
        _mockS3Client
            .Setup(x => x.ListObjectsV2Async(It.IsAny<ListObjectsV2Request>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(() => callCount++ == 0 ? firstResponse : secondResponse);

        // Act
        var result = await _service.ListObjectsAsync("test-bucket", "prefix/");

        // Assert
        Assert.Equal(3, result.Count);
        Assert.Contains("prefix/file1.json", result);
        Assert.Contains("prefix/file2.json", result);
        Assert.Contains("prefix/file3.json", result);
    }

    [Fact]
    public async Task ListObjectsAsync_EmptyPrefix_ReturnsEmptyList()
    {
        // Arrange
        var response = new ListObjectsV2Response
        {
            S3Objects = new List<S3Object>(),
            IsTruncated = false
        };

        _mockS3Client
            .Setup(x => x.ListObjectsV2Async(It.IsAny<ListObjectsV2Request>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(response);

        // Act
        var result = await _service.ListObjectsAsync("test-bucket", "nonexistent/");

        // Assert
        Assert.Empty(result);
    }

    #endregion

    #region ExistsAsync Tests

    [Fact]
    public async Task ExistsAsync_ObjectExists_ReturnsTrue()
    {
        // Arrange
        _mockS3Client
            .Setup(x => x.GetObjectMetadataAsync(It.IsAny<GetObjectMetadataRequest>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new GetObjectMetadataResponse());

        // Act
        var result = await _service.ExistsAsync("test-bucket", "existing.json");

        // Assert
        Assert.True(result);
    }

    [Fact]
    public async Task ExistsAsync_ObjectNotFound_ReturnsFalse()
    {
        // Arrange
        _mockS3Client
            .Setup(x => x.GetObjectMetadataAsync(It.IsAny<GetObjectMetadataRequest>(), It.IsAny<CancellationToken>()))
            .ThrowsAsync(new AmazonS3Exception("Not Found") { StatusCode = HttpStatusCode.NotFound });

        // Act
        var result = await _service.ExistsAsync("test-bucket", "missing.json");

        // Assert
        Assert.False(result);
    }

    #endregion

    #region Helper Methods

    private void SetupGetObjectResponse(string bucket, string key, string content)
    {
        var stream = new MemoryStream(Encoding.UTF8.GetBytes(content));
        var response = new GetObjectResponse
        {
            ResponseStream = stream
        };

        _mockS3Client
            .Setup(x => x.GetObjectAsync(
                It.Is<GetObjectRequest>(r => r.BucketName == bucket && r.Key == key),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(response);
    }

    private void SetupGetObjectNotFound(string bucket, string key)
    {
        _mockS3Client
            .Setup(x => x.GetObjectAsync(
                It.Is<GetObjectRequest>(r => r.BucketName == bucket && r.Key == key),
                It.IsAny<CancellationToken>()))
            .ThrowsAsync(new AmazonS3Exception("Not Found") { StatusCode = HttpStatusCode.NotFound });
    }

    #endregion

    #region Test Models

    private class TestModel
    {
        public string Name { get; set; } = string.Empty;
        public int Value { get; set; }
    }

    #endregion
}
