# Requirements Document

## Introduction

This feature enhances the OpenAPI specification generator to produce more complete and standards-compliant OpenAPI 3.0 documents. The goal is to support additional OpenAPI features that users commonly expect, including operation examples, deprecation markers, response headers, server definitions, tags, and external documentation links.

## Glossary

- **OpenAPI_Generator**: The Roslyn source generator that produces OpenAPI specifications from Lambda function code
- **Operation**: An HTTP method (GET, POST, etc.) on a specific path in the OpenAPI specification
- **Tag**: A grouping mechanism for organizing operations in OpenAPI documentation viewers
- **Server**: A base URL definition for the API in the OpenAPI specification

## Requirements

### Requirement 1: Operation Examples

**User Story:** As an API developer, I want to provide request and response examples for my endpoints, so that API consumers can understand expected payloads.

#### Acceptance Criteria

1. WHEN a method has XML documentation with `<example>` tags THEN the OpenAPI_Generator SHALL include those examples in the operation's request or response schema
2. WHEN a method has an `[OpenApiExample]` attribute with JSON content THEN the OpenAPI_Generator SHALL parse and include that example in the specification
3. WHEN both XML comments and attributes provide examples THEN the OpenAPI_Generator SHALL prefer the attribute value
4. WHEN an example is provided for a request body THEN the OpenAPI_Generator SHALL place it in the `requestBody.content.application/json.example` field
5. WHEN an example is provided for a response THEN the OpenAPI_Generator SHALL place it in the `responses.{statusCode}.content.application/json.example` field

### Requirement 2: Deprecated Operations

**User Story:** As an API developer, I want to mark endpoints as deprecated, so that API consumers know which endpoints to avoid.

#### Acceptance Criteria

1. WHEN a Lambda function method has the `[Obsolete]` attribute THEN the OpenAPI_Generator SHALL set `deprecated: true` on that operation
2. WHEN a Lambda function method has the `[Obsolete]` attribute with a message THEN the OpenAPI_Generator SHALL include that message in the operation description
3. WHEN a Lambda function method does not have the `[Obsolete]` attribute THEN the OpenAPI_Generator SHALL not include the deprecated field

### Requirement 3: Response Headers

**User Story:** As an API developer, I want to document response headers for my endpoints, so that API consumers know what headers to expect.

#### Acceptance Criteria

1. WHEN a method has an `[OpenApiResponseHeader]` attribute THEN the OpenAPI_Generator SHALL include that header in the specified response's headers section
2. WHEN multiple response headers are defined for the same status code THEN the OpenAPI_Generator SHALL include all headers in that response
3. WHEN a response header specifies a type THEN the OpenAPI_Generator SHALL generate the appropriate schema for that header
4. WHEN a response header is marked as required THEN the OpenAPI_Generator SHALL set `required: true` on that header

### Requirement 4: Server Definitions

**User Story:** As an API developer, I want to specify server URLs for my API, so that API consumers know where to send requests.

#### Acceptance Criteria

1. WHEN an assembly has `[OpenApiServer]` attributes THEN the OpenAPI_Generator SHALL include those servers in the specification's servers array
2. WHEN a server definition includes a description THEN the OpenAPI_Generator SHALL include that description
3. WHEN a server definition includes variables THEN the OpenAPI_Generator SHALL include those variables with their default values
4. WHEN no server attributes are present THEN the OpenAPI_Generator SHALL omit the servers section entirely

### Requirement 5: Tag Definitions and Assignment

**User Story:** As an API developer, I want to organize my endpoints into logical groups with descriptions, so that API documentation is well-organized.

#### Acceptance Criteria

1. WHEN a method has an `[OpenApiTag]` attribute THEN the OpenAPI_Generator SHALL assign that operation to the specified tag
2. WHEN an assembly has `[OpenApiTagDefinition]` attributes THEN the OpenAPI_Generator SHALL include those tag definitions with descriptions in the specification
3. WHEN a method has multiple `[OpenApiTag]` attributes THEN the OpenAPI_Generator SHALL assign that operation to all specified tags
4. WHEN a method has no `[OpenApiTag]` attribute THEN the OpenAPI_Generator SHALL assign it to a "Default" tag
5. WHEN a tag is used but not defined at assembly level THEN the OpenAPI_Generator SHALL still include the tag reference on the operation

### Requirement 6: External Documentation

**User Story:** As an API developer, I want to link to external documentation, so that API consumers can find additional resources.

#### Acceptance Criteria

1. WHEN an assembly has an `[OpenApiExternalDocs]` attribute THEN the OpenAPI_Generator SHALL include the external documentation link at the specification level
2. WHEN an operation method has an `[OpenApiExternalDocs]` attribute THEN the OpenAPI_Generator SHALL include the external documentation link at the operation level
3. WHEN external documentation includes a description THEN the OpenAPI_Generator SHALL include that description

### Requirement 7: Operation ID

**User Story:** As an API developer, I want each operation to have a unique identifier, so that code generators can create meaningful method names.

#### Acceptance Criteria

1. THE OpenAPI_Generator SHALL generate an operationId for each operation based on the method name
2. WHEN a method has an `[OpenApiOperationId]` attribute THEN the OpenAPI_Generator SHALL use that value instead of the generated one
3. WHEN duplicate operationIds would be generated THEN the OpenAPI_Generator SHALL append a numeric suffix to ensure uniqueness

### Requirement 8: Documentation Updates

**User Story:** As a library maintainer, I want all new features documented, so that users can discover and use them.

#### Acceptance Criteria

1. WHEN a new attribute is added THEN the documentation in `docs/attributes.md` SHALL be updated with usage examples
2. WHEN a new feature is added THEN the `CHANGELOG.md` SHALL be updated with the change description
3. WHEN documentation changes are made THEN the `docs/DOCUMENTATION_CHANGELOG.md` SHALL be updated for external documentation sync
4. WHEN a new feature is added THEN the Examples project SHALL demonstrate that feature with working code

### Requirement 9: Test Coverage

**User Story:** As a library maintainer, I want comprehensive tests for all new features, so that regressions are caught early.

#### Acceptance Criteria

1. WHEN a new attribute is added THEN unit tests SHALL verify the attribute is correctly read by the generator
2. WHEN a new OpenAPI output feature is added THEN integration tests SHALL verify the correct JSON structure is generated
3. WHEN the Examples project is built THEN the generated openapi.json SHALL contain examples of all new features
