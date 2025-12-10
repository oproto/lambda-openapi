# Documentation Changelog

All notable documentation changes for this project. Use this file to sync documentation updates to external documentation sets.

## [Unreleased]

### Added

#### attributes.md
- Added documentation for new `OpenApiInfoAttribute` assembly-level attribute for API metadata
- Added documentation for new `OpenApiSecuritySchemeAttribute` assembly-level attribute
- Added documentation for `OpenApiSecuritySchemeType` enum (ApiKey, Http, OAuth2, OpenIdConnect)
- Added documentation for `ApiKeyLocation` enum (Query, Header, Cookie)
- Added examples for API Key and OAuth2 security scheme configuration
- Added documentation for new `OpenApiResponseTypeAttribute` method-level attribute
- Added examples for documenting response types when using IHttpResult
- Removed documentation for `GenerateOpenApiSpecAttribute` (replaced by `OpenApiInfoAttribute`)

#### getting-started.md
- Added "FromServices Parameter Exclusion" section explaining that parameters with `[FromServices]` attribute are automatically excluded from OpenAPI parameters
- Added "API Gateway Integration" section documenting the `x-amazon-apigateway-integration` extension automatically added to all operations
- Added "AOT Compatibility" section explaining how to use the library with Native AOT compilation
- Added "Security Schemes" section with examples for configuring API Key and OAuth2 authentication
- Added "IHttpResult Return Types" section explaining OpenApiResponseType attribute usage
- Updated attribute list to include new assembly-level and method-level attributes
- Removed reference to GenerateOpenApiSpecAttribute

#### configuration.md
- Added "AOT Configuration" section documenting the `EmitCompilerGeneratedFiles` MSBuild property requirement for AOT scenarios
- Added explanation of the dual extraction strategy (source file parsing vs reflection fallback)

### Removed

#### attributes.md
- Removed `GenerateOpenApiSpecAttribute` documentation (replaced by `OpenApiInfoAttribute`)

## Format

Each entry should include:
- **File**: Which documentation file was changed
- **Section**: Which section was added/modified/removed
- **Description**: Brief description of the change
