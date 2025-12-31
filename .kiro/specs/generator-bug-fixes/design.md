# Design Document: Generator Bug Fixes

## Overview

This design addresses five distinct bugs in the OpenAPI generator and merge tool:

1. **Path Parameter Generation**: Route template parameters not being defined in the OpenAPI output
2. **DateOnly/TimeOnly Handling**: These .NET 6+ types need proper OpenAPI schema mapping
3. **Class-Level Tag Support**: The `[OpenApiTag]` attribute should work at class level
4. **AWS Type Exclusion**: Lambda payload types should not appear in API documentation
5. **Tilde Path Expansion**: Merge tool should support `~/` in configuration paths

## Architecture

Each fix is isolated to specific components:

| Issue | Component | File(s) |
|-------|-----------|---------|
| Path Parameters | Source Generator | `OpenApiSpecGenerator.cs` |
| DateOnly/TimeOnly | Source Generator | `OpenApiSpecGenerator_Schema.cs` |
| Class-Level Tags | Source Generator | `OpenApiSpecGenerator.cs` |
| AWS Type Exclusion | Source Generator | `OpenApiSpecGenerator.cs` |
| Tilde Expansion | Merge Tool | `MergeCommand.cs`, `SourceConfiguration.cs` |

## Components and Interfaces

### Fix 1: Path Parameter Generation

The issue is that path parameters in route templates (e.g., `/companies/{companyId}`) are not being defined when there's no corresponding `[FromRoute]` method parameter.

**Current Behavior**: Only parameters with `[FromRoute]` attributes are included.

**Required Behavior**: Parse the route template and ensure all `{paramName}` placeholders have corresponding parameter definitions.

```csharp
// In OpenApiSpecGenerator.cs

/// <summary>
/// Extracts path parameters from a route template string.
/// </summary>
/// <param name="routeTemplate">The route template (e.g., "/companies/{companyId}/locations/{locationId}")</param>
/// <returns>List of parameter names found in the template.</returns>
private static List<string> ExtractPathParametersFromTemplate(string routeTemplate)
{
    var parameters = new List<string>();
    var regex = new Regex(@"\{([^}]+)\}");
    var matches = regex.Matches(routeTemplate);
    
    foreach (Match match in matches)
    {
        var paramName = match.Groups[1].Value;
        // Handle constraint syntax like {id:int}
        var colonIndex = paramName.IndexOf(':');
        if (colonIndex > 0)
            paramName = paramName.Substring(0, colonIndex);
        parameters.Add(paramName);
    }
    
    return parameters;
}

/// <summary>
/// Ensures all path parameters from the route template are defined in the operation.
/// </summary>
private List<OpenApiParameter> EnsurePathParametersDefined(
    string routeTemplate,
    List<ParameterInfo> methodParameters,
    List<OpenApiParameter> existingParameters)
{
    var templateParams = ExtractPathParametersFromTemplate(routeTemplate);
    var result = new List<OpenApiParameter>(existingParameters);
    
    foreach (var templateParam in templateParams)
    {
        // Check if already defined
        if (result.Any(p => p.Name == templateParam && p.In == ParameterLocation.Path))
            continue;
            
        // Check if there's a method parameter with this name
        var methodParam = methodParameters.FirstOrDefault(
            p => p.Name.Equals(templateParam, StringComparison.OrdinalIgnoreCase));
        
        // Create parameter definition
        var param = new OpenApiParameter
        {
            Name = templateParam,
            In = ParameterLocation.Path,
            Required = true, // Path parameters are always required
            Schema = methodParam != null 
                ? CreateSchema(methodParam.TypeSymbol) 
                : new OpenApiSchema { Type = "string" }
        };
        
        result.Add(param);
    }
    
    return result;
}
```

### Fix 2: DateOnly/TimeOnly Type Handling

Add type mappings for .NET 6+ date/time types.

