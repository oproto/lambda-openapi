# Design Document: Pre-Release Fixes

## Overview

This design addresses critical issues and improvements identified during pre-release review of the Oproto Lambda OpenAPI source generator. The changes span the source generator, MSBuild task, package configuration, and documentation.

## Architecture

The library consists of three main components:

```
┌─────────────────────────────────────────────────────────────────┐
│                    Consumer Project Build                        │
├─────────────────────────────────────────────────────────────────┤
│  1. Compilation Phase                                            │
│     ┌──────────────────────────────────────────┐                │
│     │  Roslyn Compiler + Source Generator      │                │
│     │  - Analyzes Lambda function classes      │                │
│     │  - Generates OpenApiOutput.g.cs          │                │
│     │  - Embeds JSON as assembly attribute     │                │
│     └──────────────────────────────────────────┘                │
│                          │                                       │
│                          ▼                                       │
│  2. Post-Build Phase (MSBuild Task)                             │
│     ┌──────────────────────────────────────────┐                │
│     │  ExtractOpenApiSpecTask                  │                │
│     │  - Strategy 1: Parse .g.cs file (AOT)    │                │
│     │  - Strategy 2: Reflection (non-AOT)      │                │
│     │  - Writes openapi.json                   │                │
│     └──────────────────────────────────────────┘                │
└─────────────────────────────────────────────────────────────────┘
```

## Components and Interfaces

### 1. Source Generator Changes

#### Task<T> Unwrapping

Add a method to unwrap async return types:

```csharp
// OpenApiSpecGenerator.cs
private ITypeSymbol UnwrapAsyncType(ITypeSymbol typeSymbol)
{
    if (typeSymbol is INamedTypeSymbol namedType && namedType.IsGenericType)
    {
        var typeName = namedType.ConstructedFrom.ToDisplayString();
        if (typeName == "System.Threading.Tasks.Task<T>" ||
            typeName == "System.Threading.Tasks.ValueTask<T>")
        {
            return namedType.TypeArguments[0];
        }
    }
    
    // Check for non-generic Task/ValueTask
    var displayName = typeSymbol.ToDisplayString();
    if (displayName == "System.Threading.Tasks.Task" ||
        displayName == "System.Threading.Tasks.ValueTask")
    {
        return null; // Indicates void/no content
    }
    
    return typeSymbol;
}
```

#### Debug Statement Cleanup

Remove all `Console.WriteLine` calls. Wrap meaningful `Debug.WriteLine` calls:

```csharp
#if DEBUG
    Debug.WriteLine($"Processing class: {classSymbol.Name}");
#endif
```

#### HTTP Method Completeness

Update `GenerateOpenApiDocument` to handle all HTTP methods:

```csharp
switch (endpoint.HttpMethod.ToUpperInvariant())
{
    case "GET": path.Operations[OperationType.Get] = operation; break;
    case "POST": path.Operations[OperationType.Post] = operation; break;
    case "PUT": path.Operations[OperationType.Put] = operation; break;
    case "DELETE": path.Operations[OperationType.Delete] = operation; break;
    case "PATCH": path.Operations[OperationType.Patch] = operation; break;
    case "HEAD": path.Operations[OperationType.Head] = operation; break;
    case "OPTIONS": path.Operations[OperationType.Options] = operation; break;
}
```

### 2. Security Scheme Attributes

New attributes for configurable security:

```csharp
// OpenApiSecuritySchemeAttribute.cs
[AttributeUsage(AttributeTargets.Assembly, AllowMultiple = true)]
public class OpenApiSecuritySchemeAttribute : Attribute
{
    public string SchemeId { get; }
    public SecuritySchemeType Type { get; set; }
    
    // For API Key
    public string ApiKeyName { get; set; }
    public ApiKeyLocation ApiKeyLocation { get; set; }
    
    // For OAuth2
    public string AuthorizationUrl { get; set; }
    public string TokenUrl { get; set; }
    public string Scopes { get; set; } // Comma-separated "scope1:desc1,scope2:desc2"
    
    public OpenApiSecuritySchemeAttribute(string schemeId) => SchemeId = schemeId;
}

public enum SecuritySchemeType { ApiKey, OAuth2, OpenIdConnect, Http }
public enum ApiKeyLocation { Header, Query, Cookie }
```

