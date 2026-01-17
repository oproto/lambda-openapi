namespace Oproto.Lambda.OpenApi.Merge.Lambda.Services;

/// <summary>
/// Interface for emitting CloudWatch metrics.
/// </summary>
public interface IMetricsService
{
    /// <summary>
    /// Emits metrics for a successful merge operation.
    /// </summary>
    /// <param name="prefix">The API prefix that was merged.</param>
    /// <param name="durationMs">Duration of the merge operation in milliseconds.</param>
    /// <param name="filesProcessed">Number of source files processed.</param>
    /// <param name="outputWritten">Whether the output was written (vs unchanged).</param>
    /// <param name="ct">Cancellation token.</param>
    Task EmitSuccessMetricsAsync(
        string prefix,
        long durationMs,
        int filesProcessed,
        bool outputWritten,
        CancellationToken ct = default);

    /// <summary>
    /// Emits metrics for a failed merge operation.
    /// </summary>
    /// <param name="prefix">The API prefix that was being merged.</param>
    /// <param name="durationMs">Duration before failure in milliseconds.</param>
    /// <param name="errorType">The type of error that occurred.</param>
    /// <param name="ct">Cancellation token.</param>
    Task EmitFailureMetricsAsync(
        string prefix,
        long durationMs,
        string errorType,
        CancellationToken ct = default);
}
