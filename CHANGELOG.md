# Changelog

All notable changes to this project will be documented in this file.

The format is based on [Keep a Changelog](https://keepachangelog.com/en/1.1.0/),
and this project adheres to [Semantic Versioning](https://semver.org/spec/v2.0.0.html).

## [Unreleased]

### Added

- **Lambda Merge Tool**
  - New `Oproto.Lambda.OpenApi.Merge.Lambda` package for AWS Lambda-based OpenAPI merging
  - Automatic merging triggered by S3 events (object created, modified, or deleted)
  - Step Functions-based debouncing to batch rapid successive changes
  - Configurable debounce wait duration (default: 5 seconds)
  - Post-merge event checking to ensure no changes are missed during merge execution
  - Conditional writes - only updates output when merged result differs from existing
  - CloudWatch metrics: merge duration, success/failure counts, files processed
  - Comprehensive error handling with detailed logging

- **CDK Construct for Lambda Merge**
  - New `Oproto.Lambda.OpenApi.Merge.Cdk` package with reusable CDK construct
  - `OpenApiMergeConstruct` creates all required AWS resources
  - Configurable CloudWatch alarms for merge failures
  - Support for single-bucket or dual-bucket configurations
  - Multi-API prefix support with single deployment
  - Standalone CloudFormation template for non-CDK users

- **Auto-Discovery Mode (Merge Tool)**
  - New `autoDiscover` configuration option for automatic source file discovery
  - When enabled, finds all `.json` files in the directory (excluding config and output)
  - New `excludePatterns` option for glob-based file exclusion
  - Supported in both CLI merge tool and Lambda merge tool
  - Automatically excludes the output file to prevent circular merges

- **Deterministic Output**
  - OpenAPI output is now fully deterministic across multiple runs with identical input
  - Paths sorted alphabetically by path string
  - Schemas sorted alphabetically by schema name
  - Properties within schemas sorted alphabetically by property name
  - Tags sorted alphabetically by tag name
  - Tag groups sorted alphabetically by group name, with tags within groups also sorted
  - Security schemes sorted alphabetically by scheme name
  - Operations within paths sorted by HTTP method order (GET, PUT, POST, DELETE, OPTIONS, HEAD, PATCH, TRACE)
  - Responses sorted by status code in ascending order
  - Examples sorted alphabetically by example name
  - Server order preserved as declared in source code or configuration

- **Skip Unchanged Output (Merge Tool)**
  - Merge tool now skips writing output files when content matches existing file
  - New `--force` (`-f`) flag to override skip behavior and always write output
  - Verbose mode logs when files are skipped due to unchanged content

- **Tilde Path Expansion (Merge Tool)**
  - Source file paths and output paths now support `~/` expansion to user's home directory
  - Unix/macOS also supports `~username/` syntax for other users' home directories
  - Clear error messages when tilde expansion fails

- **DateOnly and TimeOnly Type Support**
  - `DateOnly` properties/parameters now generate `type: string, format: date` schemas
  - `TimeOnly` properties/parameters now generate `type: string, format: time` schemas
  - Nullable variants (`DateOnly?`, `TimeOnly?`) are fully supported
  - These types are treated as built-in types and do not generate separate schema definitions

- **Class-Level OpenApiTag Support**
  - `[OpenApiTag]` attribute can now be applied at the class level
  - All methods in a class inherit class-level tags
  - Method-level tags take precedence over class-level tags when both are present
  - Multiple class-level tags are supported

### Changed

- `OpenApiMerger.Merge()` now returns sorted documents for deterministic output
- Source generator now sorts all collections before serialization

### Fixed

- **Path Parameter Generation**
  - Path parameters in route templates (e.g., `{companyId}`) are now automatically defined in the OpenAPI specification
  - Previously, path parameters without corresponding `[FromRoute]` method parameters were missing from the spec
  - Path parameters now correctly set `in: path` and `required: true` as per OpenAPI specification
  - Type inference uses method parameter type when available, defaults to `string` otherwise

- **AWS Lambda Type Exclusion**
  - AWS Lambda infrastructure types (`APIGatewayProxyRequest`, `APIGatewayHttpApiV2ProxyRequest`, etc.) are now excluded from the OpenAPI specification
  - POST methods without `[FromBody]` parameters no longer generate a requestBody
  - Types from `Amazon.Lambda.*` namespaces are automatically filtered from parameters and schemas

## [1.2.0] - 2025-12-22

### Added

- **Tag Groups Extension (`x-tagGroups`)**
  - New `OpenApiTagGroupAttribute` for organizing related tags into logical groups at assembly level
  - Generates `x-tagGroups` extension at the root level of OpenAPI documents
  - Supports hierarchical navigation in documentation tools like Redoc
  - Multiple tag groups can be defined with `AllowMultiple = true`
  - Tag groups preserve definition order in the output
  - Tag groups can reference tags that don't exist in the API (forward references)

- **Tag Group Merging**
  - Merge tool now combines `x-tagGroups` extensions from multiple OpenAPI specifications
  - Same-named tag groups are merged with tags deduplicated
  - Tag group order is preserved (first occurrence wins)
  - Tag groups from specs that have them are preserved even when other specs don't define tag groups

- **Automatic Example Generation**
  - New `OpenApiExampleConfigAttribute` for configuring automatic example generation at assembly level
  - Automatic composition of request/response examples from property-level `[OpenApiSchema(Example = "...")]` values
  - Type-aware example conversion: numeric types become JSON numbers, booleans become JSON booleans
  - Recursive example composition for nested object types
  - Array example generation with single element examples
  - Explicit `[OpenApiExample]` attributes take precedence over composed examples

- **Default Example Generation**
  - Format-based defaults for string properties (email, uuid, date-time, date, uri, hostname, ipv4, ipv6)
  - Constraint-based defaults for numeric properties respecting `Minimum`/`Maximum` bounds
  - Constraint-based defaults for string properties respecting `MinLength`/`MaxLength` bounds
  - Type-appropriate placeholder values for properties without constraints
  - Controlled via `GenerateDefaults` property on `OpenApiExampleConfigAttribute`

- **Configuration Options**
  - `ComposeFromProperties` (default: true) - Enable/disable automatic example composition
  - `GenerateDefaults` (default: false) - Enable/disable default example generation
  - Options operate independently for fine-grained control

### Changed

- Example generation is now enabled by default with `ComposeFromProperties = true`
- Properties with `[OpenApiIgnore]` are excluded from composed examples

## [1.1.0] - 2025-12-20

### Added

- **OpenAPI Merge Tool**
  - New `Oproto.Lambda.OpenApi.Merge` library for programmatic OpenAPI specification merging
  - New `Oproto.Lambda.OpenApi.Merge.Tool` CLI tool for command-line merging
  - Merge multiple OpenAPI specifications into a single unified document
  - Path prefix support for namespacing APIs from different services
  - Operation ID prefix support for ensuring unique operation IDs
  - Schema conflict resolution strategies: `rename`, `first-wins`, `fail`
  - Automatic schema deduplication for identical schemas
  - Automatic `$ref` reference rewriting when schemas are renamed
  - Configuration file support for complex merge scenarios
  - Detailed warnings for path conflicts, schema conflicts, and operation ID conflicts
  - Verbose output mode for debugging merge operations

- **New Packages**
  - `Oproto.Lambda.OpenApi.Merge` - Core merge library (netstandard2.0)
  - `Oproto.Lambda.OpenApi.Merge.Tool` - .NET CLI tool (net8.0)

- **Documentation**
  - New `docs/merge-tool.md` with comprehensive merge tool documentation
  - Updated README.md with merge tool overview and quick start

## [1.0.0] - 2025-12-10

### Added

- **Operation Examples**
  - New `OpenApiExampleAttribute` for providing JSON request and response examples
  - Support for multiple examples per operation
  - Examples can be assigned to specific status codes or marked as request examples
  - XML `<example>` tag parsing from documentation comments

- **Deprecation Support**
  - Automatic detection of `[Obsolete]` attribute on methods
  - Sets `deprecated: true` on operations
  - Includes obsolete message in operation description

- **Response Headers**
  - New `OpenApiResponseHeaderAttribute` for documenting response headers
  - Support for multiple headers per operation
  - Headers can be assigned to specific status codes
  - Configurable type, description, and required flag

- **Server Definitions**
  - New `OpenApiServerAttribute` for defining API server URLs at assembly level
  - Support for multiple servers (production, staging, development)
  - Optional description for each server

- **Tag Definitions**
  - New `OpenApiTagDefinitionAttribute` for defining tags with descriptions at assembly level
  - Support for external documentation links per tag
  - Tags appear in the specification's tags array with full metadata

- **External Documentation**
  - New `OpenApiExternalDocsAttribute` for linking to external documentation
  - Can be applied at assembly level (API-wide) or method level (operation-specific)
  - Includes URL and optional description

- **Operation IDs**
  - New `OpenApiOperationIdAttribute` for custom operation IDs
  - Auto-generation of operation IDs from method names
  - Automatic uniqueness enforcement with numeric suffixes

- **Security Scheme Attributes**
  - New `OpenApiSecuritySchemeAttribute` for defining security schemes at assembly level
  - Support for API Key, OAuth2, HTTP (Bearer/Basic), and OpenID Connect schemes
  - Security schemes are only added when attributes are present (no more hardcoded defaults)

- **Response Type Attributes**
  - New `OpenApiResponseTypeAttribute` for explicitly documenting response types
  - Supports multiple status codes with different response types
  - Essential for methods returning `IHttpResult` where the actual response type is not inferrable
  - Automatic detection and handling of `IHttpResult` return types (omits schema when no attributes present)

- **API Info Attribute**
  - New `OpenApiInfoAttribute` for setting API title, version, and metadata at assembly level
  - Supports description, terms of service, contact info, and license info
  - Replaces the class-level `GenerateOpenApiSpecAttribute`

- **AOT Compilation Support**
  - Build task fully supports Native AOT builds without special configuration
  - Uses `MetadataLoadContext` for dependency-free assembly metadata reading
  - No need to enable `EmitCompilerGeneratedFiles` - works automatically

- **HTTP Method Support**
  - Added support for PATCH, HEAD, and OPTIONS HTTP methods

- **Examples Project** (`Oproto.Lambda.OpenApi.Examples`)
  - Complete CRUD API example demonstrating all library features
  - `ProductFunctions` class with GET, POST, PUT, DELETE operations
  - Model classes (`Product`, `CreateProductRequest`, `UpdateProductRequest`) showcasing `OpenApiSchema` and `OpenApiIgnore` attributes
  - Generated `openapi.json` demonstrating output
  - Demonstrates all new attributes: `[OpenApiServer]`, `[OpenApiTagDefinition]`, `[OpenApiExternalDocs]`, `[OpenApiTag]`, `[OpenApiOperationId]`, `[OpenApiExample]`, `[OpenApiResponseHeader]`, `[Obsolete]`

- **Documentation**
  - `docs/attributes.md` - Complete attribute reference for all attributes
  - `docs/configuration.md` - MSBuild configuration options, AOT support, and troubleshooting guide
  - Updated `docs/getting-started.md` with new features overview

### Changed

- Solution now includes the examples project for reference and validation
- Security schemes are now attribute-driven instead of hardcoded
- Error responses (4xx, 5xx) no longer include a hardcoded schema - users can define their own error types using `[OpenApiResponseType]`
- `Task<T>` and `ValueTask<T>` return types are now properly unwrapped in response schemas
- Non-generic `Task`/`ValueTask` returns now generate 204 No Content responses

### Fixed

- Fixed `[OpenApiOperation]` attribute not being applied - Summary, Description, and Deprecated properties now work correctly
- Added `OperationId` property to `[OpenApiOperation]` attribute as an alternative to `[OpenApiOperationId]`
- Fixed `Task<T>` return types appearing in OpenAPI response schemas instead of inner type `T`
- Fixed package version alignment - removed hardcoded version from SourceGenerator.csproj

### Removed

- Removed `GenerateOpenApiSpecAttribute` - replaced by assembly-level `OpenApiInfoAttribute`
- Removed hardcoded OAuth2 and API Key security scheme definitions
- Removed unused `ResponseTypeInfo` and `ExampleInfo` classes
- Removed unused `GetLambdaClassInfo` and `GetApiInfo` methods
- Removed debug `Console.WriteLine` statements from production code

### Security

- Updated `System.Text.Json` to 8.0.5 to address GHSA-8g4q-xg66-9fp4 and GHSA-hh2w-p6rv-4g7w
