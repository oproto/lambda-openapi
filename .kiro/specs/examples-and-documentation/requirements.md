# Requirements Document

## Introduction

This document specifies the requirements for creating an examples project demonstrating Oproto.Lambda.OpenApi usage with AWS Lambda Annotations, and completing the documentation suite with attribute reference and configuration guides. The examples project serves as both a usage demonstration and a validation that the library works correctly in real-world scenarios.

## Glossary

- **Oproto.Lambda.OpenApi**: The main NuGet package containing attributes for OpenAPI specification generation
- **AWS Lambda Annotations**: Amazon's library for simplified Lambda function development using attributes
- **CRUD API**: An API implementing Create, Read, Update, and Delete operations for a resource
- **OpenAPI Specification**: A standard format for describing REST APIs (formerly Swagger)
- **XML Documentation**: C# documentation comments that generate IntelliSense and API documentation

## Requirements

### Requirement 1: Examples Project Structure

**User Story:** As a developer evaluating this library, I want a working example project, so that I can understand how to integrate Oproto.Lambda.OpenApi into my own Lambda projects.

#### Acceptance Criteria

1. WHEN the solution is built THEN the Oproto.Lambda.OpenApi.Examples project SHALL compile successfully
2. WHEN viewing the examples project THEN the project SHALL reference AWS Lambda Annotations and Oproto.Lambda.OpenApi packages
3. WHEN viewing the examples project THEN the project SHALL target .NET 8.0 for Lambda compatibility
4. WHEN the examples project is built THEN the project SHALL generate an openapi.json file

### Requirement 2: CRUD API Implementation

**User Story:** As a developer learning the library, I want to see a complete CRUD API example, so that I can understand how to document all common HTTP operations.

#### Acceptance Criteria

1. WHEN viewing the examples THEN the project SHALL include a Products resource with GET, POST, PUT, and DELETE operations
2. WHEN viewing the GET operation THEN the operation SHALL demonstrate path parameters and query parameters
3. WHEN viewing the POST operation THEN the operation SHALL demonstrate request body handling with a model class
4. WHEN viewing the PUT operation THEN the operation SHALL demonstrate combining path parameters with request body
5. WHEN viewing the DELETE operation THEN the operation SHALL demonstrate path parameter usage
6. WHEN viewing the examples THEN each operation SHALL use OpenApiOperation attribute with summary and description
7. WHEN viewing the examples THEN operations SHALL be grouped using OpenApiTag attribute

### Requirement 3: Model Documentation

**User Story:** As a developer, I want to see how to document request and response models, so that I can generate accurate schema documentation.

#### Acceptance Criteria

1. WHEN viewing the examples THEN model classes SHALL use OpenApiSchema attribute to document properties
2. WHEN viewing the examples THEN model classes SHALL demonstrate validation constraints using OpenApiSchema
3. WHEN viewing the examples THEN model classes SHALL demonstrate the OpenApiIgnore attribute for internal properties
4. WHEN viewing the examples THEN model classes SHALL include XML documentation comments

### Requirement 4: Attribute Reference Documentation

**User Story:** As a developer using the library, I want comprehensive attribute documentation, so that I can understand all available options and their usage.

#### Acceptance Criteria

1. WHEN viewing docs/attributes.md THEN the document SHALL describe all six attributes in the library
2. WHEN viewing attribute documentation THEN each attribute SHALL include its target types and all properties
3. WHEN viewing attribute documentation THEN each attribute SHALL include usage examples
4. WHEN viewing attribute documentation THEN the document SHALL explain when to use each attribute

### Requirement 5: Configuration Documentation

**User Story:** As a developer, I want configuration documentation, so that I can customize the OpenAPI generation behavior.

#### Acceptance Criteria

1. WHEN viewing docs/configuration.md THEN the document SHALL explain MSBuild property configuration
2. WHEN viewing configuration documentation THEN the document SHALL explain output path customization
3. WHEN viewing configuration documentation THEN the document SHALL explain how to disable automatic generation
4. WHEN viewing configuration documentation THEN the document SHALL include troubleshooting guidance

### Requirement 6: Documentation Cross-References

**User Story:** As a developer navigating documentation, I want working links between documents, so that I can easily find related information.

#### Acceptance Criteria

1. WHEN viewing getting-started.md THEN links to attributes.md and configuration.md SHALL resolve correctly
2. WHEN viewing getting-started.md THEN links to the examples project SHALL resolve correctly
3. WHEN viewing any documentation file THEN cross-references to other documentation SHALL use relative paths

