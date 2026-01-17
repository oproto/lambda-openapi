# Lambda Merge Tool

The Lambda Merge Tool provides an AWS Lambda-based solution for automatically merging OpenAPI specification files when changes are detected in S3. This is ideal for CI/CD pipelines and automated documentation workflows where you want merged API specs to stay current without manual intervention.

## Features

- **Automatic Merging**: Triggers automatically when OpenAPI spec files are uploaded or modified in S3
- **Debouncing**: Batches rapid successive changes into a single merge operation using Step Functions
- **Flexible Configuration**: Supports both auto-discovery and explicit source file listing
- **Conditional Writes**: Only writes output when the merged result differs from existing output
- **Multi-API Support**: Single deployment can handle multiple API prefixes
- **CloudWatch Integration**: Built-in metrics and configurable alarms

## Architecture

```
┌─────────────────┐     ┌──────────────┐     ┌─────────────────┐     ┌──────────────┐
│   S3 Bucket     │────▶│  EventBridge │────▶│ Step Functions  │────▶│    Lambda    │
│ (Input Files)   │     │    Rule      │     │   (Debounce)    │     │   (Merge)    │
└─────────────────┘     └──────────────┘     └─────────────────┘     └──────────────┘
                                                                            │
                                                                            ▼
                                                                     ┌──────────────┐
                                                                     │   S3 Bucket  │
                                                                     │   (Output)   │
                                                                     └──────────────┘
```

1. **S3 Event**: User uploads/modifies a file in `{prefix}/`
2. **EventBridge Rule**: Filters events by prefix pattern, triggers Step Functions
3. **Debounce State Machine**: Waits for configurable duration, resets on new events
4. **Merge Lambda**: Loads config, discovers/loads sources, merges, compares, writes if changed

## Deployment Options

### Option 1: CDK (Recommended)

Install the CDK construct package:

```bash
dotnet add package Oproto.Lambda.OpenApi.Merge.Cdk
```

Add the construct to your CDK stack:

```csharp
using Amazon.CDK;
using Amazon.CDK.AWS.S3;
using Oproto.Lambda.OpenApi.Merge.Cdk;

public class MyStack : Stack
{
    public MyStack(Construct scope, string id, IStackProps props = null) : base(scope, id, props)
    {
        var bucket = new Bucket(this, "ApiBucket");

        var mergeConstruct = new OpenApiMergeConstruct(this, "OpenApiMerge", new OpenApiMergeConstructProps
        {
            InputBucket = bucket,
            ApiPrefixes = new[] { "publicapi/", "internalapi/" },
            DebounceSeconds = 5,
            EnableAlarms = true
        });

        // Access outputs
        new CfnOutput(this, "MergeFunctionArn", new CfnOutputProps
        {
            Value = mergeConstruct.MergeFunction.FunctionArn
        });
    }
}
```

#### CDK Construct Properties

| Property | Type | Default | Description |
|----------|------|---------|-------------|
| `InputBucket` | `IBucket` | Required | S3 bucket containing input files |
| `OutputBucket` | `IBucket` | InputBucket | S3 bucket for output files |
| `ApiPrefixes` | `string[]` | Required | List of API prefixes to monitor |
| `DebounceSeconds` | `int` | 5 | Wait time before triggering merge |
| `EnableAlarms` | `bool` | true | Create CloudWatch alarms |
| `AlarmThreshold` | `int` | 1 | Failure count threshold |
| `AlarmEvaluationPeriods` | `int` | 1 | Number of evaluation periods |
| `AlarmTopic` | `ITopic` | null | SNS topic for alarm notifications |
| `MemorySize` | `int` | 512 | Lambda memory size in MB |
| `TimeoutSeconds` | `int` | 60 | Lambda timeout in seconds |

### Option 2: CloudFormation

For users who don't use CDK, a standalone CloudFormation template is available.

#### Step 1: Build and Package the Lambda

