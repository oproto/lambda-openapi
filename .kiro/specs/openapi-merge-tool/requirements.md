# Requirements Document

## Introduction

This feature adds an OpenAPI merge tool to the Oproto.Lambda.OpenApi ecosystem. The tool enables merging multiple OpenAPI specification files from different microservices into a single unified specification. This supports organizations with distributed API architectures where each service generates its own OpenAPI spec, but a combined specification is needed for documentation portals, client SDK generation, or API gateway configuration.

The feature consists of two packages: a core merge library (`Oproto.Lambda.OpenApi.Merge`) and a .NET CLI tool (`Oproto.Lambda.OpenApi.Merge.Tool`).

## Glossary

- **Merge_Tool**: The .NET CLI tool that orchestrates OpenAPI specification merging
- **Merge_Library**: The core library containing merge logic, usable programmatically
- **Source_Spec**: An individual OpenAPI specification file to be merged
- **Merged_Spec**: The resulting combined OpenAPI specification
- **Path_Prefix**: An optional string prepended to all paths from a source specification
- **Schema_Conflict**: When two source specifications define schemas with the same name but different structures
- **OperationId_Conflict**: When two source specifications define operations with the same operationId

## Requirements

### Requirement 1: Core Merge Functionality

**User Story:** As a platform engineer, I want to merge multiple OpenAPI specifications into one, so that I can provide unified API documentation across microservices.

#### Acceptance Criteria

1. WHEN the Merge_Library receives multiple valid OpenAPI specifications THEN it SHALL produce a single valid OpenAPI specification containing all paths from all sources
2. WHEN merging specifications THEN the Merge_Library SHALL combine all unique paths from all source specifications
3. WHEN merging specifications THEN the Merge_Library SHALL combine all unique schemas from all source specifications into the components/schemas section
4. WHEN merging specifications THEN the Merge_Library SHALL combine all unique security schemes from all source specifications
5. WHEN merging specifications THEN the Merge_Library SHALL combine all unique tags from all source specifications
6. WHEN a source specification contains servers THEN the Merge_Library SHALL ignore source servers and use only the configured output servers

### Requirement 2: Path Prefix Support

**User Story:** As a platform engineer, I want to add path prefixes when merging, so that I can namespace APIs from different services.

#### Acceptance Criteria

1. WHEN a source configuration specifies a pathPrefix THEN the Merge_Library SHALL prepend that prefix to all paths from that source
2. WHEN a pathPrefix is specified THEN the Merge_Library SHALL ensure the prefix starts with a forward slash
3. WHEN no pathPrefix is specified THEN the Merge_Library SHALL use paths as-is from the source specification
4. WHEN the same path exists in multiple sources after prefix application THEN the Merge_Library SHALL report a warning and skip the duplicate

### Requirement 3: OperationId Handling

**User Story:** As a platform engineer, I want to ensure unique operation IDs across merged specs, so that client SDK generation works correctly.

#### Acceptance Criteria

1. WHEN a source configuration specifies an operationIdPrefix THEN the Merge_Library SHALL prepend that prefix to all operationIds from that source
2. WHEN no operationIdPrefix is specified THEN the Merge_Library SHALL use operationIds as-is from the source specification
3. WHEN duplicate operationIds exist after merging THEN the Merge_Library SHALL report a warning identifying the conflict

### Requirement 4: Schema Conflict Resolution

**User Story:** As a platform engineer, I want control over how schema naming conflicts are resolved, so that I can handle shared types appropriately.

#### Acceptance Criteria

1. WHEN two sources define schemas with the same name and identical structure THEN the Merge_Library SHALL merge them as a single schema
2. WHEN two sources define schemas with the same name but different structures and the conflict strategy is "rename" THEN the Merge_Library SHALL rename the conflicting schema using the source name as prefix
3. WHEN two sources define schemas with the same name but different structures and the conflict strategy is "first-wins" THEN the Merge_Library SHALL keep the first schema and report a warning
4. WHEN two sources define schemas with the same name but different structures and the conflict strategy is "fail" THEN the Merge_Library SHALL throw an error
5. WHEN a schema is renamed due to conflict THEN the Merge_Library SHALL update all $ref references to that schema throughout the merged specification

