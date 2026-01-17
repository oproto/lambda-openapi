# Requirements Document

## Introduction

This feature provides an AWS Lambda-based implementation of the OpenAPI merge tool that automatically merges multiple OpenAPI specification files when changes are detected in S3. The solution uses S3 event notifications to trigger merges, supports flexible bucket configurations, and includes debouncing to handle rapid successive changes efficiently. As part of an OSS project, the deployment model prioritizes ease of adoption through a .NET CDK construct library.

## Glossary

- **Merge_Lambda**: The AWS Lambda function that performs OpenAPI specification merging
- **Config_File**: A JSON configuration file that defines merge settings and source file references for a specific API
- **Source_Spec**: An individual OpenAPI specification file to be merged
- **Output_Spec**: The resulting merged OpenAPI specification file
- **API_Prefix**: An S3 key prefix that groups related config and source files (e.g., `/publicapi/`, `/internalapi/`)
- **Debounce_State_Machine**: An AWS Step Functions state machine that delays merge execution to batch rapid changes
- **CDK_Construct**: A reusable AWS CDK component that encapsulates the Lambda merge infrastructure
- **Input_Bucket**: The S3 bucket containing config files and source OpenAPI specs
- **Output_Bucket**: The S3 bucket where merged specs are written (may be same as Input_Bucket)

## Requirements

### Requirement 1: S3 Event-Triggered Merge Execution

**User Story:** As a developer, I want the merge process to automatically trigger when I upload or modify OpenAPI spec files in S3, so that my merged API documentation stays current without manual intervention.

#### Acceptance Criteria

1. WHEN an S3 object is created or modified with a key matching `{prefix}/config.json`, THE Merge_Lambda SHALL initiate a merge operation for that API prefix
2. WHEN an S3 object is created or modified with a key matching `{prefix}/*.json` (excluding config.json), THE Merge_Lambda SHALL initiate a merge operation for that API prefix
3. WHEN an S3 object is deleted with a key matching `{prefix}/*.json`, THE Merge_Lambda SHALL initiate a merge operation for that API prefix
4. THE Merge_Lambda SHALL extract the API_Prefix from the S3 event key to determine which config file to load
5. WHEN multiple S3 events occur within a configurable debounce window, THE Debounce_State_Machine SHALL consolidate them into a single merge execution

### Requirement 2: Configuration File Loading

**User Story:** As a developer, I want to define my merge configuration in a JSON file within the same S3 prefix, so that each API's merge settings are self-contained and version-controlled.

#### Acceptance Criteria

1. WHEN a merge is triggered, THE Merge_Lambda SHALL load the config file from `{prefix}/config.json` in the Input_Bucket
2. THE Config_File SHALL support specifying source file patterns relative to the API_Prefix
3. THE Config_File SHALL support all existing MergeConfiguration options from the Oproto.Lambda.OpenApi.Merge library
4. IF the Config_File does not exist, THEN THE Merge_Lambda SHALL return an error and log the missing configuration
5. IF the Config_File contains invalid JSON, THEN THE Merge_Lambda SHALL return an error with descriptive message

### Requirement 3: Source File Discovery and Loading

**User Story:** As a developer, I want flexibility in how source files are discovered—either auto-discovery or explicit listing—so that I can control which files are merged during development.

#### Acceptance Criteria

1. THE MergeConfiguration class SHALL support an `autoDiscover` boolean property (default: false)
2. THE MergeConfiguration class SHALL support an `excludePatterns` array for glob patterns to exclude
3. WHEN `autoDiscover` is true, THE Merge_Lambda SHALL list all `.json` files within the API_Prefix (excluding config.json, output file, and files matching excludePatterns)
4. WHEN `autoDiscover` is false, THE Config_File SHALL contain a `sources` array listing explicit file names to merge
5. THE Merge_Lambda SHALL load each source file from the Input_Bucket
6. IF a source file cannot be parsed as valid OpenAPI, THEN THE Merge_Lambda SHALL log a warning and skip that file
7. IF no valid source files are found, THEN THE Merge_Lambda SHALL return an error indicating no sources to merge
8. WHEN using explicit sources, IF a listed file does not exist, THEN THE Merge_Lambda SHALL log an error for that file and continue with remaining files
9. THE CLI merge tool SHALL also support `autoDiscover` and `excludePatterns` for consistency

