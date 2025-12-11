# Changelog

All notable changes to this project will be documented in this file.

The format is based on [Keep a Changelog](https://keepachangelog.com/en/1.1.0/),
and this project adheres to [Semantic Versioning](https://semver.org/spec/v2.0.0.html).

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
  - Build task now supports Native AOT builds via source file parsing
  - Dual extraction strategy: tries source file first, falls back to reflection
  - Enable with `EmitCompilerGeneratedFiles=true` in project file

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
