# Design Document: Rebranding and Restructure

## Overview

This design document outlines the technical approach for rebranding the Oproto.OpenApi project suite to Oproto.Lambda.OpenApi. The restructuring involves renaming projects, reorganizing namespaces, bundling components for simplified consumption, and updating all documentation for open source release.

## Architecture

### Current Structure
```
Oproto.OpenApiGenerator.sln
├── Oproto.OpenApi/                    # Attributes library
├── Oproto.OpenApiGenerator/           # Source generator + bundled components
├── Oproto.OpenApiGenerator.Tasks/     # MSBuild task
└── Oproto.OpenApiGenerator.UnitTests/ # Tests
```

### Target Structure
```
Oproto.Lambda.OpenApi.sln
├── Oproto.Lambda.OpenApi/                    # Main package (attributes + bundled generator/tasks)
│   └── Attributes/                           # Attribute classes
├── Oproto.Lambda.OpenApi.SourceGenerator/    # Source generator (bundled into main package)
├── Oproto.Lambda.OpenApi.Build/              # MSBuild task (bundled into main package)
└── Oproto.Lambda.OpenApi.Tests/              # Tests
```

### Package Distribution Model

The main `Oproto.Lambda.OpenApi` package will bundle all components:

```
Oproto.Lambda.OpenApi.nupkg
├── lib/netstandard2.0/
│   └── Oproto.Lambda.OpenApi.dll          # Attributes assembly
├── analyzers/dotnet/cs/
│   ├── Oproto.Lambda.OpenApi.SourceGenerator.dll
│   └── Microsoft.OpenApi.dll              # Dependency
├── build/
│   ├── Oproto.Lambda.OpenApi.props        # Auto-import configuration
│   ├── Oproto.Lambda.OpenApi.targets      # MSBuild targets
│   └── Oproto.Lambda.OpenApi.Build.dll    # MSBuild task
└── icon.png
```

## Components and Interfaces

### 1. Oproto.Lambda.OpenApi (Main Package)

**Purpose**: Primary package consumers install. Contains attributes and bundles all other components.

**Project Configuration**:
- Target Framework: `netstandard2.0` (for broad compatibility)
- Generates package on build
- Bundles source generator and MSBuild task assemblies
- Auto-imports via `.props` and `.targets` files

**Namespace Structure**:
```csharp
namespace Oproto.Lambda.OpenApi.Attributes
{
    // All attribute classes
    public class OpenApiOperationAttribute : Attribute { }
    public class OpenApiTagAttribute : Attribute { }
    public class OpenApiSchemaAttribute : Attribute { }
    public class OpenApiIgnoreAttribute : Attribute { }
    public class OpenApiOutputAttribute : Attribute { }
    public class GenerateOpenApiSpecAttribute : Attribute { }
}
```

### 2. Oproto.Lambda.OpenApi.SourceGenerator

**Purpose**: Roslyn source generator that analyzes Lambda functions and generates OpenAPI specs.

**Project Configuration**:
- Target Framework: `netstandard2.0`
- `IsRoslynComponent`: true
- `DevelopmentDependency`: true
- Not published as standalone package (bundled into main package)

### 3. Oproto.Lambda.OpenApi.Build

**Purpose**: MSBuild task that extracts OpenAPI specification after build.

**Project Configuration**:
- Target Framework: `netstandard2.0`
- Not published as standalone package (bundled into main package)

### 4. Build Integration Files

**Oproto.Lambda.OpenApi.props**:
```xml
<Project>
  <PropertyGroup>
    <OpenApiGeneratorTasksPath>$(MSBuildThisFileDirectory)</OpenApiGeneratorTasksPath>
  </PropertyGroup>

  <ItemGroup>
    <CompilerVisibleItemMetadata Include="AdditionalFiles" MetadataName="OpenApiGenerator"/>
    <Analyzer Include="$(MSBuildThisFileDirectory)..\analyzers\dotnet\cs\Oproto.Lambda.OpenApi.SourceGenerator.dll"/>
  </ItemGroup>

  <ItemGroup>
    <Reference Include="Oproto.Lambda.OpenApi">
      <HintPath>$(MSBuildThisFileDirectory)..\lib\netstandard2.0\Oproto.Lambda.OpenApi.dll</HintPath>
      <Private>true</Private>
    </Reference>
  </ItemGroup>
</Project>
```

