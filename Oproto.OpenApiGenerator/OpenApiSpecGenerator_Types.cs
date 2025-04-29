using System.Diagnostics;
using System.Text.Json;
using Microsoft.CodeAnalysis;
using Microsoft.OpenApi.Any;
using Microsoft.OpenApi.Models;

namespace Oproto.OpenApiGenerator;

public partial class OpenApiSpecGenerator
{
    /// <summary>
    ///     Maps basic .NET type names to their OpenAPI type equivalents.
    /// </summary>
    /// <param name="dotNetType">The .NET type name in lowercase</param>
    /// <returns>The corresponding OpenAPI type string</returns>
    /// <remarks>
    ///     Common mappings include:
    ///     - string -> string
    ///     - int/long -> integer
    ///     - double/float/decimal -> number
    ///     - bool -> boolean
    ///     - datetime -> string (with format: date-time)
    ///     - guid -> string (with format: uuid)
    /// </remarks>
    private string MapDotNetTypeToOpenApiType(string dotNetType)
    {
        return dotNetType.ToLower() switch
        {
            "string" => "string",
            "int" or "int32" or "int64" or "long" => "integer",
            "double" or "float" or "decimal" => "number",
            "bool" or "boolean" => "boolean",
            "datetime" => "string", // with format: date-time
            "guid" => "string", // with format: uuid
            _ => "object"
        };
    }

    /// <summary>
    ///     Gets the OpenAPI type for a given .NET Type instance.
    /// </summary>
    /// <param name="type">The .NET Type to map</param>
    /// <returns>The corresponding OpenAPI type string</returns>
    /// <remarks>
    ///     Handles void by mapping it to "object".
    ///     For all other types, delegates to MapDotNetTypeToOpenApiType.
    /// </remarks>
    private string GetOpenApiType(Type type)
    {
        if (type == typeof(void))
            return "object";

        return MapDotNetTypeToOpenApiType(type.Name);
    }

    /// <summary>
    ///     Determines if a type implements IEnumerable{T}, excluding string.
    /// </summary>
    /// <param name="type">The type to check</param>
    /// <returns>True if the type is an enumerable collection</returns>
    /// <remarks>
    ///     Strings are explicitly excluded despite implementing IEnumerable{char}
    ///     as they should be treated as simple types in OpenAPI schemas.
    /// </remarks>
    private bool IsEnumerable(Type type)
    {
        return type != typeof(string) &&
               type.GetInterfaces()
                   .Any(i => i.IsGenericType && i.GetGenericTypeDefinition() == typeof(IEnumerable<>));
    }

    /// <summary>
    ///     Gets the element type from an IEnumerable{T} type.
    /// </summary>
    /// <param name="type">The enumerable type</param>
    /// <returns>The type of elements in the enumerable</returns>
    /// <remarks>
    ///     Handles both direct IEnumerable{T} implementations and
    ///     types that implement IEnumerable{T} through interfaces.
    /// </remarks>
    private Type GetEnumerableType(Type type)
    {
        if (type.IsGenericType && type.GetGenericTypeDefinition() == typeof(IEnumerable<>))
            return type.GetGenericArguments()[0];

        return type.GetInterfaces()
            .First(i => i.IsGenericType && i.GetGenericTypeDefinition() == typeof(IEnumerable<>))
            .GetGenericArguments()[0];
    }

    /// <summary>
    ///     Gets the default value for a given type symbol.
    /// </summary>
    /// <param name="type">The type symbol to get a default value for</param>
    /// <returns>The default value for the type, or null if not a basic value type</returns>
    /// <remarks>
    ///     Provides appropriate defaults for all basic value types in .NET.
    ///     Used for generating example values in OpenAPI schemas.
    /// </remarks>
    private object GetDefaultValueForType(ITypeSymbol type)
    {
        // Handle basic value types
        return type.SpecialType switch
        {
            SpecialType.System_Boolean => false,
            SpecialType.System_Char => '\0',
            SpecialType.System_SByte => (sbyte)0,
            SpecialType.System_Byte => (byte)0,
            SpecialType.System_Int16 => (short)0,
            SpecialType.System_UInt16 => (ushort)0,
            SpecialType.System_Int32 => 0,
            SpecialType.System_UInt32 => 0U,
            SpecialType.System_Int64 => 0L,
            SpecialType.System_UInt64 => 0UL,
            SpecialType.System_Decimal => 0M,
            SpecialType.System_Single => 0F,
            SpecialType.System_Double => 0D,
            _ => null
        };
    }

