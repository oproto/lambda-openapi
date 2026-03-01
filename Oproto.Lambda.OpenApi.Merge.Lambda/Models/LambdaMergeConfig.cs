namespace Oproto.Lambda.OpenApi.Merge.Lambda.Models;

using System.Text.Json.Serialization;
using Oproto.Lambda.OpenApi.Merge;

/// <summary>
/// Lambda-specific merge configuration that extends the base MergeConfiguration.
/// </summary>
public class LambdaMergeConfig : MergeConfiguration
{
    /// <summary>
    /// Output bucket name. If not specified, uses input bucket.
    /// Only applicable in Lambda context.
    /// </summary>
    [JsonPropertyName("outputBucket")]
    public string? OutputBucket { get; set; }
}
