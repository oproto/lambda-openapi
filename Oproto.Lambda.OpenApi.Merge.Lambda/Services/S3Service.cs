namespace Oproto.Lambda.OpenApi.Merge.Lambda.Services;

using System.Net;
using System.Text;
using System.Text.Json;
using Amazon.S3;
using Amazon.S3.Model;
using Microsoft.Extensions.Logging;

/// <summary>
/// S3 service implementation for reading and writing objects.
/// </summary>
public class S3Service : IS3Service
{
    private readonly IAmazonS3 _s3Client;
    private readonly ILogger<S3Service> _logger;

    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        WriteIndented = true
    };

    /// <summary>
    /// Initializes a new instance of the S3Service.
    /// </summary>
    /// <param name="s3Client">The S3 client.</param>
    /// <param name="logger">The logger.</param>
    public S3Service(IAmazonS3 s3Client, ILogger<S3Service> logger)
    {
        _s3Client = s3Client ?? throw new ArgumentNullException(nameof(s3Client));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    /// <inheritdoc />
    public async Task<T?> ReadJsonAsync<T>(string bucket, string key, CancellationToken ct = default) where T : class
    {
        var content = await ReadTextAsync(bucket, key, ct);
        if (content == null)
        {
            return null;
        }

        try
        {
            return JsonSerializer.Deserialize<T>(content, JsonOptions);
        }
        catch (JsonException ex)
        {
            _logger.LogError(ex, "Failed to deserialize JSON from s3://{Bucket}/{Key}", bucket, key);
            throw;
        }
    }


    /// <inheritdoc />
    public async Task<string?> ReadTextAsync(string bucket, string key, CancellationToken ct = default)
    {
        _logger.LogDebug("Reading s3://{Bucket}/{Key}", bucket, key);

        try
        {
            var request = new GetObjectRequest
            {
                BucketName = bucket,
                Key = key
            };

            using var response = await _s3Client.GetObjectAsync(request, ct);
            using var reader = new StreamReader(response.ResponseStream, Encoding.UTF8);
            var content = await reader.ReadToEndAsync(ct);

            _logger.LogDebug("Successfully read {Length} bytes from s3://{Bucket}/{Key}", content.Length, bucket, key);
            return content;
        }
        catch (AmazonS3Exception ex) when (ex.StatusCode == HttpStatusCode.NotFound)
        {
            _logger.LogDebug("Object not found: s3://{Bucket}/{Key}", bucket, key);
            return null;
        }
    }

    /// <inheritdoc />
    public async Task WriteJsonAsync<T>(string bucket, string key, T content, CancellationToken ct = default)
    {
        _logger.LogDebug("Writing JSON to s3://{Bucket}/{Key}", bucket, key);

        var json = JsonSerializer.Serialize(content, JsonOptions);
        
        var request = new PutObjectRequest
        {
            BucketName = bucket,
            Key = key,
            ContentBody = json,
            ContentType = "application/json"
        };

        await _s3Client.PutObjectAsync(request, ct);
        _logger.LogDebug("Successfully wrote {Length} bytes to s3://{Bucket}/{Key}", json.Length, bucket, key);
    }

    /// <inheritdoc />
    public async Task WriteTextAsync(string bucket, string key, string content, string contentType = "application/json", CancellationToken ct = default)
    {
        _logger.LogDebug("Writing text to s3://{Bucket}/{Key}", bucket, key);

        var request = new PutObjectRequest
        {
            BucketName = bucket,
            Key = key,
            ContentBody = content,
            ContentType = contentType
        };

        await _s3Client.PutObjectAsync(request, ct);
        _logger.LogDebug("Successfully wrote {Length} bytes to s3://{Bucket}/{Key}", content.Length, bucket, key);
    }

    /// <inheritdoc />
    public async Task<IReadOnlyList<string>> ListObjectsAsync(string bucket, string prefix, CancellationToken ct = default)
    {
        _logger.LogDebug("Listing objects in s3://{Bucket}/{Prefix}", bucket, prefix);

        var keys = new List<string>();
        string? continuationToken = null;

        do
        {
            var request = new ListObjectsV2Request
            {
                BucketName = bucket,
                Prefix = prefix,
                ContinuationToken = continuationToken
            };

            var response = await _s3Client.ListObjectsV2Async(request, ct);

            foreach (var obj in response.S3Objects)
            {
                keys.Add(obj.Key);
            }

            continuationToken = response.IsTruncated ? response.NextContinuationToken : null;
        }
        while (continuationToken != null);

        _logger.LogDebug("Found {Count} objects in s3://{Bucket}/{Prefix}", keys.Count, bucket, prefix);
        return keys;
    }

    /// <inheritdoc />
    public async Task<bool> ExistsAsync(string bucket, string key, CancellationToken ct = default)
    {
        _logger.LogDebug("Checking existence of s3://{Bucket}/{Key}", bucket, key);

        try
        {
            var request = new GetObjectMetadataRequest
            {
                BucketName = bucket,
                Key = key
            };

            await _s3Client.GetObjectMetadataAsync(request, ct);
            _logger.LogDebug("Object exists: s3://{Bucket}/{Key}", bucket, key);
            return true;
        }
        catch (AmazonS3Exception ex) when (ex.StatusCode == HttpStatusCode.NotFound)
        {
            _logger.LogDebug("Object does not exist: s3://{Bucket}/{Key}", bucket, key);
            return false;
        }
    }
}
