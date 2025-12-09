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

### Core Attributes

All attributes are located in the `Oproto.Lambda.OpenApi.Attributes` namespace:

- `[OpenApiOperation]` - Defines operation metadata
- `[OpenApiTag]` - Groups operations by tags
- `[OpenApiSchema]` - Customizes type schemas
- `[OpenApiIgnore]` - Excludes properties from schemas
- `[OpenApiOutput]` - Specifies response output types
- `[GenerateOpenApiSpec]` - Marks assembly for OpenAPI generation

### Parameter Attributes

- `[FromRoute]` - Path parameters
- `[FromQuery]` - Query parameters
- `[FromHeader]` - Header parameters
- `[FromBody]` - Request body (JSON)

## Next Steps

- [Attribute Reference](attributes.md) - Complete attribute documentation
- [Configuration Options](configuration.md) - Advanced configuration
- [Examples](../examples/) - More complex examples