```bash
# Build the Lambda project
dotnet publish Oproto.Lambda.OpenApi.Merge.Lambda -c Release -o ./publish

# Create deployment package
cd publish && zip -r ../lambda-package.zip . && cd ..
```

#### Step 2: Upload to S3

```bash
aws s3 cp lambda-package.zip s3://your-deployment-bucket/openapi-merge/lambda-package.zip
```

#### Step 3: Deploy the Stack

```bash
aws cloudformation create-stack \
  --stack-name openapi-merge \
  --template-body file://Oproto.Lambda.OpenApi.Merge.Cdk/cloudformation/openapi-merge.yaml \
  --parameters \
    ParameterKey=InputBucketName,ParameterValue=your-api-specs-bucket \
    ParameterKey=LambdaCodeS3Bucket,ParameterValue=your-deployment-bucket \
    ParameterKey=LambdaCodeS3Key,ParameterValue=openapi-merge/lambda-package.zip \
  --capabilities CAPABILITY_NAMED_IAM
```

#### CloudFormation Parameters

| Parameter | Description | Default |
|-----------|-------------|---------|
| `InputBucketName` | S3 bucket containing input files (required) | - |
| `OutputBucketName` | S3 bucket for output files (optional) | Same as input |
| `ApiPrefixes` | Comma-separated list of API prefixes | '' |
| `LambdaCodeS3Bucket` | S3 bucket with Lambda package (required) | - |
| `LambdaCodeS3Key` | S3 key for Lambda package (required) | - |
| `MemorySize` | Lambda memory size in MB | 512 |
| `TimeoutSeconds` | Lambda timeout in seconds | 60 |
| `DebounceSeconds` | Debounce wait time in seconds | 5 |
| `EnableAlarms` | Create CloudWatch alarms | 'true' |
| `AlarmThreshold` | Failure count threshold | 1 |
| `AlarmEvaluationPeriods` | Evaluation periods for alarms | 1 |
| `AlarmSnsTopicArn` | SNS topic for alarm notifications | '' |

## Configuration File Format

Each API prefix requires a `config.json` file that defines how the merge should be performed.

### File Location

Place the config file at `{prefix}/config.json` in your S3 bucket. For example:
- `publicapi/config.json`
- `internalapi/config.json`

### Configuration Schema

```json
{
  "info": {
    "title": "string (required)",
    "version": "string (required)",
    "description": "string (optional)"
  },
  "servers": [
    {
      "url": "string (required)",
      "description": "string (optional)"
    }
  ],
  "autoDiscover": "boolean (optional, default: false)",
  "excludePatterns": ["string (optional)"],
  "sources": [
    {
      "path": "string (required when autoDiscover is false)",
      "pathPrefix": "string (optional)",
      "operationIdPrefix": "string (optional)",
      "name": "string (optional)"
    }
  ],
  "output": "string (required)",
  "outputBucket": "string (optional, Lambda-only)",
  "schemaConflict": "rename | first-wins | fail (optional, default: rename)"
}
```

### Example: Auto-Discovery Mode

When `autoDiscover` is true, the Lambda automatically finds all `.json` files in the prefix directory (excluding `config.json` and the output file).

```json
{
  "info": {
    "title": "Public API",
    "version": "1.0.0",
    "description": "Merged public API specification"
  },
  "servers": [
    {
      "url": "https://api.example.com/v1",
      "description": "Production"
    }
  ],
  "autoDiscover": true,
  "excludePatterns": ["*-draft.json", "*.backup.json"],
  "output": "merged-openapi.json",
  "schemaConflict": "rename"
}
```

### Example: Explicit Sources Mode

When `autoDiscover` is false (default), you must explicitly list the source files.

```json
{
  "info": {
    "title": "Internal API",
    "version": "2.0.0"
  },
  "autoDiscover": false,
  "sources": [
    {
      "path": "users-service.json",
      "name": "Users",
      "pathPrefix": "/users"
    },
    {
      "path": "orders-service.json",
      "name": "Orders",
      "pathPrefix": "/orders"
    }
  ],
  "output": "internal-api.json",
  "schemaConflict": "rename"
}
```

