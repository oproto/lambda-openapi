# Design Document: Tag Groups Extension and Automatic Example Generation

## Overview

This design document describes the implementation of two features for the Oproto.Lambda.OpenApi library:

1. **Tag Groups Extension (`x-tagGroups`)**: Adds support for the OpenAPI vendor extension that groups related tags together, enabling better organization in documentation tools like Redoc.

2. **Automatic Example Generation**: Enables automatic composition of request/response examples from property-level `[OpenApiSchema(Example = "...")]` values, reducing manual JSON string maintenance.

Both features require changes to the source generator (`Oproto.Lambda.OpenApi.SourceGenerator`) and the merge tool (`Oproto.Lambda.OpenApi.Merge`).

## Architecture

### Component Overview

```
┌─────────────────────────────────────────────────────────────────┐
│                    Oproto.Lambda.OpenApi                        │
│  ┌─────────────────────────────────────────────────────────┐   │
│  │                    Attributes                            │   │
│  │  - OpenApiTagGroupAttribute (NEW)                        │   │
│  │  - OpenApiExampleConfigAttribute (NEW)                   │   │
│  │  - OpenApiSchemaAttribute (existing - Example property)  │   │
│  └─────────────────────────────────────────────────────────┘   │
└─────────────────────────────────────────────────────────────────┘
                              │
                              ▼
┌─────────────────────────────────────────────────────────────────┐
│              Oproto.Lambda.OpenApi.SourceGenerator              │
│  ┌─────────────────────────────────────────────────────────┐   │
│  │              OpenApiSpecGenerator                        │   │
│  │  - GetTagGroupsFromAssembly() (NEW)                      │   │
│  │  - ComposeExampleFromProperties() (NEW)                  │   │
│  │  - GenerateDefaultExample() (NEW)                        │   │
│  │  - ApplyTagGroupsExtension() (NEW)                       │   │
│  └─────────────────────────────────────────────────────────┘   │
└─────────────────────────────────────────────────────────────────┘
                              │
                              ▼
┌─────────────────────────────────────────────────────────────────┐
│                 Oproto.Lambda.OpenApi.Merge                     │
│  ┌─────────────────────────────────────────────────────────┐   │
│  │                   OpenApiMerger                          │   │
│  │  - MergeTagGroups() (NEW)                                │   │
│  │  - ReadTagGroupsExtension() (NEW)                        │   │
│  │  - WriteTagGroupsExtension() (NEW)                       │   │
│  └─────────────────────────────────────────────────────────┘   │
└─────────────────────────────────────────────────────────────────┘
```

### Data Flow

**Tag Groups:**
```
[assembly: OpenApiTagGroup("User Management", "Users", "Auth")]
                    │
                    ▼
         GetTagGroupsFromAssembly()
                    │
                    ▼
         ApplyTagGroupsExtension()
                    │
                    ▼
    OpenApiDocument.Extensions["x-tagGroups"]
                    │
                    ▼
         JSON Output: { "x-tagGroups": [...] }
```

**Example Composition:**
```
public class Product {
    [OpenApiSchema(Example = "prod-123")]
    public string Id { get; set; }
    
    [OpenApiSchema(Example = "Widget")]
    public string Name { get; set; }
}
                    │
                    ▼
      ComposeExampleFromProperties()
                    │
                    ▼
    { "id": "prod-123", "name": "Widget" }
```

## Components and Interfaces

### New Attributes

#### OpenApiTagGroupAttribute

```csharp
namespace Oproto.Lambda.OpenApi.Attributes
{
    /// <summary>
    /// Defines a tag group for organizing related tags in the OpenAPI specification.
    /// </summary>
    [AttributeUsage(AttributeTargets.Assembly, AllowMultiple = true)]
    public class OpenApiTagGroupAttribute : Attribute
    {
        /// <summary>
        /// Initializes a new instance with the group name and associated tags.
        /// </summary>
        /// <param name="name">The name of the tag group.</param>
        /// <param name="tags">The tags that belong to this group.</param>
        public OpenApiTagGroupAttribute(string name, params string[] tags)
        {
            Name = name ?? throw new ArgumentNullException(nameof(name));
            Tags = tags ?? Array.Empty<string>();
        }

        /// <summary>
        /// Gets the name of the tag group.
        /// </summary>
        public string Name { get; }

        /// <summary>
        /// Gets the tags that belong to this group.
        /// </summary>
        public string[] Tags { get; }
    }
}
```

