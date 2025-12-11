# Design Document: OpenAPI Completeness Enhancements

## Overview

This design extends the OpenAPI source generator to support additional OpenAPI 3.0 specification features including operation examples, deprecation markers, response headers, server definitions, tags, external documentation, and operation IDs. The implementation follows the existing attribute-based pattern established by `OpenApiSchemaAttribute`, `OpenApiResponseTypeAttribute`, and `OpenApiSecuritySchemeAttribute`.

## Architecture

The existing architecture remains unchanged:
- **Source Generator** (`OpenApiSpecGenerator`): Roslyn incremental generator that processes Lambda function classes
- **Attributes** (`Oproto.Lambda.OpenApi.Attributes`): Declarative metadata for OpenAPI features
- **Build Task** (`ExtractOpenApiSpecTask`): Extracts generated OpenAPI JSON to file

New attributes will be added to the Attributes namespace and processed by the existing generator infrastructure.

## Components and Interfaces

### New Attributes

#### 1. OpenApiExampleAttribute
```csharp
[AttributeUsage(AttributeTargets.Method, AllowMultiple = true)]
public class OpenApiExampleAttribute : Attribute
{
    public string Name { get; }
    public string Value { get; }  // JSON string
    public int StatusCode { get; set; } = 200;  // For response examples
    public bool IsRequestExample { get; set; } = false;
    
    public OpenApiExampleAttribute(string name, string value) { }
}
```

#### 2. OpenApiResponseHeaderAttribute
```csharp
[AttributeUsage(AttributeTargets.Method, AllowMultiple = true)]
public class OpenApiResponseHeaderAttribute : Attribute
{
    public string Name { get; }
    public int StatusCode { get; set; } = 200;
    public string Description { get; set; }
    public Type Type { get; set; } = typeof(string);
    public bool Required { get; set; } = false;
    
    public OpenApiResponseHeaderAttribute(string name) { }
}
```

#### 3. OpenApiServerAttribute
```csharp
[AttributeUsage(AttributeTargets.Assembly, AllowMultiple = true)]
public class OpenApiServerAttribute : Attribute
{
    public string Url { get; }
    public string Description { get; set; }
    
    public OpenApiServerAttribute(string url) { }
}
```

#### 4. OpenApiTagAttribute
```csharp
[AttributeUsage(AttributeTargets.Method, AllowMultiple = true)]
public class OpenApiTagAttribute : Attribute
{
    public string Name { get; }
    
    public OpenApiTagAttribute(string name) { }
}
```

#### 5. OpenApiTagDefinitionAttribute
```csharp
[AttributeUsage(AttributeTargets.Assembly, AllowMultiple = true)]
public class OpenApiTagDefinitionAttribute : Attribute
{
    public string Name { get; }
    public string Description { get; set; }
    public string ExternalDocsUrl { get; set; }
    public string ExternalDocsDescription { get; set; }
    
    public OpenApiTagDefinitionAttribute(string name) { }
}
```

#### 6. OpenApiExternalDocsAttribute
```csharp
[AttributeUsage(AttributeTargets.Assembly | AttributeTargets.Method)]
public class OpenApiExternalDocsAttribute : Attribute
{
    public string Url { get; }
    public string Description { get; set; }
    
    public OpenApiExternalDocsAttribute(string url) { }
}
```

#### 7. OpenApiOperationIdAttribute
```csharp
[AttributeUsage(AttributeTargets.Method)]
public class OpenApiOperationIdAttribute : Attribute
{
    public string OperationId { get; }
    
    public OpenApiOperationIdAttribute(string operationId) { }
}
```

### Generator Extensions

The `OpenApiSpecGenerator` will be extended with new methods:

```csharp
// In OpenApiSpecGenerator.cs or new partial class files
private void AddExamplesToOperation(OpenApiOperation operation, IMethodSymbol method);
private void AddDeprecationInfo(OpenApiOperation operation, IMethodSymbol method);
private void AddResponseHeaders(OpenApiResponses responses, IMethodSymbol method);
private List<OpenApiServer> GetServersFromAssembly(Compilation compilation);
private List<OpenApiTag> GetTagDefinitionsFromAssembly(Compilation compilation);
private OpenApiExternalDocs GetExternalDocsFromAssembly(Compilation compilation);
private string GetOperationId(IMethodSymbol method, HashSet<string> usedIds);
private List<string> GetTagsForMethod(IMethodSymbol method);
```

## Data Models

### EndpointInfo Extensions

```csharp
public class EndpointInfo
{
    // Existing properties...
    
    // New properties
    public bool IsDeprecated { get; set; }
    public string DeprecationMessage { get; set; }
    public string OperationId { get; set; }
    public List<string> Tags { get; set; }
    public OpenApiExternalDocs ExternalDocs { get; set; }
}
```

## Correctness Properties

*A property is a characteristic or behavior that should hold true across all valid executions of a system-essentially, a formal statement about what the system should do. Properties serve as the bridge between human-readable specifications and machine-verifiable correctness guarantees.*



