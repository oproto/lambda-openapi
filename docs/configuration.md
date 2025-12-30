# Configuration Options

This guide explains how to configure Oproto Lambda OpenAPI generation behavior in your project.

## Overview

Oproto Lambda OpenAPI uses MSBuild properties and targets to control the OpenAPI specification generation process. Configuration is done through your project file (`.csproj`) and allows you to customize output paths, disable generation for specific builds, and fine-tune the generation behavior.

## MSBuild Properties

The following MSBuild properties can be set in your project file to customize OpenAPI generation:

### Core Properties

| Property | Description | Default |
|----------|-------------|---------|
| `OpenApiOutputPath` | The file path where the generated OpenAPI specification will be written | `$(ProjectDir)openapi.json` |
| `EmitCompilerGeneratedFiles` | When set to `true`, outputs the source generator files to disk for debugging | `false` |
| `CompilerGeneratedFilesOutputPath` | Directory where generated source files are written when `EmitCompilerGeneratedFiles` is enabled | `$(BaseIntermediateOutputPath)\GeneratedFiles` |

### Example Configuration

```xml
<Project Sdk="Microsoft.NET.Sdk">
  <PropertyGroup>
    <TargetFramework>net8.0</TargetFramework>
    
    <!-- Enable generated file output for debugging -->
    <EmitCompilerGeneratedFiles>true</EmitCompilerGeneratedFiles>
    <CompilerGeneratedFilesOutputPath>$(BaseIntermediateOutputPath)\GeneratedFiles</CompilerGeneratedFilesOutputPath>
  </PropertyGroup>

  <ItemGroup>
    <PackageReference Include="Oproto.Lambda.OpenApi" Version="1.0.0" />
  </ItemGroup>
</Project>
```

## Output Path Configuration

By default, the OpenAPI specification is generated to `openapi.json` in your project directory. You can customize this location by modifying the MSBuild target.

### Customizing Output Location

To change the output path, you can override the `GenerateOpenApi` target in your project file:

```xml
<Project Sdk="Microsoft.NET.Sdk">
  <PropertyGroup>
    <TargetFramework>net8.0</TargetFramework>
  </PropertyGroup>

  <ItemGroup>
    <PackageReference Include="Oproto.Lambda.OpenApi" Version="1.0.0" />
  </ItemGroup>

  <!-- Override the default output path -->
  <Target Name="CustomOpenApiOutput" AfterTargets="GenerateOpenApi">
    <Copy SourceFiles="$(ProjectDir)openapi.json" 
          DestinationFiles="$(ProjectDir)docs\api\openapi.json" 
          SkipUnchangedFiles="true" />
  </Target>
</Project>
```

### Output to Multiple Locations

If you need the specification in multiple locations (e.g., for deployment and documentation):

```xml
<Target Name="CopyOpenApiToMultipleLocations" AfterTargets="GenerateOpenApi">
  <ItemGroup>
    <OpenApiDestinations Include="$(ProjectDir)docs\openapi.json" />
    <OpenApiDestinations Include="$(SolutionDir)api-docs\$(ProjectName).json" />
  </ItemGroup>
  
  <Copy SourceFiles="$(ProjectDir)openapi.json" 
        DestinationFiles="@(OpenApiDestinations)" 
        SkipUnchangedFiles="true" />
</Target>
```

## Disabling Automatic Generation

There are several ways to disable OpenAPI generation for specific scenarios.

### Disable for Specific Configurations

To disable generation only for Release builds:

```xml
<PropertyGroup Condition="'$(Configuration)' == 'Release'">
  <OpenApiGenerationEnabled>false</OpenApiGenerationEnabled>
</PropertyGroup>

<Target Name="GenerateOpenApi" AfterTargets="Build" 
        Condition="'$(OpenApiGenerationEnabled)' != 'false'">
  <!-- Generation logic -->
</Target>
```

### Disable Completely

To completely disable OpenAPI generation for a project, you can remove the target:

```xml
<Target Name="GenerateOpenApi" />
```

Or conditionally skip it:

```xml
<PropertyGroup>
  <SkipOpenApiGeneration>true</SkipOpenApiGeneration>
</PropertyGroup>
```

### Disable via Command Line

