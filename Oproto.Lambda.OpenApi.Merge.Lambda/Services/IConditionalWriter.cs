namespace Oproto.Lambda.OpenApi.Merge.Lambda.Services;

/// <summary>
/// Result of a conditional write operation.
/// </summary>
public record ConditionalWriteResult(
    bool WasWritten,
    string? OutputKey,
    string Reason);

/// <summary>
/// Interface for conditional write operations that skip writes when content is unchanged.
/// </summary>
public interface IConditionalWriter
{
    /// <summary>
    /// Writes content to S3 only if it differs from the existing content.
    /// </summary>
    /// <param name="bucket">The S3 bucket name.</param>
    /// <param name="key">The S3 object key.</param>
    /// <param name="newContent">The new content to write.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>Result indicating whether the write occurred and why.</returns>
    Task<ConditionalWriteResult> WriteIfChangedAsync(
        string bucket,
        string key,
        string newContent,
        CancellationToken ct = default);
}
