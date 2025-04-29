using Microsoft.CodeAnalysis;
using Microsoft.OpenApi.Models;

namespace Oproto.OpenApiGenerator;

public partial class OpenApiSpecGenerator
{
    /// <summary>
    ///     Contains all schema definitions that can be referenced by $ref.
    ///     Schemas for complex types are stored here and referenced from other schemas.
    /// </summary>
    private readonly OpenApiComponents _components = new() { Schemas = new Dictionary<string, OpenApiSchema>() };

    /// <summary>
    ///     Tracks types that are currently being processed to prevent infinite recursion
    ///     when handling circular references in type definitions.
    /// </summary>
    private readonly Dictionary<ITypeSymbol, int> _processedTypes = new(SymbolEqualityComparer.Default);

    /// <summary>
    ///     Creates an OpenAPI schema for the given type symbol.
    /// </summary>
    /// <param name="typeSymbol">The type symbol to create a schema for</param>
    /// <param name="memberSymbol">Optional member symbol for additional metadata (like attributes)</param>
    /// <returns>An OpenAPI schema representing the type</returns>
    /// <remarks>
    ///     This method handles recursive type definitions by tracking processed types
    ///     to prevent infinite recursion. Complex types are added to the components
    ///     section and referenced using $ref.
    /// </remarks>
    public OpenApiSchema CreateSchema(ITypeSymbol typeSymbol, ISymbol memberSymbol = null)
    {
        if (typeSymbol == null)
            return null;

        // Check for nullable types first, before processing type references
        if (TryCreateNullableSchema(typeSymbol, memberSymbol, out var nullableSchema)) return nullableSchema;

        // Then check for special types like Ulid
        if (TryCreateSpecialTypeSchema(typeSymbol, out var specialSchema)) return specialSchema;

        // Check for collection types
        if (TryCreateCollectionSchema(typeSymbol, memberSymbol, out var collectionSchema)) return collectionSchema;

        // If we've already processed this type and it's a complex type, return a reference
        if (!_processedTypes.TryGetValue(typeSymbol, out var count))
        {
            _processedTypes[typeSymbol] = 1;
            return CreateSchemaInternal(typeSymbol, memberSymbol);
        }

        _processedTypes[typeSymbol] = count + 1;
        // Only create reference if count > 1 and it's a complex type
        return count > 1 ? CreateReferenceIfComplex(typeSymbol) : CreateSchemaInternal(typeSymbol, memberSymbol);
    }

    /// <summary>
    ///     Creates a reference schema for complex types that have already been processed.
    /// </summary>
    /// <param name="typeSymbol">The type symbol to create a reference for</param>
    /// <returns>A schema with a $ref to the type in components, or null for simple types</returns>
    /// <remarks>
    ///     Only creates references for complex types (classes, structs) that should be
    ///     in the components/schemas section. Simple types are handled inline.
    /// </remarks>
    private OpenApiSchema CreateReferenceIfComplex(ITypeSymbol typeSymbol)
    {
        if (typeSymbol is INamedTypeSymbol namedType && !IsSimpleType(typeSymbol))
            return new OpenApiSchema
            {
                Type = "object",
                Reference = new OpenApiReference { Type = ReferenceType.Schema, Id = namedType.Name }
            };

        return CreateSchemaInternal(typeSymbol, null);
    }