### Example: Dual-Bucket Configuration

Write output to a different bucket:

```json
{
  "info": {
    "title": "My API",
    "version": "1.0.0"
  },
  "autoDiscover": true,
  "output": "api-docs/merged.json",
  "outputBucket": "my-documentation-bucket"
}
```

## S3 Bucket Structure

### Single-Bucket Mode with Separate Output Prefix (Recommended)

You can specify a full path for the output to write it to a different prefix, avoiding the re-trigger issue:

```
my-api-bucket/
├── publicapi/
│   ├── config.json           # Merge configuration
│   ├── users-service.json    # Source spec
│   └── orders-service.json   # Source spec
└── output/
    └── publicapi/
        └── merged-openapi.json   # Output (not in monitored prefix)
```

Config file:
```json
{
  "info": { "title": "Public API", "version": "1.0.0" },
  "autoDiscover": true,
  "output": "output/publicapi/merged-openapi.json"
}
```

When the `output` value contains a `/`, it's treated as a full S3 key (not relative to the prefix). This lets you write output to any location in the bucket.

### Single-Bucket Mode with Same Prefix

When using a simple filename (no `/`), the output is written to the same prefix as the sources. This triggers another S3 event, but the system handles it gracefully:

1. **Conditional writes** - Only writes when content actually changes
2. **Debouncing** - Batches rapid events together  
3. **Idempotent merges** - Re-merging produces identical output, so no second write occurs

This results in one extra Step Functions execution per merge (the re-triggered one exits without writing).

```
my-api-bucket/
├── publicapi/
│   ├── config.json           # Merge configuration
│   ├── users-service.json    # Source spec
│   ├── orders-service.json   # Source spec
│   └── merged-openapi.json   # Output (triggers re-merge, but no write)
```

Config file:
```json
{
  "info": { "title": "Public API", "version": "1.0.0" },
  "autoDiscover": true,
  "output": "merged-openapi.json"
}
```

### Dual-Bucket Mode (Recommended for Production)

Using separate buckets for input and output completely eliminates the re-trigger issue:

Input bucket:
```
input-bucket/
└── publicapi/
    ├── config.json
    ├── users-service.json
    └── orders-service.json
```

Output bucket:
```
output-bucket/
└── publicapi/
    └── merged-openapi.json
```

## Debounce Behavior

The debounce mechanism prevents excessive merge operations when multiple files are uploaded in quick succession.

### How It Works

1. When an S3 event occurs, the Step Functions state machine starts a timer
2. If another event occurs for the same prefix during the wait period, the timer resets
3. When the timer expires without new events, the merge Lambda is invoked
4. If events arrive during merge execution, another merge is triggered after completion

### Timing Considerations

- **Default debounce**: 5 seconds
- **Recommended for CI/CD**: 5-10 seconds (allows batch uploads to complete)
- **Recommended for manual uploads**: 2-5 seconds (faster feedback)

### Post-Merge Event Handling

The state machine checks for events that arrived during merge execution. If new events are detected, it loops back and performs another merge to ensure no changes are missed.

```
Event 1 ──▶ Wait 5s ──▶ Merge ──▶ Check for new events ──▶ Done
                                         │
Event 2 (during merge) ◀─────────────────┘
                                         │
                                         ▼
                               Wait 5s ──▶ Merge ──▶ Done
```

## Monitoring and Observability

### CloudWatch Metrics

The Lambda emits the following metrics to the `OpenApiMerge` namespace:

| Metric | Description |
|--------|-------------|
| `MergeDuration` | Time taken to complete merge (milliseconds) |
| `MergeSuccess` | Count of successful merges |
| `MergeFailures` | Count of failed merges |
| `FilesProcessed` | Number of source files processed |

### CloudWatch Logs

The Lambda logs detailed information about each merge operation:

