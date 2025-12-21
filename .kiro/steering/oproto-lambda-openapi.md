# Oproto.Lambda.OpenApi Reference
# Updated 2025-12-16
Source generator for automatic OpenAPI spec generation from AWS Lambda functions with Lambda Annotations.

## Installation

```bash
dotnet add package Oproto.Lambda.OpenApi
```

Requirements: .NET 6.0+, Amazon.Lambda.Annotations package.

## Quick Start

```csharp
using Amazon.Lambda.Annotations;
using Amazon.Lambda.Annotations.APIGateway;
using Oproto.Lambda.OpenApi.Attributes;

// Assembly-level API info (required)
[assembly: OpenApiInfo("My API", "1.0.0", Description = "API description")]

public class Functions
{
    [LambdaFunction]
    [HttpApi(LambdaHttpMethod.Get, "/items/{id}")]
    [OpenApiOperation(Summary = "Get item", Description = "Retrieves an item by ID")]
    [OpenApiTag("Items")]
    public Task<Item> GetItem(string id) => Task.FromResult(new Item());
}
```

Build generates `openapi.json` automatically.

---

## Assembly-Level Attributes

### OpenApiInfo (required)
```csharp
[assembly: OpenApiInfo("API Title", "1.0.0",
    Description = "API description",
    TermsOfService = "https://example.com/terms",
    ContactName = "Support",
    ContactEmail = "support@example.com",
    ContactUrl = "https://example.com/contact",
    LicenseName = "MIT",
    LicenseUrl = "https://opensource.org/licenses/MIT")]
```

### OpenApiServer
```csharp
[assembly: OpenApiServer("https://api.example.com/v1", Description = "Production")]
[assembly: OpenApiServer("https://staging.example.com/v1", Description = "Staging")]
```

### OpenApiTagDefinition
```csharp
[assembly: OpenApiTagDefinition("Products",
    Description = "Product operations",
    ExternalDocsUrl = "https://docs.example.com/products",
    ExternalDocsDescription = "Product API docs")]
```

### OpenApiExternalDocs
```csharp
[assembly: OpenApiExternalDocs("https://docs.example.com", Description = "Full documentation")]
```

### OpenApiSecurityScheme
```csharp
// API Key
[assembly: OpenApiSecurityScheme("apiKey",
    Type = OpenApiSecuritySchemeType.ApiKey,
    ApiKeyName = "x-api-key",
    ApiKeyLocation = ApiKeyLocation.Header,
    Description = "API key authentication")]

// Bearer Token
[assembly: OpenApiSecurityScheme("bearer",
    Type = OpenApiSecuritySchemeType.Http,
    HttpScheme = "bearer",
    BearerFormat = "JWT",
    Description = "JWT authentication")]

// OAuth2
[assembly: OpenApiSecurityScheme("oauth2",
    Type = OpenApiSecuritySchemeType.OAuth2,
    AuthorizationUrl = "https://auth.example.com/authorize",
    TokenUrl = "https://auth.example.com/token",
    Scopes = "read:Read access,write:Write access")]
```

### OpenApiOutput
```csharp
[assembly: OpenApiOutput("main", "openapi.json")]
```

---

## Method-Level Attributes

### OpenApiOperation
```csharp
[OpenApiOperation(
    Summary = "Short description",
    Description = "Detailed description",
    OperationId = "customOperationId",
    Deprecated = true)]
```

### OpenApiOperationId
```csharp
[OpenApiOperationId("listAllProducts")]
```

### OpenApiTag
```csharp
[OpenApiTag("Products")]
[OpenApiTag("Admin")]  // Multiple tags allowed
```

### OpenApiResponseType
```csharp
[OpenApiResponseType(typeof(Product), 200, Description = "Success")]
[OpenApiResponseType(typeof(ValidationError), 400, Description = "Validation failed")]
[OpenApiResponseType(typeof(void), 404, Description = "Not found")]
```

### OpenApiResponseHeader
```csharp
[OpenApiResponseHeader("X-Request-Id", Description = "Request identifier")]
[OpenApiResponseHeader("X-Total-Count", Type = typeof(int), StatusCode = 200, Description = "Total items")]
[OpenApiResponseHeader("X-Rate-Limit", Type = typeof(int), Required = true)]
```

### OpenApiExample
```csharp
// Request example
[OpenApiExample("Create Product", "{\"name\": \"Widget\", \"price\": 9.99}", IsRequestExample = true)]

// Response example
[OpenApiExample("Success", "{\"id\": \"123\", \"name\": \"Widget\"}", StatusCode = 200)]
[OpenApiExample("Error", "{\"error\": \"Not found\"}", StatusCode = 404)]
```

### OpenApiExternalDocs (method-level)
```csharp
[OpenApiExternalDocs("https://docs.example.com/endpoint", Description = "Endpoint guide")]
```

---

## Property/Parameter Attributes

### OpenApiSchema
```csharp
public class Product
{
    [OpenApiSchema(Description = "Product ID", Format = "uuid")]
    public string Id { get; set; }

    [OpenApiSchema(Description = "Name", MinLength = 1, MaxLength = 200, Example = "Widget")]
    public string Name { get; set; }

    [OpenApiSchema(Description = "Price", Minimum = 0, Maximum = 10000, Example = "29.99")]
    public decimal Price { get; set; }

    [OpenApiSchema(Description = "Email", Format = "email", Pattern = @"^[\w-\.]+@[\w-]+\.[\w-]{2,4}$")]
    public string Email { get; set; }
}
```

Properties: `Description`, `Format`, `Example`, `Pattern`, `Minimum`, `Maximum`, `ExclusiveMinimum`, `ExclusiveMaximum`, `MinLength`, `MaxLength`

