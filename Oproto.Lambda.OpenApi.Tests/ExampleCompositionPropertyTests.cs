#nullable enable
using System.Text.Json;
using FsCheck;
using FsCheck.Xunit;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Oproto.Lambda.OpenApi.SourceGenerator;

namespace Oproto.Lambda.OpenApi.Tests;

/// <summary>
/// Property-based tests for automatic example composition functionality.
/// </summary>
public class ExampleCompositionPropertyTests
{
    /// <summary>
    /// **Feature: tag-groups-extension, Property 11: Example composition from properties**
    /// **Validates: Requirements 6.1, 6.2**
    /// 
    /// For any type where properties have [OpenApiSchema(Example = "...")] values,
    /// the Source_Generator SHALL compose an example object containing all properties that have examples.
    /// </summary>
    [Property(MaxTest = 100)]
    public Property ExampleComposition_ComposesFromPropertyExamples()
    {
        // Generate property configurations with examples
        var propertyNameGen = Gen.Elements("Name", "Title", "Description", "Value", "Count", "Price");
        var exampleValueGen = Gen.Elements("TestValue", "SampleName", "Example123", "Widget", "Product");

        var propertyConfigGen = Gen.Zip(propertyNameGen, exampleValueGen)
            .Select(t => new PropertyConfig(t.Item1, "string", t.Item2));

        // Generate 1-4 properties with examples
        var propertiesGen = Gen.ListOf(propertyConfigGen)
            .Select(props => props.DistinctBy(p => p.Name).Take(4).ToList())
            .Where(props => props.Count > 0);

        return Prop.ForAll(
            propertiesGen.ToArbitrary(),
            properties =>
            {
                var source = GenerateSourceWithPropertyExamples(properties);
                var composedExample = ExtractComposedExample(source);

                if (composedExample == null)
                    return false.Label("No composed example found in output");

                var example = composedExample.Value;
                
                // Verify all properties with examples are present in the composed example
                var allPropertiesPresent = properties.All(prop =>
                {
                    var jsonPropertyName = ToCamelCase(prop.Name);
                    return example.TryGetProperty(jsonPropertyName, out var value) &&
                           value.GetString() == prop.Example;
                });

                return allPropertiesPresent
                    .Label($"Expected all properties with examples to be present. Properties: [{string.Join(", ", properties.Select(p => $"{p.Name}={p.Example}"))}], " +
                           $"Composed: {example}");
            });
    }

    /// <summary>
    /// **Feature: tag-groups-extension, Property 12: Partial example composition**
    /// **Validates: Requirements 6.3**
    /// 
    /// For any type where only some properties have example values,
    /// the composed example SHALL include only those properties with examples.
    /// </summary>
    [Property(MaxTest = 100)]
    public Property PartialExampleComposition_IncludesOnlyPropertiesWithExamples()
    {
        var propertyNameGen = Gen.Elements("Name", "Title", "Description", "Value");
        var exampleValueGen = Gen.Elements("TestValue", "SampleName", "Example123");

        // Generate properties with examples
        var withExampleGen = Gen.Zip(propertyNameGen, exampleValueGen)
            .Select(t => new PropertyConfig(t.Item1, "string", t.Item2));

        // Generate properties without examples
        var withoutExampleGen = Gen.Elements("Id", "CreatedAt", "UpdatedAt", "Status")
            .Select(name => new PropertyConfig(name, "string", null));

        return Prop.ForAll(
            Gen.Zip(
                Gen.ListOf(withExampleGen).Select(l => l.Take(2).ToList()),
                Gen.ListOf(withoutExampleGen).Select(l => l.Take(2).ToList())
            ).Where(t => t.Item1.Count > 0).ToArbitrary(),
            tuple =>
            {
                var (withExamples, withoutExamples) = tuple;
                var allProperties = withExamples.Concat(withoutExamples)
                    .DistinctBy(p => p.Name)
                    .ToList();

                var source = GenerateSourceWithMixedProperties(allProperties);
                var composedExample = ExtractComposedExample(source);

                if (composedExample == null)
                    return false.Label("No composed example found");

                var example = composedExample.Value;

                // Properties with examples should be present
                var examplesPresent = withExamples.All(prop =>
                {
                    var jsonName = ToCamelCase(prop.Name);
                    return example.TryGetProperty(jsonName, out _);
                });

                // Properties without examples should NOT be present
                var noExtrasPresent = withoutExamples.All(prop =>
                {
                    var jsonName = ToCamelCase(prop.Name);
                    return !example.TryGetProperty(jsonName, out _);
                });

                return (examplesPresent && noExtrasPresent)
                    .Label($"Expected only properties with examples. With: [{string.Join(", ", withExamples.Select(p => p.Name))}], " +
                           $"Without: [{string.Join(", ", withoutExamples.Select(p => p.Name))}]");
            });
    }