Usage:
```csharp
[assembly: OpenApiSecurityScheme("apiKey", 
    Type = SecuritySchemeType.ApiKey, 
    ApiKeyName = "x-api-key", 
    ApiKeyLocation = ApiKeyLocation.Header)]

[assembly: OpenApiSecurityScheme("oauth2",
    Type = SecuritySchemeType.OAuth2,
    AuthorizationUrl = "https://auth.example.com/authorize",
    TokenUrl = "https://auth.example.com/token",
    Scopes = "read:Read access,write:Write access")]
```

### 3. MSBuild Task AOT Support

Update `ExtractOpenApiSpecTask` with dual extraction strategy:

```csharp
public override bool Execute()
{
    // Strategy 1: Try parsing generated source file (AOT-compatible)
    var generatedFilePath = FindGeneratedSourceFile();
    if (generatedFilePath != null && File.Exists(generatedFilePath))
    {
        var json = ExtractJsonFromSourceFile(generatedFilePath);
        if (json != null)
        {
            WriteOpenApiSpec(json);
            return true;
        }
    }
    
    // Strategy 2: Fall back to reflection (non-AOT)
    try
    {
        var json = ExtractJsonViaReflection();
        WriteOpenApiSpec(json);
        return true;
    }
    catch (Exception ex)
    {
        Log.LogError($"Failed to extract OpenAPI spec. For AOT builds, enable " +
            "EmitCompilerGeneratedFiles in your project. Error: {ex.Message}");
        return false;
    }
}

private string FindGeneratedSourceFile()
{
    // Look in standard generated files location
    var searchPaths = new[]
    {
        Path.Combine(Path.GetDirectoryName(AssemblyPath), "..", "obj", 
            "GeneratedFiles", "Oproto.Lambda.OpenApi.SourceGenerator", 
            "Oproto.Lambda.OpenApi.SourceGenerator.OpenApiSpecGenerator", 
            "OpenApiOutput.g.cs"),
        // Also check if path is provided via property
    };
    return searchPaths.FirstOrDefault(File.Exists);
}

private string ExtractJsonFromSourceFile(string filePath)
{
    var content = File.ReadAllText(filePath);
    // Parse: [assembly: OpenApiOutput(@"...", "openapi.json")]
    var match = Regex.Match(content, 
        @"\[assembly:\s*OpenApiOutput\s*\(\s*@""(.+?)""\s*,", 
        RegexOptions.Singleline);
    if (match.Success)
    {
        return match.Groups[1].Value.Replace("\"\"", "\"");
    }
    return null;
}
```

### 4. Package Configuration Changes

#### Version Alignment (SourceGenerator.csproj)

Remove hardcoded version:
```xml
<!-- Remove this line -->
<Version>1.0.0</Version>
```

#### Dependency Updates

```xml
<!-- Oproto.Lambda.OpenApi.Build.csproj -->
<PackageReference Include="Microsoft.Build.Utilities.Core" Version="17.12.6" />

<!-- Oproto.Lambda.OpenApi.SourceGenerator.csproj -->
<PackageReference Include="System.Text.Json" Version="8.0.5" />
```

### 5. Dead Code Removal

Remove the following unused code:

| Item | Location | Action |
|------|----------|--------|
| `GenerateOpenApiSpecAttribute` | Attributes/ | Remove (not used by generator) |
| `GetLambdaClassInfo` method | OpenApiSpecGenerator.cs | Remove |
| `GetApiInfo` method | OpenApiSpecGenerator.cs | Remove |
| `ResponseTypeInfo` class | SourceGenerator/ | Remove (not populated) |
| `ExampleInfo` class | SourceGenerator/ | Remove (not populated) |
| `ResponseTypes` property | EndpointInfo.cs | Remove |
| `Examples` property | DocumentationInfo.cs | Remove |

## Data Models

### Security Scheme Configuration

```csharp
internal class SecuritySchemeConfig
{
    public string SchemeId { get; set; }
    public SecuritySchemeType Type { get; set; }
    public string ApiKeyName { get; set; }
    public ApiKeyLocation? ApiKeyLocation { get; set; }
    public string AuthorizationUrl { get; set; }
    public string TokenUrl { get; set; }
    public Dictionary<string, string> Scopes { get; set; }
}
```