### Property 1: XML Example Extraction
*For any* method with XML documentation containing `<example>` tags, the generated OpenAPI operation SHALL contain those examples in the appropriate schema location.
**Validates: Requirements 1.1**

### Property 2: Attribute Example Inclusion
*For any* method with `[OpenApiExample]` attributes, the generated OpenAPI operation SHALL contain the parsed JSON examples.
**Validates: Requirements 1.2**

### Property 3: Example Attribute Precedence
*For any* method with both XML `<example>` and `[OpenApiExample]` attribute, the attribute value SHALL appear in the output (not the XML value).
**Validates: Requirements 1.3**

### Property 4: Request Example Placement
*For any* request example, the example SHALL appear at `requestBody.content.application/json.example` in the generated OpenAPI.
**Validates: Requirements 1.4**

### Property 5: Response Example Placement
*For any* response example with a given status code, the example SHALL appear at `responses.{statusCode}.content.application/json.example`.
**Validates: Requirements 1.5**

### Property 6: Obsolete Attribute Maps to Deprecated
*For any* method with `[Obsolete]` attribute, the generated operation SHALL have `deprecated: true`.
**Validates: Requirements 2.1**

### Property 7: Obsolete Message in Description
*For any* method with `[Obsolete("message")]`, the operation description SHALL contain that message.
**Validates: Requirements 2.2**

### Property 8: Non-Obsolete Methods Not Deprecated
*For any* method without `[Obsolete]` attribute, the generated operation SHALL NOT have a deprecated field.
**Validates: Requirements 2.3**

### Property 9: Response Header Inclusion
*For any* method with `[OpenApiResponseHeader]` attributes, the generated response SHALL contain those headers with correct names, types, and required flags.
**Validates: Requirements 3.1, 3.2, 3.3, 3.4**

### Property 10: Server Definitions from Assembly
*For any* assembly with `[OpenApiServer]` attributes, the generated specification SHALL contain a servers array with those URLs and descriptions.
**Validates: Requirements 4.1, 4.2**

### Property 11: No Servers When Not Defined
*For any* assembly without `[OpenApiServer]` attributes, the generated specification SHALL NOT contain a servers array.
**Validates: Requirements 4.4**

### Property 12: Tag Assignment
*For any* method with `[OpenApiTag]` attributes, the generated operation SHALL be assigned to all specified tags; methods without the attribute SHALL be assigned to "Default".
**Validates: Requirements 5.1, 5.3, 5.4, 5.5**

### Property 13: Tag Definitions
*For any* assembly with `[OpenApiTagDefinition]` attributes, the generated specification SHALL contain tag definitions with names and descriptions.
**Validates: Requirements 5.2**

### Property 14: Assembly-Level External Docs
*For any* assembly with `[OpenApiExternalDocs]` attribute, the generated specification SHALL contain an externalDocs object with URL and description.
**Validates: Requirements 6.1, 6.3**

### Property 15: Operation-Level External Docs
*For any* method with `[OpenApiExternalDocs]` attribute, the generated operation SHALL contain an externalDocs object.
**Validates: Requirements 6.2**

### Property 16: OperationId Generation
*For any* operation, the generated OpenAPI SHALL include an operationId based on the method name or `[OpenApiOperationId]` attribute value.
**Validates: Requirements 7.1, 7.2**

### Property 17: OperationId Uniqueness
*For any* set of operations, all operationIds in the generated specification SHALL be unique.
**Validates: Requirements 7.3**

## Error Handling

- **Invalid JSON in examples**: Log a warning and skip the example rather than failing the build
- **Missing required attribute properties**: Use sensible defaults (e.g., status code 200, type string)
- **Duplicate operationIds**: Automatically append numeric suffix (_2, _3, etc.)
- **Invalid URLs in server/external docs**: Include as-is, let OpenAPI validators catch issues

## Testing Strategy

### Unit Tests
- Test each new attribute class for correct property storage
- Test attribute reading from method/assembly symbols
- Test JSON parsing for example attributes

### Property-Based Tests
Using a property-based testing library (e.g., FsCheck or similar for .NET):

1. **Example extraction tests**: Generate random valid JSON examples, verify round-trip through generator
2. **Deprecation tests**: Generate methods with/without Obsolete, verify deprecated field presence
3. **Header tests**: Generate random header configurations, verify all appear in output
4. **Tag tests**: Generate random tag assignments, verify all tags appear on operations
5. **OperationId uniqueness**: Generate methods with duplicate names, verify unique IDs generated

### Integration Tests
- Build the Examples project and verify the generated `openapi.json` contains all new features
- Validate generated JSON against OpenAPI 3.0 schema

### Examples Project Updates
Add demonstrations of all new features:
- Methods with `[OpenApiExample]` attributes
- Methods with `[Obsolete]` attribute
- Methods with `[OpenApiResponseHeader]` attributes
- Assembly-level `[OpenApiServer]`, `[OpenApiTagDefinition]`, `[OpenApiExternalDocs]`
- Methods with `[OpenApiTag]` and `[OpenApiOperationId]` attributes