    /// <summary>
    /// **Feature: tag-groups-extension, Property 15: Example type conversion**
    /// **Validates: Requirements 7.1, 7.2, 7.3**
    /// 
    /// For any property with an example value, the example SHALL be converted to the appropriate JSON type.
    /// </summary>
    [Property(MaxTest = 100)]
    public Property ExampleTypeConversion_ConvertsToCorrectJsonType()
    {
        var typeExampleGen = Gen.Elements(
            ("int", "42", JsonValueKind.Number),
            ("long", "9999999999", JsonValueKind.Number),
            ("double", "3.14", JsonValueKind.Number),
            ("decimal", "99.99", JsonValueKind.Number),
            ("bool", "true", JsonValueKind.True),
            ("bool", "false", JsonValueKind.False),
            ("string", "TestString", JsonValueKind.String)
        );

        return Prop.ForAll(
            typeExampleGen.ToArbitrary(),
            tuple =>
            {
                var (typeName, exampleValue, expectedKind) = tuple;
                var properties = new List<PropertyConfig>
                {
                    new PropertyConfig("TestProperty", typeName, exampleValue)
                };

                var source = GenerateSourceWithPropertyExamples(properties);
                var composedExample = ExtractComposedExample(source);

                if (composedExample == null)
                    return false.Label("No composed example found");

                var example = composedExample.Value;

                if (!example.TryGetProperty("testProperty", out var value))
                    return false.Label("Property not found in composed example");

                var actualKind = value.ValueKind;
                var kindMatches = actualKind == expectedKind;

                return kindMatches
                    .Label($"Expected {expectedKind} for {typeName} with value '{exampleValue}', but got {actualKind}");
            });
    }

    /// <summary>
    /// **Feature: tag-groups-extension, Property 17: Nested object example composition**
    /// **Validates: Requirements 7.5**
    /// 
    /// For any property of a complex object type, the Source_Generator SHALL recursively
    /// compose examples from the nested type's property examples.
    /// </summary>
    [Property(MaxTest = 100)]
    public Property NestedObjectExampleComposition_ComposesRecursively()
    {
        var nestedPropertyGen = Gen.Elements(
            ("Street", "123 Main St"),
            ("City", "Springfield"),
            ("ZipCode", "12345")
        );

        var parentPropertyGen = Gen.Elements(
            ("Name", "John Doe"),
            ("Email", "john@example.com")
        );

        return Prop.ForAll(
            Gen.Zip(
                Gen.ListOf(parentPropertyGen).Select(l => l.Take(2).ToList()),
                Gen.ListOf(nestedPropertyGen).Select(l => l.Take(2).ToList())
            ).Where(t => t.Item1.Count > 0 && t.Item2.Count > 0).ToArbitrary(),
            tuple =>
            {
                var (parentProps, nestedProps) = tuple;
                var source = GenerateSourceWithNestedType(parentProps, nestedProps);
                var composedExample = ExtractComposedExample(source);

                if (composedExample == null)
                    return false.Label("No composed example found");

                var example = composedExample.Value;

                // Check parent properties
                var parentPropsPresent = parentProps.All(prop =>
                {
                    var jsonName = ToCamelCase(prop.Item1);
                    return example.TryGetProperty(jsonName, out _);
                });

                // Check nested object exists and has properties
                var hasNestedObject = example.TryGetProperty("address", out var addressElement) &&
                                      addressElement.ValueKind == JsonValueKind.Object;

                var nestedPropsPresent = hasNestedObject && nestedProps.All(prop =>
                {
                    var jsonName = ToCamelCase(prop.Item1);
                    return addressElement.TryGetProperty(jsonName, out _);
                });

                return (parentPropsPresent && nestedPropsPresent)
                    .Label($"Expected nested composition. Parent: [{string.Join(", ", parentProps.Select(p => p.Item1))}], " +
                           $"Nested: [{string.Join(", ", nestedProps.Select(p => p.Item1))}]");
            });
    }