#### OpenApiExampleConfigAttribute

```csharp
namespace Oproto.Lambda.OpenApi.Attributes
{
    /// <summary>
    /// Configures automatic example generation behavior at the assembly level.
    /// </summary>
    [AttributeUsage(AttributeTargets.Assembly)]
    public class OpenApiExampleConfigAttribute : Attribute
    {
        /// <summary>
        /// Gets or sets whether to automatically compose examples from property-level examples.
        /// Default is true.
        /// </summary>
        public bool ComposeFromProperties { get; set; } = true;

        /// <summary>
        /// Gets or sets whether to generate default examples for properties without explicit examples.
        /// Default is false.
        /// </summary>
        public bool GenerateDefaults { get; set; } = false;
    }
}
```

### Source Generator Extensions

#### Tag Group Processing

```csharp
// In OpenApiSpecGenerator
private List<TagGroupInfo> GetTagGroupsFromAssembly(Compilation? compilation)
{
    var tagGroups = new List<TagGroupInfo>();
    
    if (compilation == null)
        return tagGroups;

    foreach (var attr in compilation.Assembly.GetAttributes())
    {
        if (attr.AttributeClass?.Name != "OpenApiTagGroupAttribute")
            continue;

        // Constructor args: (string name, params string[] tags)
        if (attr.ConstructorArguments.Length < 1)
            continue;

        var name = attr.ConstructorArguments[0].Value as string;
        if (string.IsNullOrEmpty(name))
            continue;

        var tags = new List<string>();
        if (attr.ConstructorArguments.Length > 1)
        {
            var tagsArg = attr.ConstructorArguments[1];
            if (tagsArg.Kind == TypedConstantKind.Array)
            {
                tags.AddRange(tagsArg.Values
                    .Select(v => v.Value as string)
                    .Where(t => !string.IsNullOrEmpty(t)));
            }
        }

        tagGroups.Add(new TagGroupInfo { Name = name, Tags = tags });
    }

    return tagGroups;
}

private void ApplyTagGroupsExtension(OpenApiDocument document, List<TagGroupInfo> tagGroups)
{
    if (tagGroups == null || tagGroups.Count == 0)
        return;

    var tagGroupsArray = new OpenApiArray();
    foreach (var group in tagGroups)
    {
        var groupObject = new OpenApiObject
        {
            ["name"] = new OpenApiString(group.Name),
            ["tags"] = new OpenApiArray(group.Tags.Select(t => new OpenApiString(t)).ToList<IOpenApiAny>())
        };
        tagGroupsArray.Add(groupObject);
    }

    document.Extensions["x-tagGroups"] = tagGroupsArray;
}
```

#### Example Composition

