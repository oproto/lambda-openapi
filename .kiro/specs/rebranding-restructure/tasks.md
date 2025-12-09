# Implementation Plan

- [x] 1. Create placeholder files and update Directory.Build.props
  - [x] 1.1 Create docs/assets/icon.png placeholder file (0 bytes)
    - Create the directory structure and empty icon file
    - _Requirements: 4.3_
  - [x] 1.2 Update Directory.Build.props with new configuration
    - Replace existing content with the user-provided configuration
    - Include version management, package metadata, and icon reference
    - _Requirements: 5.1, 5.2, 5.3, 5.4_

- [x] 2. Rename and restructure Oproto.OpenApi to Oproto.Lambda.OpenApi
  - [x] 2.1 Move project folder using mv command
    - `mv Oproto.OpenApi Oproto.Lambda.OpenApi`
    - _Requirements: 1.1, 4.1_
  - [x] 2.2 Create Attributes subfolder and move attribute files
    - Create `Oproto.Lambda.OpenApi/Attributes/` folder
    - Move all attribute .cs files into the Attributes folder
    - _Requirements: 2.2_
  - [x] 2.3 Update namespace in all attribute files to Oproto.Lambda.OpenApi.Attributes
    - Update GenerateOpenApiSpecAttribute.cs
    - Update OpenApiIgnoreAttribute.cs
    - Update OpenApiOperationAttribute.cs
    - Update OpenApiOutputAttribute.cs
    - Update OpenApiSchemaAttribute.cs
    - Update OpenApiTagAttribute.cs
    - _Requirements: 2.1, 2.3_
  - [x] 2.4 Rename and update the .csproj file
    - Rename Oproto.OpenApi.csproj to Oproto.Lambda.OpenApi.csproj
    - Update PackageId, Title, Description, and URLs
    - Configure bundling of source generator and build task
    - _Requirements: 1.1_

- [x] 3. Rename Oproto.OpenApiGenerator to Oproto.Lambda.OpenApi.SourceGenerator
  - [x] 3.1 Move project folder using mv command
    - `mv Oproto.OpenApiGenerator Oproto.Lambda.OpenApi.SourceGenerator`
    - _Requirements: 1.2, 4.1_
  - [x] 3.2 Update namespace in all source files
    - Update all .cs files to use Oproto.Lambda.OpenApi.SourceGenerator namespace
    - Update references to Oproto.OpenApi to Oproto.Lambda.OpenApi.Attributes
    - _Requirements: 2.3_
  - [x] 3.3 Rename and update the .csproj file
    - Rename to Oproto.Lambda.OpenApi.SourceGenerator.csproj
    - Update PackageId and metadata
    - Update project references to new paths
    - _Requirements: 1.2_
  - [x] 3.4 Update build folder files
    - Rename props file to Oproto.Lambda.OpenApi.props
    - Create Oproto.Lambda.OpenApi.targets for MSBuild task
    - Update all assembly references to new names
    - _Requirements: 3.1, 3.2_

- [x] 4. Rename Oproto.OpenApiGenerator.Tasks to Oproto.Lambda.OpenApi.Build
  - [x] 4.1 Move project folder using mv command
    - `mv Oproto.OpenApiGenerator.Tasks Oproto.Lambda.OpenApi.Build`
    - _Requirements: 1.3, 4.1_
  - [x] 4.2 Update namespace in source files
    - Update ExtractOpenApiSpecTask.cs namespace to Oproto.Lambda.OpenApi.Build
    - _Requirements: 2.3_
  - [x] 4.3 Rename and update the .csproj file
    - Rename to Oproto.Lambda.OpenApi.Build.csproj
    - Update project references to new paths
    - _Requirements: 1.3_

- [x] 5. Rename test project to Oproto.Lambda.OpenApi.Tests
  - [x] 5.1 Move project folder using mv command
    - `mv Oproto.OpenApiGenerator.UnitTests Oproto.Lambda.OpenApi.Tests`
    - _Requirements: 1.4, 4.1_
  - [x] 5.2 Update namespaces and references in test files
    - Update all test .cs files to use new namespaces
    - Update references to renamed projects
    - _Requirements: 2.3_
  - [x] 5.3 Rename and update the .csproj file
    - Rename to Oproto.Lambda.OpenApi.Tests.csproj
    - Update project references to new paths
    - _Requirements: 1.4_

- [x] 6. Update solution file
  - [x] 6.1 Rename solution file
    - `mv Oproto.OpenApiGenerator.sln Oproto.Lambda.OpenApi.sln`
    - _Requirements: 4.2_
  - [x] 6.2 Update solution file contents
    - Update all project paths and names in the .sln file
    - _Requirements: 4.2_

- [x] 7. Update documentation
  - [x] 7.1 Update README.md with new branding and About section
    - Update all package names and repository URLs
    - Add the About section with company info, links, and maintainer
    - Update badge URLs to oproto/lambda-openapi
    - _Requirements: 6.1, 6.2, 6.3_
  - [x] 7.2 Update docs/getting-started.md
    - Update package names and namespace references
    - Update code examples to use new namespaces
    - _Requirements: 6.4_

- [x] 8. Checkpoint - Verify build and tests
  - Ensure all tests pass, ask the user if questions arise.

- [x] 9. Clean up
  - [x] 9.1 Remove old obj and bin folders
    - Delete build artifacts from old project locations if any remain
    - _Requirements: 4.1_
  - [x] 9.2 Update any remaining file references
    - Check for any hardcoded paths or references that need updating
    - _Requirements: 6.4_

- [x] 10. Final Checkpoint - Make sure all tests are passing
  - Ensure all tests pass, ask the user if questions arise.
