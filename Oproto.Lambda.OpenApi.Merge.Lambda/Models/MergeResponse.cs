namespace Oproto.Lambda.OpenApi.Merge.Lambda.Models;

using System.Collections.Generic;
using System.Text.Json.Serialization;

/// <summary>
/// Response model from the merge Lambda function.
/// </summary>
/// <param name="Success">Whether the merge operation succeeded.</param>
/// <param name="Message">A human-readable message describing the result.</param>
/// <param name="Metrics">Metrics from the merge operation.</param>
/// <param name="Warnings">Optional list of warnings encountered during merge.</param>
/// <param name="Error">Error details if the merge failed.</param>
public record MergeResponse(
    [property: JsonPropertyName("success")] bool Success,
    [property: JsonPropertyName("message")] string Message,
    [property: JsonPropertyName("metrics")] MergeMetrics Metrics,
    [property: JsonPropertyName("warnings")] IReadOnlyList<string>? Warnings = null,
    [property: JsonPropertyName("error")] string? Error = null);
