namespace Oproto.Lambda.OpenApi.Merge.Lambda.Models;

using System.Text.Json.Serialization;

/// <summary>
/// Request model for the merge Lambda function.
/// </summary>
/// <param name="InputBucket">The S3 bucket containing input files.</param>
/// <param name="Prefix">The API prefix within the bucket.</param>
/// <param name="OutputBucket">Optional output bucket. Defaults to InputBucket if not specified.</param>
public record MergeRequest(
    [property: JsonPropertyName("inputBucket")] string InputBucket,
    [property: JsonPropertyName("prefix")] string Prefix,
    [property: JsonPropertyName("outputBucket")] string? OutputBucket = null);
