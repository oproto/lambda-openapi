# Design Document: Examples Project and Documentation

## Overview

This design document outlines the technical approach for creating a comprehensive examples project demonstrating Oproto.Lambda.OpenApi usage with AWS Lambda Annotations, along with completing the documentation suite. The examples project will serve as both a learning resource and a validation that the library works correctly in real-world scenarios.

## Architecture

### Project Structure

```
Oproto.Lambda.OpenApi.sln
├── Oproto.Lambda.OpenApi/                    # Main package (existing)
├── Oproto.Lambda.OpenApi.SourceGenerator/    # Source generator (existing)
├── Oproto.Lambda.OpenApi.Build/              # MSBuild task (existing)
├── Oproto.Lambda.OpenApi.Tests/              # Tests (existing)
└── Oproto.Lambda.OpenApi.Examples/           # NEW: Example Lambda project
    ├── Functions/
    │   └── ProductFunctions.cs               # CRUD operations
    ├── Models/
    │   ├── Product.cs                        # Main model
    │   ├── CreateProductRequest.cs           # Request model
    │   └── UpdateProductRequest.cs           # Request model
    └── Oproto.Lambda.OpenApi.Examples.csproj
```

### Documentation Structure

```
docs/
├── getting-started.md    # Existing (update links)
├── attributes.md         # NEW: Complete attribute reference
└── configuration.md      # NEW: Configuration options
```

## Components and Interfaces

### 1. Examples Project (Oproto.Lambda.OpenApi.Examples)

**Purpose**: Demonstrate all library features in a realistic Lambda API scenario.

**Project Configuration**:
```xml
<Project Sdk="Microsoft.NET.Sdk">
  <PropertyGroup>
    <TargetFramework>net8.0</TargetFramework>
    <ImplicitUsings>enable</ImplicitUsings>
    <Nullable>enable</Nullable>
    <GenerateRuntimeConfigurationFiles>true</GenerateRuntimeConfigurationFiles>
    <AWSProjectType>Lambda</AWSProjectType>
  </PropertyGroup>

  <ItemGroup>
    <PackageReference Include="Amazon.Lambda.Annotations" Version="1.5.0" />
    <PackageReference Include="Amazon.Lambda.APIGatewayEvents" Version="2.7.0" />
    <PackageReference Include="Amazon.Lambda.Core" Version="2.2.0" />
    <PackageReference Include="Amazon.Lambda.Serialization.SystemTextJson" Version="2.4.0" />
  </ItemGroup>

  <ItemGroup>
    <ProjectReference Include="..\Oproto.Lambda.OpenApi\Oproto.Lambda.OpenApi.csproj" />
  </ItemGroup>
</Project>
```

### 2. ProductFunctions Class

**Purpose**: Demonstrate CRUD operations with all OpenAPI attributes.

```csharp
[GenerateOpenApiSpec("Products API", "1.0")]
public class ProductFunctions
{
    [LambdaFunction]
    [HttpApi(LambdaHttpMethod.Get, "/products")]
    [OpenApiOperation(Summary = "List all products", Description = "...")]
    [OpenApiTag("Products")]
    public Task<IEnumerable<Product>> GetProducts([FromQuery] int? limit, [FromQuery] string? category);

    [LambdaFunction]
    [HttpApi(LambdaHttpMethod.Get, "/products/{id}")]
    [OpenApiOperation(Summary = "Get product by ID", Description = "...")]
    [OpenApiTag("Products")]
    public Task<Product> GetProduct([FromRoute] string id);

    [LambdaFunction]
    [HttpApi(LambdaHttpMethod.Post, "/products")]
    [OpenApiOperation(Summary = "Create a product", Description = "...")]
    [OpenApiTag("Products")]
    public Task<Product> CreateProduct([FromBody] CreateProductRequest request);

    [LambdaFunction]
    [HttpApi(LambdaHttpMethod.Put, "/products/{id}")]
    [OpenApiOperation(Summary = "Update a product", Description = "...")]
    [OpenApiTag("Products")]
    public Task<Product> UpdateProduct([FromRoute] string id, [FromBody] UpdateProductRequest request);

    [LambdaFunction]
    [HttpApi(LambdaHttpMethod.Delete, "/products/{id}")]
    [OpenApiOperation(Summary = "Delete a product", Description = "...")]
    [OpenApiTag("Products")]
    public Task DeleteProduct([FromRoute] string id);
}
```

