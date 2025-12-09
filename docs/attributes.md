# Attribute Reference

This document provides a comprehensive reference for all attributes available in the Oproto.Lambda.OpenApi library. These attributes allow you to customize the generated OpenAPI specification for your AWS Lambda functions.

## Table of Contents

- [GenerateOpenApiSpecAttribute](#generateopenapispecattribute)
- [OpenApiOperationAttribute](#openapioperationattribute)
- [OpenApiTagAttribute](#openapitagattribute)
- [OpenApiSchemaAttribute](#openapischemattribute)
- [OpenApiIgnoreAttribute](#openapiignoreattribute)
- [OpenApiOutputAttribute](#openapioutputattribute)

---

## GenerateOpenApiSpecAttribute

Marks a class for OpenAPI specification generation. Apply this attribute to Lambda function classes to include them in the generated OpenAPI specification.

**Namespace:** `Oproto.Lambda.OpenApi.Attributes`

**Target Types:** `Class`

### Constructor Parameters

| Parameter | Type | Required | Default | Description |
|-----------|------|----------|---------|-------------|
| `serviceName` | `string` | Yes | - | The name of the service for the OpenAPI specification. This appears as the API title. |
| `version` | `string` | No | `"1.0"` | The version of the API specification. |

### Properties

| Property | Type | Description |
|----------|------|-------------|
| `ServiceName` | `string` | Gets the name of the service for the OpenAPI specification. |
| `Version` | `string` | Gets the version of the API specification. |

### When to Use

Use this attribute on any class containing Lambda functions that you want to document in your OpenAPI specification. This is typically applied to classes that contain methods decorated with `[LambdaFunction]` and `[HttpApi]` attributes.

### Usage Example

```csharp
using Oproto.Lambda.OpenApi.Attributes;
using Amazon.Lambda.Annotations;
using Amazon.Lambda.Annotations.APIGateway;

[GenerateOpenApiSpec("Products API", "1.0")]
public class ProductFunctions
{
    [LambdaFunction]
    [HttpApi(LambdaHttpMethod.Get, "/products")]
    public Task<IEnumerable<Product>> GetProducts()
    {
        // Implementation
    }
}
```

---

## OpenApiOperationAttribute

Provides additional OpenAPI operation information for methods. Use this attribute to add metadata to API operations such as summary, description, and deprecation status.

**Namespace:** `Oproto.Lambda.OpenApi.Attributes`

**Target Types:** `Method`

### Properties

| Property | Type | Required | Default | Description |
|----------|------|----------|---------|-------------|
| `Summary` | `string` | No | `null` | A short summary of what the operation does. Displayed as the operation title in documentation. |
| `Description` | `string` | No | `null` | A detailed explanation of the operation behavior. Supports markdown formatting. |
| `Deprecated` | `bool` | No | `false` | Indicates whether the operation is deprecated. |

### When to Use

Apply this attribute to Lambda function methods to provide human-readable documentation that will appear in the generated OpenAPI specification. The summary appears as a brief title, while the description can contain more detailed information about the operation's behavior, expected inputs, and outputs.

### Usage Example

```csharp
[LambdaFunction]
[HttpApi(LambdaHttpMethod.Get, "/products/{id}")]
[OpenApiOperation(
    Summary = "Get product by ID",
    Description = "Retrieves a single product by its unique identifier. Returns 404 if the product is not found.")]
public Task<Product> GetProduct(string id)
{
    // Implementation
}

// Example with deprecated operation
[LambdaFunction]
[HttpApi(LambdaHttpMethod.Get, "/products/legacy")]
[OpenApiOperation(
    Summary = "Legacy product list",
    Description = "This endpoint is deprecated. Use GET /products instead.",
    Deprecated = true)]
public Task<IEnumerable<Product>> GetProductsLegacy()
{
    // Implementation
}
```

---

## OpenApiTagAttribute

Specifies OpenAPI tag information for grouping operations. Tags can be used to group operations by resources or any other qualifier in the generated documentation.

**Namespace:** `Oproto.Lambda.OpenApi.Attributes`

**Target Types:** `Class`, `Method`, `Parameter`, `Property`

### Constructor Parameters

| Parameter | Type | Required | Default | Description |
|-----------|------|----------|---------|-------------|
| `tag` | `string` | Yes | - | The tag name used for grouping operations. |
| `description` | `string` | No | `null` | Optional description of the tag. |

### Properties

| Property | Type | Description |
|----------|------|-------------|
| `Tag` | `string` | Gets the tag name used for grouping operations. |
| `Description` | `string` | Gets the description of the tag. |

### When to Use

Use this attribute to organize your API operations into logical groups. When applied to a class, all methods in that class inherit the tag. When applied to a method, it overrides or adds to the class-level tag. Tags help API consumers navigate large APIs by grouping related operations together.

### Usage Example

```csharp
// Apply tag at class level - all methods inherit this tag
[GenerateOpenApiSpec("E-Commerce API", "1.0")]
[OpenApiTag("Products", "Operations for managing products in the catalog")]
public class ProductFunctions
{
    [LambdaFunction]
    [HttpApi(LambdaHttpMethod.Get, "/products")]
    public Task<IEnumerable<Product>> GetProducts() { }

    [LambdaFunction]
    [HttpApi(LambdaHttpMethod.Post, "/products")]
    public Task<Product> CreateProduct([FromBody] CreateProductRequest request) { }
}

// Apply tag at method level
[GenerateOpenApiSpec("E-Commerce API", "1.0")]
public class OrderFunctions
{
    [LambdaFunction]
    [HttpApi(LambdaHttpMethod.Get, "/orders")]
    [OpenApiTag("Orders", "Operations for managing customer orders")]
    public Task<IEnumerable<Order>> GetOrders() { }

    [LambdaFunction]
    [HttpApi(LambdaHttpMethod.Get, "/orders/{id}/items")]
    [OpenApiTag("Order Items")]
    public Task<IEnumerable<OrderItem>> GetOrderItems(string id) { }
}
```

---

## OpenApiSchemaAttribute

Provides additional OpenAPI schema information for properties and parameters. Use this attribute to specify validation rules, formats, and examples for schema generation.

**Namespace:** `Oproto.Lambda.OpenApi.Attributes`

**Target Types:** `Property`, `Parameter`

### Properties

| Property | Type | Required | Default | Description |
|----------|------|----------|---------|-------------|
| `Description` | `string` | No | `null` | The description of the schema property. |
| `Format` | `string` | No | `null` | The format of the schema property (e.g., `"date-time"`, `"email"`, `"uuid"`, `"uri"`). |
| `Example` | `string` | No | `null` | An example value for the schema property. |
| `Pattern` | `string` | No | `null` | A regular expression pattern that the value must match. |
| `Minimum` | `double` | No | `0` | The minimum value for numeric properties. |
| `Maximum` | `double` | No | `0` | The maximum value for numeric properties. |
| `ExclusiveMinimum` | `bool` | No | `false` | Whether the minimum value is exclusive. |
| `ExclusiveMaximum` | `bool` | No | `false` | Whether the maximum value is exclusive. |
| `MinLength` | `int` | No | `0` | The minimum length for string properties. |
| `MaxLength` | `int` | No | `0` | The maximum length for string properties. |

### When to Use

Apply this attribute to model properties or method parameters to provide detailed schema information in the OpenAPI specification. This helps API consumers understand the expected format, constraints, and examples for each field.

### Usage Examples

**Basic property documentation:**

```csharp
public class Product
{
    [OpenApiSchema(Description = "Unique product identifier", Format = "uuid")]
    public string Id { get; set; }

    [OpenApiSchema(Description = "Product name", MinLength = 1, MaxLength = 200)]
    public string Name { get; set; }

    [OpenApiSchema(Description = "Product price in USD", Minimum = 0)]
    public decimal Price { get; set; }
}
```

**With examples for request models:**

```csharp
public class CreateProductRequest
{
    [OpenApiSchema(
        Description = "Product name",
        MinLength = 1,
        MaxLength = 200,
        Example = "Widget Pro")]
    public string Name { get; set; }

    [OpenApiSchema(
        Description = "Product price in USD",
        Minimum = 0.01,
        Example = "29.99")]
    public decimal Price { get; set; }

    [OpenApiSchema(
        Description = "Product category",
        Example = "Electronics")]
    public string Category { get; set; }
}
```

**With pattern validation:**

```csharp
public class User
{
    [OpenApiSchema(
        Description = "User email address",
        Format = "email",
        Pattern = @"^[a-zA-Z0-9._%+-]+@[a-zA-Z0-9.-]+\.[a-zA-Z]{2,}$")]
    public string Email { get; set; }

    [OpenApiSchema(
        Description = "Phone number in E.164 format",
        Pattern = @"^\+[1-9]\d{1,14}$",
        Example = "+14155551234")]
    public string PhoneNumber { get; set; }
}
```

**With numeric constraints:**

```csharp
public class PaginationRequest
{
    [OpenApiSchema(
        Description = "Page number (1-based)",
        Minimum = 1,
        Example = "1")]
    public int Page { get; set; }

    [OpenApiSchema(
        Description = "Number of items per page",
        Minimum = 1,
        Maximum = 100,
        Example = "20")]
    public int PageSize { get; set; }
}
```

---

## OpenApiIgnoreAttribute

Indicates that a property or parameter should be excluded from OpenAPI documentation. Use this attribute to hide internal or implementation-specific members from the API documentation.

**Namespace:** `Oproto.Lambda.OpenApi.Attributes`

**Target Types:** `Parameter`, `Property`

### When to Use

Apply this attribute to properties or parameters that should not appear in the generated OpenAPI specification. This is useful for:

- Internal tracking fields (e.g., creation timestamps, audit fields)
- Implementation details that are not part of the public API contract
- Properties populated by the system rather than the API consumer
- Sensitive fields that should not be documented

### Usage Example

```csharp
public class Product
{
    [OpenApiSchema(Description = "Unique product identifier")]
    public string Id { get; set; }

    [OpenApiSchema(Description = "Product name")]
    public string Name { get; set; }

    [OpenApiSchema(Description = "Product price in USD")]
    public decimal Price { get; set; }

    // This property will not appear in the OpenAPI schema
    [OpenApiIgnore]
    public DateTime InternalCreatedAt { get; set; }

    // Internal audit field - excluded from documentation
    [OpenApiIgnore]
    public string LastModifiedBy { get; set; }
}
```

**Ignoring method parameters:**

```csharp
[LambdaFunction]
[HttpApi(LambdaHttpMethod.Get, "/products")]
public Task<IEnumerable<Product>> GetProducts(
    [FromQuery] int limit,
    [FromQuery] string category,
    [OpenApiIgnore] ILambdaContext context) // Lambda context is not part of the API
{
    // Implementation
}
```

---

## OpenApiOutputAttribute

Specifies the output configuration for the generated OpenAPI specification. Apply this attribute at the assembly level to configure where the OpenAPI specification should be written.

**Namespace:** `Oproto.Lambda.OpenApi.Attributes`

**Target Types:** `Assembly`

### Constructor Parameters

| Parameter | Type | Required | Description |
|-----------|------|----------|-------------|
| `specification` | `string` | Yes | The name or identifier of the specification. Used to match with the service name from `GenerateOpenApiSpecAttribute`. |
| `outputPath` | `string` | Yes | The file path where the OpenAPI specification should be written. |

### Properties

| Property | Type | Description |
|----------|------|-------------|
| `Specification` | `string` | Gets the name or identifier of the specification. |
| `OutputPath` | `string` | Gets the file path where the OpenAPI specification should be written. |

### When to Use

Use this attribute when you need to customize the output location of the generated OpenAPI specification file. By default, the specification is written to `openapi.json` in the project directory. This attribute allows you to:

- Specify a custom output path
- Generate multiple specifications to different locations
- Integrate with build pipelines that expect the specification in a specific location

### Usage Example

```csharp
// In AssemblyInfo.cs or any .cs file in your project
using Oproto.Lambda.OpenApi.Attributes;

[assembly: OpenApiOutput("Products API", "docs/api/openapi.json")]
```

**Multiple specifications:**

```csharp
// Generate separate specifications for different APIs
[assembly: OpenApiOutput("Products API", "docs/products-api.json")]
[assembly: OpenApiOutput("Orders API", "docs/orders-api.json")]
```

---

## See Also

- [Getting Started Guide](getting-started.md) - Quick start guide for using Oproto.Lambda.OpenApi
- [Configuration Guide](configuration.md) - MSBuild configuration options
- [Examples Project](../Oproto.Lambda.OpenApi.Examples/) - Complete working examples