    /// <summary>
    ///     Internal implementation of schema creation with type-specific handling.
    /// </summary>
    /// <param name="typeSymbol">The type symbol to create a schema for</param>
    /// <param name="memberSymbol">Optional member symbol for additional metadata</param>
    /// <returns>An OpenAPI schema for the type</returns>
    /// <remarks>
    ///     Handles schema creation in the following order:
    ///     1. Nullable types (Nullable{T} and nullable reference types)
    ///     2. Collection types (arrays, lists, etc.)
    ///     3. Simple types (primitives, enums)
    ///     4. Complex types (classes, structs)
    ///     Complex types are added to components/schemas and referenced using $ref.
    /// </remarks>
    private OpenApiSchema CreateSchemaInternal(ITypeSymbol typeSymbol, ISymbol memberSymbol)
    {
        /*
        // Handle nullable types first
        if (TryCreateNullableSchema(typeSymbol, memberSymbol, out var nullableSchema))
        {
            return nullableSchema;
        }

        // Check for collection types
        if (TryCreateCollectionSchema(typeSymbol, memberSymbol, out var collectionSchema))
        {
            return collectionSchema;
        }*/

        // Handle simple types (including enums)
        if (IsSimpleType(typeSymbol)) return CreateSimpleTypeSchema(typeSymbol, memberSymbol);

        // Handle complex types

        if (typeSymbol is INamedTypeSymbol complexType)
        {
            var schema = CreateComplexTypeSchema(typeSymbol);

            // Add to components if it's a named type
            if (!string.IsNullOrEmpty(complexType.Name))
            {
                _components.Schemas[complexType.Name] = schema;

                // Only return reference if we've seen this type more than once
                if (_processedTypes[typeSymbol] > 1)
                    return new OpenApiSchema
                    {
                        AllOf = new List<OpenApiSchema>
                        {
                            new()
                            {
                                Reference = new OpenApiReference
                                {
                                    Type = ReferenceType.Schema, Id = complexType.Name
                                }
                            }
                        },
                        Nullable = true
                    };
            }

            return schema;
        }

        // Fallback to simple type handling
        return CreateSimpleTypeSchema(typeSymbol, memberSymbol);
    }

    /// <summary>
    ///     Creates an OpenAPI schema for a nullable reference type.
    /// </summary>
    /// <param name="typeSymbol">The type symbol representing the reference type</param>
    /// <param name="isNullable">Whether the type should be treated as nullable</param>
    /// <returns>An OpenAPI schema representing a nullable reference</returns>
    /// <remarks>
    ///     Nullable references are represented using allOf combined with nullable:true.
    ///     This pattern was chosen for OpenAPI 3.0 compatibility and tooling support.
    ///     Example generated schema:
    ///     {
    ///     "allOf": [
    ///     {
    ///     "$ref": "#/components/schemas/ReferencedType"
    ///     }
    ///     ],
    ///     "nullable": true
    ///     }
    ///     Important implementation notes:
    ///     - Always use allOf to maintain the reference structure
    ///     - Set nullable at the root level, not inside the reference
    ///     - This approach works better with code generators than oneOf/null
    /// </remarks>
    private bool TryCreateNullableSchema(ITypeSymbol typeSymbol, ISymbol memberSymbol, out OpenApiSchema schema)
    {
        schema = null;

        // Handle Nullable<T>
        if (typeSymbol is INamedTypeSymbol namedType &&
            namedType.IsGenericType &&
            namedType.ConstructedFrom.SpecialType == SpecialType.System_Nullable_T)
        {
            schema = CreateSchema(namedType.TypeArguments[0], memberSymbol);
            schema.Nullable = true;
            return true;
        }

        // Handle reference types with nullable annotation
        if (memberSymbol is IPropertySymbol propertySymbol &&
            propertySymbol.NullableAnnotation == NullableAnnotation.Annotated)
        {
            schema = CreateSchema(typeSymbol); // Pass null to avoid infinite recursion
            schema.Nullable = true;
            return true;
        }

        return false;
    }

    /// <summary>
    ///     Attempts to create a schema for collection types.
    /// </summary>
    /// <param name="typeSymbol">The type symbol to check for collection</param>
    /// <param name="memberSymbol">The member symbol for additional metadata</param>
    /// <param name="schema">The output schema if the type is a collection</param>
    /// <returns>True if a collection schema was created, false otherwise</returns>
    /// <remarks>
    ///     Creates an array schema with the appropriate item type.
    ///     Handles arrays, lists, and other enumerable types.
    ///     Example schema:
    ///     {
    ///     "type": "array",
    ///     "items": { ... item schema ... }
    ///     }
    ///     Any attributes on the collection property are applied to the array schema.
    /// </remarks>
    private bool TryCreateCollectionSchema(ITypeSymbol typeSymbol, ISymbol memberSymbol, out OpenApiSchema schema)
    {
        schema = null;
        if (IsCollectionType(typeSymbol, out var elementType))
        {
            var itemSchema = CreateSchema(elementType, memberSymbol);
            schema = new OpenApiSchema { Type = "array", Items = itemSchema };

            if (memberSymbol != null) ApplySchemaAttributes(schema, memberSymbol);

            return true;
        }

        return false;
    }
}
