# Design Document: Dictionary Schema Support

## Overview

This design document describes the implementation of proper dictionary type handling in the Oproto.Lambda.OpenApi source generator. The solution adds dictionary detection logic to the type analysis pipeline and generates correct OpenAPI `additionalProperties` schemas for dictionary types.

### OpenAPI Dictionary Representation

Per the [OpenAPI Specification](https://swagger.io/docs/specification/data-models/dictionaries/), dictionaries (maps, hashmaps, associative arrays) are represented using `type: object` with `additionalProperties` defining the value type. This is the standard pattern recognized by all major code generators including:

- **Kiota** (Microsoft) - generates `IDictionary<string, T>` in C#
- **OpenAPI Generator** - generates `Dictionary<string, T>` in C#
- **NSwag** - generates `IDictionary<string, T>` in C#

OpenAPI only supports string keys for dictionaries, which aligns with JSON's object key constraints.

## Architecture

The implementation follows the existing partial class pattern used by the source generator, adding dictionary-specific logic to the type detection and schema creation pipeline.

```
┌─────────────────────────────────────────────────────────────────┐
│                    CreateSchema (Entry Point)                    │
└─────────────────────────────────────────────────────────────────┘
                              │
                              ▼
┌─────────────────────────────────────────────────────────────────┐
│  1. TryCreateNullableSchema (existing)                          │
│  2. TryCreateSpecialTypeSchema (existing - Ulid)                │
│  3. TryCreateCollectionSchema (existing - arrays/lists)         │
│  4. TryCreateDictionarySchema (NEW)  ◄─────────────────────────│
│  5. IsSimpleType → CreateSimpleTypeSchema (existing)            │
│  6. CreateComplexTypeSchema (existing - fallback)               │
└─────────────────────────────────────────────────────────────────┘
```

The key insight is that dictionary detection must occur:
- After nullable handling (to unwrap `Nullable<T>`)
- After collection handling (dictionaries are not arrays)
- Before complex type handling (to prevent incorrect object schema generation)

## Components and Interfaces

### New Methods in OpenApiSpecGenerator_Types.cs

```csharp
/// <summary>
/// Determines if a type is a dictionary type (Dictionary, IDictionary, IReadOnlyDictionary).
/// </summary>
/// <param name="typeSymbol">The type to check</param>
/// <param name="keyType">Output parameter for the dictionary's key type</param>
/// <param name="valueType">Output parameter for the dictionary's value type</param>
/// <returns>True if the type is a dictionary type</returns>
private bool IsDictionaryType(ITypeSymbol typeSymbol, out ITypeSymbol keyType, out ITypeSymbol valueType)
```

### New Methods in OpenApiSpecGenerator_Schema.cs

```csharp
/// <summary>
/// Attempts to create a schema for dictionary types.
/// </summary>
/// <param name="typeSymbol">The type symbol to check for dictionary</param>
/// <param name="memberSymbol">The member symbol for additional metadata</param>
/// <param name="schema">The output schema if the type is a dictionary</param>
/// <returns>True if a dictionary schema was created, false otherwise</returns>
private bool TryCreateDictionarySchema(ITypeSymbol typeSymbol, ISymbol memberSymbol, out OpenApiSchema schema)
```

### Integration Point in CreateSchema

The `CreateSchema` method in `OpenApiSpecGenerator_Schema.cs` will be modified to call `TryCreateDictionarySchema` after collection handling but before complex type handling.

## Data Models

### Dictionary Type Detection Patterns

The following type patterns will be recognized as dictionaries:

| Type Pattern | MetadataName | Detection Method |
|--------------|--------------|------------------|
| `Dictionary<K,V>` | `Dictionary`2` | Direct type check |
| `IDictionary<K,V>` | `IDictionary`2` | Direct type check |
| `IReadOnlyDictionary<K,V>` | `IReadOnlyDictionary`2` | Direct type check |
| Custom types implementing IDictionary | N/A | Interface check |

### Generated Schema Patterns

| Input Type | Generated Schema |
|------------|------------------|
| `Dictionary<string, string>` | `{ "type": "object", "additionalProperties": { "type": "string" } }` |
| `Dictionary<string, int>` | `{ "type": "object", "additionalProperties": { "type": "integer" } }` |
| `Dictionary<string, ComplexType>` | `{ "type": "object", "additionalProperties": { "$ref": "#/components/schemas/ComplexType" } }` |
| `Dictionary<string, List<T>>` | `{ "type": "object", "additionalProperties": { "type": "array", "items": {...} } }` |
| `Dictionary<string, Dictionary<string, T>>` | `{ "type": "object", "additionalProperties": { "type": "object", "additionalProperties": {...} } }` |

## Correctness Properties

*A property is a characteristic or behavior that should hold true across all valid executions of a system—essentially, a formal statement about what the system should do. Properties serve as the bridge between human-readable specifications and machine-verifiable correctness guarantees.*

### Property 1: Dictionary Type Detection

*For any* type symbol that is `Dictionary<K,V>`, `IDictionary<K,V>`, `IReadOnlyDictionary<K,V>`, or implements `IDictionary<K,V>`, the `IsDictionaryType` method SHALL return true and correctly extract the key and value types.

**Validates: Requirements 1.1, 1.2, 1.3, 1.4, 1.5**

### Property 2: Dictionary Schema Structure

*For any* dictionary type, the generated OpenAPI schema SHALL have `type: "object"` and a non-null `additionalProperties` schema (not empty `properties`).

**Validates: Requirements 5.2**

### Property 3: Simple Value Type Schema

*For any* dictionary with a simple value type (string, int, bool, decimal, DateTime, etc.), the `additionalProperties` schema SHALL have the correct OpenAPI type and format matching the value type.

**Validates: Requirements 2.1, 2.2, 2.3, 2.4, 2.5**

### Property 4: Complex Value Type Reference

*For any* dictionary with a complex (non-simple) value type, the `additionalProperties` schema SHALL contain a reference (`$ref`) to the value type's schema in components.

**Validates: Requirements 3.1**

### Property 5: Nullable Dictionary Handling

*For any* nullable dictionary type (either `Nullable<Dictionary<K,V>>` or dictionary property with nullable annotation), the generated schema SHALL have `nullable: true`.

**Validates: Requirements 4.1, 4.2**

## Error Handling

### Invalid Dictionary Types

- If a dictionary type has non-string keys, the generator will still produce an `additionalProperties` schema (OpenAPI only supports string keys for objects)
- If the value type cannot be resolved, the generator falls back to `additionalProperties: { type: "object" }`

### Circular References

- Dictionary value types that reference the containing type are handled by the existing circular reference detection in `_processedTypes`
- Self-referential dictionaries produce schemas with `$ref` to prevent infinite recursion

### Attribute Processing Errors

- Invalid JSON in `[OpenApiSchema(Example = "...")]` is handled gracefully with fallback to string representation
- Missing or malformed attributes are ignored, using default schema generation

## Testing Strategy

### Unit Tests

Unit tests will verify specific examples and edge cases:

1. `Dictionary<string, string>` produces correct schema
2. `Dictionary<string, int>` produces correct schema with integer type
3. `Dictionary<string, ComplexType>` produces schema with $ref
4. `Dictionary<string, List<string>>` produces nested array schema
5. `Dictionary<string, Dictionary<string, int>>` produces nested dictionary schema
6. Nullable dictionary produces schema with `nullable: true`
7. Dictionary with `[OpenApiSchema]` attributes applies description and example
8. Custom type implementing `IDictionary<K,V>` is detected as dictionary

### Property-Based Tests

Property-based tests will verify universal properties using the fast-check library pattern:

1. **Dictionary Detection Property**: For all generated dictionary type symbols, `IsDictionaryType` returns true
2. **Schema Structure Property**: For all dictionary types, generated schema has `additionalProperties` (not empty `properties`)
3. **Value Type Mapping Property**: For all dictionaries with simple value types, `additionalProperties.Type` matches the expected OpenAPI type
4. **Complex Type Reference Property**: For all dictionaries with complex value types, `additionalProperties` contains a `$ref`
5. **Nullable Property**: For all nullable dictionaries, schema has `nullable: true`

### Test Configuration

- Property tests will run minimum 100 iterations
- Tests will use the existing `GenerateSchemaFromSource` helper pattern from `OpenApiGeneratorTests.cs`
- Each property test will be tagged with: **Feature: dictionary-schema-support, Property N: {property_text}**

