# Implementation Plan: Lambda Merge Tool

## Overview

This implementation plan creates an AWS Lambda-based OpenAPI merge solution with S3 event triggers, Step Functions debouncing, and a CDK construct for easy deployment. The implementation is divided into phases: base library updates, Lambda function, CDK construct, and documentation.

## Tasks

- [x] 1. Update base MergeConfiguration with auto-discover support
  - [x] 1.1 Add `AutoDiscover` and `ExcludePatterns` properties to MergeConfiguration
    - Add `AutoDiscover` boolean property with default false
    - Add `ExcludePatterns` list property with default empty list
    - Add JSON serialization attributes
    - _Requirements: 3.1, 3.2_

  - [x] 1.2 Write property test for config compatibility
    - **Property 2: Config Compatibility Round-Trip**
    - **Validates: Requirements 2.3, 3.1**

  - [x] 1.3 Update CLI merge tool to support auto-discover
    - Implement file discovery logic in MergeCommand
    - Apply exclude patterns using glob matching
    - Skip output file automatically
    - _Requirements: 3.9_

  - [x] 1.4 Write unit tests for CLI auto-discover
    - Test auto-discover finds all JSON files
    - Test exclude patterns are applied
    - Test output file is excluded
    - _Requirements: 3.2, 3.9_

- [x] 2. Checkpoint - Ensure base library changes work
  - Ensure all tests pass, ask the user if questions arise.

- [x] 3. Create Lambda project structure
  - [x] 3.1 Create Oproto.Lambda.OpenApi.Merge.Lambda project
    - Create project file with Lambda Annotations dependencies
    - Add reference to Oproto.Lambda.OpenApi.Merge
    - Add AWS SDK dependencies (S3, DynamoDB, CloudWatch)
    - Create folder structure (Functions, Services, Models)
    - _Requirements: 11.1, 11.3_

  - [x] 3.2 Create data models
    - Create LambdaMergeConfig extending MergeConfiguration
    - Create MergeRequest record
    - Create MergeResponse record
    - Create MergeMetrics record
    - _Requirements: 2.3_

  - [x] 3.3 Write unit tests for data models
    - Test LambdaMergeConfig deserialization
    - Test default values
    - _Requirements: 2.3_

- [x] 4. Implement S3 service layer
  - [x] 4.1 Create IS3Service interface and S3Service implementation
    - Implement ReadJsonAsync<T>
    - Implement ReadTextAsync
    - Implement WriteJsonAsync<T>
    - Implement ListObjectsAsync
    - Implement ExistsAsync
    - _Requirements: 2.1, 3.4, 5.1_

  - [x] 4.2 Write unit tests for S3Service
    - Test JSON serialization/deserialization
    - Test list objects filtering
    - _Requirements: 2.1, 3.4_

- [x] 5. Implement config loader
  - [x] 5.1 Create IConfigLoader interface and ConfigLoader implementation
    - Load config from {prefix}/config.json
    - Validate required fields
    - Handle missing config error
    - Handle invalid JSON error
    - _Requirements: 2.1, 2.4, 2.5_

  - [x] 5.2 Write property test for prefix extraction
    - **Property 1: Prefix Extraction Consistency**
    - **Validates: Requirements 1.1, 1.2, 1.3, 1.4**

  - [x] 5.3 Write unit tests for config loader
    - Test valid config loading
    - Test missing config error
    - Test invalid JSON error
    - _Requirements: 2.4, 2.5_

- [x] 6. Implement source discovery
  - [x] 6.1 Create ISourceDiscovery interface and SourceDiscovery implementation
    - Implement auto-discover mode (list and filter files)
    - Implement explicit sources mode
    - Apply exclude patterns
    - Exclude config.json and output file
    - _Requirements: 3.2, 3.3, 3.4_

  - [x] 6.2 Write property test for auto-discovery filtering
    - **Property 3: Auto-Discovery Filtering**
    - **Validates: Requirements 3.2**

  - [x] 6.3 Write property test for explicit sources validation
    - **Property 4: Explicit Sources Validation**
    - **Validates: Requirements 3.3**

  - [x] 6.4 Write unit tests for source discovery
    - Test auto-discover finds JSON files
    - Test auto-discover excludes config.json
    - Test auto-discover excludes output file
    - Test exclude patterns work
    - Test explicit sources mode
    - _Requirements: 3.2, 3.3_

- [x] 7. Checkpoint - Ensure service layer works
  - Ensure all tests pass, ask the user if questions arise.