```csharp
// In OpenApiSpecGenerator
private IOpenApiAny? ComposeExampleFromProperties(ITypeSymbol typeSymbol, ExampleConfig config)
{
    if (typeSymbol is not INamedTypeSymbol namedType)
        return null;

    var exampleObject = new OpenApiObject();
    var hasAnyExample = false;

    foreach (var member in namedType.GetMembers().OfType<IPropertySymbol>())
    {
        // Skip ignored properties
        if (HasAttribute(member, "OpenApiIgnore"))
            continue;

        var propertyName = GetJsonPropertyName(member);
        var example = GetPropertyExample(member, config);
        
        if (example != null)
        {
            exampleObject[propertyName] = example;
            hasAnyExample = true;
        }
    }

    return hasAnyExample ? exampleObject : null;
}

private IOpenApiAny? GetPropertyExample(IPropertySymbol property, ExampleConfig config)
{
    // First check for explicit example in OpenApiSchema attribute
    var schemaAttr = property.GetAttributes()
        .FirstOrDefault(a => a.AttributeClass?.Name == "OpenApiSchemaAttribute");
    
    if (schemaAttr != null)
    {
        var exampleArg = schemaAttr.NamedArguments
            .FirstOrDefault(a => a.Key == "Example");
        
        if (exampleArg.Value.Value is string exampleValue)
        {
            return ConvertExampleToTypedValue(exampleValue, property.Type);
        }
    }

    // If no explicit example and defaults are enabled, generate one
    if (config.GenerateDefaults)
    {
        return GenerateDefaultExample(property);
    }

    return null;
}

private IOpenApiAny ConvertExampleToTypedValue(string value, ITypeSymbol type)
{
    // Convert string example to appropriate JSON type based on property type
    return type.SpecialType switch
    {
        SpecialType.System_Int32 when int.TryParse(value, out var i) => new OpenApiInteger(i),
        SpecialType.System_Int64 when long.TryParse(value, out var l) => new OpenApiLong(l),
        SpecialType.System_Double when double.TryParse(value, out var d) => new OpenApiDouble(d),
        SpecialType.System_Decimal when decimal.TryParse(value, out var m) => new OpenApiDouble((double)m),
        SpecialType.System_Single when float.TryParse(value, out var f) => new OpenApiFloat(f),
        SpecialType.System_Boolean when bool.TryParse(value, out var b) => new OpenApiBoolean(b),
        _ => new OpenApiString(value)
    };
}

private IOpenApiAny? GenerateDefaultExample(IPropertySymbol property)
{
    var type = property.Type;
    var schemaAttr = property.GetAttributes()
        .FirstOrDefault(a => a.AttributeClass?.Name == "OpenApiSchemaAttribute");

    // Check for format hint
    string? format = null;
    if (schemaAttr != null)
    {
        var formatArg = schemaAttr.NamedArguments.FirstOrDefault(a => a.Key == "Format");
        format = formatArg.Value.Value as string;
    }

    // Generate based on format
    if (!string.IsNullOrEmpty(format))
    {
        return format.ToLower() switch
        {
            "email" => new OpenApiString("user@example.com"),
            "uuid" => new OpenApiString("550e8400-e29b-41d4-a716-446655440000"),
            "date-time" => new OpenApiString(DateTime.UtcNow.ToString("O")),
            "date" => new OpenApiString(DateTime.UtcNow.ToString("yyyy-MM-dd")),
            "uri" or "url" => new OpenApiString("https://example.com"),
            "hostname" => new OpenApiString("example.com"),
            "ipv4" => new OpenApiString("192.168.1.1"),
            "ipv6" => new OpenApiString("::1"),
            _ => null
        };
    }

    // Generate based on type
    return type.SpecialType switch
    {
        SpecialType.System_String => new OpenApiString("string"),
        SpecialType.System_Int32 => new OpenApiInteger(0),
        SpecialType.System_Int64 => new OpenApiLong(0),
        SpecialType.System_Double => new OpenApiDouble(0.0),
        SpecialType.System_Decimal => new OpenApiDouble(0.0),
        SpecialType.System_Boolean => new OpenApiBoolean(false),
        _ => null
    };
}
```

### Merge Tool Extensions

#### Tag Group Merging

```csharp
// In OpenApiMerger
private void MergeTagGroups(
    OpenApiDocument mergedDocument,
    List<(SourceConfiguration Source, OpenApiDocument Document)> documents)
{
    var mergedGroups = new Dictionary<string, List<string>>();
    var groupOrder = new List<string>();

    foreach (var (source, document) in documents)
    {
        var tagGroups = ReadTagGroupsExtension(document);
        
        foreach (var group in tagGroups)
        {
            if (!mergedGroups.ContainsKey(group.Name))
            {
                mergedGroups[group.Name] = new List<string>();
                groupOrder.Add(group.Name);
            }

            // Merge tags, deduplicating
            foreach (var tag in group.Tags)
            {
                if (!mergedGroups[group.Name].Contains(tag))
                {
                    mergedGroups[group.Name].Add(tag);
                }
            }
        }
    }

    if (mergedGroups.Count > 0)
    {
        WriteTagGroupsExtension(mergedDocument, groupOrder, mergedGroups);
    }
}

private List<TagGroupInfo> ReadTagGroupsExtension(OpenApiDocument document)
{
    var tagGroups = new List<TagGroupInfo>();

    if (!document.Extensions.TryGetValue("x-tagGroups", out var extension))
        return tagGroups;

    if (extension is not OpenApiArray groupsArray)
        return tagGroups;

    foreach (var item in groupsArray)
    {
        if (item is not OpenApiObject groupObj)
            continue;

        var name = (groupObj["name"] as OpenApiString)?.Value;
        if (string.IsNullOrEmpty(name))
            continue;

        var tags = new List<string>();
        if (groupObj["tags"] is OpenApiArray tagsArray)
        {
            tags.AddRange(tagsArray
                .OfType<OpenApiString>()
                .Select(s => s.Value)
                .Where(t => !string.IsNullOrEmpty(t)));
        }

        tagGroups.Add(new TagGroupInfo { Name = name, Tags = tags });
    }

    return tagGroups;
}

private void WriteTagGroupsExtension(
    OpenApiDocument document,
    List<string> groupOrder,
    Dictionary<string, List<string>> groups)
{
    var tagGroupsArray = new OpenApiArray();

    foreach (var groupName in groupOrder)
    {
        if (!groups.TryGetValue(groupName, out var tags))
            continue;

        var groupObject = new OpenApiObject
        {
            ["name"] = new OpenApiString(groupName),
            ["tags"] = new OpenApiArray(tags.Select(t => new OpenApiString(t)).ToList<IOpenApiAny>())
        };
        tagGroupsArray.Add(groupObject);
    }

    document.Extensions["x-tagGroups"] = tagGroupsArray;
}
```