    /// <summary>
    ///     Identifies simple/primitive types including string, numeric types, and DateTime.
    /// </summary>
    /// <param name="typeSymbol">The type symbol to check</param>
    /// <returns>True if the type is a simple type</returns>
    /// <remarks>
    ///     Simple types include:
    ///     - Enums (treated as strings with enum values in OpenAPI)
    ///     - Primitive types (bool, string, int, etc.)
    ///     - DateTime
    ///     This method must be called before IsCollectionType to ensure proper type handling.
    /// </remarks>
    private bool IsSimpleType(ITypeSymbol typeSymbol)
    {
        if (typeSymbol == null)
            return false;

        if (typeSymbol.TypeKind == TypeKind.Enum)
            return true;

        return typeSymbol.SpecialType switch
        {
            SpecialType.System_Boolean or
                SpecialType.System_String or
                SpecialType.System_Int32 or
                SpecialType.System_Int64 or
                SpecialType.System_Single or
                SpecialType.System_Double or
                SpecialType.System_Decimal => true,
            _ => typeSymbol.Name == "DateTime" || typeSymbol.ToString() == "System.DateTime"
        };
    }

    /// <summary>
    ///     Creates an OpenAPI schema for simple types including enums, DateTime, and primitive types.
    ///     For complex/reference types, see CreateComplexTypeSchema.
    /// </summary>
    /// <param name="typeSymbol">The type symbol to create a schema for</param>
    /// <param name="memberSymbol">Optional member symbol for additional attributes</param>
    /// <returns>An OpenAPI schema representing the type</returns>
    /// <remarks>
    ///     For reference types that can be null, the schema uses this structure:
    ///     {
    ///     "allOf": [
    ///     {
    ///     "$ref": "#/components/schemas/ReferencedType"
    ///     }
    ///     ],
    ///     "nullable": true
    ///     }
    ///     For simple types, the schema will be:
    ///     - Enums: string type with enum values
    ///     - DateTime: string type with format: date-time
    ///     - Primitives: mapped to appropriate OpenAPI types (integer, number, string, boolean)
    /// </remarks>
    private OpenApiSchema CreateSimpleTypeSchema(ITypeSymbol typeSymbol, ISymbol memberSymbol = null)
    {
        var schema = new OpenApiSchema();

        // Handle enums
        if (typeSymbol.TypeKind == TypeKind.Enum)
        {
            schema.Type = "string";
            schema.Enum = typeSymbol.GetMembers()
                .Where(m => m.Kind == SymbolKind.Field)
                .Select(m => new OpenApiString(m.Name))
                .Cast<IOpenApiAny>()
                .ToList();

            if (memberSymbol != null) ApplySchemaAttributes(schema, memberSymbol);

            return schema;
        }

        // Check for DateTime
        if (typeSymbol.Name == "DateTime" || typeSymbol.ToString() == "System.DateTime")
        {
            schema.Type = "string";
            schema.Format = "date-time";
            return schema;
        }

        // Handle other types
        schema.Type = typeSymbol.SpecialType switch
        {
            SpecialType.System_Int32 or SpecialType.System_Int64 => "integer",
            SpecialType.System_Single or SpecialType.System_Double or SpecialType.System_Decimal => "number",
            SpecialType.System_String => "string",
            SpecialType.System_Boolean => "boolean",
            _ => "string"
        };

        schema.Format = typeSymbol.SpecialType switch
        {
            SpecialType.System_Int64 => "int64",
            SpecialType.System_Double => "double",
            SpecialType.System_Decimal => "decimal",
            _ => null
        };

        // Only set default example if no member symbol (meaning no attributes to process)
        schema.Example = typeSymbol switch
        {
            { Name: "Int32" } or { Name: "System.Int32" } => new OpenApiInteger(42),
            { Name: "Int64" } or { Name: "System.Int64" } => new OpenApiLong(9999999999),
            { Name: "Double" } or { Name: "System.Double" } => new OpenApiDouble(3.14159),
            { Name: "Decimal" } or { Name: "System.Decimal" } => new OpenApiDouble(123.45),
            { Name: "String" } or { Name: "System.String" } => new OpenApiString("sample string"),
            { Name: "Boolean" } or { Name: "System.Boolean" } => new OpenApiBoolean(true),
            { Name: "DateTime" } or { Name: "System.DateTime" } => new OpenApiString(DateTime.UtcNow.ToString("O")),
            _ => null
        };

        // Apply any attributes after setting default values
        if (memberSymbol != null) ApplySchemaAttributes(schema, memberSymbol);

        return schema;
    }

