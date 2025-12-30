# Design Document: Deterministic Output

## Overview

This design ensures that OpenAPI specification outputs are deterministic across multiple runs. The key insight is that non-determinism in OpenAPI output typically comes from dictionary/collection iteration order, which varies based on insertion order or hash codes. By enforcing consistent ordering at serialization time, we guarantee identical output for identical input.

The design also adds a "skip unchanged" feature to the merge tool, preventing unnecessary file writes when content hasn't changed.

## Architecture

The solution involves two main areas:

1. **Ordering Enforcement**: Modify the serialization/output phase to sort collections before writing
2. **Skip Unchanged**: Add file comparison logic to the merge tool CLI

### Ordering Strategy

Rather than modifying every place where collections are built, we'll implement ordering at the final output stage:

1. **Source Generator**: Sort collections in `MergeOpenApiDocs` before serialization
2. **Merger**: Sort collections in the `Merge` method before returning the result
3. **Merge Tool**: Sort collections before writing (as a safety net)

This approach minimizes code changes and ensures ordering regardless of how documents are constructed.

### Ordering Rules

| Collection | Ordering Rule |
|------------|---------------|
| Paths | Alphabetical by path string |
| Schemas | Alphabetical by schema name |
| Properties (within schemas) | Alphabetical by property name |
| Tags | Alphabetical by tag name |
| Tag Groups | Alphabetical by group name |
| Tags within Tag Groups | Alphabetical by tag name |
| Security Schemes | Alphabetical by scheme name |
| Operations (within paths) | HTTP method order: GET, PUT, POST, DELETE, OPTIONS, HEAD, PATCH, TRACE |
| Responses | Ascending by status code (numeric) |
| Examples | Alphabetical by example name |
| Servers | Preserve declaration/configuration order |

## Components and Interfaces

### New Component: OpenApiDocumentSorter

A static utility class that sorts all collections in an OpenAPI document for deterministic output.

```csharp
namespace Oproto.Lambda.OpenApi.Merge;

/// <summary>
/// Sorts OpenAPI document collections for deterministic output.
/// </summary>
public static class OpenApiDocumentSorter
{
    /// <summary>
    /// HTTP method ordering for operations within a path.
    /// </summary>
    private static readonly OperationType[] OperationOrder = 
    {
        OperationType.Get,
        OperationType.Put,
        OperationType.Post,
        OperationType.Delete,
        OperationType.Options,
        OperationType.Head,
        OperationType.Patch,
        OperationType.Trace
    };

    /// <summary>
    /// Sorts all collections in the document for deterministic output.
    /// Returns a new document with sorted collections.
    /// </summary>
    public static OpenApiDocument Sort(OpenApiDocument document);

    /// <summary>
    /// Sorts paths alphabetically.
    /// </summary>
    internal static OpenApiPaths SortPaths(OpenApiPaths paths);

    /// <summary>
    /// Sorts schemas alphabetically by name.
    /// </summary>
    internal static IDictionary<string, OpenApiSchema> SortSchemas(
        IDictionary<string, OpenApiSchema> schemas);

    /// <summary>
    /// Sorts properties within a schema alphabetically.
    /// </summary>
    internal static OpenApiSchema SortSchemaProperties(OpenApiSchema schema);

    /// <summary>
    /// Sorts tags alphabetically by name.
    /// </summary>
    internal static IList<OpenApiTag> SortTags(IList<OpenApiTag> tags);

    /// <summary>
    /// Sorts security schemes alphabetically by name.
    /// </summary>
    internal static IDictionary<string, OpenApiSecurityScheme> SortSecuritySchemes(
        IDictionary<string, OpenApiSecurityScheme> schemes);

    /// <summary>
    /// Sorts operations within a path by HTTP method order.
    /// </summary>
    internal static IDictionary<OperationType, OpenApiOperation> SortOperations(
        IDictionary<OperationType, OpenApiOperation> operations);

    /// <summary>
    /// Sorts responses by status code (ascending).
    /// </summary>
    internal static OpenApiResponses SortResponses(OpenApiResponses responses);

    /// <summary>
    /// Sorts examples alphabetically by name.
    /// </summary>
    internal static IDictionary<string, OpenApiExample> SortExamples(
        IDictionary<string, OpenApiExample> examples);

    /// <summary>
    /// Sorts tag groups alphabetically by name, and tags within groups alphabetically.
    /// </summary>
    internal static void SortTagGroups(OpenApiDocument document);
}
```