### 3. Model Classes

**Product.cs**:
```csharp
/// <summary>
/// Represents a product in the catalog.
/// </summary>
public class Product
{
    [OpenApiSchema(Description = "Unique product identifier", Format = "uuid")]
    public string Id { get; set; }

    [OpenApiSchema(Description = "Product name", MinLength = 1, MaxLength = 200)]
    public string Name { get; set; }

    [OpenApiSchema(Description = "Product price in USD", Minimum = 0)]
    public decimal Price { get; set; }

    [OpenApiSchema(Description = "Product category")]
    public string Category { get; set; }

    [OpenApiIgnore]
    public DateTime InternalCreatedAt { get; set; }
}
```

**CreateProductRequest.cs**:
```csharp
/// <summary>
/// Request model for creating a new product.
/// </summary>
public class CreateProductRequest
{
    [OpenApiSchema(Description = "Product name", MinLength = 1, MaxLength = 200, Example = "Widget Pro")]
    public string Name { get; set; }

    [OpenApiSchema(Description = "Product price in USD", Minimum = 0.01, Example = "29.99")]
    public decimal Price { get; set; }

    [OpenApiSchema(Description = "Product category", Example = "Electronics")]
    public string Category { get; set; }
}
```

## Data Models

### Documentation File Structure

**attributes.md sections**:
1. Overview
2. GenerateOpenApiSpecAttribute
3. OpenApiOperationAttribute
4. OpenApiTagAttribute
5. OpenApiSchemaAttribute
6. OpenApiIgnoreAttribute
7. OpenApiOutputAttribute

**configuration.md sections**:
1. Overview
2. MSBuild Properties
3. Output Path Configuration
4. Disabling Generation
5. Troubleshooting

## Correctness Properties

*A property is a characteristic or behavior that should hold true across all valid executions of a system-essentially, a formal statement about what the system should do. Properties serve as the bridge between human-readable specifications and machine-verifiable correctness guarantees.*

Based on the prework analysis, most acceptance criteria are example-based tests verifying specific structural requirements. The examples project is a demonstration/documentation project rather than a library with algorithmic behavior, so property-based testing is not applicable.

**Verification Approach**:
- Build verification: The examples project compiles successfully
- OpenAPI generation: The build produces a valid openapi.json file
- Documentation links: All cross-references resolve to existing files

Since this feature involves creating documentation and example code rather than implementing testable business logic, correctness will be verified through:
1. Successful compilation of the examples project
2. Generation of openapi.json during build
3. Manual review of documentation completeness

No property-based tests are applicable for this feature.

## Error Handling

### Build Errors

1. **Missing Package References**: Clear NuGet restore errors if Lambda Annotations packages are unavailable
2. **OpenAPI Generation Failures**: Build warnings/errors from the source generator if attributes are misconfigured

### Documentation Errors

1. **Broken Links**: Relative paths ensure links work in both GitHub and local viewing
2. **Missing Files**: Getting-started.md links will fail until attributes.md and configuration.md are created

## Testing Strategy

### Unit Testing

No unit tests are required for this feature as it consists of:
- Example code (demonstration, not library code)
- Documentation files (markdown)

### Integration Testing

The examples project serves as an integration test:
1. Build the examples project to verify compilation
2. Verify openapi.json is generated
3. Optionally validate the generated OpenAPI spec against the OpenAPI 3.0 schema

### Property-Based Testing

Not applicable for this feature. The examples project demonstrates library usage rather than implementing testable algorithms. Documentation files are static content.

### Manual Verification

- Review generated openapi.json for correctness
- Verify all documentation links resolve
- Review attribute documentation for completeness