```csharp
// In OpenApiSpecGenerator_Schema.cs - modify GetOpenApiType method

private (string Type, string Format) GetOpenApiType(ITypeSymbol typeSymbol)
{
    var typeName = typeSymbol.ToDisplayString();
    
    // Handle nullable types
    if (typeSymbol is INamedTypeSymbol namedType && 
        namedType.OriginalDefinition.SpecialType == SpecialType.System_Nullable_T)
    {
        typeName = namedType.TypeArguments[0].ToDisplayString();
    }
    
    return typeName switch
    {
        // Existing mappings...
        "System.DateTime" => ("string", "date-time"),
        "System.DateTimeOffset" => ("string", "date-time"),
        
        // New mappings for .NET 6+ types
        "System.DateOnly" => ("string", "date"),
        "System.TimeOnly" => ("string", "time"),
        
        // ... other mappings
        _ => ("object", null)
    };
}

// Ensure these types are NOT added to components/schemas
private bool IsBuiltInType(ITypeSymbol typeSymbol)
{
    var typeName = typeSymbol.ToDisplayString();
    var builtInTypes = new HashSet<string>
    {
        "System.String", "System.Int32", "System.Int64", "System.Boolean",
        "System.Double", "System.Decimal", "System.DateTime", "System.DateTimeOffset",
        "System.Guid", "System.TimeSpan", "System.Uri",
        // Add new types
        "System.DateOnly", "System.TimeOnly"
    };
    
    return builtInTypes.Contains(typeName);
}
```

### Fix 3: Class-Level Tag Support

Modify `ExtractTagsFromMethod` to also check the containing class.

```csharp
// In OpenApiSpecGenerator.cs

/// <summary>
/// Extracts tag names from [OpenApiTag] attributes on a method or its containing class.
/// Method-level tags take precedence over class-level tags.
/// Returns a list with "Default" if no tags are specified.
/// </summary>
private List<string> ExtractTagsFromMethod(IMethodSymbol methodSymbol)
{
    var tags = new List<string>();

    // First, check method-level tags
    foreach (var attr in methodSymbol.GetAttributes())
    {
        if (attr.AttributeClass?.Name != "OpenApiTagAttribute")
            continue;

        if (attr.ConstructorArguments.Length > 0 &&
            attr.ConstructorArguments[0].Value is string tagName &&
            !string.IsNullOrEmpty(tagName))
        {
            tags.Add(tagName);
        }
    }

    // If method has tags, use those (method-level takes precedence)
    if (tags.Count > 0)
        return tags;

    // Otherwise, check class-level tags
    var containingType = methodSymbol.ContainingType;
    if (containingType != null)
    {
        foreach (var attr in containingType.GetAttributes())
        {
            if (attr.AttributeClass?.Name != "OpenApiTagAttribute")
                continue;

            if (attr.ConstructorArguments.Length > 0 &&
                attr.ConstructorArguments[0].Value is string tagName &&
                !string.IsNullOrEmpty(tagName))
            {
                tags.Add(tagName);
            }
        }
    }

    // Default to "Default" tag if none specified
    if (tags.Count == 0)
    {
        tags.Add("Default");
    }

    return tags;
}
```

### Fix 4: AWS Type Exclusion

Filter out AWS Lambda types from parameters and schemas.