**Oproto.Lambda.OpenApi.targets**:
```xml
<Project>
  <PropertyGroup>
    <TasksAssemblyPath>$(MSBuildThisFileDirectory)Oproto.Lambda.OpenApi.Build.dll</TasksAssemblyPath>
  </PropertyGroup>

  <UsingTask TaskName="Oproto.Lambda.OpenApi.Build.ExtractOpenApiSpecTask"
             AssemblyFile="$(TasksAssemblyPath)"/>

  <Target Name="GenerateOpenApi" AfterTargets="Build">
    <Message Text="Starting OpenAPI Generation" Importance="high"/>
    <ExtractOpenApiSpecTask
      AssemblyPath="$(TargetDir)$(TargetFileName)"
      OutputPath="$(ProjectDir)openapi.json"/>
  </Target>
</Project>
```

## Data Models

### File Rename Mapping

| Current Path | New Path |
|-------------|----------|
| `Oproto.OpenApiGenerator.sln` | `Oproto.Lambda.OpenApi.sln` |
| `Oproto.OpenApi/` | `Oproto.Lambda.OpenApi/` |
| `Oproto.OpenApi/*.cs` | `Oproto.Lambda.OpenApi/Attributes/*.cs` |
| `Oproto.OpenApiGenerator/` | `Oproto.Lambda.OpenApi.SourceGenerator/` |
| `Oproto.OpenApiGenerator.Tasks/` | `Oproto.Lambda.OpenApi.Build/` |
| `Oproto.OpenApiGenerator.UnitTests/` | `Oproto.Lambda.OpenApi.Tests/` |

### Namespace Mapping

| Current Namespace | New Namespace |
|------------------|---------------|
| `Oproto.OpenApi` | `Oproto.Lambda.OpenApi.Attributes` |
| `Oproto.OpenApiGenerator` | `Oproto.Lambda.OpenApi.SourceGenerator` |
| `Oproto.OpenApiGenerator.Tasks` | `Oproto.Lambda.OpenApi.Build` |

## Correctness Properties

*A property is a characteristic or behavior that should hold true across all valid executions of a system-essentially, a formal statement about what the system should do. Properties serve as the bridge between human-readable specifications and machine-verifiable correctness guarantees.*

Based on the prework analysis, most acceptance criteria are example-based tests (verifying specific configurations and outputs) rather than universal properties. The restructuring is primarily a refactoring operation where correctness is verified through:

1. **Build Success**: The solution builds without errors after all renames
2. **Test Passage**: All existing unit tests pass after namespace updates
3. **Package Contents**: Generated packages contain expected assemblies in correct locations

Since this is a refactoring/renaming task rather than new functionality, property-based testing is not applicable. Correctness will be verified through:
- Successful compilation
- Existing test suite passing
- Manual verification of package structure

## Error Handling

### Migration Errors

1. **Missing File References**: If `mv` commands fail, the build will fail with clear file-not-found errors
2. **Namespace Mismatches**: Compiler errors will indicate any remaining old namespace references
3. **Package Reference Errors**: NuGet restore will fail if project references are incorrect

### Build Configuration Errors

1. **Icon Not Found**: Conditional include in Directory.Build.props handles missing icon gracefully
2. **Version Override**: Default version `0.0.0-dev` ensures local builds always work

## Testing Strategy

### Unit Testing

The existing test suite in `Oproto.Lambda.OpenApi.Tests` will be updated to:
- Use new namespaces
- Reference renamed projects
- Verify the same functionality as before

Test categories:
- Source generator output verification
- MSBuild task execution
- Attribute behavior

### Integration Testing

Manual verification steps:
1. Build solution and verify all projects compile
2. Create test NuGet package and inspect contents
3. Install package in sample project and verify auto-configuration works
4. Build sample project and verify OpenAPI spec is generated

### Property-Based Testing

Not applicable for this refactoring task. The changes are structural (renames, moves) rather than behavioral.
