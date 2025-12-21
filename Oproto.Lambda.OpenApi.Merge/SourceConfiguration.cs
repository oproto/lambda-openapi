namespace Oproto.Lambda.OpenApi.Merge;

using System.Text.Json.Serialization;

/// <summary>
/// Configuration for a single source OpenAPI specification.
/// </summary>
public class SourceConfiguration
{
    /// <summary>
    /// File path to the OpenAPI specification.
    /// </summary>
    [JsonPropertyName("path")]
    public string Path { get; set; } = string.Empty;

    /// <summary>
    /// Optional prefix to prepend to all paths from this source.
    /// </summary>
    [JsonPropertyName("pathPrefix")]
    public string? PathPrefix { get; set; }

    /// <summary>
    /// Optional prefix to prepend to all operationIds from this source.
    /// </summary>
    [JsonPropertyName("operationIdPrefix")]
    public string? OperationIdPrefix { get; set; }

    /// <summary>
    /// Friendly name for this source (used in warnings/errors).
    /// Defaults to filename if not specified.
    /// </summary>
    [JsonPropertyName("name")]
    public string? Name { get; set; }
}
