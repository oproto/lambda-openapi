using Amazon.CDK;
using Amazon.CDK.AWS.CloudWatch;
using Amazon.CDK.AWS.CloudWatch.Actions;
using Amazon.CDK.AWS.DynamoDB;
using Amazon.CDK.AWS.Events;
using Amazon.CDK.AWS.Events.Targets;
using Amazon.CDK.AWS.IAM;
using Amazon.CDK.AWS.Lambda;
using Amazon.CDK.AWS.S3;
using Amazon.CDK.AWS.SNS;
using Amazon.CDK.AWS.StepFunctions;
using Constructs;

namespace Oproto.Lambda.OpenApi.Merge.Cdk;

/// <summary>
/// CDK construct that creates the OpenAPI merge infrastructure including:
/// - Lambda function for merging OpenAPI specs
/// - DynamoDB table for debounce state
/// - Step Functions state machine for debouncing
/// - EventBridge rules for S3 events
/// - CloudWatch alarms (optional)
/// </summary>
public class OpenApiMergeConstruct : Construct
{
    /// <summary>
    /// The Lambda function that performs the merge operation.
    /// </summary>
    public IFunction MergeFunction { get; }

    /// <summary>
    /// The Step Functions state machine that handles debouncing.
    /// </summary>
    public IStateMachine StateMachine { get; }

    /// <summary>
    /// The DynamoDB table used for debounce state tracking.
    /// </summary>
    public ITable DebounceTable { get; }

    /// <summary>
    /// The CloudWatch alarm for merge failures (if enabled).
    /// </summary>
    public IAlarm? FailureAlarm { get; }

    /// <summary>
    /// The effective output bucket (either explicitly provided or same as input).
    /// </summary>
    public IBucket OutputBucket { get; }

    public OpenApiMergeConstruct(Construct scope, string id, OpenApiMergeConstructProps props)
        : base(scope, id)
    {
        ValidateProps(props);

        OutputBucket = props.OutputBucket ?? props.InputBucket;

        // Create DynamoDB table for debounce state
        DebounceTable = CreateDebounceTable(props);

        // Create Lambda function
        MergeFunction = CreateMergeFunction(props);

        // Create Step Functions state machine
        StateMachine = CreateStateMachine(props);

        // Create EventBridge rules for S3 events
        CreateEventBridgeRules(props);

        // Create CloudWatch alarms if enabled
        if (props.EnableAlarms)
        {
            FailureAlarm = CreateFailureAlarm(props);
        }

        // Create outputs
        CreateOutputs();
    }

    private static void ValidateProps(OpenApiMergeConstructProps props)
    {
        if (props.ApiPrefixes == null || props.ApiPrefixes.Count == 0)
        {
            throw new ArgumentException("At least one API prefix must be specified", nameof(props));
        }

        if (props.DebounceSeconds < 1)
        {
            throw new ArgumentException("DebounceSeconds must be at least 1", nameof(props));
        }

        if (props.MemorySize < 128 || props.MemorySize > 10240)
        {
            throw new ArgumentException("MemorySize must be between 128 and 10240 MB", nameof(props));
        }

        if (props.TimeoutSeconds < 1 || props.TimeoutSeconds > 900)
        {
            throw new ArgumentException("TimeoutSeconds must be between 1 and 900", nameof(props));
        }
    }

    private Table CreateDebounceTable(OpenApiMergeConstructProps props)
    {
        return new Table(this, "DebounceTable", new TableProps
        {
            TableName = props.DebounceTableName,
            PartitionKey = new Amazon.CDK.AWS.DynamoDB.Attribute
            {
                Name = "prefix",
                Type = AttributeType.STRING
            },
            BillingMode = BillingMode.PAY_PER_REQUEST,
            RemovalPolicy = RemovalPolicy.DESTROY,
            TimeToLiveAttribute = "ttl"
        });
    }

    private Function CreateMergeFunction(OpenApiMergeConstructProps props)
    {
        var function = new Function(this, "MergeFunction", new FunctionProps
        {
            FunctionName = props.FunctionName,
            Runtime = Runtime.DOTNET_8,
            Handler = "Oproto.Lambda.OpenApi.Merge.Lambda::Oproto.Lambda.OpenApi.Merge.Lambda.Functions.MergeFunction_HandleMerge_Generated::HandleMerge",
            Code = Code.FromAsset(GetLambdaAssetPath()),
            MemorySize = props.MemorySize,
            Timeout = Duration.Seconds(props.TimeoutSeconds),
            Environment = new Dictionary<string, string>
            {
                ["OUTPUT_BUCKET"] = OutputBucket.BucketName
            },
            Tracing = Tracing.ACTIVE
        });

        // Grant S3 permissions
        props.InputBucket.GrantRead(function);
        OutputBucket.GrantReadWrite(function);

        // Grant CloudWatch metrics permissions
        function.AddToRolePolicy(new PolicyStatement(new PolicyStatementProps
        {
            Actions = new[] { "cloudwatch:PutMetricData" },
            Resources = new[] { "*" }
        }));

        return function;
    }

