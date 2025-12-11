using Microsoft.CodeAnalysis;
using Microsoft.OpenApi.Any;
using Microsoft.OpenApi.Models;

namespace Oproto.Lambda.OpenApi.SourceGenerator;

public partial class OpenApiSpecGenerator
{
    /// <summary>
    ///     Applies OpenApiSchema and OpenApiExample attributes to the schema.
    ///     Handles validation rules (min/max length, pattern) and examples.
    ///     Attribute definitions must be included in the compilation for tests.
    /// </summary>
    private void ApplySchemaAttributes(OpenApiSchema schema, ISymbol memberSymbol)
    {
        // If this is a property that overrides a base property, get the base property's attributes first
        var attributes = memberSymbol.GetAttributes().ToList();
        if (memberSymbol is IPropertySymbol propertySymbol && propertySymbol.IsOverride)
            if (propertySymbol.OverriddenProperty != null)
                attributes.InsertRange(0, propertySymbol.OverriddenProperty.GetAttributes());

        // Check for OpenApiSchema attribute
        var schemaAttribute = attributes.FirstOrDefault(a =>
            a.AttributeClass?.Name is "OpenApiSchema" or "OpenApiSchemaAttribute");

        if (schemaAttribute != null)
            foreach (var namedArg in schemaAttribute.NamedArguments)
                // Only apply if the property is actually set in the attribute
                ApplySchemaProperty(schema, namedArg.Key, namedArg.Value.Value);

        // Check for OpenApiExample attribute
        var exampleAttribute = attributes.FirstOrDefault(a =>
            a.AttributeClass?.Name is "OpenApiExample" or "OpenApiExampleAttribute");

        if (exampleAttribute != null)
        {
            var exampleValue = exampleAttribute.ConstructorArguments.FirstOrDefault().Value;
            if (exampleValue != null)
                schema.Example = ConvertToOpenApiAny(exampleValue);
        }
    }

    private void ApplySchemaProperty(OpenApiSchema schema, string propertyName, object value)
    {
        switch (propertyName)
        {
            case "Example":
                if (value != null)
                {
                    // For string examples, use the value directly
                    if (value is string stringValue)
                        schema.Example = new OpenApiString(stringValue);
                    else
                        schema.Example = ConvertToOpenApiAny(value);
                }

                break;
            case "Description":
                if (value is string description)
                    schema.Description = description;
                break;
            case "MinLength":
                if (value is int minLength)
                    schema.MinLength = minLength;
                break;
            case "MaxLength":
                if (value is int maxLength)
                    schema.MaxLength = maxLength;
                break;
            case "Pattern":
                if (value is string pattern)
                    schema.Pattern = pattern;
                break;
            case "Minimum":
                if (value is double minDouble)
                    schema.Minimum = Convert.ToDecimal(minDouble);
                break;
            case "Maximum":
                if (value is double maxDouble)
                    schema.Maximum = Convert.ToDecimal(maxDouble);
                break;
        }
    }


    private IOpenApiAny ConvertToOpenApiAny(object value)
    {
        if (value == null)
            return null;

        // If it's a string, try to parse it as a number first
        if (value is string s)
        {
            if (int.TryParse(s, out var intValue))
                return new OpenApiInteger(intValue);
            if (long.TryParse(s, out var longValue))
                return new OpenApiLong(longValue);
            if (double.TryParse(s, out var doubleValue))
                return new OpenApiDouble(doubleValue);
            return new OpenApiString(s);
        }

        return value switch
        {
            int i => new OpenApiInteger(i),
            long l => new OpenApiLong(l),
            double d => new OpenApiDouble(d),
            decimal m => new OpenApiDouble((double)m),
            bool b => new OpenApiBoolean(b),
            DateTime dt => new OpenApiString(dt.ToString("O")),
            _ => value != null ? new OpenApiString(value.ToString()) : null
        };
    }


    private void ApplyOperationAttribute(OpenApiOperation operation, IMethodSymbol methodSymbol)
    {
        var attribute = GetAttribute(methodSymbol, "OpenApiOperation");
        if (attribute == null)
            return;

        foreach (var namedArg in attribute.NamedArguments)
            switch (namedArg.Key)
            {
                case "Summary":
                    var summary = namedArg.Value.Value?.ToString();
                    if (!string.IsNullOrEmpty(summary))
                        operation.Summary = summary;
                    break;
                case "Description":
                    var description = namedArg.Value.Value?.ToString();
                    if (!string.IsNullOrEmpty(description))
                        operation.Description = description;
                    break;
                case "Deprecated":
                    if (namedArg.Value.Value is bool deprecated)
                        operation.Deprecated = deprecated;
                    break;
                case "OperationId":
                    var operationId = namedArg.Value.Value?.ToString();
                    if (!string.IsNullOrEmpty(operationId))
                        operation.OperationId = operationId;
                    break;
            }
    }

    private AttributeData GetAttribute(ISymbol symbol, string attributeName)
    {
        return symbol.GetAttributes()
            .FirstOrDefault(attr =>
                attr.AttributeClass?.Name == attributeName ||
                attr.AttributeClass?.Name == attributeName + "Attribute");
    }

    private bool HasAttribute(ISymbol symbol, string attributeName)
    {
        return symbol.GetAttributes()
            .Any(a => a.AttributeClass?.Name == attributeName);
    }
}