    /// <summary>
    /// **Feature: tag-groups-extension, Property 16: Array example generation**
    /// **Validates: Requirements 7.4**
    /// 
    /// For any array-type property with element examples, the Source_Generator SHALL generate
    /// an array example containing appropriately typed elements.
    /// </summary>
    [Property(MaxTest = 100)]
    public Property ArrayExampleGeneration_GeneratesArrayWithElementExample()
    {
        // Generate element type configurations
        var elementTypeGen = Gen.Elements(
            ("string", "string"),
            ("int", "integer"),
            ("bool", "boolean")
        );

        // Generate array property names
        var arrayPropertyNameGen = Gen.Elements("Items", "Tags", "Values", "Numbers", "Flags");

        // Generate parent property configurations
        var parentPropertyGen = Gen.Elements(
            ("Name", "TestName"),
            ("Title", "TestTitle"),
            ("Description", "TestDescription")
        );

        return Prop.ForAll(
            Gen.Zip(
                arrayPropertyNameGen,
                elementTypeGen,
                Gen.ListOf(parentPropertyGen).Select(l => l.Take(2).ToList())
            ).Where(t => t.Item3.Count > 0).ToArbitrary(),
            tuple =>
            {
                var (arrayPropertyName, elementTypeInfo, parentProps) = tuple;
                var (elementType, _) = elementTypeInfo;

                var source = GenerateSourceWithArrayProperty(arrayPropertyName, elementType, parentProps);
                var composedExample = ExtractComposedExample(source);

                if (composedExample == null)
                    return false.Label("No composed example found in output");

                var example = composedExample.Value;

                // Check that the array property exists and is an array
                var arrayJsonName = ToCamelCase(arrayPropertyName);
                if (!example.TryGetProperty(arrayJsonName, out var arrayElement))
                    return false.Label($"Array property '{arrayJsonName}' not found in composed example");

                var isArray = arrayElement.ValueKind == JsonValueKind.Array;
                if (!isArray)
                    return false.Label($"Expected array for '{arrayJsonName}', but got {arrayElement.ValueKind}");

                // Check that the array has at least one element
                var hasElements = arrayElement.GetArrayLength() > 0;
                if (!hasElements)
                    return false.Label($"Expected array '{arrayJsonName}' to have at least one element");

                // Check that parent properties with examples are also present
                var parentPropsPresent = parentProps.All(prop =>
                {
                    var jsonName = ToCamelCase(prop.Item1);
                    return example.TryGetProperty(jsonName, out _);
                });

                return (isArray && hasElements && parentPropsPresent)
                    .Label($"Expected array with elements for '{arrayPropertyName}' and parent properties present");
            });
    }

    private record PropertyConfig(string Name, string Type, string? Example);

    private static string ToCamelCase(string value)
    {
        if (string.IsNullOrEmpty(value)) return value;
        if (value.Length == 1) return value.ToLowerInvariant();
        return char.ToLowerInvariant(value[0]) + value.Substring(1);
    }

