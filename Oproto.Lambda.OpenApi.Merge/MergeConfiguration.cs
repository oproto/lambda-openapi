namespace Oproto.Lambda.OpenApi.Merge;

using System.Collections.Generic;
using System.Text.Json.Serialization;

/// <summary>
/// Configuration for merging multiple OpenAPI specifications.
/// </summary>
public class MergeConfiguration
{
    /// <summary>
    /// Info block for the merged specification.
    /// </summary>
    [JsonPropertyName("info")]
    public MergeInfoConfiguration Info { get; set; } = new MergeInfoConfiguration();

    /// <summary>
    /// Server definitions for the merged specification.
    /// </summary>
    [JsonPropertyName("servers")]
    public List<MergeServerConfiguration> Servers { get; set; } = new List<MergeServerConfiguration>();

    /// <summary>
    /// Source specifications to merge.
    /// </summary>
    [JsonPropertyName("sources")]
    public List<SourceConfiguration> Sources { get; set; } = new List<SourceConfiguration>();

    /// <summary>
    /// Output file path for the merged specification.
    /// </summary>
    [JsonPropertyName("output")]
    public string Output { get; set; } = "merged-openapi.json";

    /// <summary>
    /// Strategy for handling schema naming conflicts.
    /// </summary>
    [JsonPropertyName("schemaConflict")]
    [JsonConverter(typeof(SchemaConflictStrategyConverter))]
    public SchemaConflictStrategy SchemaConflict { get; set; } = SchemaConflictStrategy.Rename;

    /// <summary>
    /// Whether to auto-discover source files in the directory.
    /// When true, ignores the sources list and discovers all .json files.
    /// Default: false (use explicit sources list).
    /// </summary>
    [JsonPropertyName("autoDiscover")]
    public bool AutoDiscover { get; set; } = false;

    /// <summary>
    /// Glob patterns for files to exclude from auto-discovery.
    /// Only used when autoDiscover is true.
    /// Always excludes the output file automatically.
    /// </summary>
    [JsonPropertyName("excludePatterns")]
    public List<string> ExcludePatterns { get; set; } = new List<string>();
}

/// <summary>
/// Info configuration for the merged specification.
/// </summary>
public class MergeInfoConfiguration
{
    /// <summary>
    /// API title.
    /// </summary>
    [JsonPropertyName("title")]
    public string? Title { get; set; }

    /// <summary>
    /// API version.
    /// </summary>
    [JsonPropertyName("version")]
    public string? Version { get; set; }

    /// <summary>
    /// API description.
    /// </summary>
    [JsonPropertyName("description")]
    public string? Description { get; set; }
}

/// <summary>
/// Server configuration for the merged specification.
/// </summary>
public class MergeServerConfiguration
{
    /// <summary>
    /// Server URL.
    /// </summary>
    [JsonPropertyName("url")]
    public string Url { get; set; } = string.Empty;

    /// <summary>
    /// Server description.
    /// </summary>
    [JsonPropertyName("description")]
    public string? Description { get; set; }
}

/// <summary>
/// Strategy for handling schema naming conflicts.
/// </summary>
public enum SchemaConflictStrategy
{
    /// <summary>
    /// Rename conflicting schemas using source name as prefix.
    /// </summary>
    Rename,

    /// <summary>
    /// Keep the first schema encountered, ignore subsequent conflicts.
    /// </summary>
    FirstWins,

    /// <summary>
    /// Throw an exception on schema conflicts.
    /// </summary>
    Fail
}
