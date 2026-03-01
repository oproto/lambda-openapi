# Requirements Document

## Introduction

This specification addresses several bugs and missing features discovered during OpenAPI Generator usage. The issues include missing path parameter definitions in generated specs, DateOnly/TimeOnly type handling conflicts, class-level tag attribute support, POST requests incorrectly including AWS payload objects, and tilde path expansion in merge tool configurations.

## Glossary

- **Source_Generator**: The Roslyn-based source generator (`OpenApiSpecGenerator`) that produces OpenAPI specifications from Lambda function attributes at compile time.
- **Merge_Tool**: The CLI tool (`Oproto.Lambda.OpenApi.Merge.Tool`) that combines multiple OpenAPI documents.
- **Path_Parameter**: A parameter defined in the URL path template (e.g., `{companyId}` in `/companies/{companyId}/locations`).
- **OpenAPI_Validator**: External tools (like OpenAPI Generator) that validate OpenAPI specifications for correctness.
- **AWS_Payload_Object**: Lambda-specific request/response types like `APIGatewayProxyRequest` that should not appear in public API documentation.

## Requirements

### Requirement 1: Path Parameter Definition Generation

**User Story:** As an API developer, I want path parameters extracted from route templates to be properly defined in the OpenAPI specification, so that OpenAPI validators and code generators work correctly.

#### Acceptance Criteria

1. WHEN a route template contains path parameters (e.g., `/companies/{companyId}/locations/{locationId}`), THE Source_Generator SHALL generate parameter definitions for each path parameter in the operation.
2. WHEN a path parameter in the route template does not have a corresponding `[FromRoute]` method parameter, THE Source_Generator SHALL still generate a parameter definition with type `string` and `required: true`.
3. WHEN a path parameter is defined both in the route template and as a `[FromRoute]` method parameter, THE Source_Generator SHALL use the method parameter's type information for the schema.
4. FOR ALL generated path parameters, THE Source_Generator SHALL set `in: path` and `required: true` as per OpenAPI specification requirements.

### Requirement 2: DateOnly and TimeOnly Type Handling

**User Story:** As an API developer, I want DateOnly and TimeOnly types to be correctly mapped to OpenAPI string formats, so that generated client code uses the correct .NET types.

#### Acceptance Criteria

1. WHEN a property or parameter is of type `DateOnly`, THE Source_Generator SHALL generate a schema with `type: string` and `format: date`.
2. WHEN a property or parameter is of type `TimeOnly`, THE Source_Generator SHALL generate a schema with `type: string` and `format: time`.
3. WHEN a property or parameter is of type `DateOnly?` or `TimeOnly?`, THE Source_Generator SHALL generate the same schema as the non-nullable version with `nullable: true`.
4. THE Source_Generator SHALL NOT generate separate schema definitions for DateOnly or TimeOnly in the components/schemas section.

### Requirement 3: Class-Level OpenApiTag Attribute Support

**User Story:** As an API developer, I want to apply `[OpenApiTag]` at the class level, so that all operations in a class inherit the same tag without repeating the attribute on every method.

#### Acceptance Criteria

1. WHEN a class has an `[OpenApiTag]` attribute, THE Source_Generator SHALL apply that tag to all operations defined in that class.
2. WHEN a method has its own `[OpenApiTag]` attribute, THE Source_Generator SHALL use the method-level tag(s) instead of the class-level tag(s).
3. WHEN a class has multiple `[OpenApiTag]` attributes, THE Source_Generator SHALL apply all class-level tags to operations without method-level tags.
4. WHEN neither the class nor the method has an `[OpenApiTag]` attribute, THE Source_Generator SHALL assign the operation to the "Default" tag.

### Requirement 4: Exclude AWS Lambda Payload Types from Schema

**User Story:** As an API developer, I want AWS Lambda-specific types to be excluded from the OpenAPI specification, so that my API documentation only shows business-relevant types.

#### Acceptance Criteria

1. WHEN a POST method has no `[FromBody]` parameter, THE Source_Generator SHALL NOT generate a requestBody for that operation.
2. THE Source_Generator SHALL NOT include `APIGatewayProxyRequest`, `APIGatewayHttpApiV2ProxyRequest`, or similar AWS Lambda types in the components/schemas section.
3. WHEN a method parameter is of an AWS Lambda payload type without `[FromBody]`, THE Source_Generator SHALL exclude that parameter from the operation's parameters.
4. THE Source_Generator SHALL recognize AWS Lambda types by their namespace (`Amazon.Lambda.APIGatewayEvents`) or by common type names.

### Requirement 5: Tilde Path Expansion in Merge Configuration

**User Story:** As a developer, I want to use `~/` in merge configuration file paths, so that I can reference files relative to my home directory.

#### Acceptance Criteria

1. WHEN a source file path in the merge configuration starts with `~/`, THE Merge_Tool SHALL expand it to the user's home directory.
2. WHEN a source file path starts with `~username/`, THE Merge_Tool SHALL expand it to that user's home directory (Unix-style).
3. WHEN the output path in the merge configuration starts with `~/`, THE Merge_Tool SHALL expand it to the user's home directory.
4. IF tilde expansion fails (e.g., user not found), THEN THE Merge_Tool SHALL report a clear error message identifying the problematic path.

### Requirement 6: Documentation Updates

**User Story:** As a library user, I want documentation to reflect all bug fixes and new behaviors, so that I can understand how to use the library correctly.

#### Acceptance Criteria

1. WHEN path parameter generation is fixed, THE documentation SHALL explain that path parameters are automatically extracted from route templates.
2. WHEN DateOnly/TimeOnly support is added, THE documentation SHALL list these types in the supported type mappings.
3. WHEN class-level tag support is added, THE documentation SHALL show examples of class-level `[OpenApiTag]` usage.
4. WHEN the CHANGELOG is updated, THE CHANGELOG SHALL describe each bug fix with the symptom and resolution.

