namespace Oproto.Lambda.OpenApi.Merge.Lambda.Services;

using Oproto.Lambda.OpenApi.Merge;
using Oproto.Lambda.OpenApi.Merge.Lambda.Models;

/// <summary>
/// Interface for discovering source OpenAPI specification files.
/// </summary>
public interface ISourceDiscovery
{
    /// <summary>
    /// Discovers source files based on configuration.
    /// </summary>
    /// <param name="bucket">The S3 bucket name.</param>
    /// <param name="prefix">The API prefix (e.g., "publicapi/").</param>
    /// <param name="config">The merge configuration.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>A list of discovered source files.</returns>
    Task<IReadOnlyList<DiscoveredSource>> DiscoverSourcesAsync(
        string bucket,
        string prefix,
        LambdaMergeConfig config,
        CancellationToken ct = default);
}

/// <summary>
/// Represents a discovered source file for merging.
/// </summary>
/// <param name="Key">The full S3 key of the source file.</param>
/// <param name="Name">The friendly name for this source (filename without extension if not specified).</param>
/// <param name="ExplicitConfig">The explicit source configuration if provided, null for auto-discovered sources.</param>
public record DiscoveredSource(
    string Key,
    string Name,
    SourceConfiguration? ExplicitConfig);
