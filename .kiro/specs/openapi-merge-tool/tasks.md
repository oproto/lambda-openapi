# Implementation Plan: OpenAPI Merge Tool

## Overview

This implementation plan creates the OpenAPI merge tool in two packages: a core library (`Oproto.Lambda.OpenApi.Merge`) and a CLI tool (`Oproto.Lambda.OpenApi.Merge.Tool`). The implementation uses Microsoft.OpenApi for document handling and FsCheck for property-based testing.

## Tasks

- [x] 1. Set up project structure
  - [x] 1.1 Create Oproto.Lambda.OpenApi.Merge project
    - Create new class library project targeting netstandard2.0
    - Add Microsoft.OpenApi package reference
    - Add System.Text.Json package reference
    - Add to solution file
    - _Requirements: 11.1_

  - [x] 1.2 Create Oproto.Lambda.OpenApi.Merge.Tool project
    - Create new console application targeting net8.0
    - Add System.CommandLine package reference
    - Add project reference to Merge library
    - Configure as dotnet tool with command name `openapi-merge`
    - Add to solution file
    - _Requirements: 11.2, 11.3, 11.4_

  - [x] 1.3 Create Oproto.Lambda.OpenApi.Merge.Tests project
    - Create new xUnit test project
    - Add FsCheck and FsCheck.Xunit package references
    - Add project reference to Merge library
    - Add to solution file
    - _Requirements: 9.1, 9.2, 9.3_

- [x] 2. Implement configuration models
  - [x] 2.1 Create MergeConfiguration and related classes
    - Implement MergeConfiguration with Info, Servers, Sources, Output, SchemaConflict
    - Implement MergeInfoConfiguration with Title, Version, Description
    - Implement MergeServerConfiguration with Url, Description
    - Implement SourceConfiguration with Path, PathPrefix, OperationIdPrefix, Name
    - Implement SchemaConflictStrategy enum
    - Add JSON serialization attributes
    - _Requirements: 5.1, 5.2, 6.1, 6.2, 6.3_

  - [x] 2.2 Write unit tests for configuration deserialization
    - Test JSON deserialization of MergeConfiguration
    - Test default values
    - Test validation of required fields
    - _Requirements: 6.4_

- [x] 3. Implement merge result models
  - [x] 3.1 Create MergeResult and MergeWarning classes
    - Implement MergeResult with Document, Warnings, Success
    - Implement MergeWarning with Type, Message, SourceName
    - Implement MergeWarningType enum
    - _Requirements: 8.1, 8.2, 8.3_

- [x] 4. Implement schema deduplication
  - [x] 4.1 Create SchemaDeduplicator class
    - Implement schema tracking dictionary
    - Implement structural equality comparison for OpenApiSchema
    - Implement AddSchema method with conflict detection
    - Implement rename logic with source name prefix
    - Implement GetRenames method for reference tracking
    - Implement GetSchemas method
    - _Requirements: 4.1, 4.2, 4.3, 4.4_

  - [x] 4.2 Write property test for schema structural equality
    - **Property 12: Schema Structural Deduplication**
    - Generate random identical schemas, verify single output
    - **Validates: Requirements 4.1**

  - [x] 4.3 Write property test for schema rename on conflict
    - **Property 13: Schema Rename on Conflict**
    - Generate conflicting schemas, verify rename with prefix
    - **Validates: Requirements 4.2**

- [x] 5. Implement path merging
  - [x] 5.1 Create PathMerger class
    - Implement path collection with conflict detection
    - Implement prefix application with leading slash normalization
    - Implement operationId prefix application
    - Implement $ref reference rewriting
    - _Requirements: 2.1, 2.2, 2.3, 2.4, 3.1, 3.2, 3.3, 4.5_

  - [x] 5.2 Write property test for path prefix application
    - **Property 6: Path Prefix Application**
    - Generate paths and prefixes, verify all output paths start with prefix
    - **Validates: Requirements 2.1, 2.2**

  - [x] 5.3 Write property test for reference rewriting
    - **Property 16: Reference Rewriting**
    - Generate documents with schema refs, rename schemas, verify refs updated
    - **Validates: Requirements 4.5**

