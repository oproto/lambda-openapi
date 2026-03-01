# Requirements Document

## Introduction

This document specifies the requirements for adding proper dictionary type handling to the Oproto.Lambda.OpenApi source generator. Currently, dictionary types (`Dictionary<K,V>`, `IDictionary<K,V>`, etc.) are incorrectly treated as complex objects with empty properties, producing invalid OpenAPI schemas. This feature will enable the generator to produce correct `additionalProperties` schemas for dictionary types.

## Glossary

- **Source_Generator**: The Oproto.Lambda.OpenApi.SourceGenerator that analyzes C# code and produces OpenAPI specification files
- **Dictionary_Type**: Any .NET type that implements `IDictionary<TKey, TValue>`, including `Dictionary<K,V>`, `IDictionary<K,V>`, and `IReadOnlyDictionary<K,V>`
- **OpenAPI_Schema**: A JSON Schema-based structure that describes the shape of data in an OpenAPI specification
- **Additional_Properties_Schema**: An OpenAPI schema pattern using `additionalProperties` to describe objects with dynamic string keys and typed values
- **Value_Type**: The type of values stored in a dictionary (the `TValue` in `Dictionary<TKey, TValue>`)
- **Simple_Type**: Primitive types (string, int, bool, etc.), enums, DateTime, DateOnly, TimeOnly, and Guid
- **Complex_Type**: Non-primitive types that require a `$ref` reference in OpenAPI schemas

## Requirements

### Requirement 1: Dictionary Type Detection

**User Story:** As a developer using the source generator, I want dictionary types to be correctly identified, so that they are not incorrectly treated as complex objects.

#### Acceptance Criteria

1. WHEN the Source_Generator encounters a `Dictionary<TKey, TValue>` type, THE Source_Generator SHALL identify it as a Dictionary_Type
2. WHEN the Source_Generator encounters an `IDictionary<TKey, TValue>` type, THE Source_Generator SHALL identify it as a Dictionary_Type
3. WHEN the Source_Generator encounters an `IReadOnlyDictionary<TKey, TValue>` type, THE Source_Generator SHALL identify it as a Dictionary_Type
4. WHEN the Source_Generator encounters a type implementing `IDictionary<TKey, TValue>`, THE Source_Generator SHALL identify it as a Dictionary_Type
5. WHEN the Source_Generator identifies a Dictionary_Type, THE Source_Generator SHALL extract the Value_Type from the type arguments

### Requirement 2: Dictionary Schema Generation with Simple Value Types

**User Story:** As a developer, I want dictionaries with simple value types to generate correct OpenAPI schemas, so that API consumers understand the data structure.

#### Acceptance Criteria

1. WHEN the Source_Generator creates a schema for `Dictionary<string, string>`, THE Source_Generator SHALL produce a schema with `type: "object"` and `additionalProperties: { type: "string" }`
2. WHEN the Source_Generator creates a schema for `Dictionary<string, int>`, THE Source_Generator SHALL produce a schema with `type: "object"` and `additionalProperties: { type: "integer" }`
3. WHEN the Source_Generator creates a schema for `Dictionary<string, bool>`, THE Source_Generator SHALL produce a schema with `type: "object"` and `additionalProperties: { type: "boolean" }`
4. WHEN the Source_Generator creates a schema for `Dictionary<string, decimal>`, THE Source_Generator SHALL produce a schema with `type: "object"` and `additionalProperties: { type: "number" }`
5. WHEN the Source_Generator creates a schema for `Dictionary<string, DateTime>`, THE Source_Generator SHALL produce a schema with `type: "object"` and `additionalProperties: { type: "string", format: "date-time" }`

### Requirement 3: Dictionary Schema Generation with Complex Value Types

**User Story:** As a developer, I want dictionaries with complex value types to generate schemas with proper references, so that nested types are correctly documented.

#### Acceptance Criteria

1. WHEN the Source_Generator creates a schema for a dictionary with a Complex_Type value, THE Source_Generator SHALL produce a schema with `additionalProperties` containing a `$ref` to the value type
2. WHEN the Source_Generator creates a schema for `Dictionary<string, List<T>>`, THE Source_Generator SHALL produce a schema with `additionalProperties` containing an array schema
3. WHEN the Source_Generator creates a schema for `Dictionary<string, Dictionary<string, T>>`, THE Source_Generator SHALL produce a schema with nested `additionalProperties` schemas

### Requirement 4: Nullable Dictionary Handling

**User Story:** As a developer, I want nullable dictionaries to be correctly represented in the schema, so that optional dictionary properties are properly documented.

#### Acceptance Criteria

1. WHEN the Source_Generator creates a schema for `Dictionary<string, T>?` (nullable dictionary), THE Source_Generator SHALL produce a schema with `nullable: true`
2. WHEN the Source_Generator creates a schema for a dictionary property with nullable annotation, THE Source_Generator SHALL set `nullable: true` on the schema

### Requirement 5: Dictionary Type Priority in Schema Creation

**User Story:** As a developer, I want dictionary detection to occur before complex type handling, so that dictionaries are not incorrectly processed as regular objects.

#### Acceptance Criteria

1. WHEN the Source_Generator processes a type, THE Source_Generator SHALL check for Dictionary_Type before checking for Complex_Type
2. WHEN a Dictionary_Type is detected, THE Source_Generator SHALL NOT fall through to CreateComplexTypeSchema
3. WHEN a type is both a Dictionary_Type and has other properties, THE Source_Generator SHALL treat it as a Dictionary_Type (additionalProperties takes precedence)

### Requirement 6: Schema Attribute Support for Dictionaries

**User Story:** As a developer, I want to apply OpenApiSchema attributes to dictionary properties, so that I can customize the generated schema.

#### Acceptance Criteria

1. WHEN a dictionary property has an `[OpenApiSchema]` attribute with Description, THE Source_Generator SHALL apply the description to the dictionary schema
2. WHEN a dictionary property has an `[OpenApiSchema]` attribute with Example, THE Source_Generator SHALL apply the example to the dictionary schema
