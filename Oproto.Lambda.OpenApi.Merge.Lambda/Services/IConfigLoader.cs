namespace Oproto.Lambda.OpenApi.Merge.Lambda.Services;

using Oproto.Lambda.OpenApi.Merge.Lambda.Models;

/// <summary>
/// Interface for loading merge configuration from S3.
/// </summary>
public interface IConfigLoader
{
    /// <summary>
    /// Loads and validates the merge configuration from S3.
    /// </summary>
    /// <param name="bucket">The S3 bucket name.</param>
    /// <param name="prefix">The API prefix (e.g., "publicapi/").</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>The loaded and validated configuration.</returns>
    /// <exception cref="ConfigNotFoundException">Thrown when the config file does not exist.</exception>
    /// <exception cref="InvalidConfigException">Thrown when the config file contains invalid JSON.</exception>
    Task<LambdaMergeConfig> LoadConfigAsync(string bucket, string prefix, CancellationToken ct = default);

    /// <summary>
    /// Extracts the API prefix from an S3 object key.
    /// </summary>
    /// <param name="key">The S3 object key (e.g., "publicapi/config.json" or "internal/v2/service.json").</param>
    /// <returns>The extracted prefix (e.g., "publicapi/" or "internal/v2/").</returns>
    string ExtractPrefix(string key);
}