### Requirement 4: Merge Execution

**User Story:** As a developer, I want the Lambda to use the existing Oproto.Lambda.OpenApi.Merge library for merging, so that I get consistent behavior with the CLI tool.

#### Acceptance Criteria

1. THE Merge_Lambda SHALL use the OpenApiMerger class from Oproto.Lambda.OpenApi.Merge to perform merges
2. THE Merge_Lambda SHALL pass the loaded Config_File settings to the merger
3. WHEN the merge completes successfully, THE Merge_Lambda SHALL produce a valid OpenAPI specification
4. IF the merge fails due to conflicts, THEN THE Merge_Lambda SHALL return an error with conflict details

### Requirement 5: Output Comparison and Conditional Write

**User Story:** As a developer, I want the Lambda to only write the output file when the merged result differs from the existing output, so that downstream processes aren't triggered unnecessarily.

#### Acceptance Criteria

1. WHEN a merge completes, THE Merge_Lambda SHALL read the existing Output_Spec from the Output_Bucket if it exists
2. THE Merge_Lambda SHALL compare the new merged spec with the existing Output_Spec
3. IF the merged spec differs from the existing Output_Spec, THEN THE Merge_Lambda SHALL write the new spec to the Output_Bucket
4. IF the merged spec is identical to the existing Output_Spec, THEN THE Merge_Lambda SHALL skip writing and log that no changes were detected
5. THE comparison SHALL be performed on normalized JSON to ignore formatting differences

### Requirement 6: Flexible Bucket Configuration

**User Story:** As a developer, I want to configure whether input and output use the same bucket or separate buckets, so that I can adapt to different organizational requirements.

#### Acceptance Criteria

1. THE CDK_Construct SHALL support a single-bucket mode where Input_Bucket and Output_Bucket are the same
2. THE CDK_Construct SHALL support a dual-bucket mode where Input_Bucket and Output_Bucket are different
3. WHEN using single-bucket mode, THE Output_Spec SHALL be written to a configurable output path within the same bucket
4. WHEN using dual-bucket mode, THE Merge_Lambda SHALL have read permissions on Input_Bucket and write permissions on Output_Bucket
5. THE Config_File SHALL specify the output file path relative to the Output_Bucket

### Requirement 7: Debounce via Step Functions

**User Story:** As a developer, I want rapid successive file changes to be debounced into a single merge operation, so that I don't waste compute resources on intermediate states.

#### Acceptance Criteria

1. WHEN an S3 event triggers the system, THE Debounce_State_Machine SHALL start or reset a wait timer for that API_Prefix
2. THE Debounce_State_Machine SHALL use a configurable wait duration (default 5 seconds)
3. WHEN the wait timer expires without new events for the same API_Prefix, THE Debounce_State_Machine SHALL invoke the Merge_Lambda
4. IF a new event arrives for the same API_Prefix during the wait period, THE Debounce_State_Machine SHALL reset the timer
5. THE Debounce_State_Machine SHALL track separate timers for each distinct API_Prefix
6. THE Debounce_State_Machine SHALL be defined using JSONata query language for data transformations where possible

### Requirement 8: CDK Construct Library for Easy Deployment

**User Story:** As an OSS consumer, I want a simple CDK construct that I can add to my infrastructure code, so that I can deploy the merge Lambda with minimal configuration.

#### Acceptance Criteria

1. THE CDK_Construct SHALL be published as a separate NuGet package (Oproto.Lambda.OpenApi.Merge.Cdk)
2. THE CDK_Construct SHALL accept Input_Bucket as a required parameter
3. THE CDK_Construct SHALL accept Output_Bucket as an optional parameter (defaults to Input_Bucket)
4. THE CDK_Construct SHALL accept a list of API_Prefix values to configure S3 event filters
5. THE CDK_Construct SHALL create all necessary IAM roles and permissions
6. THE CDK_Construct SHALL create the Debounce_State_Machine with configurable wait duration
7. THE CDK_Construct SHALL expose the created Lambda function ARN and Step Function ARN as outputs