- Merge start and completion with timing
- S3 read and write operations
- Warnings for skipped files
- Full error details with stack traces

### CloudWatch Alarms

When `EnableAlarms` is true, an alarm is created for merge failures:

- **Alarm Name**: `{stack-name}-merge-failures`
- **Threshold**: Configurable (default: 1 failure)
- **Period**: 5 minutes
- **Action**: Optional SNS notification

## Troubleshooting

### Common Issues

#### Extra Step Functions executions in single-bucket mode

**Cause**: When using single-bucket mode with output in the same prefix, writing the merged output triggers another S3 event, which starts another Step Functions execution.

**Behavior**: The re-triggered execution will:
1. Load config and discover sources
2. Perform the merge (producing identical output)
3. Compare with existing output (finds no changes)
4. Skip writing (no actual S3 write occurs)
5. Exit cleanly

**Impact**: One extra Step Functions execution per merge. No infinite loop occurs because the second merge doesn't write anything.

**Solution**: For high-volume scenarios, use dual-bucket mode to eliminate this overhead entirely.

#### Config file not found

```
Error: Configuration file not found at publicapi/config.json
```

**Solution**: Ensure `config.json` exists at the correct prefix path in your S3 bucket.

#### Invalid JSON in config

```
Error: Invalid JSON: Unexpected character at position 42
```

**Solution**: Validate your `config.json` with a JSON linter.

#### No valid source files found

```
Error: No valid source files found
```

**Solution**: 
- If using `autoDiscover: true`, ensure `.json` files exist in the prefix
- If using explicit sources, verify the file paths are correct
- Check that files aren't excluded by `excludePatterns`

#### Schema conflict (with fail strategy)

```
Error: Schema conflict: 'Response' is defined differently in 'Users Service' and 'Products Service'
```

**Solution**: Use `schemaConflict: "rename"` or `"first-wins"`, or manually resolve the conflict in your source specs.

#### Access denied

```
Error: Access denied to my-bucket/publicapi/config.json
```

**Solution**: Verify the Lambda's IAM role has `s3:GetObject` permission on the input bucket and `s3:PutObject` on the output bucket.

### Debugging Tips

1. **Check CloudWatch Logs**: The Lambda logs detailed information about each step
2. **Verify S3 Event Notifications**: Ensure EventBridge is receiving S3 events
3. **Check Step Functions Execution**: View the state machine execution history in the AWS Console
4. **Test Locally**: Use the CLI merge tool to test your configuration before deploying

### Step Functions Execution States

| State | Description |
|-------|-------------|
| `ExtractPrefix` | Extracts API prefix from S3 key |
| `CheckExistingExecution` | Checks if another execution is handling this prefix |
| `WaitForDebounce` | Waits for debounce period |
| `InvokeMergeLambda` | Invokes the merge Lambda |
| `CheckPostMergeEvents` | Checks for events that arrived during merge |
| `CleanupExecution` | Removes debounce state from DynamoDB |

## Best Practices

1. **Use dual-bucket mode for high-volume scenarios**: If you have frequent updates, using a separate output bucket eliminates the re-trigger overhead entirely

2. **Use meaningful prefixes**: Organize APIs by domain or team (e.g., `payments/`, `users/`, `admin/`)

2. **Set appropriate debounce**: Balance between responsiveness and efficiency based on your upload patterns

3. **Use auto-discover for simple setups**: When all specs in a prefix should be merged

4. **Use explicit sources for control**: When you need path prefixes or want to exclude certain files

5. **Monitor merge failures**: Set up SNS notifications for the CloudWatch alarm

6. **Version control configs**: Keep your `config.json` files in version control alongside your source specs

7. **Test with CLI first**: Use the CLI merge tool to validate your configuration before deploying to Lambda

## Related Documentation

- [Merge Tool CLI](merge-tool.md) - Command-line merge tool documentation
- [CDK Construct README](../Oproto.Lambda.OpenApi.Merge.Cdk/README.md) - CDK-specific documentation
