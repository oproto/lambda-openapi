namespace Oproto.Lambda.OpenApi.Merge.Lambda.Services;

using Microsoft.Extensions.Logging;
using Oproto.Lambda.OpenApi.Merge;
using Oproto.Lambda.OpenApi.Merge.Lambda.Models;

/// <summary>
/// Discovers source OpenAPI specification files from S3.
/// </summary>
public class SourceDiscovery : ISourceDiscovery
{
    private const string ConfigFileName = "config.json";

    private readonly IS3Service _s3Service;
    private readonly ILogger<SourceDiscovery> _logger;

    /// <summary>
    /// Initializes a new instance of the SourceDiscovery.
    /// </summary>
    /// <param name="s3Service">The S3 service for listing files.</param>
    /// <param name="logger">The logger.</param>
    public SourceDiscovery(IS3Service s3Service, ILogger<SourceDiscovery> logger)
    {
        _s3Service = s3Service ?? throw new ArgumentNullException(nameof(s3Service));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    /// <inheritdoc />
    public async Task<IReadOnlyList<DiscoveredSource>> DiscoverSourcesAsync(
        string bucket,
        string prefix,
        LambdaMergeConfig config,
        CancellationToken ct = default)
    {
        if (config.AutoDiscover)
        {
            return await DiscoverSourcesAutoAsync(bucket, prefix, config, ct);
        }

        return DiscoverSourcesExplicit(prefix, config);
    }


    /// <summary>
    /// Discovers sources using auto-discovery mode (list and filter files).
    /// </summary>
    private async Task<IReadOnlyList<DiscoveredSource>> DiscoverSourcesAutoAsync(
        string bucket,
        string prefix,
        LambdaMergeConfig config,
        CancellationToken ct)
    {
        _logger.LogInformation("Auto-discovering source files in s3://{Bucket}/{Prefix}", bucket, prefix);

        // List all objects in the prefix
        var allKeys = await _s3Service.ListObjectsAsync(bucket, prefix, ct);

        // Filter to only JSON files within the immediate prefix (not nested)
        var jsonFiles = allKeys
            .Where(key => IsJsonFile(key))
            .Where(key => IsInImmediatePrefix(key, prefix))
            .ToList();

        _logger.LogDebug("Found {Count} JSON files in prefix", jsonFiles.Count);

        // Build the output file key for exclusion
        var outputKey = BuildOutputKey(prefix, config.Output);

        // Filter out config.json, output file, and excluded patterns
        var sources = new List<DiscoveredSource>();
        foreach (var key in jsonFiles)
        {
            var filename = GetFilename(key);

            // Exclude config.json
            if (filename.Equals(ConfigFileName, StringComparison.OrdinalIgnoreCase))
            {
                _logger.LogDebug("Excluding config file: {Key}", key);
                continue;
            }

            // Exclude output file
            if (key.Equals(outputKey, StringComparison.OrdinalIgnoreCase))
            {
                _logger.LogDebug("Excluding output file: {Key}", key);
                continue;
            }

            // Check exclude patterns
            if (MatchesExcludePattern(filename, config.ExcludePatterns))
            {
                _logger.LogDebug("Excluding file matching exclude pattern: {Key}", key);
                continue;
            }

            // Add as discovered source
            var name = GetNameFromFilename(filename);
            sources.Add(new DiscoveredSource(key, name, null));
        }

        _logger.LogInformation("Auto-discovered {Count} source files", sources.Count);
        return sources;
    }

    /// <summary>
    /// Discovers sources using explicit sources mode.
    /// </summary>
    private IReadOnlyList<DiscoveredSource> DiscoverSourcesExplicit(
        string prefix,
        LambdaMergeConfig config)
    {
        _logger.LogInformation("Using explicit sources from configuration");

        var sources = new List<DiscoveredSource>();

        foreach (var sourceConfig in config.Sources)
        {
            var key = BuildSourceKey(prefix, sourceConfig.Path);
            var name = sourceConfig.Name ?? GetNameFromFilename(GetFilename(key));

            sources.Add(new DiscoveredSource(key, name, sourceConfig));
        }

        _logger.LogInformation("Found {Count} explicit source files", sources.Count);
        return sources;
    }


    /// <summary>
    /// Checks if a key represents a JSON file.
    /// </summary>
    private static bool IsJsonFile(string key)
    {
        return key.EndsWith(".json", StringComparison.OrdinalIgnoreCase);
    }

    /// <summary>
    /// Checks if a key is in the immediate prefix (not in a subdirectory).
    /// </summary>
    private static bool IsInImmediatePrefix(string key, string prefix)
    {
        // Remove the prefix from the key
        var relativePath = key;
        if (!string.IsNullOrEmpty(prefix))
        {
            if (!key.StartsWith(prefix, StringComparison.OrdinalIgnoreCase))
            {
                return false;
            }
            relativePath = key.Substring(prefix.Length);
        }

        // If there's a slash in the relative path, it's in a subdirectory
        return !relativePath.Contains('/');
    }

    /// <summary>
    /// Gets the filename from a full S3 key.
    /// </summary>
    private static string GetFilename(string key)
    {
        var lastSlash = key.LastIndexOf('/');
        return lastSlash >= 0 ? key.Substring(lastSlash + 1) : key;
    }

    /// <summary>
    /// Gets a friendly name from a filename (removes .json extension).
    /// </summary>
    private static string GetNameFromFilename(string filename)
    {
        if (filename.EndsWith(".json", StringComparison.OrdinalIgnoreCase))
        {
            return filename.Substring(0, filename.Length - 5);
        }
        return filename;
    }

    /// <summary>
    /// Builds the full S3 key for the output file.
    /// </summary>
    private static string BuildOutputKey(string prefix, string output)
    {
        // If output starts with '/', treat it as absolute (remove leading slash for S3)
        if (output.StartsWith("/"))
        {
            return output.TrimStart('/');
        }

        // If output contains '/', treat it as a full path (not relative to prefix)
        if (output.Contains("/"))
        {
            return output;
        }

        // Otherwise, it's a simple filename relative to the prefix
        if (string.IsNullOrEmpty(prefix))
        {
            return output;
        }

        // Ensure prefix ends with /
        if (!prefix.EndsWith("/"))
        {
            prefix += "/";
        }

        return prefix + output;
    }

    /// <summary>
    /// Builds the full S3 key for a source file.
    /// </summary>
    private static string BuildSourceKey(string prefix, string path)
    {
        if (string.IsNullOrEmpty(prefix))
        {
            return path;
        }

        // Ensure prefix ends with /
        if (!prefix.EndsWith("/"))
        {
            prefix += "/";
        }

        return prefix + path;
    }

    /// <summary>
    /// Checks if a filename matches any of the exclude patterns.
    /// </summary>
    private static bool MatchesExcludePattern(string filename, IReadOnlyList<string> excludePatterns)
    {
        if (excludePatterns == null || excludePatterns.Count == 0)
        {
            return false;
        }

        foreach (var pattern in excludePatterns)
        {
            if (MatchesGlobPattern(filename, pattern))
            {
                return true;
            }
        }

        return false;
    }

    /// <summary>
    /// Matches a filename against a simple glob pattern.
    /// Supports * (any characters) and ? (single character).
    /// </summary>
    private static bool MatchesGlobPattern(string filename, string pattern)
    {
        if (string.IsNullOrEmpty(pattern))
        {
            return false;
        }

        // Convert glob pattern to regex
        var regexPattern = "^" + System.Text.RegularExpressions.Regex.Escape(pattern)
            .Replace("\\*", ".*")
            .Replace("\\?", ".") + "$";

        return System.Text.RegularExpressions.Regex.IsMatch(
            filename,
            regexPattern,
            System.Text.RegularExpressions.RegexOptions.IgnoreCase);
    }
}
