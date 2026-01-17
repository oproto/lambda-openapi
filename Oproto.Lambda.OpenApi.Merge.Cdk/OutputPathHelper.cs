namespace Oproto.Lambda.OpenApi.Merge.Cdk;

/// <summary>
/// Helper class for constructing output paths for merged OpenAPI specs.
/// </summary>
public static class OutputPathHelper
{
    /// <summary>
    /// Constructs the full output path for a merged OpenAPI spec.
    /// If outputFilename starts with '/' or contains '/', it's treated as an absolute/full path.
    /// Otherwise, it's relative to the prefix.
    /// </summary>
    /// <param name="prefix">The API prefix (e.g., "publicapi/")</param>
    /// <param name="outputFilename">The output filename or path from config</param>
    /// <returns>The full S3 key for the output file</returns>
    public static string ConstructOutputPath(string prefix, string outputFilename)
    {
        if (string.IsNullOrWhiteSpace(outputFilename))
        {
            throw new ArgumentException("Output filename cannot be null or empty", nameof(outputFilename));
        }

        // If output starts with '/', treat it as absolute (remove leading slash for S3)
        if (outputFilename.StartsWith("/"))
        {
            return outputFilename.TrimStart('/');
        }

        // If output contains '/', treat it as a full path (not relative to prefix)
        if (outputFilename.Contains("/"))
        {
            return outputFilename;
        }

        // Otherwise, it's a simple filename relative to the prefix
        if (string.IsNullOrWhiteSpace(prefix))
        {
            return outputFilename;
        }

        // Normalize prefix to ensure it ends with /
        var normalizedPrefix = prefix.TrimEnd('/') + "/";

        return normalizedPrefix + outputFilename;
    }

    /// <summary>
    /// Extracts the prefix from an S3 key.
    /// </summary>
    /// <param name="key">The full S3 key (e.g., "publicapi/service.json")</param>
    /// <returns>The prefix portion (e.g., "publicapi/")</returns>
    public static string ExtractPrefix(string key)
    {
        if (string.IsNullOrWhiteSpace(key))
        {
            throw new ArgumentException("Key cannot be null or empty", nameof(key));
        }

        var lastSlashIndex = key.LastIndexOf('/');
        if (lastSlashIndex < 0)
        {
            // No slash found, return empty prefix
            return string.Empty;
        }

        return key.Substring(0, lastSlashIndex + 1);
    }
}
