using Microsoft.CodeAnalysis;
using Microsoft.OpenApi.Any;
using Microsoft.OpenApi.Models;

namespace Oproto.Lambda.OpenApi.SourceGenerator;

public partial class OpenApiSpecGenerator
{
    /// <summary>
    /// Reads [OpenApiExampleConfig] attribute from the assembly to get example generation configuration.
    /// </summary>
    /// <param name="compilation">The compilation to read attributes from.</param>
    /// <returns>An ExampleConfig with values from the attribute, or defaults if not present.</returns>
    private ExampleConfig GetExampleConfigFromAssembly(Compilation? compilation)
    {
        var config = new ExampleConfig();

        if (compilation == null)
            return config;

        foreach (var attr in compilation.Assembly.GetAttributes())
        {
            if (attr.AttributeClass?.Name != "OpenApiExampleConfigAttribute")
                continue;

            // Named arguments
            foreach (var namedArg in attr.NamedArguments)
            {
                switch (namedArg.Key)
                {
                    case "ComposeFromProperties":
                        if (namedArg.Value.Value is bool composeFromProperties)
                            config.ComposeFromProperties = composeFromProperties;
                        break;
                    case "GenerateDefaults":
                        if (namedArg.Value.Value is bool generateDefaults)
                            config.GenerateDefaults = generateDefaults;
                        break;
                }
            }

            break; // Only process first attribute
        }

        return config;
    }

    /// <summary>
    /// Gets the JSON property name for a property symbol.
    /// Checks for JsonPropertyName attribute first, then falls back to camelCase conversion.
    /// </summary>
    /// <param name="property">The property symbol to get the name for.</param>
    /// <returns>The JSON property name.</returns>
    private string GetJsonPropertyName(IPropertySymbol property)
    {
        // Check for JsonPropertyName attribute
        var jsonPropertyNameAttr = property.GetAttributes()
            .FirstOrDefault(a => a.AttributeClass?.Name == "JsonPropertyNameAttribute");

        if (jsonPropertyNameAttr != null &&
            jsonPropertyNameAttr.ConstructorArguments.Length > 0 &&
            jsonPropertyNameAttr.ConstructorArguments[0].Value is string jsonName &&
            !string.IsNullOrEmpty(jsonName))
        {
            return jsonName;
        }

        // Fall back to camelCase conversion
        return ToCamelCase(property.Name);
    }

    /// <summary>
    /// Converts a string to camelCase.
    /// </summary>
    /// <param name="value">The string to convert.</param>
    /// <returns>The camelCase version of the string.</returns>
    private static string ToCamelCase(string value)
    {
        if (string.IsNullOrEmpty(value))
            return value;

        if (value.Length == 1)
            return value.ToLowerInvariant();

        return char.ToLowerInvariant(value[0]) + value.Substring(1);
    }