```csharp
// In OpenApiSpecGenerator.cs

/// <summary>
/// Determines if a type is an AWS Lambda infrastructure type that should be excluded.
/// </summary>
private bool IsAwsLambdaType(ITypeSymbol typeSymbol)
{
    var typeName = typeSymbol.ToDisplayString();
    var containingNamespace = typeSymbol.ContainingNamespace?.ToDisplayString() ?? "";
    
    // Check namespace
    if (containingNamespace.StartsWith("Amazon.Lambda."))
        return true;
    
    // Check common type names
    var awsTypeNames = new HashSet<string>
    {
        "APIGatewayProxyRequest",
        "APIGatewayProxyResponse",
        "APIGatewayHttpApiV2ProxyRequest",
        "APIGatewayHttpApiV2ProxyResponse",
        "ILambdaContext",
        "LambdaContext"
    };
    
    return awsTypeNames.Contains(typeSymbol.Name);
}

// Modify parameter extraction to exclude AWS types
private List<ParameterInfo> ExtractParameters(IMethodSymbol methodSymbol)
{
    var parameters = new List<ParameterInfo>();
    
    foreach (var parameter in methodSymbol.Parameters)
    {
        // Skip FromServices parameters
        if (HasAttribute(parameter, "FromServicesAttribute"))
            continue;
            
        // Skip AWS Lambda types without explicit [FromBody]
        if (IsAwsLambdaType(parameter.Type) && !HasAttribute(parameter, "FromBodyAttribute"))
            continue;
        
        // ... rest of parameter extraction
    }
    
    return parameters;
}

// Modify schema generation to exclude AWS types
private bool ShouldGenerateSchema(ITypeSymbol typeSymbol)
{
    if (IsBuiltInType(typeSymbol))
        return false;
        
    if (IsAwsLambdaType(typeSymbol))
        return false;
        
    return true;
}
```

### Fix 5: Tilde Path Expansion

Add path expansion utility to the merge tool.

```csharp
// New file: Oproto.Lambda.OpenApi.Merge/PathExpander.cs

namespace Oproto.Lambda.OpenApi.Merge;

/// <summary>
/// Utility for expanding paths with tilde notation.
/// </summary>
public static class PathExpander
{
    /// <summary>
    /// Expands a path that may contain tilde notation.
    /// </summary>
    /// <param name="path">The path to expand.</param>
    /// <returns>The expanded path.</returns>
    /// <exception cref="ArgumentException">Thrown when tilde expansion fails.</exception>
    public static string ExpandPath(string path)
    {
        if (string.IsNullOrEmpty(path))
            return path;
            
        if (!path.StartsWith("~"))
            return path;
            
        // Handle ~/path (current user's home)
        if (path.StartsWith("~/") || path == "~")
        {
            var home = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);
            if (string.IsNullOrEmpty(home))
                throw new ArgumentException($"Cannot expand path '{path}': Unable to determine home directory.");
                
            return path == "~" ? home : Path.Combine(home, path.Substring(2));
        }
        
        // Handle ~username/path (Unix-style, other user's home)
        // This is primarily for Unix systems
        var slashIndex = path.IndexOf('/');
        var username = slashIndex > 0 ? path.Substring(1, slashIndex - 1) : path.Substring(1);
        
        // On Windows, we don't support ~username syntax
        if (Environment.OSVersion.Platform == PlatformID.Win32NT)
            throw new ArgumentException($"Cannot expand path '{path}': ~username syntax is not supported on Windows.");
            
        // On Unix, try to resolve the user's home directory
        var userHome = $"/home/{username}";
        if (!Directory.Exists(userHome))
        {
            // Try /Users for macOS
            userHome = $"/Users/{username}";
            if (!Directory.Exists(userHome))
                throw new ArgumentException($"Cannot expand path '{path}': User '{username}' not found.");
        }
        
        return slashIndex > 0 
            ? Path.Combine(userHome, path.Substring(slashIndex + 1))
            : userHome;
    }
}

// Modify MergeCommand.cs to use PathExpander
private static async Task<OpenApiDocument?> LoadOpenApiDocumentAsync(string filePath, bool verbose)
{
    // Expand tilde in path
    var expandedPath = PathExpander.ExpandPath(filePath);
    
    if (!File.Exists(expandedPath))
    {
        Console.Error.WriteLine($"Error: Source file not found: {filePath}");
        if (expandedPath != filePath)
            Console.Error.WriteLine($"  (expanded to: {expandedPath})");
        return null;
    }
    
    // ... rest of loading logic
}
```

## Data Models

No new data models required. Existing OpenAPI models from Microsoft.OpenApi are used.

## Correctness Properties