You can also disable generation when building from the command line:

```bash
dotnet build -p:SkipOpenApiGeneration=true
```

### Conditional Generation Based on Environment

```xml
<Target Name="GenerateOpenApi" AfterTargets="Build" 
        Condition="'$(CI)' != 'true'">
  <!-- Only generate locally, not in CI -->
</Target>
```

## AOT (Ahead-of-Time) Compilation Support

Oproto Lambda OpenAPI fully supports Native AOT builds without any special configuration. The OpenAPI specification is extracted during the Build phase (before AOT compilation), so it works seamlessly with AOT projects.

### How It Works

The build task runs `AfterTargets="Build"`, which executes before AOT publishing:

1. **Build** → IL assembly created with attributes → **OpenAPI extraction runs** → `openapi.json` generated
2. **Publish** → AOT compiles IL to native binary

The extraction uses `MetadataLoadContext` to read assembly metadata without loading dependencies, making it compatible with any project configuration.

### AOT Build Configuration Example

No special configuration is required for AOT projects:

```xml
<Project Sdk="Microsoft.NET.Sdk">
  <PropertyGroup>
    <TargetFramework>net8.0</TargetFramework>
    <PublishAot>true</PublishAot>
  </PropertyGroup>

  <ItemGroup>
    <PackageReference Include="Oproto.Lambda.OpenApi" Version="1.0.0" />
  </ItemGroup>
</Project>
```

### Optional: Emitting Generated Files for Debugging

If you want to inspect the generated source code for debugging purposes, you can optionally enable compiler-generated file output:

```xml
<PropertyGroup>
  <EmitCompilerGeneratedFiles>true</EmitCompilerGeneratedFiles>
</PropertyGroup>
```

This is not required for OpenAPI generation to work - it's purely for debugging the source generator output.

## Debugging Generated Files

When troubleshooting generation issues, enable compiler-generated file output:

```xml
<PropertyGroup>
  <EmitCompilerGeneratedFiles>true</EmitCompilerGeneratedFiles>
  <CompilerGeneratedFilesOutputPath>$(BaseIntermediateOutputPath)\GeneratedFiles</CompilerGeneratedFilesOutputPath>
</PropertyGroup>
```

Generated files will appear in:
- `obj/GeneratedFiles/Oproto.Lambda.OpenApi.SourceGenerator/`

## Type Mappings

The source generator automatically maps .NET types to OpenAPI schema types. The following table shows the supported type mappings:

### Primitive Types

| .NET Type | OpenAPI Type | OpenAPI Format |
|-----------|--------------|----------------|
| `string` | `string` | - |
| `int`, `Int32` | `integer` | `int32` |
| `long`, `Int64` | `integer` | `int64` |
| `short`, `Int16` | `integer` | `int32` |
| `byte` | `integer` | `int32` |
| `float`, `Single` | `number` | `float` |
| `double`, `Double` | `number` | `double` |
| `decimal`, `Decimal` | `number` | `double` |
| `bool`, `Boolean` | `boolean` | - |

### Date and Time Types

| .NET Type | OpenAPI Type | OpenAPI Format | Notes |
|-----------|--------------|----------------|-------|
| `DateTime` | `string` | `date-time` | ISO 8601 format |
| `DateTimeOffset` | `string` | `date-time` | ISO 8601 format with timezone |
| `DateOnly` | `string` | `date` | .NET 6+ date without time (e.g., `2024-12-30`) |
| `TimeOnly` | `string` | `time` | .NET 6+ time without date (e.g., `14:30:00`) |
| `TimeSpan` | `string` | `duration` | ISO 8601 duration format |

### Other Common Types

| .NET Type | OpenAPI Type | OpenAPI Format |
|-----------|--------------|----------------|
| `Guid` | `string` | `uuid` |
| `Uri` | `string` | `uri` |
| `byte[]` | `string` | `byte` |

### Nullable Types

All nullable variants (e.g., `int?`, `DateTime?`, `DateOnly?`, `TimeOnly?`) are mapped to the same OpenAPI type and format as their non-nullable counterparts, with `nullable: true` added to the schema.

### Collection Types