### Modified Component: OpenApiMerger

The `Merge` method will call `OpenApiDocumentSorter.Sort()` before returning the result.

```csharp
public MergeResult Merge(
    MergeConfiguration config,
    IEnumerable<(SourceConfiguration Source, OpenApiDocument Document)> documents)
{
    // ... existing merge logic ...

    // Sort for deterministic output
    var sortedDocument = OpenApiDocumentSorter.Sort(mergedDocument);

    return new MergeResult(sortedDocument, warnings, success: true);
}
```

### Modified Component: MergeCommand

Add `--force` flag and skip-unchanged logic.

```csharp
// New option
var forceOption = new Option<bool>(
    new[] { "-f", "--force" },
    "Force write output even if unchanged");

// Modified write logic
private static async Task<bool> WriteOpenApiDocumentAsync(
    OpenApiDocument document, 
    string outputPath, 
    bool verbose,
    bool force)
{
    // ... validation ...

    var json = document.SerializeAsJson(OpenApiSpecVersion.OpenApi3_0);
    
    // Check if file exists and content matches
    if (!force && File.Exists(outputPath))
    {
        var existingContent = await File.ReadAllTextAsync(outputPath);
        if (existingContent == json)
        {
            if (verbose)
            {
                Console.WriteLine($"Output unchanged, skipping write: {outputPath}");
            }
            return false; // Indicates file was not written
        }
    }

    await File.WriteAllTextAsync(outputPath, json);
    return true; // Indicates file was written
}
```

### Modified Component: OpenApiSpecGenerator (Source Generator)

The source generator will sort the merged document before serialization.

```csharp
private OpenApiDocument MergeOpenApiDocs(ImmutableArray<OpenApiDocument?> docs, Compilation? compilation)
{
    // ... existing merge logic ...

    // Sort for deterministic output before returning
    return SortDocument(mergedDoc);
}

/// <summary>
/// Sorts all collections in the document for deterministic output.
/// </summary>
private static OpenApiDocument SortDocument(OpenApiDocument document)
{
    // Sort paths
    if (document.Paths != null && document.Paths.Count > 0)
    {
        var sortedPaths = new OpenApiPaths();
        foreach (var path in document.Paths.OrderBy(p => p.Key, StringComparer.Ordinal))
        {
            sortedPaths[path.Key] = SortPathItem(path.Value);
        }
        document.Paths = sortedPaths;
    }

    // Sort schemas
    if (document.Components?.Schemas != null && document.Components.Schemas.Count > 0)
    {
        var sortedSchemas = new Dictionary<string, OpenApiSchema>();
        foreach (var schema in document.Components.Schemas.OrderBy(s => s.Key, StringComparer.Ordinal))
        {
            sortedSchemas[schema.Key] = SortSchemaProperties(schema.Value);
        }
        document.Components.Schemas = sortedSchemas;
    }

    // Sort tags
    if (document.Tags != null && document.Tags.Count > 0)
    {
        document.Tags = document.Tags.OrderBy(t => t.Name, StringComparer.Ordinal).ToList();
    }

    // Sort security schemes
    if (document.Components?.SecuritySchemes != null && document.Components.SecuritySchemes.Count > 0)
    {
        var sortedSchemes = new Dictionary<string, OpenApiSecurityScheme>();
        foreach (var scheme in document.Components.SecuritySchemes.OrderBy(s => s.Key, StringComparer.Ordinal))
        {
            sortedSchemes[scheme.Key] = scheme.Value;
        }
        document.Components.SecuritySchemes = sortedSchemes;
    }

    // Sort tag groups
    SortTagGroups(document);

    return document;
}
```

## Data Models

