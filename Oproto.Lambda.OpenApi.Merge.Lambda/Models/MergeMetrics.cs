namespace Oproto.Lambda.OpenApi.Merge.Lambda.Models;

using System.Text.Json.Serialization;

/// <summary>
/// Metrics from a merge operation.
/// </summary>
/// <param name="SourceFilesProcessed">Number of source files that were processed.</param>
/// <param name="SchemasMergedCount">Number of schemas merged.</param>
/// <param name="PathsMergedCount">Number of paths merged.</param>
/// <param name="DurationMs">Duration of the merge operation in milliseconds.</param>
/// <param name="OutputWritten">Whether the output file was written (false if unchanged).</param>
/// <param name="OutputKey">The S3 key of the output file, if written.</param>
public record MergeMetrics(
    [property: JsonPropertyName("sourceFilesProcessed")] int SourceFilesProcessed,
    [property: JsonPropertyName("schemasMergedCount")] int SchemasMergedCount,
    [property: JsonPropertyName("pathsMergedCount")] int PathsMergedCount,
    [property: JsonPropertyName("durationMs")] long DurationMs,
    [property: JsonPropertyName("outputWritten")] bool OutputWritten,
    [property: JsonPropertyName("outputKey")] string? OutputKey = null);