### Requirement 8a: CloudFormation Template for Non-CDK Users

**User Story:** As a developer who doesn't use CDK, I want a CloudFormation template that I can deploy directly, so that I can use the merge Lambda without adopting CDK.

#### Acceptance Criteria

1. THE project SHALL include a standalone CloudFormation template (YAML format)
2. THE CloudFormation template SHALL accept parameters for Input_Bucket, Output_Bucket, and API_Prefix list
3. THE CloudFormation template SHALL create the same resources as the CDK_Construct
4. THE CloudFormation template SHALL be documented with deployment instructions
5. THE CDK_Construct SHALL be capable of synthesizing to the CloudFormation template for consistency

### Requirement 9: Multi-API Support with Single Deployment

**User Story:** As a developer, I want a single Lambda deployment to handle multiple API prefixes, so that I minimize infrastructure overhead.

#### Acceptance Criteria

1. THE Merge_Lambda SHALL be capable of processing events for any API_Prefix
2. THE CDK_Construct SHALL configure S3 event notifications for all specified API_Prefix values
3. WHEN processing an event, THE Merge_Lambda SHALL dynamically determine the API_Prefix from the S3 key
4. THE Merge_Lambda SHALL maintain no state between invocations (stateless design)

### Requirement 10: Error Handling and Observability

**User Story:** As an operator, I want comprehensive logging and configurable metrics/alarms, so that I can troubleshoot issues and monitor the merge process according to my needs.

#### Acceptance Criteria

1. THE Merge_Lambda SHALL log the start and completion of each merge operation with timing information
2. THE Merge_Lambda SHALL log all S3 read and write operations
3. IF an error occurs, THEN THE Merge_Lambda SHALL log the full error details including stack trace
4. THE Merge_Lambda SHALL emit CloudWatch metrics for: merge duration, success count, failure count, files processed
5. THE CDK_Construct SHALL accept an `enableAlarms` boolean parameter (default: true)
6. WHEN `enableAlarms` is true, THE CDK_Construct SHALL create a CloudWatch alarm for merge failures
7. THE CDK_Construct SHALL accept an `alarmThreshold` parameter for failure count threshold (default: 1)
8. THE CDK_Construct SHALL accept an `alarmEvaluationPeriods` parameter (default: 1)
9. THE CDK_Construct SHALL accept an optional SNS topic ARN for alarm notifications

### Requirement 11: Lambda Annotations Integration

**User Story:** As a developer familiar with the Oproto.Lambda.OpenApi library, I want the merge Lambda to use Lambda Annotations, so that the codebase is consistent and I can learn from the implementation.

#### Acceptance Criteria

1. THE Merge_Lambda SHALL be implemented using Amazon.Lambda.Annotations
2. THE Merge_Lambda SHALL use the S3 event source binding
3. THE Merge_Lambda SHALL follow the same coding patterns as the Oproto.Lambda.OpenApi.Examples project

### Requirement 12: Documentation and Changelog Updates

**User Story:** As a user of the library, I want comprehensive documentation for the new Lambda merge tool and updated changelogs, so that I can understand how to use and deploy it.

#### Acceptance Criteria

1. THE project SHALL update CHANGELOG.md with all new features and breaking changes
2. THE project SHALL create docs/lambda-merge.md with deployment and usage instructions
3. THE documentation SHALL include example config files for both auto-discover and explicit sources modes
4. THE documentation SHALL include CDK construct usage examples
5. THE documentation SHALL include CloudFormation deployment instructions
6. THE documentation SHALL document the debounce behavior and timing considerations
7. THE existing docs/merge-tool.md SHALL be updated to document `autoDiscover` and `excludePatterns` options
8. THE README.md SHALL be updated to reference the new Lambda merge tool