## Correctness Properties

*A property is a characteristic or behavior that should hold true across all valid executions of a system-essentially, a formal statement about what the system should do. Properties serve as the bridge between human-readable specifications and machine-verifiable correctness guarantees.*

### Property 1: Task<T> Unwrapping Preserves Inner Type

*For any* Lambda function with return type `Task<T>` or `ValueTask<T>`, the generated OpenAPI response schema SHALL be equivalent to the schema that would be generated for return type `T` directly.

**Validates: Requirements 1.1, 1.3, 1.4**

### Property 2: Security Scheme Attribute Round-Trip

*For any* valid `OpenApiSecuritySchemeAttribute` configuration, the generated OpenAPI security scheme definition SHALL contain all specified properties with their exact values.

**Validates: Requirements 4.1, 4.3, 4.4, 4.5**

### Property 3: Generated Source File JSON Extraction

*For any* valid `OpenApiOutput.g.cs` file generated by the source generator, parsing the file to extract the JSON string SHALL produce output identical to the JSON embedded in the assembly attribute.

**Validates: Requirements 11.2, 11.4**

### Property 4: HTTP Method Mapping Completeness

*For any* Lambda function with a valid HTTP method attribute (GET, POST, PUT, DELETE, PATCH, HEAD, OPTIONS), the generated OpenAPI specification SHALL include an operation under the corresponding lowercase method key.

**Validates: Requirements 6.1, 6.2, 6.3**

## Error Handling

| Scenario | Behavior |
|----------|----------|
| Task without generic argument | Generate response with no content schema |
| Unknown HTTP method | Log warning, skip endpoint |
| Invalid security scheme config | Log error, skip scheme |
| Generated file not found (AOT) | Fall back to reflection |
| Reflection fails (AOT) | Log error with EmitCompilerGeneratedFiles instructions |
| Malformed generated file | Log error, fail extraction |

## Testing Strategy

### Property-Based Testing

Use **FsCheck** (or **Hedgehog** for C#) for property-based tests:

1. **Task<T> Unwrapping Property Test**
   - Generate random type structures
   - Wrap in Task<T>/ValueTask<T>
   - Verify schema equivalence

2. **Security Scheme Round-Trip Property Test**
   - Generate random valid security configurations
   - Verify all properties appear in output

3. **JSON Extraction Property Test**
   - Generate valid OpenAPI JSON strings
   - Create mock .g.cs file content
   - Verify extraction produces identical JSON

### Unit Tests

| Test | Description |
|------|-------------|
| `Task_String_UnwrapsToString` | Task<string> produces string schema |
| `Task_ComplexType_UnwrapsToComplexType` | Task<Order> produces Order schema |
| `Task_NonGeneric_ProducesNoContent` | Task produces empty response |
| `ValueTask_UnwrapsCorrectly` | ValueTask<T> behaves like Task<T> |
| `Patch_Method_GeneratesCorrectOperation` | PATCH produces "patch" key |
| `Head_Method_GeneratesCorrectOperation` | HEAD produces "head" key |
| `Options_Method_GeneratesCorrectOperation` | OPTIONS produces "options" key |
| `NoSecurityAttributes_NoSecuritySchemes` | Empty security when no attributes |
| `ApiKeyAttribute_GeneratesApiKeyScheme` | API key config maps correctly |
| `OAuth2Attribute_GeneratesOAuth2Scheme` | OAuth2 config maps correctly |
| `ExtractFromGeneratedFile_MatchesReflection` | Both extraction methods produce same result |
| `ExtractFromGeneratedFile_HandlesEscapedQuotes` | Escaped quotes in JSON handled |
| `NoGeneratedFile_FallsBackToReflection` | Fallback behavior works |

### Integration Tests

| Test | Description |
|------|-------------|
| `FullBuild_WithEmitGeneratedFiles_ExtractsSpec` | End-to-end with AOT-compatible path |
| `FullBuild_WithoutEmitGeneratedFiles_ExtractsSpec` | End-to-end with reflection path |