    /// <summary>
    /// Converts a string example value to the appropriate IOpenApiAny type based on the property type.
    /// </summary>
    /// <param name="value">The string value to convert.</param>
    /// <param name="type">The type symbol to convert to.</param>
    /// <returns>An IOpenApiAny representing the typed value, or a string fallback on parse failure.</returns>
    private IOpenApiAny ConvertExampleToTypedValue(string value, ITypeSymbol type)
    {
        if (string.IsNullOrEmpty(value))
            return new OpenApiString(value ?? string.Empty);

        // Handle nullable types - unwrap to get the underlying type
        if (type is INamedTypeSymbol namedType &&
            namedType.IsGenericType &&
            namedType.ConstructedFrom.SpecialType == SpecialType.System_Nullable_T)
        {
            type = namedType.TypeArguments[0];
        }

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

    /// <summary>
    /// Gets the example value for a property from its OpenApiSchema attribute.
    /// </summary>
    /// <param name="property">The property symbol to get the example for.</param>
    /// <param name="config">The example configuration.</param>
    /// <returns>An IOpenApiAny representing the example value, or null if no example is available.</returns>
    private IOpenApiAny? GetPropertyExample(IPropertySymbol property, ExampleConfig config)
    {
        // First check for explicit example in OpenApiSchema attribute
        var schemaAttr = property.GetAttributes()
            .FirstOrDefault(a => a.AttributeClass?.Name == "OpenApiSchemaAttribute" ||
                                 a.AttributeClass?.Name == "OpenApiSchema");

        if (schemaAttr != null)
        {
            var exampleArg = schemaAttr.NamedArguments
                .FirstOrDefault(a => a.Key == "Example");

            if (exampleArg.Value.Value is string exampleValue && !string.IsNullOrEmpty(exampleValue))
            {
                return ConvertExampleToTypedValue(exampleValue, property.Type);
            }
        }

        // If no explicit example and defaults are enabled, generate one
        if (config.GenerateDefaults)
        {
            return GenerateDefaultExample(property, schemaAttr);
        }

        return null;
    }

    /// <summary>
    /// Generates a default example value for a property based on its type, format, and constraints.
    /// </summary>
    /// <param name="property">The property symbol to generate an example for.</param>
    /// <param name="schemaAttr">The OpenApiSchema attribute data, if present.</param>
    /// <returns>An IOpenApiAny representing the generated example, or null if no example can be generated.</returns>
    private IOpenApiAny? GenerateDefaultExample(IPropertySymbol property, AttributeData? schemaAttr)
    {
        var type = property.Type;

        // Handle nullable types - unwrap to get the underlying type
        if (type is INamedTypeSymbol namedType &&
            namedType.IsGenericType &&
            namedType.ConstructedFrom.SpecialType == SpecialType.System_Nullable_T)
        {
            type = namedType.TypeArguments[0];
        }

        // Extract schema attributes for format and constraints
        string? format = null;
        double? minimum = null;
        double? maximum = null;
        int? minLength = null;
        int? maxLength = null;

        if (schemaAttr != null)
        {
            foreach (var namedArg in schemaAttr.NamedArguments)
            {
                switch (namedArg.Key)
                {
                    case "Format":
                        format = namedArg.Value.Value as string;
                        break;
                    case "Minimum":
                        if (namedArg.Value.Value is double minVal)
                            minimum = minVal;
                        break;
                    case "Maximum":
                        if (namedArg.Value.Value is double maxVal)
                            maximum = maxVal;
                        break;
                    case "MinLength":
                        if (namedArg.Value.Value is int minLen)
                            minLength = minLen;
                        break;
                    case "MaxLength":
                        if (namedArg.Value.Value is int maxLen)
                            maxLength = maxLen;
                        break;
                }
            }
        }

        // Try format-based default first (for strings)
        if (type.SpecialType == SpecialType.System_String && !string.IsNullOrEmpty(format))
        {
            var formatDefault = GenerateFormatBasedDefault(format);
            if (formatDefault != null)
                return formatDefault;
        }

        // Try constraint-based default for numerics
        if (IsNumericType(type) && (minimum.HasValue || maximum.HasValue))
        {
            return GenerateConstraintBasedNumericDefault(type, minimum, maximum);
        }

        // Try constraint-based default for strings
        if (type.SpecialType == SpecialType.System_String && (minLength.HasValue || maxLength.HasValue))
        {
            return GenerateConstraintBasedStringDefault(minLength, maxLength);
        }

        // Fall back to type-based placeholder
        return GenerateTypePlaceholder(type);
    }

    /// <summary>
    /// Generates a default example value based on the format string.
    /// </summary>
    /// <param name="format">The format string (e.g., "email", "uuid", "date-time").</param>
    /// <returns>An IOpenApiAny representing the format-appropriate default, or null if format is not recognized.</returns>
    private IOpenApiAny? GenerateFormatBasedDefault(string format)
    {
        return format.ToLowerInvariant() switch
        {
            "email" => new OpenApiString("user@example.com"),
            "uuid" => new OpenApiString("550e8400-e29b-41d4-a716-446655440000"),
            "date-time" => new OpenApiString("2024-01-15T10:30:00Z"),
            "date" => new OpenApiString("2024-01-15"),
            "uri" or "url" => new OpenApiString("https://example.com"),
            "hostname" => new OpenApiString("example.com"),
            "ipv4" => new OpenApiString("192.168.1.1"),
            "ipv6" => new OpenApiString("2001:0db8:85a3:0000:0000:8a2e:0370:7334"),
            "time" => new OpenApiString("10:30:00"),
            "password" => new OpenApiString("********"),
            "byte" => new OpenApiString("U3dhZ2dlciByb2Nrcw=="),
            "binary" => new OpenApiString("<binary>"),
            _ => null
        };
    }

    /// <summary>
    /// Checks if a type is a numeric type.
    /// </summary>
    private bool IsNumericType(ITypeSymbol type)
    {
        return type.SpecialType switch
        {
            SpecialType.System_Int32 => true,
            SpecialType.System_Int64 => true,
            SpecialType.System_Int16 => true,
            SpecialType.System_Byte => true,
            SpecialType.System_Double => true,
            SpecialType.System_Single => true,
            SpecialType.System_Decimal => true,
            _ => false
        };
    }

    /// <summary>
    /// Generates a constraint-based default example for numeric types.
    /// Uses midpoint if both bounds specified, minimum if only minimum, maximum if only maximum.
    /// </summary>
    /// <param name="type">The type symbol for the numeric property.</param>
    /// <param name="minimum">The minimum constraint, if specified.</param>
    /// <param name="maximum">The maximum constraint, if specified.</param>
    /// <returns>An IOpenApiAny representing the numeric default within bounds.</returns>
    private IOpenApiAny GenerateConstraintBasedNumericDefault(ITypeSymbol type, double? minimum, double? maximum)
    {
        double value;

        if (minimum.HasValue && maximum.HasValue)
        {
            // Use midpoint when both bounds are specified
            value = (minimum.Value + maximum.Value) / 2.0;
        }
        else if (minimum.HasValue)
        {
            // Use minimum when only minimum is specified
            value = minimum.Value;
        }
        else if (maximum.HasValue)
        {
            // Use maximum when only maximum is specified
            value = maximum.Value;
        }
        else
        {
            // Fallback to 0 (shouldn't reach here due to caller check)
            value = 0;
        }

        // Convert to appropriate type
        return type.SpecialType switch
        {
            SpecialType.System_Int32 => new OpenApiInteger((int)Math.Round(value)),
            SpecialType.System_Int64 => new OpenApiLong((long)Math.Round(value)),
            SpecialType.System_Int16 => new OpenApiInteger((int)Math.Round(value)),
            SpecialType.System_Byte => new OpenApiInteger((int)Math.Round(Math.Max(0, Math.Min(255, value)))),
            SpecialType.System_Double => new OpenApiDouble(value),
            SpecialType.System_Single => new OpenApiFloat((float)value),
            SpecialType.System_Decimal => new OpenApiDouble(value),
            _ => new OpenApiDouble(value)
        };
    }

    /// <summary>
    /// Generates a constraint-based default example for string types.
    /// Creates a string with length within the specified MinLength/MaxLength bounds.
    /// </summary>
    /// <param name="minLength">The minimum length constraint, if specified.</param>
    /// <param name="maxLength">The maximum length constraint, if specified.</param>
    /// <returns>An OpenApiString with appropriate length.</returns>
    private IOpenApiAny GenerateConstraintBasedStringDefault(int? minLength, int? maxLength)
    {
        const string baseString = "string";
        const string paddingChar = "x";

        int targetLength;

        if (minLength.HasValue && maxLength.HasValue)
        {
            // Use midpoint when both bounds are specified
            targetLength = (minLength.Value + maxLength.Value) / 2;
        }
        else if (minLength.HasValue)
        {
            // Use minimum length when only minimum is specified
            targetLength = minLength.Value;
        }
        else if (maxLength.HasValue)
        {
            // Use maximum length when only maximum is specified, but cap at reasonable size
            targetLength = Math.Min(maxLength.Value, 50);
        }
        else
        {
            // Fallback (shouldn't reach here due to caller check)
            return new OpenApiString(baseString);
        }

        // Ensure target length is at least 1
        targetLength = Math.Max(1, targetLength);

        // Generate string of appropriate length
        if (targetLength <= baseString.Length)
        {
            return new OpenApiString(baseString.Substring(0, targetLength));
        }
        else
        {
            // Pad the base string to reach target length
            var padding = new string(paddingChar[0], targetLength - baseString.Length);
            return new OpenApiString(baseString + padding);
        }
    }

    /// <summary>
    /// Generates a type-appropriate placeholder value for properties without constraints.
    /// </summary>
    /// <param name="type">The type symbol to generate a placeholder for.</param>
    /// <returns>An IOpenApiAny representing the type-appropriate placeholder, or null if type is not supported.</returns>
    private IOpenApiAny? GenerateTypePlaceholder(ITypeSymbol type)
    {
        // Handle nullable types - unwrap to get the underlying type
        if (type is INamedTypeSymbol namedType &&
            namedType.IsGenericType &&
            namedType.ConstructedFrom.SpecialType == SpecialType.System_Nullable_T)
        {
            type = namedType.TypeArguments[0];
        }

        return type.SpecialType switch
        {
            SpecialType.System_String => new OpenApiString("string"),
            SpecialType.System_Int32 => new OpenApiInteger(0),
            SpecialType.System_Int64 => new OpenApiLong(0),
            SpecialType.System_Int16 => new OpenApiInteger(0),
            SpecialType.System_Byte => new OpenApiInteger(0),
            SpecialType.System_Double => new OpenApiDouble(0.0),
            SpecialType.System_Single => new OpenApiFloat(0.0f),
            SpecialType.System_Decimal => new OpenApiDouble(0.0),
            SpecialType.System_Boolean => new OpenApiBoolean(false),
            SpecialType.System_DateTime => new OpenApiString("2024-01-15T10:30:00Z"),
            _ => HandleSpecialTypePlaceholder(type)
        };
    }

    /// <summary>
    /// Handles placeholder generation for special types like Guid, DateTimeOffset, etc.
    /// </summary>
    private IOpenApiAny? HandleSpecialTypePlaceholder(ITypeSymbol type)
    {
        var typeName = type.ToDisplayString();

        // Handle common types by full name
        if (typeName == "System.Guid" || typeName == "Guid")
        {
            return new OpenApiString("550e8400-e29b-41d4-a716-446655440000");
        }

        if (typeName == "System.DateTimeOffset" || typeName == "DateTimeOffset")
        {
            return new OpenApiString("2024-01-15T10:30:00+00:00");
        }

        if (typeName == "System.TimeSpan" || typeName == "TimeSpan")
        {
            return new OpenApiString("01:30:00");
        }

        if (typeName == "System.Uri" || typeName == "Uri")
        {
            return new OpenApiString("https://example.com");
        }

        // Return null for unsupported types
        return null;
    }

    /// <summary>
    /// Tracks types currently being processed to prevent infinite recursion with circular references.
    /// </summary>
    private readonly HashSet<ITypeSymbol> _processingExampleTypes = new(SymbolEqualityComparer.Default);

    /// <summary>
    /// Composes an example object from property-level examples.
    /// </summary>
    /// <param name="typeSymbol">The type symbol to compose an example for.</param>
    /// <param name="config">The example configuration.</param>
    /// <returns>An OpenApiObject with property examples, or null if no examples are available.</returns>
    private IOpenApiAny? ComposeExampleFromProperties(ITypeSymbol typeSymbol, ExampleConfig config)
    {
        if (typeSymbol is not INamedTypeSymbol namedType)
            return null;

        // Prevent infinite recursion for circular references
        if (_processingExampleTypes.Contains(typeSymbol))
            return null;

        _processingExampleTypes.Add(typeSymbol);

        try
        {
            var exampleObject = new OpenApiObject();
            var hasAnyExample = false;

            foreach (var member in namedType.GetMembers().OfType<IPropertySymbol>())
            {
                // Skip compiler-generated properties
                if (member.IsImplicitlyDeclared ||
                    member.Name.Equals("EqualityContract", StringComparison.OrdinalIgnoreCase))
                    continue;

                // Skip ignored properties
                if (member.GetAttributes().Any(a =>
                        a.AttributeClass?.Name is "OpenApiIgnore" or "OpenApiIgnoreAttribute"))
                    continue;

                var propertyName = GetJsonPropertyName(member);
                var example = GetPropertyExample(member, config);

                // If no direct example, check for array/collection types first
                if (example == null && IsCollectionType(member.Type, out var elementType))
                {
                    // Handle array/list properties - generate array with single element example
                    var elementExample = GetElementExample(elementType, config);
                    if (elementExample != null)
                    {
                        var arrayExample = new OpenApiArray { elementExample };
                        exampleObject[propertyName] = arrayExample;
                        hasAnyExample = true;
                    }
                }
                // If no direct example, check if it's a complex type and try to compose recursively
                else if (example == null && !IsSimpleType(member.Type))
                {
                    // Handle nested complex objects
                    var nestedExample = ComposeExampleFromProperties(member.Type, config);
                    if (nestedExample != null)
                    {
                        exampleObject[propertyName] = nestedExample;
                        hasAnyExample = true;
                    }
                }
                else if (example != null)
                {
                    exampleObject[propertyName] = example;
                    hasAnyExample = true;
                }
            }

            return hasAnyExample ? exampleObject : null;
        }
        finally
        {
            _processingExampleTypes.Remove(typeSymbol);
        }
    }

    /// <summary>
    /// Gets an example value for an array element type.
    /// </summary>
    /// <param name="elementType">The element type of the array.</param>
    /// <param name="config">The example configuration.</param>
    /// <returns>An IOpenApiAny representing the element example, or null if no example can be generated.</returns>
    private IOpenApiAny? GetElementExample(ITypeSymbol elementType, ExampleConfig config)
    {
        // For simple types, generate a type-appropriate placeholder
        if (IsSimpleType(elementType))
        {
            // Generate a default example for the element type
            return GenerateTypePlaceholder(elementType);
        }

        // For complex types, try to compose an example from properties
        return ComposeExampleFromProperties(elementType, config);
    }
}