- [x] 6. Implement core merger
  - [x] 6.1 Create OpenApiMerger class
    - Implement Merge method orchestrating SchemaDeduplicator and PathMerger
    - Implement info block configuration
    - Implement server configuration
    - Implement tag merging
    - Implement security scheme merging
    - _Requirements: 1.1, 1.2, 1.3, 1.4, 1.5, 1.6_

  - [x] 6.2 Write property test for path preservation
    - **Property 1: Path Preservation**
    - Generate multiple documents, verify all paths in output
    - **Validates: Requirements 1.1, 1.2**

  - [x] 6.3 Write property test for schema preservation
    - **Property 2: Schema Preservation**
    - Generate documents with unique schemas, verify all in output
    - **Validates: Requirements 1.3**

- [x] 7. Checkpoint - Core library complete
  - Ensure all tests pass, ask the user if questions arise.

- [x] 8. Implement CLI tool
  - [x] 8.1 Create MergeCommand with System.CommandLine
    - Implement --config option for config file
    - Implement -o/--output option
    - Implement --title and --version options
    - Implement --schema-conflict option
    - Implement -v/--verbose option
    - Implement files positional argument
    - _Requirements: 6.1, 7.1, 7.2, 7.3, 7.4_

  - [x] 8.2 Implement config file loading
    - Load and deserialize JSON config file
    - Validate required fields
    - Handle file not found errors
    - _Requirements: 6.1, 6.2, 6.3, 6.4_

  - [x] 8.3 Implement direct invocation mode
    - Build MergeConfiguration from CLI arguments
    - Load source files from positional arguments
    - _Requirements: 7.1, 7.2, 7.3_

  - [x] 8.4 Implement output and error handling
    - Write merged spec to output file
    - Write warnings to stderr
    - Implement verbose progress output
    - Return appropriate exit codes
    - _Requirements: 8.4, 8.5, 9.1, 9.2, 9.3_

  - [x] 8.5 Create Program.cs entry point
    - Set up root command
    - Add merge command
    - Configure async invocation
    - _Requirements: 11.3, 11.4_

- [x] 9. Checkpoint - CLI tool complete
  - Ensure all tests pass, ask the user if questions arise.

- [x] 10. Create test fixtures and integration tests
  - [x] 10.1 Create test data files
    - Create valid OpenAPI test files (simple, with-schemas, with-security, with-tags)
    - Create conflict test files (duplicate paths, identical schemas, different schemas)
    - Create config test files (basic, with-prefixes, invalid)
    - Create invalid test files (not-json, not-openapi)
    - _Requirements: 9.1, 9.2, 9.3_

  - [x] 10.2 Write integration tests for config-based merge
    - Test loading config and merging sources
    - Test output file creation
    - _Requirements: 6.1, 6.2, 6.3_

  - [x] 10.3 Write integration tests for error scenarios
    - Test missing source file
    - Test invalid JSON
    - Test invalid OpenAPI
    - Test missing config fields
    - _Requirements: 6.4, 9.1, 9.2, 9.3_

- [x] 11. Update documentation
  - [x] 11.1 Update README.md
    - Add Merge Tool section with overview
    - Add installation instructions
    - Add quick start example
    - Add link to detailed docs
    - _Requirements: 10.1_

  - [x] 11.2 Create docs/merge-tool.md
    - Document installation (global and local tool)
    - Document CLI usage with examples
    - Document configuration file format
    - Document schema conflict strategies
    - Document error handling and exit codes
    - _Requirements: 10.2_

  - [x] 11.3 Update CHANGELOG.md
    - Add new version section
    - Document Merge Tool feature
    - List new packages
    - _Requirements: 10.3_

- [ ] 12. Final checkpoint
  - Ensure all tests pass, ask the user if questions arise.

## Notes

- All tasks are required for comprehensive implementation
- Each task references specific requirements for traceability
- Checkpoints ensure incremental validation
- Property tests validate universal correctness properties
- Unit tests validate specific examples and edge cases