## Data Models

### TagGroupInfo

```csharp
/// <summary>
/// Represents a tag group for the x-tagGroups extension.
/// </summary>
internal class TagGroupInfo
{
    /// <summary>
    /// The name of the tag group.
    /// </summary>
    public string Name { get; set; }

    /// <summary>
    /// The tags that belong to this group.
    /// </summary>
    public List<string> Tags { get; set; } = new List<string>();
}
```

### ExampleConfig

```csharp
/// <summary>
/// Configuration for automatic example generation.
/// </summary>
internal class ExampleConfig
{
    /// <summary>
    /// Whether to compose examples from property-level examples.
    /// </summary>
    public bool ComposeFromProperties { get; set; } = true;

    /// <summary>
    /// Whether to generate default examples for properties without explicit examples.
    /// </summary>
    public bool GenerateDefaults { get; set; } = false;
}
```

## Correctness Properties

*A property is a characteristic or behavior that should hold true across all valid executions of a system-essentially, a formal statement about what the system should do. Properties serve as the bridge between human-readable specifications and machine-verifiable correctness guarantees.*


### Tag Groups Properties

**Property 1: Tag group attribute parsing**
*For any* `OpenApiTagGroupAttribute` with a valid name and array of tags, the Source_Generator SHALL correctly extract the group name and all associated tags.
**Validates: Requirements 1.2**

**Property 2: Tag group order preservation**
*For any* sequence of `OpenApiTagGroupAttribute` attributes applied to an assembly, the Source_Generator SHALL output the tag groups in the same order as they are defined.
**Validates: Requirements 1.3, 2.4**

**Property 3: Tag group output presence**
*For any* assembly with one or more `OpenApiTagGroupAttribute` attributes, the generated OpenAPI document SHALL contain an `x-tagGroups` extension at the root level.
**Validates: Requirements 2.1**

**Property 4: Tag group JSON structure**
*For any* tag group in the output, the JSON object SHALL contain a `name` property (string) and a `tags` property (array of strings).
**Validates: Requirements 2.2, 5.1, 5.2**

**Property 5: Tag group merge combination**
*For any* set of OpenAPI documents with tag groups, merging them SHALL result in all tag groups from all sources appearing in the merged output.
**Validates: Requirements 3.1**

**Property 6: Tag group merge same-name merging**
*For any* two OpenAPI documents that define tag groups with the same name, the Merge_Tool SHALL combine the tags from both groups into a single group with that name.
**Validates: Requirements 3.2**

**Property 7: Tag group merge deduplication**
*For any* merged tag group, the tags array SHALL contain no duplicate entries.
**Validates: Requirements 3.3**

**Property 8: Tag group merge preservation**
*For any* set of OpenAPI documents where some have tag groups and others do not, all tag groups from documents that have them SHALL be preserved in the merged output.
**Validates: Requirements 3.4**

**Property 9: Tag group merge order**
*For any* sequence of OpenAPI documents being merged, tag groups SHALL appear in the order of their first occurrence across all documents, with new groups appended after existing ones.
**Validates: Requirements 4.1, 4.2, 4.3**

**Property 10: Tag group round-trip**
*For any* valid `x-tagGroups` extension in an OpenAPI document, reading and then writing the document SHALL produce an equivalent `x-tagGroups` structure.
**Validates: Requirements 5.3**

### Example Generation Properties

**Property 11: Example composition from properties**
*For any* type where properties have `[OpenApiSchema(Example = "...")]` values, the Source_Generator SHALL compose an example object containing all properties that have examples.
**Validates: Requirements 6.1, 6.2**

**Property 12: Partial example composition**
*For any* type where only some properties have example values, the composed example SHALL include only those properties with examples, excluding properties without examples.
**Validates: Requirements 6.3**