    private StateMachine CreateStateMachine(OpenApiMergeConstructProps props)
    {
        var stateMachineDefinition = CreateStateMachineDefinition(props);

        var stateMachine = new StateMachine(this, "DebounceStateMachine", new StateMachineProps
        {
            StateMachineName = props.StateMachineName,
            DefinitionBody = DefinitionBody.FromString(stateMachineDefinition),
            StateMachineType = StateMachineType.STANDARD,
            Timeout = Duration.Minutes(10),
            TracingEnabled = true
        });

        // Grant DynamoDB permissions to state machine
        DebounceTable.GrantReadWriteData(stateMachine);

        // Grant Lambda invoke permissions to state machine
        MergeFunction.GrantInvoke(stateMachine);

        return stateMachine;
    }

    private string CreateStateMachineDefinition(OpenApiMergeConstructProps props)
    {
        var substitutions = StateMachineDefinitionLoader.CreateSubstitutions(
            debounceTableName: DebounceTable.TableName,
            mergeFunctionArn: MergeFunction.FunctionArn,
            outputBucket: OutputBucket.BucketName,
            debounceSeconds: props.DebounceSeconds
        );

        return StateMachineDefinitionLoader.LoadDefinition(substitutions);
    }

    private void CreateEventBridgeRules(OpenApiMergeConstructProps props)
    {
        foreach (var prefix in props.ApiPrefixes)
        {
            var normalizedPrefix = prefix.TrimEnd('/');
            var ruleName = $"OpenApiMerge-{normalizedPrefix.Replace("/", "-")}";

            var rule = new Rule(this, $"S3EventRule-{normalizedPrefix.Replace("/", "-")}", new RuleProps
            {
                RuleName = ruleName.Length > 64 ? ruleName.Substring(0, 64) : ruleName,
                EventPattern = new EventPattern
                {
                    Source = new[] { "aws.s3" },
                    DetailType = new[] { "Object Created", "Object Deleted" },
                    Detail = new Dictionary<string, object>
                    {
                        ["bucket"] = new Dictionary<string, object>
                        {
                            ["name"] = new[] { props.InputBucket.BucketName }
                        },
                        ["object"] = new Dictionary<string, object>
                        {
                            ["key"] = new[] { new Dictionary<string, string> { ["prefix"] = normalizedPrefix + "/" } }
                        }
                    }
                }
            });

            rule.AddTarget(new SfnStateMachine(StateMachine));
        }
    }

    private Alarm CreateFailureAlarm(OpenApiMergeConstructProps props)
    {
        var metric = new Metric(new MetricProps
        {
            Namespace = "OpenApiMerge",
            MetricName = "MergeFailures",
            Statistic = "Sum",
            Period = Duration.Minutes(5)
        });

        var alarm = new Alarm(this, "MergeFailureAlarm", new AlarmProps
        {
            AlarmName = $"OpenApiMerge-Failures",
            AlarmDescription = "Alarm when OpenAPI merge operations fail",
            Metric = metric,
            Threshold = props.AlarmThreshold,
            EvaluationPeriods = props.AlarmEvaluationPeriods,
            ComparisonOperator = ComparisonOperator.GREATER_THAN_OR_EQUAL_TO_THRESHOLD,
            TreatMissingData = TreatMissingData.NOT_BREACHING
        });

        if (props.AlarmTopic != null)
        {
            alarm.AddAlarmAction(new SnsAction(props.AlarmTopic));
        }

        return alarm;
    }

    private void CreateOutputs()
    {
        _ = new CfnOutput(this, "MergeFunctionArn", new CfnOutputProps
        {
            Value = MergeFunction.FunctionArn,
            Description = "ARN of the OpenAPI merge Lambda function"
        });

        _ = new CfnOutput(this, "StateMachineArn", new CfnOutputProps
        {
            Value = StateMachine.StateMachineArn,
            Description = "ARN of the debounce Step Functions state machine"
        });

        _ = new CfnOutput(this, "DebounceTableName", new CfnOutputProps
        {
            Value = DebounceTable.TableName,
            Description = "Name of the DynamoDB debounce table"
        });
    }

    private static string GetLambdaAssetPath()
    {
        // This returns the path to the Lambda project for bundling
        // In a real deployment, this would be the published output directory
        return Path.Combine(
            Path.GetDirectoryName(typeof(OpenApiMergeConstruct).Assembly.Location) ?? ".",
            "..",
            "..",
            "..",
            "..",
            "Oproto.Lambda.OpenApi.Merge.Lambda",
            "bin",
            "Release",
            "net8.0",
            "publish"
        );
    }
}
