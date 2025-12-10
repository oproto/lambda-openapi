# Requirements Document

## Introduction

This specification addresses critical issues, code quality improvements, and documentation gaps identified during pre-release review of the Oproto Lambda OpenAPI source generator. The goal is to prepare the library for public release with correct functionality, clean code, and comprehensive documentation.

## Glossary

- **Source Generator**: A Roslyn-based compile-time code generator that produces C# source during compilation
- **MSBuild Task**: A custom build task that runs after compilation to extract the OpenAPI specification from the compiled assembly
- **OpenAPI Specification**: A JSON document describing REST API endpoints, parameters, and schemas
- **Task<T>**: .NET's asynchronous wrapper type that should be unwrapped to expose the inner type T in API documentation
- **AOT (Ahead-of-Time)**: Compilation mode where code is compiled to native code before runtime, incompatible with reflection

## Requirements

### Requirement 1: Task<T> Return Type Unwrapping

**User Story:** As an API consumer, I want the OpenAPI specification to show the actual return type of async methods, so that I can understand what data the API returns without seeing internal .NET implementation details.

#### Acceptance Criteria

1. WHEN a Lambda function returns `Task<T>` THEN the OpenAPI schema SHALL display only the schema for type `T`
2. WHEN a Lambda function returns `Task` (non-generic) THEN the OpenAPI response schema SHALL indicate no content or void response
3. WHEN a Lambda function returns `ValueTask<T>` THEN the OpenAPI schema SHALL display only the schema for type `T`
4. WHEN a Lambda function returns a non-Task type directly THEN the OpenAPI schema SHALL display that type unchanged

### Requirement 2: Remove Debug Output from Production Code

**User Story:** As a developer using this library, I want clean build output without debug messages, so that my build logs are not cluttered with internal library diagnostics.

#### Acceptance Criteria

1. WHEN the source generator executes THEN the generator SHALL NOT emit Console.WriteLine output
2. WHEN the source generator executes in DEBUG configuration THEN the generator MAY emit Debug.WriteLine output for diagnostics
3. WHEN the source generator executes in RELEASE configuration THEN the generator SHALL NOT emit any debug output
4. WHEN evaluating existing debug statements THEN the system SHALL retain only statements that provide meaningful diagnostic value under conditional compilation

### Requirement 3: Package Version Alignment

**User Story:** As a package maintainer, I want all projects in the solution to use consistent versioning, so that package versions are predictable and aligned.

#### Acceptance Criteria

1. WHEN building any project in the solution THEN the project SHALL use version properties from Directory.Build.props
2. WHEN the SourceGenerator project is built THEN the package version SHALL match the version defined in Directory.Build.props
3. WHEN version is overridden via MSBuild properties THEN all projects SHALL respect the override consistently

### Requirement 4: Security Scheme Configuration

**User Story:** As an API developer, I want to configure OAuth and API key security schemes for my API, so that the generated OpenAPI specification accurately reflects my API's authentication requirements.

#### Acceptance Criteria

1. WHEN a user applies an OpenApiSecurityScheme attribute at assembly level THEN the system SHALL use those values for security definitions
2. WHEN no security scheme attributes are defined THEN the system SHALL NOT include hardcoded example security schemes
3. WHEN multiple security schemes are defined THEN the system SHALL include all defined schemes in the specification
4. WHERE OAuth2 security is configured THEN the system SHALL allow specification of authorization URL, token URL, and scopes
5. WHERE API key security is configured THEN the system SHALL allow specification of header name and location

### Requirement 5: Dependency Version Management

**User Story:** As a library consumer, I want the library to have minimal and flexible dependencies, so that I can manage my own dependency versions without conflicts.

#### Acceptance Criteria

1. WHEN System.Text.Json is referenced by the source generator THEN the reference SHALL specify a version range allowing consumer override
2. WHEN the MSBuild task references dependencies THEN those dependencies SHALL be updated to versions without known vulnerabilities
3. WHEN the library is consumed THEN the consumer SHALL be able to use their own compatible version of transitive dependencies

### Requirement 6: HTTP Method Support Completeness

**User Story:** As an API developer, I want all standard HTTP methods to be supported, so that I can document PATCH, HEAD, and OPTIONS endpoints.

#### Acceptance Criteria

1. WHEN a Lambda function uses PATCH HTTP method THEN the OpenAPI specification SHALL include the operation under the patch key
2. WHEN a Lambda function uses HEAD HTTP method THEN the OpenAPI specification SHALL include the operation under the head key
3. WHEN a Lambda function uses OPTIONS HTTP method THEN the OpenAPI specification SHALL include the operation under the options key