    private static string GenerateSourceWithArrayProperty(
        string arrayPropertyName,
        string elementType,
        List<(string Name, string Example)> parentProps)
    {
        var parentPropertyDeclarations = string.Join("\n    ", parentProps.Select(p =>
            $@"[Oproto.Lambda.OpenApi.Attributes.OpenApiSchema(Example = ""{p.Example}"")]
    public string {p.Name} {{ get; set; }}"));

        var arrayType = elementType switch
        {
            "int" => "List<int>",
            "bool" => "List<bool>",
            _ => "List<string>"
        };

        return $@"
using Amazon.Lambda.Annotations;
using Amazon.Lambda.Annotations.APIGateway;
using System.Collections.Generic;
using System.Threading.Tasks;

public class TestRequest
{{
    {parentPropertyDeclarations}
    public {arrayType} {arrayPropertyName} {{ get; set; }}
}}

public class TestResponse
{{
    public int Id {{ get; set; }}
}}

public class TestFunctions 
{{
    [LambdaFunction]
    [HttpApi(LambdaHttpMethod.Post, ""/items"")]
    public TestResponse CreateItem([FromBody] TestRequest request) => new TestResponse {{ Id = 1 }};
}}";
    }

    private static string GenerateSourceWithPropertyExamples(List<PropertyConfig> properties)
    {
        var propertyDeclarations = string.Join("\n    ", properties.Select(p =>
        {
            var typeStr = MapType(p.Type);
            var attr = p.Example != null
                ? $@"[Oproto.Lambda.OpenApi.Attributes.OpenApiSchema(Example = ""{p.Example}"")]"
                : "";
            return $@"{attr}
    public {typeStr} {p.Name} {{ get; set; }}";
        }));

        return $@"
using Amazon.Lambda.Annotations;
using Amazon.Lambda.Annotations.APIGateway;
using System.Threading.Tasks;

public class TestRequest
{{
    {propertyDeclarations}
}}

public class TestResponse
{{
    public int Id {{ get; set; }}
}}

public class TestFunctions 
{{
    [LambdaFunction]
    [HttpApi(LambdaHttpMethod.Post, ""/items"")]
    public TestResponse CreateItem([FromBody] TestRequest request) => new TestResponse {{ Id = 1 }};
}}";
    }

    private static string GenerateSourceWithMixedProperties(List<PropertyConfig> properties)
    {
        var propertyDeclarations = string.Join("\n    ", properties.Select(p =>
        {
            var typeStr = MapType(p.Type);
            var attr = p.Example != null
                ? $@"[Oproto.Lambda.OpenApi.Attributes.OpenApiSchema(Example = ""{p.Example}"")]"
                : "";
            return $@"{attr}
    public {typeStr} {p.Name} {{ get; set; }}";
        }));

        return $@"
using Amazon.Lambda.Annotations;
using Amazon.Lambda.Annotations.APIGateway;
using System.Threading.Tasks;

public class TestRequest
{{
    {propertyDeclarations}
}}

public class TestResponse
{{
    public int Id {{ get; set; }}
}}

public class TestFunctions 
{{
    [LambdaFunction]
    [HttpApi(LambdaHttpMethod.Post, ""/items"")]
    public TestResponse CreateItem([FromBody] TestRequest request) => new TestResponse {{ Id = 1 }};
}}";
    }

    private static string GenerateSourceWithNestedType(
        List<(string Name, string Example)> parentProps,
        List<(string Name, string Example)> nestedProps)
    {
        var parentPropertyDeclarations = string.Join("\n    ", parentProps.Select(p =>
            $@"[Oproto.Lambda.OpenApi.Attributes.OpenApiSchema(Example = ""{p.Example}"")]
    public string {p.Name} {{ get; set; }}"));

        var nestedPropertyDeclarations = string.Join("\n    ", nestedProps.Select(p =>
            $@"[Oproto.Lambda.OpenApi.Attributes.OpenApiSchema(Example = ""{p.Example}"")]
    public string {p.Name} {{ get; set; }}"));

        return $@"
using Amazon.Lambda.Annotations;
using Amazon.Lambda.Annotations.APIGateway;
using System.Threading.Tasks;

public class Address
{{
    {nestedPropertyDeclarations}
}}

public class TestRequest
{{
    {parentPropertyDeclarations}
    public Address Address {{ get; set; }}
}}

public class TestResponse
{{
    public int Id {{ get; set; }}
}}

public class TestFunctions 
{{
    [LambdaFunction]
    [HttpApi(LambdaHttpMethod.Post, ""/items"")]
    public TestResponse CreateItem([FromBody] TestRequest request) => new TestResponse {{ Id = 1 }};
}}";
    }

    private static string MapType(string type) => type switch
    {
        "int" => "int",
        "long" => "long",
        "double" => "double",
        "decimal" => "decimal",
        "float" => "float",
        "bool" => "bool",
        _ => "string"
    };

    private JsonElement? ExtractComposedExample(string source)
    {
        try
        {
            var compilation = CompilerHelper.CreateCompilation(source);
            var generator = new OpenApiSpecGenerator();

            var driver = CSharpGeneratorDriver.Create(generator);
            driver.RunGeneratorsAndUpdateCompilation(compilation,
                out var outputCompilation,
                out var diagnostics);

            if (diagnostics.Any(d => d.Severity == DiagnosticSeverity.Error))
                return null;

            var jsonContent = ExtractOpenApiJson(outputCompilation);
            if (string.IsNullOrEmpty(jsonContent))
                return null;

            var doc = JsonDocument.Parse(jsonContent);

            // Look for the example in the request body schema
            if (doc.RootElement.TryGetProperty("paths", out var paths))
            {
                foreach (var path in paths.EnumerateObject())
                {
                    foreach (var operation in path.Value.EnumerateObject())
                    {
                        if (operation.Name.StartsWith("x-") || operation.Name == "parameters")
                            continue;

                        if (operation.Value.TryGetProperty("requestBody", out var requestBody) &&
                            requestBody.TryGetProperty("content", out var content) &&
                            content.TryGetProperty("application/json", out var mediaType))
                        {
                            // Check for example at media type level
                            if (mediaType.TryGetProperty("example", out var example))
                            {
                                return example;
                            }

                            // Check for example in schema
                            if (mediaType.TryGetProperty("schema", out var schema) &&
                                schema.TryGetProperty("example", out var schemaExample))
                            {
                                return schemaExample;
                            }
                        }
                    }
                }
            }

            // Also check components/schemas for the example
            if (doc.RootElement.TryGetProperty("components", out var components) &&
                components.TryGetProperty("schemas", out var schemas))
            {
                foreach (var schemaEntry in schemas.EnumerateObject())
                {
                    if (schemaEntry.Name == "TestRequest" &&
                        schemaEntry.Value.TryGetProperty("example", out var example))
                    {
                        return example;
                    }
                }
            }
        }
        catch
        {
            // Return null on error
        }

        return null;
    }

    private string ExtractOpenApiJson(Compilation outputCompilation)
    {
        var generatedFile = outputCompilation.SyntaxTrees
            .FirstOrDefault(x => x.FilePath.EndsWith("OpenApiOutput.g.cs"));

        if (generatedFile == null)
            return string.Empty;

        var generatedContent = generatedFile.GetRoot().GetText().ToString();

        var attributeStart = generatedContent.IndexOf("[assembly: OpenApiOutput(@\"") + 26;
        var attributeEnd = generatedContent.LastIndexOf("\", \"openapi.json\")]");

        if (attributeStart < 26 || attributeEnd < 0)
            return string.Empty;

        var rawJson = generatedContent[attributeStart..attributeEnd];

        return rawJson
            .Replace("\"\"", "\"")
            .Replace("\r\n", " ")
            .Replace("\n", " ")
            .Replace("\r", " ")
            .Replace("\\\"", "\"")
            .Trim()
            .TrimStart('"')
            .TrimEnd('"');
    }
}


/// <summary>
/// Property-based tests for example placement and precedence functionality.
/// </summary>
public class ExamplePlacementPropertyTests
{
    /// <summary>
    /// **Feature: tag-groups-extension, Property 13: Example placement in schemas**
    /// **Validates: Requirements 6.4**
    /// 
    /// For any request body or response schema type with composed examples,
    /// the example SHALL appear in the appropriate location in the OpenAPI document.
    /// </summary>
    [Property(MaxTest = 100)]
    public Property ExamplePlacement_AppearsInRequestBody()
    {
        var propertyNameGen = Gen.Elements("Name", "Title", "Description", "Value");
        var exampleValueGen = Gen.Elements("TestValue", "SampleName", "Example123");

        var propertyConfigGen = Gen.Zip(propertyNameGen, exampleValueGen)
            .Select(t => new PropertyConfig(t.Item1, "string", t.Item2));

        var propertiesGen = Gen.ListOf(propertyConfigGen)
            .Select(props => props.DistinctBy(p => p.Name).Take(3).ToList())
            .Where(props => props.Count > 0);

        return Prop.ForAll(
            propertiesGen.ToArbitrary(),
            properties =>
            {
                var source = GenerateSourceWithPropertyExamples(properties);
                var hasExampleInRequestBody = CheckExampleInRequestBody(source);

                return hasExampleInRequestBody
                    .Label($"Expected example in requestBody.content.application/json.example for properties: [{string.Join(", ", properties.Select(p => p.Name))}]");
            });
    }

    /// <summary>
    /// **Feature: tag-groups-extension, Property 14: Explicit example precedence**
    /// **Validates: Requirements 6.5**
    /// 
    /// For any operation that has both property-level examples and an explicit [OpenApiExample] attribute,
    /// the explicit example SHALL be used instead of the composed example.
    /// </summary>
    [Property(MaxTest = 100)]
    public Property ExplicitExamplePrecedence_UsesExplicitOverComposed()
    {
        var explicitExampleGen = Gen.Elements(
            "{\"explicit\": true, \"source\": \"attribute\"}",
            "{\"fromAttribute\": \"yes\", \"id\": 999}",
            "{\"type\": \"explicit\", \"value\": 42}");

        return Prop.ForAll(
            explicitExampleGen.ToArbitrary(),
            explicitExample =>
            {
                // Create source with both property-level examples AND explicit [OpenApiExample]
                var source = GenerateSourceWithBothExampleTypes(explicitExample);
                var extractedExample = ExtractRequestBodyExample(source);

                if (extractedExample == null)
                    return false.Label("No example found in request body");

                // The explicit example should be used, not the composed one
                var hasExplicitContent = extractedExample.Contains("explicit") ||
                                         extractedExample.Contains("fromAttribute") ||
                                         extractedExample.Contains("999") ||
                                         extractedExample.Contains("42");

                // Should NOT have composed content
                var noComposedContent = !extractedExample.Contains("ComposedValue") &&
                                        !extractedExample.Contains("PropertyExample");

                return (hasExplicitContent && noComposedContent)
                    .Label($"Expected explicit example content, but got: {extractedExample}");
            });
    }

    private record PropertyConfig(string Name, string Type, string? Example);

    private static string GenerateSourceWithPropertyExamples(List<PropertyConfig> properties)
    {
        var propertyDeclarations = string.Join("\n    ", properties.Select(p =>
        {
            var attr = p.Example != null
                ? $@"[Oproto.Lambda.OpenApi.Attributes.OpenApiSchema(Example = ""{p.Example}"")]"
                : "";
            return $@"{attr}
    public string {p.Name} {{ get; set; }}";
        }));

        return $@"
using Amazon.Lambda.Annotations;
using Amazon.Lambda.Annotations.APIGateway;
using System.Threading.Tasks;

public class TestRequest
{{
    {propertyDeclarations}
}}

public class TestResponse
{{
    public int Id {{ get; set; }}
}}

public class TestFunctions 
{{
    [LambdaFunction]
    [HttpApi(LambdaHttpMethod.Post, ""/items"")]
    public TestResponse CreateItem([FromBody] TestRequest request) => new TestResponse {{ Id = 1 }};
}}";
    }

    private static string GenerateSourceWithBothExampleTypes(string explicitExample)
    {
        var escapedExample = explicitExample.Replace("\"", "\\\"");

        return $@"
using Amazon.Lambda.Annotations;
using Amazon.Lambda.Annotations.APIGateway;
using System.Threading.Tasks;

public class TestRequest
{{
    [Oproto.Lambda.OpenApi.Attributes.OpenApiSchema(Example = ""ComposedValue"")]
    public string Name {{ get; set; }}

    [Oproto.Lambda.OpenApi.Attributes.OpenApiSchema(Example = ""PropertyExample"")]
    public string Description {{ get; set; }}
}}

public class TestResponse
{{
    public int Id {{ get; set; }}
}}

public class TestFunctions 
{{
    [LambdaFunction]
    [HttpApi(LambdaHttpMethod.Post, ""/items"")]
    [Oproto.Lambda.OpenApi.Attributes.OpenApiExample(""Explicit Example"", ""{escapedExample}"", IsRequestExample = true)]
    public TestResponse CreateItem([FromBody] TestRequest request) => new TestResponse {{ Id = 1 }};
}}";
    }

    private bool CheckExampleInRequestBody(string source)
    {
        try
        {
            var compilation = CompilerHelper.CreateCompilation(source);
            var generator = new OpenApiSpecGenerator();

            var driver = CSharpGeneratorDriver.Create(generator);
            driver.RunGeneratorsAndUpdateCompilation(compilation,
                out var outputCompilation,
                out var diagnostics);

            if (diagnostics.Any(d => d.Severity == DiagnosticSeverity.Error))
                return false;

            var jsonContent = ExtractOpenApiJson(outputCompilation);
            if (string.IsNullOrEmpty(jsonContent))
                return false;

            using var doc = JsonDocument.Parse(jsonContent);

            if (doc.RootElement.TryGetProperty("paths", out var paths))
            {
                foreach (var path in paths.EnumerateObject())
                {
                    foreach (var operation in path.Value.EnumerateObject())
                    {
                        if (operation.Name.StartsWith("x-") || operation.Name == "parameters")
                            continue;

                        if (operation.Value.TryGetProperty("requestBody", out var requestBody) &&
                            requestBody.TryGetProperty("content", out var content) &&
                            content.TryGetProperty("application/json", out var mediaType) &&
                            mediaType.TryGetProperty("example", out _))
                        {
                            return true;
                        }
                    }
                }
            }
        }
        catch
        {
            // Return false on error
        }

        return false;
    }

    private string? ExtractRequestBodyExample(string source)
    {
        try
        {
            var compilation = CompilerHelper.CreateCompilation(source);
            var generator = new OpenApiSpecGenerator();

            var driver = CSharpGeneratorDriver.Create(generator);
            driver.RunGeneratorsAndUpdateCompilation(compilation,
                out var outputCompilation,
                out var diagnostics);

            if (diagnostics.Any(d => d.Severity == DiagnosticSeverity.Error))
                return null;

            var jsonContent = ExtractOpenApiJson(outputCompilation);
            if (string.IsNullOrEmpty(jsonContent))
                return null;

            using var doc = JsonDocument.Parse(jsonContent);

            if (doc.RootElement.TryGetProperty("paths", out var paths))
            {
                foreach (var path in paths.EnumerateObject())
                {
                    foreach (var operation in path.Value.EnumerateObject())
                    {
                        if (operation.Name.StartsWith("x-") || operation.Name == "parameters")
                            continue;

                        if (operation.Value.TryGetProperty("requestBody", out var requestBody) &&
                            requestBody.TryGetProperty("content", out var content) &&
                            content.TryGetProperty("application/json", out var mediaType) &&
                            mediaType.TryGetProperty("example", out var example))
                        {
                            return example.ToString();
                        }
                    }
                }
            }
        }
        catch
        {
            return null;
        }

        return null;
    }

    private string ExtractOpenApiJson(Compilation outputCompilation)
    {
        var generatedFile = outputCompilation.SyntaxTrees
            .FirstOrDefault(x => x.FilePath.EndsWith("OpenApiOutput.g.cs"));

        if (generatedFile == null)
            return string.Empty;

        var generatedContent = generatedFile.GetRoot().GetText().ToString();

        var attributeStart = generatedContent.IndexOf("[assembly: OpenApiOutput(@\"") + 26;
        var attributeEnd = generatedContent.LastIndexOf("\", \"openapi.json\")]");

        if (attributeStart < 26 || attributeEnd < 0)
            return string.Empty;

        var rawJson = generatedContent[attributeStart..attributeEnd];

        return rawJson
            .Replace("\"\"", "\"")
            .Replace("\r\n", " ")
            .Replace("\n", " ")
            .Replace("\r", " ")
            .Replace("\\\"", "\"")
            .Trim()
            .TrimStart('"')
            .TrimEnd('"');
    }
}
