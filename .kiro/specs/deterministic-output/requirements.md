# Requirements Document

## Introduction

This feature ensures that OpenAPI specification outputs are deterministic - meaning that if no changes are made to the source APIs, the generated OpenAPI JSON files will always be identical. This is critical for CI/CD pipelines, version control, and avoiding unnecessary file changes. Additionally, this feature adds the ability to skip writing output files when they match existing files, with an optional override flag.

## Glossary

- **Source_Generator**: The Roslyn-based source generator (`OpenApiSpecGenerator`) that produces OpenAPI specifications from Lambda function attributes at compile time.
- **Merger**: The `OpenApiMerger` component that combines multiple OpenAPI documents into a single unified specification.
- **Deterministic_Output**: Output that is identical across multiple runs when given the same input, regardless of execution order or timing.
- **Schema_Ordering**: The order in which schemas appear in the `components/schemas` section of an OpenAPI document.
- **Path_Ordering**: The order in which paths appear in the `paths` section of an OpenAPI document.
- **Tag_Ordering**: The order in which tags appear in the `tags` section of an OpenAPI document.
- **Property_Ordering**: The order in which properties appear within a schema definition.
- **Skip_Unchanged**: A feature that prevents writing output files when the new content matches the existing file content.

## Requirements

### Requirement 1: Deterministic Path Ordering

**User Story:** As a developer, I want paths in the generated OpenAPI specification to always appear in the same order, so that version control diffs only show actual changes.

#### Acceptance Criteria

1. WHEN the Source_Generator processes Lambda functions, THE Source_Generator SHALL output paths in alphabetical order by path string.
2. WHEN the Merger combines multiple OpenAPI documents, THE Merger SHALL output paths in alphabetical order by path string.
3. FOR ALL valid OpenAPI documents, generating then re-generating without changes SHALL produce identical path ordering.

### Requirement 2: Deterministic Schema Ordering

**User Story:** As a developer, I want schemas in the generated OpenAPI specification to always appear in the same order, so that version control diffs only show actual changes.

#### Acceptance Criteria

1. WHEN the Source_Generator processes types, THE Source_Generator SHALL output schemas in alphabetical order by schema name.
2. WHEN the Merger combines schemas from multiple sources, THE Merger SHALL output schemas in alphabetical order by schema name.
3. FOR ALL valid OpenAPI documents with schemas, generating then re-generating without changes SHALL produce identical schema ordering.

### Requirement 3: Deterministic Property Ordering Within Schemas

**User Story:** As a developer, I want properties within schemas to always appear in the same order, so that version control diffs only show actual changes.

#### Acceptance Criteria

1. WHEN the Source_Generator generates schema properties, THE Source_Generator SHALL output properties in alphabetical order by property name.
2. WHEN the Merger clones schemas, THE Merger SHALL preserve or establish alphabetical ordering of properties.
3. FOR ALL valid schemas with properties, generating then re-generating without changes SHALL produce identical property ordering.

### Requirement 4: Deterministic Tag Ordering

**User Story:** As a developer, I want tags in the generated OpenAPI specification to always appear in the same order, so that version control diffs only show actual changes.

#### Acceptance Criteria

1. WHEN the Source_Generator collects tags from operations, THE Source_Generator SHALL output tags in alphabetical order by tag name.
2. WHEN the Merger combines tags from multiple sources, THE Merger SHALL output tags in alphabetical order by tag name.
3. FOR ALL valid OpenAPI documents with tags, generating then re-generating without changes SHALL produce identical tag ordering.

### Requirement 5: Deterministic Tag Group Ordering

**User Story:** As a developer, I want tag groups in the x-tagGroups extension to always appear in the same order, so that version control diffs only show actual changes.

#### Acceptance Criteria

1. WHEN the Source_Generator processes OpenApiTagGroup attributes, THE Source_Generator SHALL output tag groups in alphabetical order by group name.
2. WHEN the Merger combines tag groups from multiple sources, THE Merger SHALL output tag groups in alphabetical order by group name.
3. WHEN tag groups contain tags, THE Merger SHALL output tags within each group in alphabetical order.

### Requirement 6: Deterministic Server Ordering

**User Story:** As a developer, I want servers in the generated OpenAPI specification to always appear in the same order, so that version control diffs only show actual changes.

#### Acceptance Criteria

1. WHEN the Source_Generator processes OpenApiServer attributes, THE Source_Generator SHALL output servers in the order they are declared in source code.
2. WHEN the Merger combines servers from configuration, THE Merger SHALL output servers in the order they appear in the configuration.

### Requirement 7: Deterministic Security Scheme Ordering

**User Story:** As a developer, I want security schemes in the generated OpenAPI specification to always appear in the same order, so that version control diffs only show actual changes.

#### Acceptance Criteria

1. WHEN the Source_Generator processes security schemes, THE Source_Generator SHALL output security schemes in alphabetical order by scheme name.
2. WHEN the Merger combines security schemes from multiple sources, THE Merger SHALL output security schemes in alphabetical order by scheme name.

### Requirement 8: Deterministic Operation Ordering Within Paths

**User Story:** As a developer, I want operations within each path to always appear in the same order, so that version control diffs only show actual changes.

#### Acceptance Criteria

1. WHEN the Source_Generator generates operations for a path, THE Source_Generator SHALL output operations in a consistent order (GET, PUT, POST, DELETE, OPTIONS, HEAD, PATCH, TRACE).
2. WHEN the Merger combines operations for a path, THE Merger SHALL output operations in the same consistent order.

### Requirement 9: Deterministic Response Ordering

**User Story:** As a developer, I want responses within operations to always appear in the same order, so that version control diffs only show actual changes.

#### Acceptance Criteria

1. WHEN the Source_Generator generates responses for an operation, THE Source_Generator SHALL output responses in ascending order by status code.
2. WHEN the Merger clones responses, THE Merger SHALL preserve or establish ascending order by status code.

### Requirement 10: Skip Unchanged Output Files

**User Story:** As a developer, I want the build process to skip writing output files when they haven't changed, so that I don't get unnecessary file modifications in version control.

#### Acceptance Criteria

1. WHEN the Merge_Tool generates output and an existing file matches the new content, THE Merge_Tool SHALL skip writing the file by default.
2. WHEN the Merge_Tool skips writing a file, THE Merge_Tool SHALL log a message indicating the file was unchanged.
3. WHEN the --force flag is provided, THE Merge_Tool SHALL write the output file regardless of whether it matches existing content.
4. IF the output file does not exist, THEN THE Merge_Tool SHALL write the file regardless of any flags.

### Requirement 11: Deterministic Example Ordering

**User Story:** As a developer, I want examples in the generated OpenAPI specification to always appear in the same order, so that version control diffs only show actual changes.

#### Acceptance Criteria

1. WHEN the Source_Generator generates examples for a media type, THE Source_Generator SHALL output examples in alphabetical order by example name.
2. WHEN the Merger clones examples, THE Merger SHALL preserve or establish alphabetical ordering of examples.
