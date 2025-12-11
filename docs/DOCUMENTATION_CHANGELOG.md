# Documentation Changelog

All notable documentation changes for this project. Use this file to sync documentation updates to external documentation sets.

## [Unreleased]

### Changed

#### configuration.md
- Simplified AOT compilation section - `EmitCompilerGeneratedFiles` is no longer required
- Updated to reflect that OpenAPI extraction now works automatically with AOT builds using MetadataLoadContext
- Moved `EmitCompilerGeneratedFiles` documentation to optional debugging section

### Added

#### attributes.md
- Added `OperationId` property documentation to `OpenApiOperationAttribute`
- Added "Deprecation" subsection explaining both `[OpenApiOperation(Deprecated=true)]` and `[Obsolete]` approaches
- Added documentation for `OpenApiExampleAttribute` - request/response examples with JSON content
- Added documentation for `OpenApiResponseHeaderAttribute` - response header documentation
- Added documentation for `OpenApiServerAttribute` - server URL definitions
- Added documentation for `OpenApiTagDefinitionAttribute` - tag definitions with descriptions
- Added documentation for `OpenApiExternalDocsAttribute` - external documentation links
- Added documentation for `OpenApiOperationIdAttribute` - custom operation IDs
- Added "Deprecation Support" section explaining `[Obsolete]` attribute detection
- Reorganized Table of Contents into Assembly-Level, Method-Level, and Property/Parameter sections
- Added comprehensive usage examples for all new attributes

#### getting-started.md
- Added "Deprecation with [Obsolete]" section
- Added "Response Headers" section with `[OpenApiResponseHeader]` examples
- Added "Request and Response Examples" section with `[OpenApiExample]` examples
- Added "Operation IDs" section with `[OpenApiOperationId]` examples
- Updated assembly-level attributes list to include `[OpenApiServer]`, `[OpenApiTagDefinition]`, `[OpenApiExternalDocs]`
- Updated method-level attributes list to include `[OpenApiOperationId]`, `[OpenApiResponseHeader]`, `[OpenApiExample]`, `[OpenApiExternalDocs]`

### Changed

#### attributes.md
- Updated Table of Contents with all new attributes organized by target type

## Format

Each entry should include:
- **File**: Which documentation file was changed
- **Section**: Which section was added/modified/removed
- **Description**: Brief description of the change