**Property 13: Example placement in schemas**
*For any* request body or response schema type with composed examples, the example SHALL appear in the appropriate location in the OpenAPI document.
**Validates: Requirements 6.4**

**Property 14: Explicit example precedence**
*For any* operation that has both property-level examples (for composition) and an explicit `[OpenApiExample]` attribute, the explicit example SHALL be used instead of the composed example.
**Validates: Requirements 6.5**

**Property 15: Example type conversion**
*For any* property with an example value, the example SHALL be converted to the appropriate JSON type: numeric types to JSON numbers, boolean types to JSON booleans, and string types to JSON strings.
**Validates: Requirements 7.1, 7.2, 7.3**

**Property 16: Array example generation**
*For any* array-type property with element examples, the Source_Generator SHALL generate an array example containing appropriately typed elements.
**Validates: Requirements 7.4**

**Property 17: Nested object example composition**
*For any* property of a complex object type, the Source_Generator SHALL recursively compose examples from the nested type's property examples.
**Validates: Requirements 7.5**

**Property 18: Format-based default generation**
*For any* string property with a `Format` specified (email, uuid, date-time, etc.) but no explicit example, the Source_Generator SHALL generate a format-appropriate default example when default generation is enabled.
**Validates: Requirements 8.1**

**Property 19: Constraint-based numeric defaults**
*For any* numeric property with `Minimum` and/or `Maximum` constraints but no explicit example, the generated default example SHALL be within the specified bounds when default generation is enabled.
**Validates: Requirements 8.2**

**Property 20: Constraint-based string defaults**
*For any* string property with `MinLength` and/or `MaxLength` constraints but no explicit example, the generated default example SHALL have a length within the specified bounds when default generation is enabled.
**Validates: Requirements 8.3**

**Property 21: Type-based placeholder defaults**
*For any* property without an explicit example and without constraints, the Source_Generator SHALL generate a type-appropriate placeholder value when default generation is enabled.
**Validates: Requirements 8.4**

**Property 22: Config disables auto-generation**
*For any* assembly where automatic example generation is disabled via configuration, the Source_Generator SHALL NOT compose examples from property-level values and SHALL only use explicit `[OpenApiExample]` attributes.
**Validates: Requirements 9.3**

**Property 23: Independent config options**
*For any* configuration, the `ComposeFromProperties` and `GenerateDefaults` options SHALL operate independently, allowing example composition without default generation.
**Validates: Requirements 9.5**

## Error Handling

### Tag Groups

| Error Condition | Handling |
|-----------------|----------|
| Empty tag group name | Skip the attribute, do not include in output |
| Empty tags array | Include group with empty tags array (valid per spec) |
| Invalid attribute format | Skip the attribute silently |
| Duplicate group names in same assembly | Include both (later one wins in output) |

### Example Generation

| Error Condition | Handling |
|-----------------|----------|
| Example value cannot be parsed as target type | Fall back to string representation |
| Circular reference in nested types | Stop recursion, use null or skip property |
| Property marked with `[OpenApiIgnore]` | Skip property in composed example |
| Empty example string | Include as empty string in output |

## Testing Strategy

### Property-Based Testing

The implementation will use **FsCheck** for property-based testing in C#. Each correctness property will be implemented as a property test with minimum 100 iterations.

**Test Configuration:**
```csharp
[Property(MaxTest = 100)]
public Property TagGroupOrderPreservation()
{
    return Prop.ForAll(
        Arb.From<NonEmptyArray<TagGroupInfo>>(),
        tagGroups => {
            // Test implementation
        });
}
```

### Unit Tests

Unit tests will cover:
- Specific examples demonstrating correct behavior
- Edge cases (empty inputs, null values, boundary conditions)
- Integration points between components
- Error conditions and fallback behavior

### Test Organization

```
Oproto.Lambda.OpenApi.Tests/
├── TagGroupAttributeTests.cs          # Attribute parsing tests
├── TagGroupGenerationPropertyTests.cs # Property tests for generation
├── TagGroupMergePropertyTests.cs      # Property tests for merging
├── ExampleCompositionPropertyTests.cs # Property tests for examples
├── ExampleTypeConversionTests.cs      # Type conversion tests
└── ExampleConfigurationTests.cs       # Configuration tests
```

### Test Data Generators

Custom FsCheck generators will be created for:
- `TagGroupInfo` with valid names and tag arrays
- `OpenApiDocument` with various tag group configurations
- Property types with example values
- Nested object structures for recursive testing
