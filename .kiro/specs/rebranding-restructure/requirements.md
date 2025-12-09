# Requirements Document

## Introduction

This document specifies the requirements for rebranding and restructuring the Oproto.OpenApi project suite to Oproto.Lambda.OpenApi. The restructuring includes renaming projects, reorganizing namespaces, bundling components for easier consumption, and updating documentation for open source release.

## Glossary

- **Oproto.Lambda.OpenApi**: The main NuGet package that consumers install, containing attributes and bundled source generator
- **Oproto.Lambda.OpenApi.SourceGenerator**: The Roslyn source generator that produces OpenAPI specifications from Lambda functions
- **Oproto.Lambda.OpenApi.Attributes**: Namespace containing all attribute classes for annotating Lambda functions
- **MSBuild Task**: A custom build task that extracts OpenAPI documents during the build process
- **Source Generator**: A Roslyn compiler feature that generates code at compile time

## Requirements

### Requirement 1: Project Renaming

**User Story:** As a developer consuming this library, I want consistent and descriptive package names, so that I can easily identify and install the correct packages.

#### Acceptance Criteria

1. WHEN the solution is built THEN the Oproto.OpenApi project SHALL produce a package named Oproto.Lambda.OpenApi
2. WHEN the solution is built THEN the Oproto.OpenApiGenerator project SHALL produce a package named Oproto.Lambda.OpenApi.SourceGenerator
3. WHEN the solution is built THEN the Oproto.OpenApiGenerator.Tasks project SHALL produce a package named Oproto.Lambda.OpenApi.Build
4. WHEN the solution is built THEN the test project SHALL be named Oproto.Lambda.OpenApi.Tests

### Requirement 2: Namespace Reorganization

**User Story:** As a developer using the library, I want attributes organized in a dedicated namespace, so that I can easily discover and import them.

#### Acceptance Criteria

1. WHEN a developer imports attributes THEN the attributes SHALL be located in the Oproto.Lambda.OpenApi.Attributes namespace
2. WHEN the source files are organized THEN attribute classes SHALL reside in an Attributes folder within the Oproto.Lambda.OpenApi project
3. WHEN existing code references old namespaces THEN the build SHALL fail until updated to new namespaces

### Requirement 3: Package Bundling

**User Story:** As a developer, I want to install a single package that includes all necessary components, so that I don't have to manually configure multiple dependencies.

#### Acceptance Criteria

1. WHEN a developer installs Oproto.Lambda.OpenApi THEN the package SHALL automatically include the source generator
2. WHEN a developer installs Oproto.Lambda.OpenApi THEN the MSBuild task for OpenAPI extraction SHALL be automatically configured
3. WHEN the package is installed THEN no additional manual configuration SHALL be required for basic functionality

### Requirement 4: Directory and File Structure

**User Story:** As a maintainer, I want the project structure to reflect the new naming conventions, so that the codebase is organized and maintainable.

#### Acceptance Criteria

1. WHEN viewing the solution THEN project folders SHALL match their project names
2. WHEN viewing the solution THEN the solution file SHALL be named Oproto.Lambda.OpenApi.sln
3. WHEN the icon is referenced THEN a placeholder icon file SHALL exist at docs/assets/icon.png

### Requirement 5: Build Configuration

**User Story:** As a maintainer, I want centralized build configuration, so that version management and package metadata are consistent across all projects.

#### Acceptance Criteria

1. WHEN the solution is built THEN Directory.Build.props SHALL define common version properties
2. WHEN CI/CD overrides the version THEN all projects SHALL use the overridden version
3. WHEN packages are created THEN all packages SHALL include the icon, license, and repository metadata
4. WHEN packages are created THEN symbol packages SHALL be generated in snupkg format

### Requirement 6: Documentation Updates

**User Story:** As a potential contributor or user, I want accurate documentation, so that I can understand the project and find relevant resources.

#### Acceptance Criteria

1. WHEN viewing the README THEN repository references SHALL point to oproto/lambda-openapi
2. WHEN viewing the README THEN the About section SHALL include company information and maintainer details
3. WHEN viewing the README THEN links to related projects SHALL be present and accurate
4. WHEN viewing documentation THEN all references to old package names SHALL be updated to new names