### Requirement 5: Merged Specification Metadata

**User Story:** As a platform engineer, I want to configure the merged specification's metadata, so that it accurately represents the combined API.

#### Acceptance Criteria

1. WHEN a merge configuration specifies info properties THEN the Merged_Spec SHALL use those values for title, version, and description
2. WHEN a merge configuration specifies servers THEN the Merged_Spec SHALL include those server definitions
3. WHEN a merge configuration does not specify info properties THEN the Merge_Tool SHALL require them or fail with a clear error message

### Requirement 6: CLI Tool Configuration

**User Story:** As a developer, I want to configure merging via a JSON file, so that I can version control my merge settings.

#### Acceptance Criteria

1. WHEN the Merge_Tool is invoked with --config THEN it SHALL read merge settings from the specified JSON file
2. WHEN the configuration file specifies sources THEN the Merge_Tool SHALL load and merge those OpenAPI files
3. WHEN the configuration file specifies an output path THEN the Merge_Tool SHALL write the merged specification to that location
4. IF the configuration file is invalid or missing required fields THEN the Merge_Tool SHALL exit with a non-zero code and descriptive error message

### Requirement 7: CLI Tool Direct Invocation

**User Story:** As a developer, I want to merge specs directly from the command line, so that I can quickly merge without a config file.

#### Acceptance Criteria

1. WHEN the Merge_Tool is invoked with file paths as arguments THEN it SHALL merge those files
2. WHEN the Merge_Tool is invoked with --output THEN it SHALL write the merged specification to that path
3. WHEN the Merge_Tool is invoked with --title and --version THEN it SHALL use those values for the merged specification info
4. WHEN the Merge_Tool is invoked without required arguments THEN it SHALL display usage help and exit with a non-zero code

### Requirement 8: Merge Warnings and Diagnostics

**User Story:** As a developer, I want clear feedback about merge issues, so that I can identify and resolve problems.

#### Acceptance Criteria

1. WHEN the Merge_Library encounters a path conflict THEN it SHALL include a warning in the merge result
2. WHEN the Merge_Library encounters a schema conflict THEN it SHALL include a warning in the merge result describing the conflict and resolution
3. WHEN the Merge_Library encounters an operationId conflict THEN it SHALL include a warning in the merge result
4. WHEN the Merge_Tool completes with warnings THEN it SHALL output those warnings to stderr
5. WHEN the --verbose flag is specified THEN the Merge_Tool SHALL output detailed progress information

### Requirement 9: Input Validation

**User Story:** As a developer, I want clear errors when input files are invalid, so that I can fix issues quickly.

#### Acceptance Criteria

1. IF a source file does not exist THEN the Merge_Tool SHALL exit with a non-zero code and identify the missing file
2. IF a source file is not valid JSON THEN the Merge_Tool SHALL exit with a non-zero code and identify the parse error
3. IF a source file is not a valid OpenAPI specification THEN the Merge_Tool SHALL exit with a non-zero code and describe the validation error

### Requirement 10: Documentation

**User Story:** As a library user, I want comprehensive documentation, so that I can learn how to use the merge tool effectively.

#### Acceptance Criteria

1. WHEN the merge tool is released THEN the README.md SHALL include a section describing the merge tool with installation and usage examples
2. WHEN the merge tool is released THEN a new docs/merge-tool.md SHALL provide detailed documentation including configuration reference
3. WHEN the merge tool is released THEN the CHANGELOG.md SHALL document the new feature

### Requirement 11: Package Distribution

**User Story:** As a developer, I want to install the merge tool easily, so that I can start using it quickly.

#### Acceptance Criteria

1. THE Merge_Library SHALL be published as NuGet package Oproto.Lambda.OpenApi.Merge
2. THE Merge_Tool SHALL be published as NuGet tool package Oproto.Lambda.OpenApi.Merge.Tool
3. WHEN installed as a global tool THEN the Merge_Tool SHALL be invocable as `dotnet openapi-merge`
4. WHEN installed as a local tool THEN the Merge_Tool SHALL be invocable as `dotnet tool run openapi-merge`
