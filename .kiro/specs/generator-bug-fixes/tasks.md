# Implementation Plan: Generator Bug Fixes

## Overview

This implementation addresses five bugs in the OpenAPI generator and merge tool. Each fix is isolated to specific components and can be implemented independently.

## Tasks

- [x] 1. Fix Path Parameter Generation
  - [x] 1.1 Add ExtractPathParametersFromTemplate method to OpenApiSpecGenerator
    - Implement regex-based extraction of `{paramName}` from route templates
    - Handle constraint syntax like `{id:int}` by stripping the constraint
    - Return list of parameter names found in template
    - _Requirements: 1.1, 1.2_

  - [x] 1.2 Add EnsurePathParametersDefined method to OpenApiSpecGenerator
    - Compare template parameters with existing parameter definitions
    - Add missing parameters with `in: path` and `required: true`
    - Use method parameter type if available, otherwise default to `string`
    - _Requirements: 1.1, 1.2, 1.3, 1.4_

  - [x] 1.3 Integrate path parameter generation into CreateOperation
    - Call EnsurePathParametersDefined after extracting method parameters
    - Pass route template and method parameters
    - _Requirements: 1.1_

  - [x] 1.4 Write property test for path parameter completeness
    - **Property 1: Path Parameter Completeness**
    - **Validates: Requirements 1.1, 1.4**

  - [x] 1.5 Write property test for path parameter type inference
    - **Property 2: Path Parameter Type Inference**
    - **Validates: Requirements 1.2, 1.3**

- [x] 2. Checkpoint - Ensure path parameter tests pass
  - Ensure all tests pass, ask the user if questions arise.

- [x] 3. Fix DateOnly/TimeOnly Type Handling
  - [x] 3.1 Add DateOnly/TimeOnly mappings to GetOpenApiType
    - Map `System.DateOnly` to `("string", "date")`
    - Map `System.TimeOnly` to `("string", "time")`
    - Handle nullable variants
    - _Requirements: 2.1, 2.2, 2.3_

  - [x] 3.2 Add DateOnly/TimeOnly to IsBuiltInType check
    - Ensure these types are not added to components/schemas
    - _Requirements: 2.4_

  - [x] 3.3 Write property test for DateOnly/TimeOnly schema mapping
    - **Property 3: DateOnly/TimeOnly Schema Mapping**
    - **Validates: Requirements 2.1, 2.2, 2.3, 2.4**

- [x] 4. Checkpoint - Ensure DateOnly/TimeOnly tests pass
  - Ensure all tests pass, ask the user if questions arise.

- [x] 5. Fix Class-Level OpenApiTag Support
  - [x] 5.1 Modify ExtractTagsFromMethod to check containing class
    - First check method-level tags
    - If no method tags, check class-level tags
    - Method-level tags take precedence
    - _Requirements: 3.1, 3.2, 3.3_

  - [x] 5.2 Write property test for class-level tag inheritance
    - **Property 4: Class-Level Tag Inheritance**
    - **Validates: Requirements 3.1, 3.3**

  - [x] 5.3 Write property test for method-level tag precedence
    - **Property 5: Method-Level Tag Precedence**
    - **Validates: Requirements 3.2**

- [x] 6. Checkpoint - Ensure tag inheritance tests pass
  - Ensure all tests pass, ask the user if questions arise.

- [x] 7. Fix AWS Type Exclusion
  - [x] 7.1 Add IsAwsLambdaType method to OpenApiSpecGenerator
    - Check namespace starts with `Amazon.Lambda.`
    - Check common type names (APIGatewayProxyRequest, etc.)
    - _Requirements: 4.2, 4.3, 4.4_

  - [x] 7.2 Modify parameter extraction to exclude AWS types
    - Skip AWS types without explicit `[FromBody]`
    - _Requirements: 4.1, 4.3_

  - [x] 7.3 Modify schema generation to exclude AWS types
    - Add IsAwsLambdaType check to ShouldGenerateSchema
    - _Requirements: 4.2_

  - [x] 7.4 Write property test for AWS type exclusion
    - **Property 6: AWS Type Exclusion**
    - **Validates: Requirements 4.2, 4.3**

  - [x] 7.5 Write property test for POST without body
    - **Property 7: POST Without Body**
    - **Validates: Requirements 4.1**

- [x] 8. Checkpoint - Ensure AWS type exclusion tests pass
  - Ensure all tests pass, ask the user if questions arise.

- [x] 9. Fix Tilde Path Expansion in Merge Tool
  - [x] 9.1 Create PathExpander utility class
    - Implement ExpandPath method for `~/` expansion
    - Handle `~username/` syntax for Unix
    - Throw clear error on expansion failure
    - _Requirements: 5.1, 5.2, 5.3, 5.4_

  - [x] 9.2 Integrate PathExpander into MergeCommand
    - Expand source file paths before loading
    - Expand output path before writing
    - Show expanded path in error messages
    - _Requirements: 5.1, 5.3_

  - [x] 9.3 Write unit tests for PathExpander
    - Test `~/` expansion to home directory
    - Test error handling for invalid paths
    - Test Windows vs Unix behavior
    - _Requirements: 5.1, 5.3, 5.4_

  - [x] 9.4 Write property test for tilde path expansion
    - **Property 8: Tilde Path Expansion**
    - **Validates: Requirements 5.1, 5.3**

- [x] 10. Checkpoint - Ensure tilde expansion tests pass
  - Ensure all tests pass, ask the user if questions arise.

- [x] 11. Update Documentation
  - [x] 11.1 Update docs/attributes.md with class-level tag example
    - Show `[OpenApiTag]` on class with inheritance behavior
    - _Requirements: 6.3_

  - [x] 11.2 Update docs/configuration.md with DateOnly/TimeOnly support
    - Add to type mapping table
    - _Requirements: 6.2_

  - [x] 11.3 Update docs/merge-tool.md with tilde path support
    - Document `~/` syntax in configuration paths
    - _Requirements: 6.1_

  - [x] 11.4 Update CHANGELOG.md with bug fixes
    - Document each fix with symptom and resolution
    - _Requirements: 6.4_

- [x] 12. Final checkpoint - Ensure all tests pass
  - Ensure all tests pass, ask the user if questions arise.

## Notes

- All tasks including tests are required for comprehensive coverage
- Each fix is independent and can be implemented in any order
- The source generator cannot reference the Merge project, so fixes are isolated
- Use `StringComparer.Ordinal` for consistent string comparisons
- Property tests should use FsCheck with minimum 100 iterations

