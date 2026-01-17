namespace Oproto.Lambda.OpenApi.Merge.Lambda.Services;

using System.Text.Json;
using Microsoft.Extensions.Logging;
using Oproto.Lambda.OpenApi.Merge.Lambda.Models;

/// <summary>
/// Loads and validates merge configuration from S3.
/// </summary>
public class ConfigLoader : IConfigLoader
{
    private const string ConfigFileName = "config.json";

    private readonly IS3Service _s3Service;
    private readonly ILogger<ConfigLoader> _logger;

    /// <summary>
    /// Initializes a new instance of the ConfigLoader.
    /// </summary>
    /// <param name="s3Service">The S3 service for reading files.</param>
    /// <param name="logger">The logger.</param>
    public ConfigLoader(IS3Service s3Service, ILogger<ConfigLoader> logger)
    {
        _s3Service = s3Service ?? throw new ArgumentNullException(nameof(s3Service));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    /// <inheritdoc />
    public async Task<LambdaMergeConfig> LoadConfigAsync(string bucket, string prefix, CancellationToken ct = default)
    {
        var configKey = GetConfigKey(prefix);
        _logger.LogInformation("Loading configuration from s3://{Bucket}/{Key}", bucket, configKey);

        // Read the config file content
        string? content;
        try
        {
            content = await _s3Service.ReadTextAsync(bucket, configKey, ct);
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            _logger.LogError(ex, "Failed to read configuration from s3://{Bucket}/{Key}", bucket, configKey);
            throw;
        }

        // Check if config exists
        if (content == null)
        {
            _logger.LogError("Configuration file not found at s3://{Bucket}/{Key}", bucket, configKey);
            throw new ConfigNotFoundException(bucket, configKey);
        }

        // Parse the JSON
        LambdaMergeConfig config;
        try
        {
            config = JsonSerializer.Deserialize<LambdaMergeConfig>(content)
                ?? throw new InvalidConfigException(bucket, configKey, "Deserialization returned null");
        }
        catch (JsonException ex)
        {
            _logger.LogError(ex, "Invalid JSON in configuration file s3://{Bucket}/{Key}", bucket, configKey);
            throw new InvalidConfigException(bucket, configKey, $"Invalid JSON: {ex.Message}", ex);
        }

        // Validate required fields
        ValidateConfig(bucket, configKey, config);

        _logger.LogInformation(
            "Successfully loaded configuration: Title={Title}, Version={Version}, AutoDiscover={AutoDiscover}",
            config.Info.Title,
            config.Info.Version,
            config.AutoDiscover);

        return config;
    }

    /// <inheritdoc />
    public string ExtractPrefix(string key)
    {
        if (string.IsNullOrEmpty(key))
        {
            return string.Empty;
        }

        // Find the last slash to separate the filename from the path
        var lastSlashIndex = key.LastIndexOf('/');
        
        if (lastSlashIndex < 0)
        {
            // No slash found - file is at root level, no prefix
            return string.Empty;
        }

        // Return everything up to and including the last slash
        return key.Substring(0, lastSlashIndex + 1);
    }

    /// <summary>
    /// Gets the config file key for a given prefix.
    /// </summary>
    private static string GetConfigKey(string prefix)
    {
        // Ensure prefix ends with / if not empty
        if (!string.IsNullOrEmpty(prefix) && !prefix.EndsWith("/"))
        {
            prefix += "/";
        }

        return $"{prefix}{ConfigFileName}";
    }

    /// <summary>
    /// Validates the configuration has all required fields.
    /// </summary>
    private void ValidateConfig(string bucket, string key, LambdaMergeConfig config)
    {
        var errors = new List<string>();

        // Info.Title is required
        if (string.IsNullOrWhiteSpace(config.Info.Title))
        {
            errors.Add("Missing required field: info.title");
        }

        // Info.Version is required
        if (string.IsNullOrWhiteSpace(config.Info.Version))
        {
            errors.Add("Missing required field: info.version");
        }

        // Output is required
        if (string.IsNullOrWhiteSpace(config.Output))
        {
            errors.Add("Missing required field: output");
        }

        // If autoDiscover is false, sources must be specified
        if (!config.AutoDiscover && (config.Sources == null || config.Sources.Count == 0))
        {
            errors.Add("No sources specified and autoDiscover is disabled");
        }

        if (errors.Count > 0)
        {
            var errorMessage = string.Join("; ", errors);
            _logger.LogError("Configuration validation failed for s3://{Bucket}/{Key}: {Errors}", bucket, key, errorMessage);
            throw new InvalidConfigException(bucket, key, errorMessage);
        }
    }
}
