namespace Oproto.Lambda.OpenApi.Merge.Lambda.Services;

using Amazon.CloudWatch;
using Amazon.CloudWatch.Model;
using Microsoft.Extensions.Logging;

/// <summary>
/// Service for emitting CloudWatch metrics for merge operations.
/// </summary>
public class MetricsService : IMetricsService
{
    private const string Namespace = "Oproto/OpenApiMerge";
    private const string DimensionName = "ApiPrefix";

    private readonly IAmazonCloudWatch _cloudWatch;
    private readonly ILogger<MetricsService> _logger;

    /// <summary>
    /// Initializes a new instance of the MetricsService.
    /// </summary>
    /// <param name="cloudWatch">The CloudWatch client.</param>
    /// <param name="logger">The logger.</param>
    public MetricsService(IAmazonCloudWatch cloudWatch, ILogger<MetricsService> logger)
    {
        _cloudWatch = cloudWatch ?? throw new ArgumentNullException(nameof(cloudWatch));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    /// <inheritdoc />
    public async Task EmitSuccessMetricsAsync(
        string prefix,
        long durationMs,
        int filesProcessed,
        bool outputWritten,
        CancellationToken ct = default)
    {
        var timestamp = DateTime.UtcNow;
        var dimension = new Dimension
        {
            Name = DimensionName,
            Value = NormalizePrefix(prefix)
        };

        var metricData = new List<MetricDatum>
        {
            // Merge duration
            new MetricDatum
            {
                MetricName = "MergeDuration",
                Value = durationMs,
                Unit = StandardUnit.Milliseconds,
                TimestampUtc = timestamp,
                Dimensions = new List<Dimension> { dimension }
            },
            // Success count
            new MetricDatum
            {
                MetricName = "MergeSuccess",
                Value = 1,
                Unit = StandardUnit.Count,
                TimestampUtc = timestamp,
                Dimensions = new List<Dimension> { dimension }
            },
            // Files processed
            new MetricDatum
            {
                MetricName = "FilesProcessed",
                Value = filesProcessed,
                Unit = StandardUnit.Count,
                TimestampUtc = timestamp,
                Dimensions = new List<Dimension> { dimension }
            },
            // Output written (1 if written, 0 if unchanged)
            new MetricDatum
            {
                MetricName = "OutputWritten",
                Value = outputWritten ? 1 : 0,
                Unit = StandardUnit.Count,
                TimestampUtc = timestamp,
                Dimensions = new List<Dimension> { dimension }
            }
        };

        await PutMetricsAsync(metricData, ct);
    }

    /// <inheritdoc />
    public async Task EmitFailureMetricsAsync(
        string prefix,
        long durationMs,
        string errorType,
        CancellationToken ct = default)
    {
        var timestamp = DateTime.UtcNow;
        var prefixDimension = new Dimension
        {
            Name = DimensionName,
            Value = NormalizePrefix(prefix)
        };
        var errorDimension = new Dimension
        {
            Name = "ErrorType",
            Value = errorType
        };

        var metricData = new List<MetricDatum>
        {
            // Merge duration (even for failures)
            new MetricDatum
            {
                MetricName = "MergeDuration",
                Value = durationMs,
                Unit = StandardUnit.Milliseconds,
                TimestampUtc = timestamp,
                Dimensions = new List<Dimension> { prefixDimension }
            },
            // Failure count
            new MetricDatum
            {
                MetricName = "MergeFailure",
                Value = 1,
                Unit = StandardUnit.Count,
                TimestampUtc = timestamp,
                Dimensions = new List<Dimension> { prefixDimension }
            },
            // Failure count by error type
            new MetricDatum
            {
                MetricName = "MergeFailure",
                Value = 1,
                Unit = StandardUnit.Count,
                TimestampUtc = timestamp,
                Dimensions = new List<Dimension> { prefixDimension, errorDimension }
            }
        };

        await PutMetricsAsync(metricData, ct);
    }

    /// <summary>
    /// Puts metrics to CloudWatch.
    /// </summary>
    private async Task PutMetricsAsync(List<MetricDatum> metricData, CancellationToken ct)
    {
        try
        {
            var request = new PutMetricDataRequest
            {
                Namespace = Namespace,
                MetricData = metricData
            };

            await _cloudWatch.PutMetricDataAsync(request, ct);
            _logger.LogDebug("Emitted {Count} metrics to CloudWatch", metricData.Count);
        }
        catch (Exception ex)
        {
            // Log but don't fail the operation if metrics emission fails
            _logger.LogWarning(ex, "Failed to emit CloudWatch metrics");
        }
    }

    /// <summary>
    /// Normalizes the prefix for use as a dimension value.
    /// </summary>
    private static string NormalizePrefix(string prefix)
    {
        if (string.IsNullOrEmpty(prefix))
        {
            return "root";
        }

        // Remove trailing slash for cleaner dimension values
        return prefix.TrimEnd('/');
    }
}