### OpenApiIgnore
```csharp
[OpenApiIgnore]
public DateTime InternalTimestamp { get; set; }
```

---

## Complete Example

```csharp
using Amazon.Lambda.Annotations;
using Amazon.Lambda.Annotations.APIGateway;
using Amazon.Lambda.Core;
using Amazon.Lambda.Serialization.SystemTextJson;
using Oproto.Lambda.OpenApi.Attributes;

[assembly: LambdaSerializer(typeof(DefaultLambdaJsonSerializer))]
[assembly: OpenApiInfo("Products API", "1.0", Description = "Product catalog API")]
[assembly: OpenApiServer("https://api.example.com/v1", Description = "Production")]
[assembly: OpenApiTagDefinition("Products", Description = "Product operations")]
[assembly: OpenApiExternalDocs("https://docs.example.com", Description = "Full docs")]

namespace MyApi;

public class Product
{
    [OpenApiSchema(Description = "Product ID")]
    public string Id { get; set; }

    [OpenApiSchema(Description = "Name", MinLength = 1, MaxLength = 200)]
    public string Name { get; set; }

    [OpenApiSchema(Description = "Price in USD", Minimum = 0)]
    public decimal Price { get; set; }

    [OpenApiIgnore]
    public DateTime CreatedAt { get; set; }
}

public class CreateProductRequest
{
    [OpenApiSchema(Description = "Name", MinLength = 1, MaxLength = 200, Example = "Widget")]
    public string Name { get; set; }

    [OpenApiSchema(Description = "Price", Minimum = 0.01, Example = "29.99")]
    public decimal Price { get; set; }
}

public class ProductFunctions
{
    [LambdaFunction]
    [HttpApi(LambdaHttpMethod.Get, "/products")]
    [OpenApiOperation(Summary = "List products", Description = "Get all products")]
    [OpenApiTag("Products")]
    [OpenApiOperationId("listProducts")]
    [OpenApiResponseHeader("X-Total-Count", Type = typeof(int), Description = "Total count")]
    [OpenApiExample("Products", "[{\"id\":\"1\",\"name\":\"Widget\",\"price\":9.99}]", StatusCode = 200)]
    public Task<IEnumerable<Product>> GetProducts([FromQuery] int limit = 100)
    {
        return Task.FromResult<IEnumerable<Product>>(new List<Product>());
    }

    [LambdaFunction]
    [HttpApi(LambdaHttpMethod.Get, "/products/{id}")]
    [OpenApiOperation(Summary = "Get product", Description = "Get product by ID")]
    [OpenApiTag("Products")]
    [OpenApiOperationId("getProduct")]
    [OpenApiResponseType(typeof(Product), 200, Description = "Product found")]
    [OpenApiResponseType(typeof(void), 404, Description = "Not found")]
    public Task<Product> GetProduct(string id)
    {
        return Task.FromResult(new Product { Id = id });
    }

    [LambdaFunction]
    [HttpApi(LambdaHttpMethod.Post, "/products")]
    [OpenApiOperation(Summary = "Create product")]
    [OpenApiTag("Products")]
    [OpenApiOperationId("createProduct")]
    [OpenApiExample("Request", "{\"name\":\"Widget\",\"price\":9.99}", IsRequestExample = true)]
    [OpenApiExample("Response", "{\"id\":\"123\",\"name\":\"Widget\",\"price\":9.99}", StatusCode = 200)]
    [OpenApiResponseHeader("Location", Description = "Created resource URL")]
    public Task<Product> CreateProduct([FromBody] CreateProductRequest request)
    {
        return Task.FromResult(new Product { Id = "123", Name = request.Name, Price = request.Price });
    }

    [LambdaFunction]
    [HttpApi(LambdaHttpMethod.Delete, "/products/{id}")]
    [OpenApiOperation(Summary = "Delete product", Deprecated = true)]
    [OpenApiTag("Products")]
    [Obsolete("Use archive endpoint instead")]
    public Task DeleteProduct(string id)
    {
        return Task.CompletedTask;
    }
}
```

---

## Attribute Quick Reference

| Attribute | Target | Purpose |
|-----------|--------|---------|
| `OpenApiInfo` | Assembly | API title, version, contact info |
| `OpenApiServer` | Assembly | Server URLs |
| `OpenApiTagDefinition` | Assembly | Tag metadata |
| `OpenApiExternalDocs` | Assembly/Method | External documentation links |
| `OpenApiSecurityScheme` | Assembly | Security definitions |
| `OpenApiOutput` | Assembly | Output file configuration |
| `OpenApiOperation` | Method | Operation summary, description, deprecation |
| `OpenApiOperationId` | Method | Custom operation ID |
| `OpenApiTag` | Class/Method | Tag assignment |
| `OpenApiResponseType` | Method | Response type per status code |
| `OpenApiResponseHeader` | Method | Response headers |
| `OpenApiExample` | Method | Request/response examples |
| `OpenApiSchema` | Property/Parameter | Schema constraints and metadata |
| `OpenApiIgnore` | Property/Parameter | Exclude from documentation |

---

## Security Scheme Types

| Type | Use Case |
|------|----------|
| `ApiKey` | Header, query, or cookie API keys |
| `Http` | Basic or Bearer authentication |
| `OAuth2` | OAuth 2.0 flows |
| `OpenIdConnect` | OpenID Connect discovery |

## ApiKeyLocation Values

| Value | Description |
|-------|-------------|
| `Header` | API key in request header |
| `Query` | API key as query parameter |
| `Cookie` | API key in cookie |