### Requirement 7: Dead Code Removal

**User Story:** As a maintainer, I want the codebase to contain only used code, so that maintenance burden is reduced and the code is easier to understand.

#### Acceptance Criteria

1. WHEN the GenerateOpenApiSpecAttribute is not used by the generator THEN the attribute SHALL be removed or the generator SHALL be updated to use it
2. WHEN ResponseTypeInfo class is not populated or used THEN the class and related code SHALL be removed or the feature SHALL be completed
3. WHEN ExampleInfo class is not populated or used THEN the class and related code SHALL be removed or the feature SHALL be completed
4. WHEN GetLambdaClassInfo method is not called THEN the method SHALL be removed
5. WHEN GetApiInfo method is not called THEN the method SHALL be removed

### Requirement 8: Documentation Completeness

**User Story:** As a new user of the library, I want comprehensive documentation, so that I can understand how to use all features correctly.

#### Acceptance Criteria

1. WHEN a user reads getting-started.md THEN the documentation SHALL explain that FromServices parameters are automatically excluded
2. WHEN a user reads the documentation THEN the documentation SHALL explain the x-amazon-apigateway-integration extension
3. WHEN documentation changes are made THEN a DOCUMENTATION_CHANGELOG.md file SHALL track those changes
4. WHEN the main CHANGELOG.md is updated THEN it SHALL reflect all code and documentation changes for the release

### Requirement 9: Test Coverage Improvements

**User Story:** As a maintainer, I want comprehensive test coverage, so that regressions are caught before release.

#### Acceptance Criteria

1. WHEN Task<T> unwrapping is implemented THEN tests SHALL verify the unwrapped type appears in the schema
2. WHEN PATCH/HEAD/OPTIONS methods are supported THEN tests SHALL verify each method type generates correct OpenAPI
3. WHEN security scheme attributes are implemented THEN tests SHALL verify custom security configurations
4. WHEN the library is used in AOT scenarios THEN documentation SHALL explain any limitations or requirements

### Requirement 10: Build Process and AOT Compatibility

**User Story:** As a user of the library, I want to understand the build process and have options for AOT scenarios, so that I can use the library regardless of my deployment target.

#### Acceptance Criteria

1. WHEN a user reads the documentation THEN the documentation SHALL explain the two-phase build process (source generator embeds JSON as assembly attribute, MSBuild task extracts via reflection)
2. WHEN the build process uses reflection THEN the documentation SHALL note that AOT-only builds cannot use the automatic extraction
3. WHEN a user targets AOT THEN the documentation SHALL describe the workaround of building with AOT disabled first to extract the spec
4. IF an alternative extraction method is feasible THEN the system SHALL provide a generated entry point or CLI command that outputs the OpenAPI spec without reflection

### Requirement 11: AOT-Compatible OpenAPI Extraction

**User Story:** As a developer deploying to AOT environments, I want automatic OpenAPI extraction that works without reflection, so that my build process works regardless of compilation mode.

#### Acceptance Criteria

1. WHEN the MSBuild task executes THEN the task SHALL first check if `EmitCompilerGeneratedFiles` is enabled and generated source files exist
2. IF generated source files exist THEN the MSBuild task SHALL parse the `OpenApiOutput.g.cs` file directly to extract the JSON string
3. IF generated source files do not exist THEN the MSBuild task SHALL fall back to reflection-based extraction from the compiled assembly
4. WHEN parsing the generated source file THEN the task SHALL extract the JSON from the assembly attribute string literal
5. WHEN reflection-based extraction fails and no generated files exist THEN the task SHALL log an error with instructions to enable `EmitCompilerGeneratedFiles`
6. WHEN a user targets AOT THEN the documentation SHALL recommend enabling `EmitCompilerGeneratedFiles` for reliable extraction

**Implementation Note:** The MSBuild task will have two extraction strategies:
1. **Primary (AOT-safe):** Parse the generated `.g.cs` file from `$(CompilerGeneratedFilesOutputPath)` to extract the JSON string literal
2. **Fallback (non-AOT):** Use reflection to read the assembly attribute (current behavior)

This approach works because source generators always produce the `.g.cs` file during compilation, and when `EmitCompilerGeneratedFiles=true`, that file is persisted to disk where the MSBuild task can read it.