    /// <summary>
    ///     Determines if a type is a collection (IEnumerable{T} or List{T}).
    /// </summary>
    /// <param name="typeSymbol">The type to check</param>
    /// <param name="elementType">Output parameter for the collection's element type</param>
    /// <returns>True if the type is a collection</returns>
    /// <remarks>
    ///     Handles both arrays and generic collections:
    ///     - Direct array types (T[])
    ///     - IEnumerable{T} implementations
    ///     - Direct generic collection types
    ///     Uses MetadataName for reliable type detection in the compilation context.
    /// </remarks>
    private bool IsCollectionType(ITypeSymbol typeSymbol, out ITypeSymbol elementType)
    {
        elementType = null;
        Debug.WriteLine($"Checking type: {typeSymbol.ToDisplayString()}");
        Debug.WriteLine($"Kind: {typeSymbol.TypeKind}");

        // Handle array types
        if (typeSymbol is IArrayTypeSymbol arrayType)
        {
            elementType = arrayType.ElementType;
            return true;
        }

        // Handle generic collections
        if (typeSymbol is INamedTypeSymbol namedType && namedType.IsGenericType)
        {
            Debug.WriteLine($"MetadataName: {namedType.MetadataName}");
            Debug.WriteLine($"Is Generic: {namedType.IsGenericType}");
            Debug.WriteLine("Interfaces:");

            // Direct type checks first
            // Todo: is there a better way to do this?
            var isCollection = namedType.MetadataName == "List`1" || // Catches List<T>
                               namedType.MetadataName == "IList`1" || // Catches IList<T>
                               namedType.MetadataName == "IReadOnlyList`1" || // Catches IReadOnlyList<T>
                               namedType.MetadataName == "IEnumerable`1"; // Catches IEnumerable<T>

            if (isCollection)
            {
                elementType = namedType.TypeArguments[0];
                return true;
            }

            // Interface checks as fallback
            if (namedType.AllInterfaces.Any(i => i.MetadataName == "IEnumerable`1"))
            {
                elementType = namedType.TypeArguments[0];
                return true;
            }
        }

        return false;
    }

    private bool IsCollectionInterface(INamedTypeSymbol type)
    {
        if (!type.IsGenericType)
            return false;

        var originalDefinition = type.OriginalDefinition;

        // Check if the type implements any of the common collection interfaces
        return originalDefinition.AllInterfaces.Any(i =>
            i.ToDisplayString() == "System.Collections.Generic.IEnumerable<T>" ||
            i.ToDisplayString() == "System.Collections.Generic.ICollection<T>" ||
            i.ToDisplayString() == "System.Collections.Generic.IList<T>" ||
            i.ToDisplayString() == "System.Collections.Generic.IReadOnlyList<T>" ||
            i.ToDisplayString() == "System.Collections.Generic.IReadOnlyCollection<T>");
    }

    /// <summary>
    ///     Gets the element type from a collection type symbol.
    /// </summary>
    /// <param name="typeSymbol">The collection type symbol</param>
    /// <returns>The element type symbol, or null if not a collection</returns>
    /// <remarks>
    ///     Supports:
    ///     - Array types (returns element type)
    ///     - Generic collections (returns first type argument)
    ///     Used in conjunction with IsCollectionType for complete collection handling.
    /// </remarks>
    private ITypeSymbol GetCollectionElementType(ITypeSymbol typeSymbol)
    {
        if (typeSymbol is IArrayTypeSymbol arrayType)
            return arrayType.ElementType;

        if (typeSymbol is INamedTypeSymbol { IsGenericType: true } namedType)
            return namedType.TypeArguments[0];

        return null;
    }

    private IOpenApiAny JsonElementToOpenApiAny(JsonElement element)
    {
        switch (element.ValueKind)
        {
            case JsonValueKind.Array:
                var array = new OpenApiArray();
                foreach (var item in element.EnumerateArray()) array.Add(JsonElementToOpenApiAny(item));

                return array;

            case JsonValueKind.Object:
                var obj = new OpenApiObject();
                foreach (var property in element.EnumerateObject())
                    obj.Add(property.Name, JsonElementToOpenApiAny(property.Value));

                return obj;

            case JsonValueKind.String:
                return new OpenApiString(element.GetString());

            case JsonValueKind.Number:
                if (element.TryGetInt32(out var intValue))
                    return new OpenApiInteger(intValue);
                if (element.TryGetInt64(out var longValue))
                    return new OpenApiLong(longValue);
                return new OpenApiFloat((float)element.GetDouble());

            case JsonValueKind.True:
            case JsonValueKind.False:
                return new OpenApiBoolean(element.GetBoolean());

            case JsonValueKind.Null:
                return new OpenApiNull();

            default:
                return new OpenApiString(element.ToString());
        }
    }

    private bool TryCreateSpecialTypeSchema(ITypeSymbol typeSymbol, out OpenApiSchema schema)
    {
        schema = null;

        // Check if it's a Ulid type
        if (typeSymbol.Name == "Ulid" && typeSymbol.ContainingNamespace?.ToString() == "System")
        {
            schema = new OpenApiSchema
            {
                Type = "string",
                Format = "ulid", // Custom format to indicate it's a ULID
                Pattern = "^[0-9A-HJKMNP-TV-Z]{26}$" // ULID pattern (base32)
            };
            return true;
        }

        return false;
    }
}
