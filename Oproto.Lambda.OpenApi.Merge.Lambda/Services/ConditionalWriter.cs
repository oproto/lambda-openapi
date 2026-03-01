namespace Oproto.Lambda.OpenApi.Merge.Lambda.Services;

using Microsoft.Extensions.Logging;

/// <summary>
/// Service for conditional write operations that skip writes when content is unchanged.
/// Implements semantic comparison to ignore formatting differences.
/// </summary>
public class ConditionalWriter : IConditionalWriter
{
    private readonly IS3Service _s3Service;
    private readonly IOutputComparer _outputComparer;
    private readonly ILogger<ConditionalWriter> _logger;

    /// <summary>
    /// Initializes a new instance of the ConditionalWriter.
    /// </summary>
    /// <param name="s3Service">The S3 service.</param>
    /// <param name="outputComparer">The output comparer.</param>
    /// <param name="logger">The logger.</param>
    public ConditionalWriter(
        IS3Service s3Service,
        IOutputComparer outputComparer,
        ILogger<ConditionalWriter> logger)
    {
        _s3Service = s3Service ?? throw new ArgumentNullException(nameof(s3Service));
        _outputComparer = outputComparer ?? throw new ArgumentNullException(nameof(outputComparer));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    /// <inheritdoc />
    public async Task<ConditionalWriteResult> WriteIfChangedAsync(
        string bucket,
        string key,
        string newContent,
        CancellationToken ct = default)
    {
        if (string.IsNullOrEmpty(bucket))
            throw new ArgumentNullException(nameof(bucket));
        if (string.IsNullOrEmpty(key))
            throw new ArgumentNullException(nameof(key));
        if (newContent == null)
            throw new ArgumentNullException(nameof(newContent));

        _logger.LogDebug("Checking if write is needed for s3://{Bucket}/{Key}", bucket, key);

        // Try to read existing content
        string? existingContent = null;
        try
        {
            existingContent = await _s3Service.ReadTextAsync(bucket, key, ct);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Could not read existing content from s3://{Bucket}/{Key}, will write new content", bucket, key);
        }

        // If no existing content, always write
        if (existingContent == null)
        {
            _logger.LogDebug("No existing content found, writing new content to s3://{Bucket}/{Key}", bucket, key);
            await _s3Service.WriteTextAsync(bucket, key, newContent, "application/json", ct);
            return new ConditionalWriteResult(
                WasWritten: true,
                OutputKey: key,
                Reason: "File did not exist");
        }

        // Compare content semantically
        if (_outputComparer.AreEquivalent(existingContent, newContent))
        {
            _logger.LogInformation("Content unchanged, skipping write to s3://{Bucket}/{Key}", bucket, key);
            return new ConditionalWriteResult(
                WasWritten: false,
                OutputKey: key,
                Reason: "Content unchanged");
        }

        // Content differs, write new content
        _logger.LogDebug("Content changed, writing to s3://{Bucket}/{Key}", bucket, key);
        await _s3Service.WriteTextAsync(bucket, key, newContent, "application/json", ct);
        return new ConditionalWriteResult(
            WasWritten: true,
            OutputKey: key,
            Reason: "Content changed");
    }
}
