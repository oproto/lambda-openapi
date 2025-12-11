# Attribute Reference

This document provides a comprehensive reference for all attributes available in the Oproto.Lambda.OpenApi library. These attributes allow you to customize the generated OpenAPI specification for your AWS Lambda functions.

## Table of Contents

### Assembly-Level Attributes
- [OpenApiInfoAttribute](#openapiinfoattribute) - API metadata (title, version, description)
- [OpenApiSecuritySchemeAttribute](#openapisecurityschemeattribute) - Security scheme definitions
- [OpenApiServerAttribute](#openapiservattribute) - Server URL definitions
- [OpenApiTagDefinitionAttribute](#openapitagdefinitionattribute) - Tag definitions with descriptions
- [OpenApiExternalDocsAttribute](#openapiexternaldocsattribute) - External documentation links (also method-level)
- [OpenApiOutputAttribute](#openapioutputattribute) - Output file configuration

### Method-Level Attributes
- [OpenApiOperationAttribute](#openapioperationattribute) - Operation metadata (summary, description)
- [OpenApiOperationIdAttribute](#openapioperationidattribute) - Custom operation IDs
- [OpenApiTagAttribute](#openapitagattribute) - Tag assignment for operations
- [OpenApiResponseTypeAttribute](#openapiresponsetypeattribute) - Response type documentation
- [OpenApiResponseHeaderAttribute](#openapiresponseheaderattribute) - Response header documentation
- [OpenApiExampleAttribute](#openapiexampleattribute) - Request/response examples

### Property/Parameter Attributes
- [OpenApiSchemaAttribute](#openapischemattribute) - Schema customization
- [OpenApiIgnoreAttribute](#openapiignoreattribute) - Exclude from documentation

---

## OpenApiInfoAttribute

Specifies global OpenAPI document information at the assembly level. Use this attribute to set the API title, version, description, and other metadata that appears in the generated OpenAPI specification's info section.

**Namespace:** `Oproto.Lambda.OpenApi.Attributes`

**Target Types:** `Assembly`

### Constructor Parameters

| Parameter | Type | Required | Default | Description |
|-----------|------|----------|---------|-------------|
| `title` | `string` | Yes | - | The title of the API. |
| `version` | `string` | No | `"1.0.0"` | The version of the API. |

### Properties

| Property | Type | Description |
|----------|------|-------------|
| `Title` | `string` | Gets the title of the API. |
| `Version` | `string` | Gets the version of the API. |
| `Description` | `string` | Gets or sets a description of the API. |
| `TermsOfService` | `string` | Gets or sets the URL to the Terms of Service. |
| `ContactName` | `string` | Gets or sets the contact name. |
| `ContactEmail` | `string` | Gets or sets the contact email. |
| `ContactUrl` | `string` | Gets or sets the contact URL. |
| `LicenseName` | `string` | Gets or sets the license name. |
| `LicenseUrl` | `string` | Gets or sets the license URL. |

### When to Use

Use this attribute at the assembly level to configure the OpenAPI document's info section. This is typically placed in `AssemblyInfo.cs` or at the top of any `.cs` file in your project. Without this attribute, the API title defaults to "API Documentation" and version to "1.0.0".

### Usage Examples

**Basic usage:**

```csharp
[assembly: OpenApiInfo("My API", "1.0.0")]
```

**With description:**

```csharp
[assembly: OpenApiInfo("Products API", "2.0.0", 
    Description = "API for managing products in the catalog")]
```

**Full metadata:**

```csharp
[assembly: OpenApiInfo("E-Commerce API", "1.0.0",
    Description = "Complete e-commerce API for managing products, orders, and customers",
    TermsOfService = "https://example.com/terms",
    ContactName = "API Support",
    ContactEmail = "api-support@example.com",
    ContactUrl = "https://example.com/support",
    LicenseName = "MIT",
    LicenseUrl = "https://opensource.org/licenses/MIT")]
```

---

## OpenApiSecuritySchemeAttribute

Defines a security scheme for the OpenAPI specification. Apply this attribute at the assembly level to define security schemes that can be referenced by endpoints.

**Namespace:** `Oproto.Lambda.OpenApi.Attributes`

**Target Types:** `Assembly`

**Allow Multiple:** Yes

### Constructor Parameters

| Parameter | Type | Required | Description |
|-----------|------|----------|-------------|
| `schemeId` | `string` | Yes | The unique identifier for this security scheme. |

### Properties

| Property | Type | Default | Description |
|----------|------|---------|-------------|
| `Type` | `OpenApiSecuritySchemeType` | `ApiKey` | The type of security scheme (ApiKey, Http, OAuth2, OpenIdConnect). |
| `ApiKeyName` | `string` | `null` | The name of the API key (for ApiKey type). |
| `ApiKeyLocation` | `ApiKeyLocation` | `Header` | The location of the API key (Header, Query, Cookie). |
| `AuthorizationUrl` | `string` | `null` | The authorization URL (for OAuth2 type). |
| `TokenUrl` | `string` | `null` | The token URL (for OAuth2 type). |
| `Scopes` | `string` | `null` | Available scopes in format "scope1:Description 1,scope2:Description 2". |
| `HttpScheme` | `string` | `null` | The HTTP authentication scheme name (for Http type, e.g., "bearer", "basic"). |
| `BearerFormat` | `string` | `null` | The bearer format hint (for Http type with bearer scheme, e.g., "JWT"). |
| `OpenIdConnectUrl` | `string` | `null` | The OpenID Connect URL (for OpenIdConnect type). |
| `Description` | `string` | `null` | A description of the security scheme. |

### When to Use

Use this attribute to define security schemes for your API. Security schemes are only added to the OpenAPI specification when this attribute is present. Without this attribute, no security schemes will be defined.

### Usage Examples

**API Key Authentication:**

```csharp
[assembly: OpenApiSecurityScheme("apiKey",
    Type = OpenApiSecuritySchemeType.ApiKey,
    ApiKeyName = "x-api-key",
    ApiKeyLocation = ApiKeyLocation.Header,
    Description = "API key authentication via header")]
```

**OAuth2 with Authorization Code Flow:**

```csharp
[assembly: OpenApiSecurityScheme("oauth2",
    Type = OpenApiSecuritySchemeType.OAuth2,
    AuthorizationUrl = "https://auth.example.com/authorize",
    TokenUrl = "https://auth.example.com/token",
    Scopes = "read:Read access,write:Write access",
    Description = "OAuth 2.0 authentication")]
```

**Bearer Token (JWT):**

```csharp
[assembly: OpenApiSecurityScheme("bearerAuth",
    Type = OpenApiSecuritySchemeType.Http,
    HttpScheme = "bearer",
    BearerFormat = "JWT",
    Description = "JWT Bearer token authentication")]
```

**Multiple Security Schemes:**

```csharp
[assembly: OpenApiSecurityScheme("apiKey",
    Type = OpenApiSecuritySchemeType.ApiKey,
    ApiKeyName = "x-api-key",
    ApiKeyLocation = ApiKeyLocation.Header)]

[assembly: OpenApiSecurityScheme("oauth2",
    Type = OpenApiSecuritySchemeType.OAuth2,
    AuthorizationUrl = "https://auth.example.com/authorize",
    TokenUrl = "https://auth.example.com/token",
    Scopes = "read:Read access,write:Write access")]
```

---

## OpenApiServerAttribute

Specifies server URLs for the OpenAPI specification. Apply this attribute at the assembly level to define the base URLs where the API is hosted.

**Namespace:** `Oproto.Lambda.OpenApi.Attributes`

**Target Types:** `Assembly`

**Allow Multiple:** Yes

### Constructor Parameters

| Parameter | Type | Required | Description |
|-----------|------|----------|-------------|
| `url` | `string` | Yes | The URL of the server. |

### Properties

| Property | Type | Default | Description |
|----------|------|---------|-------------|
| `Url` | `string` | - | Gets the URL of the server. |
| `Description` | `string` | `null` | Gets or sets a description of the server. |

### When to Use

Use this attribute to define the server URLs where your API is hosted. This is useful for:

- Documenting production, staging, and development environments
- Providing base URLs for API consumers
- Supporting multiple deployment environments in a single specification

When no `[OpenApiServer]` attributes are present, the servers section is omitted from the specification entirely.

### Usage Examples

**Single server:**

```csharp
[assembly: OpenApiServer("https://api.example.com/v1", Description = "Production server")]
```

**Multiple environments:**

```csharp
[assembly: OpenApiServer("https://api.example.com/v1", Description = "Production server")]
[assembly: OpenApiServer("https://staging-api.example.com/v1", Description = "Staging server")]
[assembly: OpenApiServer("https://localhost:5000", Description = "Local development")]
```

---

## OpenApiTagDefinitionAttribute

Defines a tag with metadata for the OpenAPI specification. Tags defined at the assembly level appear in the specification's tags array with descriptions and optional external documentation links.

**Namespace:** `Oproto.Lambda.OpenApi.Attributes`

**Target Types:** `Assembly`

**Allow Multiple:** Yes

### Constructor Parameters

| Parameter | Type | Required | Description |
|-----------|------|----------|-------------|
| `name` | `string` | Yes | The name of the tag. |

### Properties

| Property | Type | Default | Description |
|----------|------|---------|-------------|
| `Name` | `string` | - | Gets the name of the tag. |
| `Description` | `string` | `null` | Gets or sets a description of the tag. |
| `ExternalDocsUrl` | `string` | `null` | Gets or sets the URL for external documentation about this tag. |
| `ExternalDocsDescription` | `string` | `null` | Gets or sets a description for the external documentation. |

### When to Use

Use this attribute to provide rich metadata for tags used in your API. While operations can be assigned to tags using `[OpenApiTag]`, this attribute allows you to:

- Add descriptions that appear in documentation viewers
- Link to external documentation for each tag
- Define tags before they are used by operations

### Usage Examples

**Basic tag definition:**

```csharp
[assembly: OpenApiTagDefinition("Products", Description = "Operations for managing products")]
```

**With external documentation:**

```csharp
[assembly: OpenApiTagDefinition("Orders", 
    Description = "Order management operations",
    ExternalDocsUrl = "https://docs.example.com/orders",
    ExternalDocsDescription = "Complete order API guide")]
```

**Multiple tag definitions:**

```csharp
[assembly: OpenApiTagDefinition("Products", Description = "Product catalog operations")]
[assembly: OpenApiTagDefinition("Orders", Description = "Order management operations")]
[assembly: OpenApiTagDefinition("Admin", 
    Description = "Administrative operations",
    ExternalDocsUrl = "https://docs.example.com/admin")]
```

---

## OpenApiExternalDocsAttribute

Specifies external documentation links for the API or individual operations. Can be applied at the assembly level for API-wide documentation or at the method level for operation-specific documentation.

**Namespace:** `Oproto.Lambda.OpenApi.Attributes`

**Target Types:** `Assembly`, `Method`

### Constructor Parameters

| Parameter | Type | Required | Description |
|-----------|------|----------|-------------|
| `url` | `string` | Yes | The URL for the external documentation. |

### Properties

| Property | Type | Default | Description |
|----------|------|---------|-------------|
| `Url` | `string` | - | Gets the URL for the external documentation. |
| `Description` | `string` | `null` | Gets or sets a description of the external documentation. |

### When to Use

Use this attribute to link to external documentation resources:

- **Assembly level**: Links to comprehensive API documentation, tutorials, or guides
- **Method level**: Links to detailed documentation for specific operations

### Usage Examples

**Assembly-level (API-wide) documentation:**

```csharp
[assembly: OpenApiExternalDocs("https://docs.example.com/api", 
    Description = "Full API documentation and tutorials")]
```

**Method-level (operation-specific) documentation:**

```csharp
[LambdaFunction]
[HttpApi(LambdaHttpMethod.Get, "/products/{id}")]
[OpenApiExternalDocs("https://docs.example.com/products/get-by-id", 
    Description = "Detailed guide for retrieving products")]
public Task<Product> GetProduct(string id)
{
    // Implementation
}
```

**Combined usage:**

```csharp
// Assembly level - general API docs
[assembly: OpenApiExternalDocs("https://docs.example.com/api", Description = "API Reference")]

// Method level - specific operation docs
[OpenApiExternalDocs("https://docs.example.com/auth/oauth", Description = "OAuth flow guide")]
public Task<TokenResponse> GetToken([FromBody] TokenRequest request) { }
```

---

## OpenApiResponseTypeAttribute

Specifies the response type for a specific HTTP status code in the OpenAPI specification. Use this attribute to document the actual response types for API operations, especially when the method return type (e.g., `IHttpResult`) doesn't reflect the actual response body.

**Namespace:** `Oproto.Lambda.OpenApi.Attributes`

**Target Types:** `Method`

**Allow Multiple:** Yes

### Constructor Parameters

| Parameter | Type | Required | Default | Description |
|-----------|------|----------|---------|-------------|
| `responseType` | `Type` | Yes | - | The type of the response body. |
| `statusCode` | `int` | No | `200` | The HTTP status code this response applies to. |

### Properties

| Property | Type | Description |
|----------|------|-------------|
| `ResponseType` | `Type` | Gets the type of the response body. |
| `StatusCode` | `int` | Gets the HTTP status code this response applies to. |
| `Description` | `string` | Gets or sets a description of the response. |

### When to Use

Use this attribute when your Lambda functions return `IHttpResult` or similar wrapper types that don't reflect the actual response body. This is common when using patterns like:

- Returning `IHttpResult` from Lambda Annotations
- Using a service layer that wraps responses with status codes
- Returning different types for different status codes

Without this attribute, methods returning `IHttpResult` will have no response schema documented. With this attribute, you can explicitly specify what the API actually returns.

### Usage Examples

**Basic usage with IHttpResult:**

```csharp
[LambdaFunction]
[HttpApi(LambdaHttpMethod.Post, "/products")]
[OpenApiResponseType(typeof(Product), 201, Description = "Returns the created product")]
public async Task<IHttpResult> CreateProduct([FromBody] CreateProductRequest request)
{
    var product = await _service.CreateProduct(request);
    return HttpResults.Created($"/products/{product.Id}", product);
}
```

**Multiple response types:**

```csharp
[LambdaFunction]
[HttpApi(LambdaHttpMethod.Get, "/products/{id}")]
[OpenApiResponseType(typeof(Product), 200, Description = "Returns the product")]
[OpenApiResponseType(typeof(ErrorResponse), 404, Description = "Product not found")]
public async Task<IHttpResult> GetProduct(string id)
{
    var product = await _service.GetProduct(id);
    if (product == null)
        return HttpResults.NotFound(new ErrorResponse { Message = "Product not found" });
    return HttpResults.Ok(product);
}
```

**With paginated responses:**

```csharp
[LambdaFunction]
[HttpApi(LambdaHttpMethod.Post, "/products/query")]
[OpenApiResponseType(typeof(PagedResult<Product>), 200, Description = "Returns paginated products")]
[OpenApiResponseType(typeof(ValidationError), 400, Description = "Invalid query parameters")]
public async Task<IHttpResult> QueryProducts([FromBody] QueryRequest request)
{
    // Implementation
}
```

**Void response (no content):**

```csharp
[LambdaFunction]
[HttpApi(LambdaHttpMethod.Delete, "/products/{id}")]
[OpenApiResponseType(null, 204, Description = "Product deleted successfully")]
[OpenApiResponseType(typeof(ErrorResponse), 404, Description = "Product not found")]
public async Task<IHttpResult> DeleteProduct(string id)
{
    await _service.DeleteProduct(id);
    return HttpResults.NoContent();
}
```

### Behavior

When `[OpenApiResponseType]` attributes are present on a method:
- The generator uses these attributes to define response schemas
- The method's return type is ignored for response documentation
- Error responses (400, 401, 403, 500) are still added automatically unless explicitly specified

When no `[OpenApiResponseType]` attributes are present:
- If the return type is `IHttpResult` or similar, no response schema is documented
- Otherwise, the return type is used as the 200 response schema (with Task unwrapping)

---

## OpenApiOperationAttribute

Provides additional OpenAPI operation information for methods. Use this attribute to add metadata to API operations such as summary, description, deprecation status, and operation ID.

**Namespace:** `Oproto.Lambda.OpenApi.Attributes`

**Target Types:** `Method`

### Properties

| Property | Type | Required | Default | Description |
|----------|------|----------|---------|-------------|
| `Summary` | `string` | No | `null` | A short summary of what the operation does. Displayed as the operation title in documentation. |
| `Description` | `string` | No | `null` | A detailed explanation of the operation behavior. Supports markdown formatting. |
| `Deprecated` | `bool` | No | `false` | Indicates whether the operation is deprecated. Can also be set using `[Obsolete]`. |
| `OperationId` | `string` | No | `null` | A custom operation ID for code generation. If not specified, generated from method name. |

### When to Use

Apply this attribute to Lambda function methods to provide human-readable documentation that will appear in the generated OpenAPI specification. The summary appears as a brief title, while the description can contain more detailed information about the operation's behavior, expected inputs, and outputs.

This attribute values override XML documentation comments when both are present.

### Deprecation

Operations can be marked as deprecated in two ways:
- Using `[OpenApiOperation(Deprecated = true)]`
- Using the standard `[Obsolete]` attribute (which also captures the deprecation message)

Both approaches work, and can be combined. The `[Obsolete]` attribute is recommended when you also want compiler warnings.

### Usage Examples

**Basic usage:**

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
```

**With operation ID:**

```csharp
[LambdaFunction]
[HttpApi(LambdaHttpMethod.Get, "/products")]
[OpenApiOperation(
    Summary = "List all products",
    OperationId = "listAllProducts")]
public Task<IEnumerable<Product>> GetProducts()
{
    // Implementation
}
```

**Deprecated operation:**

```csharp
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

// Or using [Obsolete] for compiler warnings too:
[LambdaFunction]
[HttpApi(LambdaHttpMethod.Get, "/products/old")]
[Obsolete("Use GET /products instead")]
public Task<IEnumerable<Product>> GetProductsOld()
{
    // Implementation
}
```

---

## OpenApiOperationIdAttribute

Specifies a custom operation ID for an API operation. Operation IDs are used by code generators to create meaningful method names in client SDKs.

**Namespace:** `Oproto.Lambda.OpenApi.Attributes`

**Target Types:** `Method`

### Constructor Parameters

| Parameter | Type | Required | Description |
|-----------|------|----------|-------------|
| `operationId` | `string` | Yes | The custom operation ID. |

### Properties

| Property | Type | Description |
|----------|------|-------------|
| `OperationId` | `string` | Gets the custom operation ID. |

### When to Use

Use this attribute when you want to:

- Override the auto-generated operation ID (which defaults to the method name)
- Ensure consistent naming across API versions
- Follow specific naming conventions for client SDK generation

Without this attribute, the generator creates operation IDs based on the method name and ensures uniqueness by appending numeric suffixes if needed.

### Usage Examples

**Basic usage:**

```csharp
[LambdaFunction]
[HttpApi(LambdaHttpMethod.Get, "/products")]
[OpenApiOperationId("listAllProducts")]
public Task<IEnumerable<Product>> GetProducts()
{
    // Implementation
}
```

**RESTful naming convention:**

```csharp
[LambdaFunction]
[HttpApi(LambdaHttpMethod.Get, "/products/{id}")]
[OpenApiOperationId("getProductById")]
public Task<Product> GetProduct(string id) { }

[LambdaFunction]
[HttpApi(LambdaHttpMethod.Post, "/products")]
[OpenApiOperationId("createProduct")]
public Task<Product> CreateProduct([FromBody] CreateProductRequest request) { }

[LambdaFunction]
[HttpApi(LambdaHttpMethod.Put, "/products/{id}")]
[OpenApiOperationId("updateProduct")]
public Task<Product> UpdateProduct(string id, [FromBody] UpdateProductRequest request) { }

[LambdaFunction]
[HttpApi(LambdaHttpMethod.Delete, "/products/{id}")]
[OpenApiOperationId("deleteProduct")]
public Task DeleteProduct(string id) { }
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

## OpenApiResponseHeaderAttribute

Specifies response headers for an API operation. Use this attribute to document headers that are returned in API responses.

**Namespace:** `Oproto.Lambda.OpenApi.Attributes`

**Target Types:** `Method`

**Allow Multiple:** Yes

### Constructor Parameters

| Parameter | Type | Required | Description |
|-----------|------|----------|-------------|
| `name` | `string` | Yes | The name of the response header. |

### Properties

| Property | Type | Default | Description |
|----------|------|---------|-------------|
| `Name` | `string` | - | Gets the name of the response header. |
| `StatusCode` | `int` | `200` | Gets or sets the HTTP status code this header applies to. |
| `Description` | `string` | `null` | Gets or sets a description of the header. |
| `Type` | `Type` | `typeof(string)` | Gets or sets the type of the header value. |
| `Required` | `bool` | `false` | Gets or sets whether the header is required. |

### When to Use

Use this attribute to document response headers that your API returns. This is useful for:

- Pagination headers (X-Total-Count, X-Page-Size)
- Rate limiting headers (X-Rate-Limit-Remaining)
- Request tracking headers (X-Request-Id)
- Custom application headers

### Usage Examples

**Basic header:**

```csharp
[LambdaFunction]
[HttpApi(LambdaHttpMethod.Get, "/products/{id}")]
[OpenApiResponseHeader("X-Request-Id", Description = "Unique request identifier for tracing")]
public Task<Product> GetProduct(string id)
{
    // Implementation
}
```

**Multiple headers with types:**

```csharp
[LambdaFunction]
[HttpApi(LambdaHttpMethod.Get, "/products")]
[OpenApiResponseHeader("X-Total-Count", Description = "Total number of products", Type = typeof(int))]
[OpenApiResponseHeader("X-Page-Size", Description = "Number of products per page", Type = typeof(int))]
[OpenApiResponseHeader("X-Has-More", Description = "Whether more pages exist", Type = typeof(bool))]
public Task<IEnumerable<Product>> GetProducts([FromQuery] int page = 1)
{
    // Implementation
}
```

**Headers for different status codes:**

```csharp
[LambdaFunction]
[HttpApi(LambdaHttpMethod.Post, "/products")]
[OpenApiResponseHeader("Location", StatusCode = 201, Description = "URL of the created resource")]
[OpenApiResponseHeader("X-Request-Id", StatusCode = 201, Description = "Request identifier")]
[OpenApiResponseHeader("Retry-After", StatusCode = 429, Description = "Seconds to wait before retrying", Type = typeof(int))]
public Task<Product> CreateProduct([FromBody] CreateProductRequest request)
{
    // Implementation
}
```

**Required header:**

```csharp
[LambdaFunction]
[HttpApi(LambdaHttpMethod.Get, "/products")]
[OpenApiResponseHeader("X-Api-Version", Description = "API version", Required = true)]
public Task<IEnumerable<Product>> GetProducts()
{
    // Implementation
}
```

---

## OpenApiExampleAttribute

Specifies request or response examples for an API operation. Use this attribute to provide JSON examples that help API consumers understand expected payloads.

**Namespace:** `Oproto.Lambda.OpenApi.Attributes`

**Target Types:** `Method`

**Allow Multiple:** Yes

### Constructor Parameters

| Parameter | Type | Required | Description |
|-----------|------|----------|-------------|
| `name` | `string` | Yes | The name of the example. |
| `value` | `string` | Yes | The JSON string value of the example. |

### Properties

| Property | Type | Default | Description |
|----------|------|---------|-------------|
| `Name` | `string` | - | Gets the name of the example. |
| `Value` | `string` | - | Gets the JSON string value of the example. |
| `StatusCode` | `int` | `200` | Gets or sets the HTTP status code this example applies to (for response examples). |
| `IsRequestExample` | `bool` | `false` | Gets or sets whether this is a request body example. |

### When to Use

Use this attribute to provide concrete examples of request and response payloads. Examples help API consumers:

- Understand the expected data format
- Test API calls with realistic data
- Generate sample code in documentation tools

### Usage Examples

**Response example:**

```csharp
[LambdaFunction]
[HttpApi(LambdaHttpMethod.Get, "/products/{id}")]
[OpenApiExample("Single Product", 
    "{\"id\": \"123\", \"name\": \"Widget Pro\", \"price\": 29.99, \"category\": \"Electronics\"}", 
    StatusCode = 200)]
public Task<Product> GetProduct(string id)
{
    // Implementation
}
```

**Request example:**

```csharp
[LambdaFunction]
[HttpApi(LambdaHttpMethod.Post, "/products")]
[OpenApiExample("Create Product Request", 
    "{\"name\": \"New Widget\", \"price\": 19.99, \"category\": \"Electronics\"}", 
    IsRequestExample = true)]
public Task<Product> CreateProduct([FromBody] CreateProductRequest request)
{
    // Implementation
}
```

**Multiple examples:**

```csharp
[LambdaFunction]
[HttpApi(LambdaHttpMethod.Post, "/products")]
[OpenApiExample("Basic Product", 
    "{\"name\": \"Simple Widget\", \"price\": 9.99}", 
    IsRequestExample = true)]
[OpenApiExample("Full Product", 
    "{\"name\": \"Premium Widget\", \"price\": 49.99, \"category\": \"Premium\", \"description\": \"High-end widget\"}", 
    IsRequestExample = true)]
[OpenApiExample("Created Response", 
    "{\"id\": \"456\", \"name\": \"Simple Widget\", \"price\": 9.99}", 
    StatusCode = 200)]
public Task<Product> CreateProduct([FromBody] CreateProductRequest request)
{
    // Implementation
}
```

**Examples for different status codes:**

```csharp
[LambdaFunction]
[HttpApi(LambdaHttpMethod.Get, "/products/{id}")]
[OpenApiResponseType(typeof(Product), 200)]
[OpenApiResponseType(typeof(ErrorResponse), 404)]
[OpenApiExample("Found Product", 
    "{\"id\": \"123\", \"name\": \"Widget\"}", 
    StatusCode = 200)]
[OpenApiExample("Not Found Error", 
    "{\"message\": \"Product not found\", \"errorCode\": \"PRODUCT_NOT_FOUND\"}", 
    StatusCode = 404)]
public Task<IHttpResult> GetProduct(string id)
{
    // Implementation
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

## Deprecation Support

The generator automatically detects the standard .NET `[Obsolete]` attribute and marks operations as deprecated in the OpenAPI specification.

### Behavior

- When a method has `[Obsolete]`, the operation's `deprecated` field is set to `true`
- When `[Obsolete]` includes a message, that message is appended to the operation description
- Methods without `[Obsolete]` do not include the deprecated field

### Usage Example

```csharp
[LambdaFunction]
[HttpApi(LambdaHttpMethod.Delete, "/products/{id}")]
[OpenApiOperation(Summary = "Delete a product")]
[Obsolete("Use the archive endpoint instead. This endpoint will be removed in v2.0.")]
public Task DeleteProduct(string id)
{
    // Implementation
}
```

This generates:

```json
{
  "delete": {
    "summary": "Delete a product",
    "description": "Deprecated: Use the archive endpoint instead. This endpoint will be removed in v2.0.",
    "deprecated": true
  }
}
```

---

## See Also

- [Getting Started Guide](getting-started.md) - Quick start guide for using Oproto.Lambda.OpenApi
- [Configuration Guide](configuration.md) - MSBuild configuration options
- [Examples Project](../Oproto.Lambda.OpenApi.Examples/) - Complete working examples