| .NET Type | OpenAPI Type |
|-----------|--------------|
| `T[]`, `List<T>`, `IEnumerable<T>`, `ICollection<T>` | `array` with `items` schema for `T` |
| `Dictionary<string, T>` | `object` with `additionalProperties` schema for `T` |

### Complex Types

Any type not listed above is treated as a complex object type and generates a schema in the `components/schemas` section with properties for each public property of the type.

### Example: DateOnly and TimeOnly Usage

```csharp
public class Appointment
{
    [OpenApiSchema(Description = "The date of the appointment")]
    public DateOnly Date { get; set; }
    
    [OpenApiSchema(Description = "The start time of the appointment")]
    public TimeOnly StartTime { get; set; }
    
    [OpenApiSchema(Description = "The end time of the appointment (optional)")]
    public TimeOnly? EndTime { get; set; }
}
```

This generates the following OpenAPI schema:

```json
{
  "Appointment": {
    "type": "object",
    "properties": {
      "date": {
        "type": "string",
        "format": "date",
        "description": "The date of the appointment"
      },
      "startTime": {
        "type": "string",
        "format": "time",
        "description": "The start time of the appointment"
      },
      "endTime": {
        "type": "string",
        "format": "time",
        "nullable": true,
        "description": "The end time of the appointment (optional)"
      }
    }
  }
}
```

## Troubleshooting

### Common Issues

#### OpenAPI file not generated

**Symptoms:** Build succeeds but no `openapi.json` file is created.

**Possible causes and solutions:**

1. **No `[GenerateOpenApiSpec]` attribute on class**
   - Ensure at least one class has the `[GenerateOpenApiSpec]` attribute
   - The attribute should be on a class containing Lambda functions

2. **No Lambda functions detected**
   - Verify methods have both `[LambdaFunction]` and `[HttpApi]` or `[RestApi]` attributes
   - Check that Amazon.Lambda.Annotations package is referenced

3. **Build task not running**
   - Verify the package reference is correct
   - Check build output for warnings or errors

#### Generated spec is empty or minimal

**Symptoms:** The `openapi.json` file exists but contains no endpoints.

**Possible causes and solutions:**

1. **Missing HTTP method attributes**
   - Each Lambda function needs `[HttpApi]` or `[RestApi]` attribute with method and route

2. **Incorrect attribute usage**
   ```csharp
   // Correct
   [LambdaFunction]
   [HttpApi(LambdaHttpMethod.Get, "/products")]
   public Task<Product[]> GetProducts() { }
   
   // Incorrect - missing HttpApi
   [LambdaFunction]
   public Task<Product[]> GetProducts() { }
   ```

#### Schema not generated for models

**Symptoms:** Request/response models appear as empty objects in the spec.

**Possible causes and solutions:**

1. **Properties are not public**
   - Ensure model properties have public getters

2. **Properties marked with `[OpenApiIgnore]`**
   - Check if properties are accidentally ignored

3. **Missing XML documentation**
   - While not required, XML docs improve schema descriptions

#### Build errors related to source generator

**Symptoms:** Build fails with errors mentioning the source generator.

**Possible causes and solutions:**

1. **Version conflicts**
   - Ensure compatible versions of all packages
   - Try cleaning and rebuilding: `dotnet clean && dotnet build`

2. **Missing dependencies**
   - Verify all required packages are installed:
     ```xml
     <PackageReference Include="Amazon.Lambda.Annotations" Version="1.6.1" />
     <PackageReference Include="Oproto.Lambda.OpenApi" Version="1.0.0" />
     ```

3. **IDE caching issues**
   - Restart your IDE
   - Delete `obj` and `bin` folders and rebuild

### Viewing Generator Diagnostics

To see detailed output from the source generator:

1. Enable verbose MSBuild output:
   ```bash
   dotnet build -v detailed
   ```

2. Look for messages starting with "OpenAPI Generation" in the build output

3. Enable generated file output to inspect the generated code:
   ```xml
   <EmitCompilerGeneratedFiles>true</EmitCompilerGeneratedFiles>
   ```

### Getting Help

If you continue to experience issues:

1. Check the [GitHub Issues](https://github.com/oproto/lambda-openapi/issues) for known problems
2. Enable verbose logging and include the output when reporting issues
3. Include your project configuration and attribute usage in bug reports
