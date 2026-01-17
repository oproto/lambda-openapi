namespace Oproto.Lambda.OpenApi.Merge.Lambda.Functions;

using System.Diagnostics;
using System.Text.Json;
using Amazon.Lambda.Annotations;
using Amazon.Lambda.Core;
using Microsoft.Extensions.Logging;
using Microsoft.OpenApi;
using Microsoft.OpenApi.Extensions;
using Microsoft.OpenApi.Models;
using Microsoft.OpenApi.Readers;
using Oproto.Lambda.OpenApi.Merge.Lambda.Models;
using Oproto.Lambda.OpenApi.Merge.Lambda.Services;

/// <summary>
/// Lambda function for merging OpenAPI specifications from S3.
/// </summary>
public class MergeFunction
{
    private readonly IS3Service _s3Service;
    private readonly IConfigLoader _configLoader;
    private readonly ISourceDiscovery _sourceDiscovery;
    private readonly IConditionalWriter _conditionalWriter;
    private readonly IMetricsService _metricsService;
    private readonly ILogger<MergeFunction> _logger;

    /// <summary>
    /// Initializes a new instance of the MergeFunction.
    /// </summary>
    /// <param name="s3Service">The S3 service for reading/writing files.</param>
    /// <param name="configLoader">The config loader for loading merge configuration.</param>
    /// <param name="sourceDiscovery">The source discovery service.</param>
    /// <param name="conditionalWriter">The conditional writer for output.</param>
    /// <param name="metricsService">The metrics service for CloudWatch.</param>
    /// <param name="logger">The logger.</param>
    public MergeFunction(
        IS3Service s3Service,
        IConfigLoader configLoader,
        ISourceDiscovery sourceDiscovery,
        IConditionalWriter conditionalWriter,
        IMetricsService metricsService,
        ILogger<MergeFunction> logger)
    {
        _s3Service = s3Service ?? throw new ArgumentNullException(nameof(s3Service));
        _configLoader = configLoader ?? throw new ArgumentNullException(nameof(configLoader));
        _sourceDiscovery = sourceDiscovery ?? throw new ArgumentNullException(nameof(sourceDiscovery));
        _conditionalWriter = conditionalWriter ?? throw new ArgumentNullException(nameof(conditionalWriter));
        _metricsService = metricsService ?? throw new ArgumentNullException(nameof(metricsService));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    /// <summary>
    /// Handles the merge request from Step Functions.
    /// </summary>
    /// <param name="request">The merge request containing bucket and prefix information.</param>
    /// <param name="context">The Lambda context.</param>
    /// <returns>The merge response with metrics and status.</returns>
    [LambdaFunction(ResourceName = "Merge")]
    public async Task<MergeResponse> Merge(
        MergeRequest request,
        ILambdaContext context)
    {
        var stopwatch = Stopwatch.StartNew();
        var warnings = new List<string>();

        _logger.LogInformation(
            "Starting merge operation for s3://{Bucket}/{Prefix}",
            request.InputBucket,
            request.Prefix);

        try
        {
            // 1. Load configuration
            var config = await _configLoader.LoadConfigAsync(
                request.InputBucket,
                request.Prefix);

            // Determine output bucket (use request override, then config, then input bucket)
            var outputBucket = request.OutputBucket ?? config.OutputBucket ?? request.InputBucket;

            // 2. Discover source files
            var sources = await _sourceDiscovery.DiscoverSourcesAsync(
                request.InputBucket,
                request.Prefix,
                config);

            if (sources.Count == 0)
            {
                return CreateErrorResponse(
                    "No valid source files found",
                    stopwatch.ElapsedMilliseconds);
            }

            _logger.LogInformation("Discovered {Count} source files", sources.Count);

            // 3. Load and parse source documents
            var documents = new List<(SourceConfiguration Source, OpenApiDocument Document)>();
            foreach (var source in sources)
            {
                var document = await LoadOpenApiDocumentAsync(
                    request.InputBucket,
                    source.Key,
                    warnings);

                if (document != null)
                {
                    // Create source configuration for the merger
                    var sourceConfig = source.ExplicitConfig ?? new SourceConfiguration
                    {
                        Path = source.Key,
                        Name = source.Name
                    };

                    documents.Add((sourceConfig, document));
                }
            }

            if (documents.Count == 0)
            {
                return CreateErrorResponse(
                    "No valid OpenAPI documents could be loaded",
                    stopwatch.ElapsedMilliseconds,
                    warnings);
            }

            _logger.LogInformation("Loaded {Count} valid OpenAPI documents", documents.Count);

            // 4. Perform merge
            var merger = new OpenApiMerger();
            var mergeResult = merger.Merge(config, documents);

            // Add merge warnings to our warnings list
            foreach (var warning in mergeResult.Warnings)
            {
                warnings.Add(warning.ToString());
            }

            if (!mergeResult.Success)
            {
                return CreateErrorResponse(
                    "Merge operation failed",
                    stopwatch.ElapsedMilliseconds,
                    warnings);
            }

            // 5. Serialize merged document
            var mergedJson = SerializeOpenApiDocument(mergeResult.Document);

            // 6. Build output key
            var outputKey = BuildOutputKey(request.Prefix, config.Output);

            // 7. Write output conditionally (only if changed)
            var writeResult = await _conditionalWriter.WriteIfChangedAsync(
                outputBucket,
                outputKey,
                mergedJson);

            stopwatch.Stop();

            // 8. Build metrics
            var metrics = new MergeMetrics(
                SourceFilesProcessed: documents.Count,
                SchemasMergedCount: mergeResult.Document.Components?.Schemas?.Count ?? 0,
                PathsMergedCount: mergeResult.Document.Paths?.Count ?? 0,
                DurationMs: stopwatch.ElapsedMilliseconds,
                OutputWritten: writeResult.WasWritten,
                OutputKey: writeResult.OutputKey);

            var message = writeResult.WasWritten
                ? $"Merge completed successfully. Output written to s3://{outputBucket}/{outputKey}"
                : $"Merge completed successfully. Output unchanged at s3://{outputBucket}/{outputKey}";

            _logger.LogInformation(
                "Merge completed: {SourceCount} sources, {SchemaCount} schemas, {PathCount} paths, {Duration}ms, Written={Written}",
                metrics.SourceFilesProcessed,
                metrics.SchemasMergedCount,
                metrics.PathsMergedCount,
                metrics.DurationMs,
                metrics.OutputWritten);

            // Emit success metrics to CloudWatch
            await _metricsService.EmitSuccessMetricsAsync(
                request.Prefix,
                metrics.DurationMs,
                metrics.SourceFilesProcessed,
                metrics.OutputWritten);

            return new MergeResponse(
                Success: true,
                Message: message,
                Metrics: metrics,
                Warnings: warnings.Count > 0 ? warnings : null);
        }
        catch (ConfigNotFoundException ex)
        {
            _logger.LogError(ex, "Configuration not found");
            await _metricsService.EmitFailureMetricsAsync(
                request.Prefix,
                stopwatch.ElapsedMilliseconds,
                "ConfigNotFound");
            return CreateErrorResponse(
                ex.Message,
                stopwatch.ElapsedMilliseconds);
        }
        catch (InvalidConfigException ex)
        {
            _logger.LogError(ex, "Invalid configuration");
            await _metricsService.EmitFailureMetricsAsync(
                request.Prefix,
                stopwatch.ElapsedMilliseconds,
                "InvalidConfig");
            return CreateErrorResponse(
                ex.Message,
                stopwatch.ElapsedMilliseconds);
        }
        catch (NoValidSourcesException ex)
        {
            _logger.LogError(ex, "No valid sources found");
            await _metricsService.EmitFailureMetricsAsync(
                request.Prefix,
                stopwatch.ElapsedMilliseconds,
                "NoValidSources");
            return CreateErrorResponse(
                ex.Message,
                stopwatch.ElapsedMilliseconds,
                warnings);
        }
        catch (MergeConflictException ex)
        {
            _logger.LogError(ex, "Merge conflict occurred");
            await _metricsService.EmitFailureMetricsAsync(
                request.Prefix,
                stopwatch.ElapsedMilliseconds,
                "MergeConflict");
            return CreateErrorResponse(
                ex.Message,
                stopwatch.ElapsedMilliseconds,
                warnings);
        }
        catch (S3OperationException ex)
        {
            _logger.LogError(ex, "S3 operation failed");
            await _metricsService.EmitFailureMetricsAsync(
                request.Prefix,
                stopwatch.ElapsedMilliseconds,
                "S3Error");
            return CreateErrorResponse(
                ex.Message,
                stopwatch.ElapsedMilliseconds,
                warnings);
        }
        catch (Amazon.S3.AmazonS3Exception ex)
        {
            _logger.LogError(ex, "S3 error during merge operation");
            await _metricsService.EmitFailureMetricsAsync(
                request.Prefix,
                stopwatch.ElapsedMilliseconds,
                $"S3_{ex.ErrorCode}");
            var errorMessage = ex.ErrorCode switch
            {
                "AccessDenied" => $"Access denied to S3 resource: {ex.Message}",
                "NoSuchBucket" => $"Bucket not found: {ex.Message}",
                "NoSuchKey" => $"Object not found: {ex.Message}",
                _ => $"S3 error: {ex.Message}"
            };
            return CreateErrorResponse(
                errorMessage,
                stopwatch.ElapsedMilliseconds,
                warnings);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Unexpected error during merge operation");
            await _metricsService.EmitFailureMetricsAsync(
                request.Prefix,
                stopwatch.ElapsedMilliseconds,
                "UnexpectedError");
            return CreateErrorResponse(
                $"Unexpected error: {ex.Message}",
                stopwatch.ElapsedMilliseconds,
                warnings);
        }
    }

    /// <summary>
    /// Loads and parses an OpenAPI document from S3.
    /// </summary>
    private async Task<OpenApiDocument?> LoadOpenApiDocumentAsync(
        string bucket,
        string key,
        List<string> warnings)
    {
        _logger.LogDebug("Loading OpenAPI document from s3://{Bucket}/{Key}", bucket, key);

        try
        {
            var content = await _s3Service.ReadTextAsync(bucket, key);

            if (content == null)
            {
                var warning = $"Source file not found: s3://{bucket}/{key}";
                _logger.LogWarning(warning);
                warnings.Add(warning);
                return null;
            }

            var reader = new OpenApiStringReader();
            var document = reader.Read(content, out var diagnostic);

            if (diagnostic.Errors.Count > 0)
            {
                var errorMessages = string.Join("; ", diagnostic.Errors.Select(e => e.Message));
                var warning = $"Invalid OpenAPI document at s3://{bucket}/{key}: {errorMessages}";
                _logger.LogWarning(warning);
                warnings.Add(warning);
                return null;
            }

            return document;
        }
        catch (Exception ex)
        {
            var warning = $"Failed to load source file s3://{bucket}/{key}: {ex.Message}";
            _logger.LogWarning(ex, "Failed to load source file s3://{Bucket}/{Key}", bucket, key);
            warnings.Add(warning);
            return null;
        }
    }

    /// <summary>
    /// Serializes an OpenAPI document to JSON.
    /// </summary>
    private static string SerializeOpenApiDocument(OpenApiDocument document)
    {
        return document.SerializeAsJson(OpenApiSpecVersion.OpenApi3_0);
    }

    /// <summary>
    /// Builds the full S3 key for the output file.
    /// If output starts with '/' or contains '/', it's treated as an absolute/full path.
    /// Otherwise, it's relative to the prefix.
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
    /// Creates an error response with the given message.
    /// </summary>
    private static MergeResponse CreateErrorResponse(
        string error,
        long durationMs,
        List<string>? warnings = null)
    {
        return new MergeResponse(
            Success: false,
            Message: "Merge operation failed",
            Metrics: new MergeMetrics(
                SourceFilesProcessed: 0,
                SchemasMergedCount: 0,
                PathsMergedCount: 0,
                DurationMs: durationMs,
                OutputWritten: false),
            Warnings: warnings?.Count > 0 ? warnings : null,
            Error: error);
    }
}