*A property is a characteristic or behavior that should hold true across all valid executions of a system-essentially, a formal statement about what the system should do. Properties serve as the bridge between human-readable specifications and machine-verifiable correctness guarantees.*

### Property 1: Path Parameter Completeness

*For any* route template containing path parameters (e.g., `{id}`, `{companyId}`), the generated OpenAPI operation SHALL contain a parameter definition for each path parameter with `in: path` and `required: true`.

**Validates: Requirements 1.1, 1.4**

### Property 2: Path Parameter Type Inference

*For any* path parameter that has a corresponding `[FromRoute]` method parameter, the generated parameter schema SHALL use the method parameter's type. For path parameters without a corresponding method parameter, the schema SHALL default to `type: string`.

**Validates: Requirements 1.2, 1.3**

### Property 3: DateOnly/TimeOnly Schema Mapping

*For any* property or parameter of type `DateOnly`, `TimeOnly`, `DateOnly?`, or `TimeOnly?`, the generated schema SHALL have `type: string` with the appropriate format (`date` for DateOnly, `time` for TimeOnly), and these types SHALL NOT appear as separate schema definitions in components/schemas.

**Validates: Requirements 2.1, 2.2, 2.3, 2.4**

### Property 4: Class-Level Tag Inheritance

*For any* operation method without method-level `[OpenApiTag]` attributes, if the containing class has `[OpenApiTag]` attributes, the operation SHALL be assigned all class-level tags.

**Validates: Requirements 3.1, 3.3**

### Property 5: Method-Level Tag Precedence

*For any* operation method with method-level `[OpenApiTag]` attributes, the operation SHALL use only the method-level tags, ignoring any class-level tags.

**Validates: Requirements 3.2**

### Property 6: AWS Type Exclusion

*For any* generated OpenAPI specification, the components/schemas section SHALL NOT contain AWS Lambda types (types from `Amazon.Lambda.*` namespaces or common Lambda type names like `APIGatewayProxyRequest`).

**Validates: Requirements 4.2, 4.3**

### Property 7: POST Without Body

*For any* POST operation where no parameter has the `[FromBody]` attribute, the operation SHALL NOT have a requestBody defined.

**Validates: Requirements 4.1**

### Property 8: Tilde Path Expansion

*For any* file path starting with `~/`, the merge tool SHALL expand it to the user's home directory before attempting to access the file.

**Validates: Requirements 5.1, 5.3**

## Error Handling

| Scenario | Handling |
|----------|----------|
| Path parameter in template but not in method | Generate with `type: string` |
| DateOnly/TimeOnly on older .NET | Type not recognized, falls back to object |
| Tilde expansion fails | Throw `ArgumentException` with clear message |
| AWS type detection false positive | User can use `[FromBody]` to force inclusion |
| Invalid route template syntax | Log warning, skip parameter extraction |

## Testing Strategy

### Property-Based Testing

We will use FsCheck for property-based testing, consistent with the existing test suite. Each correctness property will be implemented as a property-based test with minimum 100 iterations.

**Test Configuration:**
- Framework: xUnit with FsCheck.Xunit
- Minimum iterations: 100 per property
- Generators: Custom generators for route templates, type combinations, and tag configurations

### Unit Tests

Unit tests will cover:
- Specific route template patterns (single param, multiple params, constraints)
- DateOnly/TimeOnly nullable variants
- Tag inheritance edge cases (no tags, class only, method only, both)
- AWS type detection for various type names
- Tilde expansion on different platforms

### Test File Organization

```
Oproto.Lambda.OpenApi.Tests/
  PathParameterPropertyTests.cs      # Property tests for path parameter generation
  DateTimeTypePropertyTests.cs       # Property tests for DateOnly/TimeOnly
  TagInheritancePropertyTests.cs     # Property tests for class-level tags
  AwsTypeExclusionPropertyTests.cs   # Property tests for AWS type filtering

Oproto.Lambda.OpenApi.Merge.Tests/
  PathExpanderTests.cs               # Unit tests for tilde expansion
```

