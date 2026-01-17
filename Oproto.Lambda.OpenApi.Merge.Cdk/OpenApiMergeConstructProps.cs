using Amazon.CDK.AWS.S3;
using Amazon.CDK.AWS.SNS;

namespace Oproto.Lambda.OpenApi.Merge.Cdk;

/// <summary>
/// Properties for configuring the OpenAPI merge construct.
/// </summary>
public class OpenApiMergeConstructProps
{
    /// <summary>
    /// The S3 bucket containing input files (config and source specs). Required.
    /// </summary>
    public required IBucket InputBucket { get; init; }

    /// <summary>
    /// The S3 bucket for output files. Defaults to InputBucket if not specified.
    /// </summary>
    public IBucket? OutputBucket { get; init; }

    /// <summary>
    /// List of API prefixes to monitor for changes (e.g., "publicapi/", "internalapi/").
    /// Each prefix should end with a forward slash.
    /// </summary>
    public required IReadOnlyList<string> ApiPrefixes { get; init; }

    /// <summary>
    /// Debounce wait duration in seconds. Default: 5.
    /// This is the time to wait after the last S3 event before triggering a merge.
    /// </summary>
    public int DebounceSeconds { get; init; } = 5;

    /// <summary>
    /// Whether to create CloudWatch alarms for merge failures. Default: true.
    /// </summary>
    public bool EnableAlarms { get; init; } = true;

    /// <summary>
    /// Failure count threshold for CloudWatch alarms. Default: 1.
    /// An alarm will trigger when failures exceed this threshold.
    /// </summary>
    public int AlarmThreshold { get; init; } = 1;

    /// <summary>
    /// Number of evaluation periods for CloudWatch alarms. Default: 1.
    /// </summary>
    public int AlarmEvaluationPeriods { get; init; } = 1;

    /// <summary>
    /// Optional SNS topic for alarm notifications.
    /// If not specified, alarms will be created without notification actions.
    /// </summary>
    public ITopic? AlarmTopic { get; init; }

    /// <summary>
    /// Lambda memory size in MB. Default: 512.
    /// </summary>
    public int MemorySize { get; init; } = 512;

    /// <summary>
    /// Lambda timeout in seconds. Default: 60.
    /// </summary>
    public int TimeoutSeconds { get; init; } = 60;

    /// <summary>
    /// Optional custom name for the Lambda function.
    /// If not specified, CDK will generate a unique name.
    /// </summary>
    public string? FunctionName { get; init; }

    /// <summary>
    /// Optional custom name for the Step Functions state machine.
    /// If not specified, CDK will generate a unique name.
    /// </summary>
    public string? StateMachineName { get; init; }

    /// <summary>
    /// Optional custom name for the DynamoDB debounce table.
    /// If not specified, CDK will generate a unique name.
    /// </summary>
    public string? DebounceTableName { get; init; }
}