No new data models are required. The existing OpenAPI models from Microsoft.OpenApi are used.

## Correctness Properties

*A property is a characteristic or behavior that should hold true across all valid executions of a system-essentially, a formal statement about what the system should do. Properties serve as the bridge between human-readable specifications and machine-verifiable correctness guarantees.*

### Property 1: Path Ordering

*For any* OpenAPI document with multiple paths, after sorting, the paths SHALL appear in alphabetical order by path string using ordinal comparison.

**Validates: Requirements 1.1, 1.2**

### Property 2: Schema Ordering

*For any* OpenAPI document with multiple schemas, after sorting, the schemas SHALL appear in alphabetical order by schema name using ordinal comparison.

**Validates: Requirements 2.1, 2.2**

### Property 3: Property Ordering Within Schemas

*For any* OpenAPI schema with multiple properties, after sorting, the properties SHALL appear in alphabetical order by property name using ordinal comparison.

**Validates: Requirements 3.1, 3.2**

### Property 4: Tag Ordering

*For any* OpenAPI document with multiple tags, after sorting, the tags SHALL appear in alphabetical order by tag name using ordinal comparison.

**Validates: Requirements 4.1, 4.2**

### Property 5: Tag Group Ordering

*For any* OpenAPI document with tag groups, after sorting, the tag groups SHALL appear in alphabetical order by group name, and tags within each group SHALL appear in alphabetical order.

**Validates: Requirements 5.1, 5.2, 5.3**

### Property 6: Security Scheme Ordering

*For any* OpenAPI document with multiple security schemes, after sorting, the security schemes SHALL appear in alphabetical order by scheme name using ordinal comparison.

**Validates: Requirements 7.1, 7.2**

### Property 7: Operation Ordering

*For any* path with multiple operations, after sorting, the operations SHALL appear in HTTP method order: GET, PUT, POST, DELETE, OPTIONS, HEAD, PATCH, TRACE.

**Validates: Requirements 8.1, 8.2**

### Property 8: Response Ordering

*For any* operation with multiple responses, after sorting, the responses SHALL appear in ascending order by status code (treating status codes as integers, with "default" sorted last).

**Validates: Requirements 9.1, 9.2**

### Property 9: Example Ordering

*For any* media type with multiple examples, after sorting, the examples SHALL appear in alphabetical order by example name using ordinal comparison.

**Validates: Requirements 11.1, 11.2**

### Property 10: Output Idempotence (Round-Trip)

*For any* valid OpenAPI document, serializing the document, then deserializing and re-serializing SHALL produce identical JSON output.

**Validates: Requirements 1.3, 2.3, 3.3, 4.3**

### Property 11: Server Order Preservation

*For any* OpenAPI document with servers, the servers SHALL appear in the same order as they were declared in source code or configuration.

**Validates: Requirements 6.1, 6.2**

## Error Handling

| Scenario | Handling |
|----------|----------|
| Null document passed to sorter | Return null or throw ArgumentNullException |
| Empty collections | Return empty sorted collection (no-op) |
| File read error during skip-unchanged check | Log warning and proceed with write |
| File write permission denied | Propagate exception with clear message |

## Testing Strategy

### Property-Based Testing

We will use FsCheck for property-based testing, consistent with the existing test suite. Each correctness property will be implemented as a property-based test with minimum 100 iterations.

**Test Configuration:**
- Framework: xUnit with FsCheck.Xunit
- Minimum iterations: 100 per property
- Generators: Custom generators for OpenAPI documents with various collection sizes

### Unit Tests

Unit tests will cover:
- Edge cases (empty collections, single items, null values)
- Skip-unchanged file comparison logic
- Force flag behavior
- Specific ordering edge cases (e.g., "default" response sorting)

### Test File Organization

```
Oproto.Lambda.OpenApi.Merge.Tests/
  DeterministicOutputPropertyTests.cs    # Property tests for sorter
  OpenApiDocumentSorterTests.cs          # Unit tests for sorter

Oproto.Lambda.OpenApi.Tests/
  DeterministicGeneratorPropertyTests.cs # Property tests for source generator ordering
```
