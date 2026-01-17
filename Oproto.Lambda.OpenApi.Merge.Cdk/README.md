# Oproto.Lambda.OpenApi.Merge.Cdk

AWS CDK construct for deploying the OpenAPI merge Lambda function with S3 event triggers and Step Functions debouncing.

## Installation

### CDK (Recommended)

```bash
dotnet add package Oproto.Lambda.OpenApi.Merge.Cdk
```

### CloudFormation (Alternative)

For users who don't use CDK, a standalone CloudFormation template is available at `cloudformation/openapi-merge.yaml`.

## Quick Start

### Using CDK

```csharp
using Amazon.CDK;
using Amazon.CDK.AWS.S3;
using Oproto.Lambda.OpenApi.Merge.Cdk;

var bucket = new Bucket(this, "ApiBucket");

var mergeConstruct = new OpenApiMergeConstruct(this, "OpenApiMerge", new OpenApiMergeConstructProps
{
    InputBucket = bucket,
    ApiPrefixes = new[] { "publicapi/", "internalapi/" },
    DebounceSeconds = 5,
    EnableAlarms = true
});
```

### Using CloudFormation

1. First, build and package the Lambda function:
   ```bash
   dotnet publish Oproto.Lambda.OpenApi.Merge.Lambda -c Release -o ./publish
   cd publish && zip -r ../lambda-package.zip . && cd ..
   ```

2. Upload the package to S3:
   ```bash
   aws s3 cp lambda-package.zip s3://your-deployment-bucket/openapi-merge/lambda-package.zip
   ```

3. Deploy the CloudFormation stack:
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

## Features

- Automatic OpenAPI spec merging on S3 file changes
- Step Functions-based debouncing to batch rapid changes
- Configurable CloudWatch alarms
- Support for single-bucket or dual-bucket configurations
- Multi-API prefix support with single deployment

## CloudFormation Parameters

| Parameter | Description | Default |
|-----------|-------------|---------|
| `InputBucketName` | S3 bucket containing input files (required) | - |
| `OutputBucketName` | S3 bucket for output files (optional, defaults to input bucket) | '' |
| `ApiPrefixes` | Comma-separated list of API prefixes to monitor | '' |
| `LambdaCodeS3Bucket` | S3 bucket containing the Lambda deployment package (required) | - |
| `LambdaCodeS3Key` | S3 key for the Lambda deployment package (required) | - |
| `MemorySize` | Lambda memory size in MB | 512 |
| `TimeoutSeconds` | Lambda timeout in seconds | 60 |
| `DebounceSeconds` | Debounce wait time in seconds | 5 |
| `EnableAlarms` | Whether to create CloudWatch alarms | 'true' |
| `AlarmThreshold` | Failure count threshold for alarms | 1 |
| `AlarmEvaluationPeriods` | Number of evaluation periods for alarms | 1 |
| `AlarmSnsTopicArn` | Optional SNS topic ARN for alarm notifications | '' |

## CloudFormation Outputs

| Output | Description |
|--------|-------------|
| `MergeFunctionArn` | ARN of the OpenAPI merge Lambda function |
| `MergeFunctionName` | Name of the Lambda function |
| `StateMachineArn` | ARN of the debounce Step Functions state machine |
| `StateMachineName` | Name of the state machine |
| `DebounceTableName` | Name of the DynamoDB debounce table |
| `DebounceTableArn` | ARN of the DynamoDB table |
| `EventBridgeRuleArn` | ARN of the EventBridge rule for S3 events |
| `OutputBucketName` | Name of the output S3 bucket |

## Documentation

See the [full documentation](https://github.com/oproto/lambda-openapi/blob/main/docs/lambda-merge.md) for detailed usage instructions.
