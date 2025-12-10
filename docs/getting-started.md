# Getting Started with Oproto Lambda OpenAPI

This guide will help you get started with Oproto Lambda OpenAPI in your AWS Lambda projects.

## Prerequisites

- .NET 6.0 or later
- AWS Lambda Annotations package
- An AWS Lambda project

## Installation

Install the NuGet package in your AWS Lambda project:

```bash
dotnet add package Oproto.Lambda.OpenApi
```

## Basic Usage

### 1. Decorate Your Lambda Functions

Add OpenAPI attributes to your Lambda functions:

```csharp
using Amazon.Lambda.Annotations;
using Oproto.Lambda.OpenApi.Attributes;

[LambdaFunction]
[OpenApiOperation("GetUser", "Retrieves user information by ID")]
[OpenApiTag("Users")]
public async Task<APIGatewayProxyResponse> GetUser(
    [FromRoute] string userId,
    [FromQuery] bool includeDetails = false)
{
    // Your implementation
    return new APIGatewayProxyResponse
    {
        StatusCode = 200,
        Body = JsonSerializer.Serialize(new { UserId = userId, Details = includeDetails })
    };
}
```

### 2. Build Your Project

When you build your project, the OpenAPI specification will be automatically generated:

```bash
dotnet build
```

This creates an `openapi.json` file in your project directory.

### 3. View the Generated Specification

The generated OpenAPI specification includes:
- Endpoint definitions
- Parameter schemas
- Response schemas
- Tags and descriptions

## Available Attributes

### Assembly-Level Attributes

Configure your API at the assembly level (typically in `AssemblyInfo.cs` or any `.cs` file):

- `[OpenApiInfo]` - Sets API title, version, and metadata
- `[OpenApiSecurityScheme]` - Defines security schemes (API Key, OAuth2, etc.)

```csharp
[assembly: OpenApiInfo("My API", "1.0.0", Description = "API for managing resources")]
```

### Method-Level Attributes

All attributes are located in the `Oproto.Lambda.OpenApi.Attributes` namespace:

- `[OpenApiOperation]` - Defines operation metadata (summary, description, deprecated)
- `[OpenApiTag]` - Groups operations by tags
- `[OpenApiResponseType]` - Explicitly documents response types (useful for `IHttpResult` returns)

### Property/Parameter Attributes

- `[OpenApiSchema]` - Customizes type schemas (format, validation, examples)
- `[OpenApiIgnore]` - Excludes properties from schemas

### Parameter Attributes

- `[FromRoute]` - Path parameters
- `[FromQuery]` - Query parameters
- `[FromHeader]` - Header parameters
- `[FromBody]` - Request body (JSON)

## Important Behaviors

### FromServices Parameters Are Excluded

Parameters decorated with `[FromServices]` are automatically excluded from the OpenAPI specification. These are dependency injection parameters that are not part of the HTTP API contract:

```csharp
[LambdaFunction]
[HttpApi(LambdaHttpMethod.Get, "/products")]
public async Task<IEnumerable<Product>> GetProducts(
    [FromServices] IProductService productService,  // Excluded from OpenAPI
    [FromQuery] int limit = 10)                     // Included in OpenAPI
{
    return await productService.GetProducts(limit);
}
```

### API Gateway Integration Extension

The generated OpenAPI specification includes the `x-amazon-apigateway-integration` extension for each operation. This extension is required for deploying to AWS API Gateway:

```json
{
  "x-amazon-apigateway-integration": {
    "type": "aws_proxy",
    "httpMethod": "POST",
    "uri": "${LambdaFunctionArn}",
    "payloadFormatVersion": "2.0"
  }
}
```

The `payloadFormatVersion` is automatically set based on the API type:
- `2.0` for HTTP APIs (`[HttpApi]`)
- `1.0` for REST APIs (`[RestApi]`)

### Async Return Types

The generator automatically unwraps `Task<T>` and `ValueTask<T>` return types. For example:

```csharp
// This method returns Task<Product>
public async Task<Product> GetProduct(string id) { ... }

// The OpenAPI response schema will be for Product, not Task<Product>
```

Methods returning non-generic `Task` or `ValueTask` generate a `204 No Content` response.

### IHttpResult Return Types

When your Lambda functions return `IHttpResult` (from Lambda Annotations), the generator cannot infer the actual response type. Use `[OpenApiResponseType]` to explicitly document responses:

```csharp
[LambdaFunction]
[HttpApi(LambdaHttpMethod.Get, "/products/{id}")]
[OpenApiResponseType(typeof(Product), 200, Description = "Returns the product")]
[OpenApiResponseType(typeof(ErrorResponse), 404, Description = "Product not found")]
public async Task<IHttpResult> GetProduct(string id)
{
    var product = await _service.GetProduct(id);
    if (product == null)
        return HttpResults.NotFound(new ErrorResponse { Message = "Not found" });
    return HttpResults.Ok(product);
}
```

## AOT Compatibility

For Native AOT builds, the standard reflection-based extraction may not work. To enable AOT-compatible extraction:

1. Enable compiler-generated files in your project:

```xml
<PropertyGroup>
  <EmitCompilerGeneratedFiles>true</EmitCompilerGeneratedFiles>
</PropertyGroup>
```

2. The build task will automatically parse the generated source file instead of using reflection.

## Security Schemes

Security schemes are only added to the OpenAPI specification when you define them using assembly-level attributes:

```csharp
// In your project (e.g., AssemblyInfo.cs)
[assembly: OpenApiSecurityScheme("apiKey",
    Type = OpenApiSecuritySchemeType.ApiKey,
    ApiKeyName = "x-api-key",
    ApiKeyLocation = ApiKeyLocation.Header)]
```

See the [Attribute Reference](attributes.md#openapisecurityschemeattribute) for more details.

## Next Steps

- [Attribute Reference](attributes.md) - Complete attribute documentation
- [Configuration Options](configuration.md) - Advanced configuration
- [Examples](../Oproto.Lambda.OpenApi.Examples/) - Working example project with CRUD operations