- [x] 8. Implement output comparison
  - [x] 8.1 Create output comparison logic
    - Implement JSON normalization (sorted keys, consistent formatting)
    - Implement semantic comparison
    - _Requirements: 5.2, 5.5_

  - [x] 8.2 Write property test for output comparison normalization
    - **Property 5: Output Comparison Normalization**
    - **Validates: Requirements 5.2, 5.5**

  - [x] 8.3 Write property test for conditional write
    - **Property 6: Conditional Write Correctness**
    - **Validates: Requirements 5.3, 5.4**

- [x] 9. Implement merge Lambda function
  - [x] 9.1 Create MergeFunction with Lambda Annotations
    - Implement HandleMerge method
    - Wire up dependency injection
    - Extract prefix from request
    - Load config, discover sources, merge, compare, write
    - Return MergeResponse with metrics
    - _Requirements: 1.4, 4.1, 4.2, 4.3, 5.3, 5.4, 11.1, 11.2_

  - [x] 9.2 Implement error handling
    - Handle config not found
    - Handle invalid config
    - Handle no valid sources
    - Handle merge conflicts
    - Handle S3 errors
    - _Requirements: 2.4, 2.5, 3.5, 3.6, 3.7, 4.4_

  - [x] 9.3 Implement CloudWatch metrics emission
    - Emit merge duration metric
    - Emit success/failure count
    - Emit files processed count
    - _Requirements: 10.1, 10.4_

  - [x] 9.4 Write unit tests for merge function
    - Test successful merge flow
    - Test error handling scenarios
    - Test conditional write logic
    - _Requirements: 4.1, 4.2, 5.3, 5.4_

- [x] 10. Checkpoint - Ensure Lambda function works
  - Ensure all tests pass, ask the user if questions arise.

- [x] 11. Create CDK construct project
  - [x] 11.1 Create Oproto.Lambda.OpenApi.Merge.Cdk project
    - Create project file with CDK dependencies
    - Add reference to Lambda project for asset bundling
    - _Requirements: 8.1_

  - [x] 11.2 Create OpenApiMergeConstructProps
    - Define all configurable properties
    - Set sensible defaults
    - _Requirements: 8.2, 8.3, 8.4, 8.6, 10.5, 10.7, 10.8, 10.9_

  - [x] 11.3 Implement OpenApiMergeConstruct
    - Create DynamoDB table for debounce state
    - Create Lambda function with proper IAM role
    - Create Step Functions state machine
    - Create EventBridge rules for S3 events
    - Configure S3 event notifications
    - Create CloudWatch alarms (if enabled)
    - Expose outputs (Lambda ARN, Step Function ARN)
    - _Requirements: 8.5, 8.6, 8.7, 6.1, 6.2, 6.4_

  - [x] 11.4 Write property test for output path construction
    - **Property 7: Output Path Construction**
    - **Validates: Requirements 6.3, 6.5**

- [x] 12. Create Step Functions state machine definition
  - [x] 12.1 Create state machine JSON with JSONata
    - Implement prefix extraction
    - Implement debounce logic with DynamoDB
    - Implement post-merge event checking
    - Handle merge Lambda invocation
    - Implement cleanup
    - _Requirements: 7.1, 7.2, 7.3, 7.4, 7.5, 7.6_

- [x] 13. Create CloudFormation template
  - [x] 13.1 Create standalone CloudFormation template
    - Define parameters for buckets and prefixes
    - Create all resources matching CDK construct
    - Add outputs for created resources
    - _Requirements: 8a.1, 8a.2, 8a.3, 8a.4_

- [x] 14. Checkpoint - Ensure CDK and CloudFormation work
  - Ensure all tests pass, ask the user if questions arise.

- [x] 15. Update documentation
  - [x] 15.1 Create docs/lambda-merge.md
    - Document deployment options (CDK vs CloudFormation)
    - Document config file format
    - Include example configs
    - Document debounce behavior
    - Document troubleshooting
    - _Requirements: 12.2, 12.3, 12.4, 12.5, 12.6_

  - [x] 15.2 Update docs/merge-tool.md
    - Document autoDiscover option
    - Document excludePatterns option
    - Add examples
    - _Requirements: 12.7_

  - [x] 15.3 Update CHANGELOG.md
    - Document new Lambda merge tool
    - Document autoDiscover and excludePatterns additions
    - Document any breaking changes
    - _Requirements: 12.1_

  - [x] 15.4 Update README.md
    - Add section for Lambda merge tool
    - Link to detailed documentation
    - _Requirements: 12.8_

- [x] 16. Final checkpoint - All tests pass and documentation complete
  - Ensure all tests pass, ask the user if questions arise.

## Notes

- All tasks are required for comprehensive implementation
- Each task references specific requirements for traceability
- Checkpoints ensure incremental validation
- Property tests validate universal correctness properties
- Unit tests validate specific examples and edge cases
- The implementation uses C# consistent with the existing codebase
- FsCheck is used for property-based testing (already in use in the project)
