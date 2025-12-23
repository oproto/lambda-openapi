#nullable enable
using System.Text.Json;
using FsCheck;
using FsCheck.Xunit;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Oproto.Lambda.OpenApi.SourceGenerator;

namespace Oproto.Lambda.OpenApi.Tests;

/// <summary>
/// Property-based tests for default example generation functionality.
/// </summary>
public class DefaultExampleGenerationPropertyTests
{
    /// <summary>
    /// **Feature: tag-groups-extension, Property 18: Format-based default generation**
    /// **Validates: Requirements 8.1**
    /// 
    /// For any string property with a Format specified (email, uuid, date-time, etc.) but no explicit example,
    /// the Source_Generator SHALL generate a format-appropriate default example when default generation is enabled.
    /// </summary>
    [Property(MaxTest = 100)]
    public Property FormatBasedDefaultGeneration_GeneratesFormatAppropriateDefaults()
    {
        var formatExpectedGen = Gen.Elements(
            ("email", "user@example.com"),
            ("uuid", "550e8400-e29b-41d4-a716-446655440000"),
            ("date-time", "2024-01-15T10:30:00Z"),
            ("date", "2024-01-15"),
            ("uri", "https://example.com"),
            ("hostname", "example.com"),
            ("ipv4", "192.168.1.1"),
            ("ipv6", "2001:0db8:85a3:0000:0000:8a2e:0370:7334")
        );

        return Prop.ForAll(
            formatExpectedGen.ToArbitrary(),
            tuple =>
            {
                var (format, expectedValue) = tuple;
                var source = GenerateSourceWithFormat("TestProperty", format);
                var composedExample = ExtractComposedExample(source);

                if (composedExample == null)
                    return false.Label($"No composed example found for format '{format}'");

                var example = composedExample.Value;

                if (!example.TryGetProperty("testProperty", out var value))
                    return false.Label($"Property 'testProperty' not found in composed example for format '{format}'");

                var actualValue = value.GetString();
                var matches = actualValue == expectedValue;

                return matches
                    .Label($"Expected '{expectedValue}' for format '{format}', but got '{actualValue}'");
            });
    }

    /// <summary>
    /// **Feature: tag-groups-extension, Property 19: Constraint-based numeric defaults**
    /// **Validates: Requirements 8.2**
    /// 
    /// For any numeric property with Minimum and/or Maximum constraints but no explicit example,
    /// the generated default example SHALL be within the specified bounds when default generation is enabled.
    /// </summary>
    [Property(MaxTest = 100)]
    public Property ConstraintBasedNumericDefaults_GeneratesValueWithinBounds()
    {
        // Generate min/max pairs where min <= max
        var boundsGen = Gen.Choose(0, 100)
            .SelectMany(min => Gen.Choose(min, min + 100).Select(max => (min, max)));

        return Prop.ForAll(
            boundsGen.ToArbitrary(),
            bounds =>
            {
                var (min, max) = bounds;
                var source = GenerateSourceWithNumericConstraints("TestProperty", "int", min, max);
                var composedExample = ExtractComposedExample(source);

                if (composedExample == null)
                    return false.Label($"No composed example found for numeric constraints min={min}, max={max}");

                var example = composedExample.Value;

                if (!example.TryGetProperty("testProperty", out var value))
                    return false.Label($"Property 'testProperty' not found in composed example");

                var actualValue = value.GetInt32();
                var withinBounds = actualValue >= min && actualValue <= max;

                return withinBounds
                    .Label($"Expected value within [{min}, {max}], but got {actualValue}");
            });
    }

    /// <summary>
    /// **Feature: tag-groups-extension, Property 20: Constraint-based string defaults**
    /// **Validates: Requirements 8.3**
    /// 
    /// For any string property with MinLength and/or MaxLength constraints but no explicit example,
    /// the generated default example SHALL have a length within the specified bounds when default generation is enabled.
    /// </summary>
    [Property(MaxTest = 100)]
    public Property ConstraintBasedStringDefaults_GeneratesStringWithinLengthBounds()
    {
        // Generate minLength/maxLength pairs where minLength <= maxLength
        var boundsGen = Gen.Choose(1, 20)
            .SelectMany(minLen => Gen.Choose(minLen, minLen + 30).Select(maxLen => (minLen, maxLen)));

        return Prop.ForAll(
            boundsGen.ToArbitrary(),
            bounds =>
            {
                var (minLength, maxLength) = bounds;
                var source = GenerateSourceWithStringConstraints("TestProperty", minLength, maxLength);
                var composedExample = ExtractComposedExample(source);

                if (composedExample == null)
                    return false.Label($"No composed example found for string constraints minLength={minLength}, maxLength={maxLength}");

                var example = composedExample.Value;

                if (!example.TryGetProperty("testProperty", out var value))
                    return false.Label($"Property 'testProperty' not found in composed example");

                var actualValue = value.GetString();
                if (actualValue == null)
                    return false.Label("Property value is null");

                var actualLength = actualValue.Length;
                var withinBounds = actualLength >= minLength && actualLength <= maxLength;

                return withinBounds
                    .Label($"Expected string length within [{minLength}, {maxLength}], but got length {actualLength} (value: '{actualValue}')");
            });
    }

    /// <summary>
    /// **Feature: tag-groups-extension, Property 21: Type-based placeholder defaults**
    /// **Validates: Requirements 8.4**
    /// 
    /// For any property without an explicit example and without constraints,
    /// the Source_Generator SHALL generate a type-appropriate placeholder value when default generation is enabled.
    /// </summary>
    [Property(MaxTest = 100)]
    public Property TypeBasedPlaceholderDefaults_GeneratesTypeAppropriateValues()
    {
        var typeExpectedGen = Gen.Elements(
            ("string", JsonValueKind.String),
            ("int", JsonValueKind.Number),
            ("long", JsonValueKind.Number),
            ("double", JsonValueKind.Number),
            ("decimal", JsonValueKind.Number),
            ("bool", JsonValueKind.False)  // Default bool is false
        );

        return Prop.ForAll(
            typeExpectedGen.ToArbitrary(),
            tuple =>
            {
                var (typeName, expectedKind) = tuple;
                var source = GenerateSourceWithTypeOnly("TestProperty", typeName);
                var composedExample = ExtractComposedExample(source);

                if (composedExample == null)
                    return false.Label($"No composed example found for type '{typeName}'");

                var example = composedExample.Value;

                if (!example.TryGetProperty("testProperty", out var value))
                    return false.Label($"Property 'testProperty' not found in composed example for type '{typeName}'");

                var actualKind = value.ValueKind;
                var kindMatches = actualKind == expectedKind;

                return kindMatches
                    .Label($"Expected {expectedKind} for type '{typeName}', but got {actualKind}");
            });
    }

    private static string ToCamelCase(string value)
    {
        if (string.IsNullOrEmpty(value)) return value;
        if (value.Length == 1) return value.ToLowerInvariant();
        return char.ToLowerInvariant(value[0]) + value.Substring(1);
    }

    private static string GenerateSourceWithFormat(string propertyName, string format)
    {
        return $@"
using Amazon.Lambda.Annotations;
using Amazon.Lambda.Annotations.APIGateway;
using System.Threading.Tasks;

[assembly: Oproto.Lambda.OpenApi.Attributes.OpenApiExampleConfig(GenerateDefaults = true)]

public class TestRequest
{{
    [Oproto.Lambda.OpenApi.Attributes.OpenApiSchema(Format = ""{format}"")]
    public string {propertyName} {{ get; set; }}
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

    private static string GenerateSourceWithNumericConstraints(string propertyName, string typeName, int minimum, int maximum)
    {
        return $@"
using Amazon.Lambda.Annotations;
using Amazon.Lambda.Annotations.APIGateway;
using System.Threading.Tasks;

[assembly: Oproto.Lambda.OpenApi.Attributes.OpenApiExampleConfig(GenerateDefaults = true)]

public class TestRequest
{{
    [Oproto.Lambda.OpenApi.Attributes.OpenApiSchema(Minimum = {minimum}, Maximum = {maximum})]
    public {typeName} {propertyName} {{ get; set; }}
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

    private static string GenerateSourceWithStringConstraints(string propertyName, int minLength, int maxLength)
    {
        return $@"
using Amazon.Lambda.Annotations;
using Amazon.Lambda.Annotations.APIGateway;
using System.Threading.Tasks;

[assembly: Oproto.Lambda.OpenApi.Attributes.OpenApiExampleConfig(GenerateDefaults = true)]

public class TestRequest
{{
    [Oproto.Lambda.OpenApi.Attributes.OpenApiSchema(MinLength = {minLength}, MaxLength = {maxLength})]
    public string {propertyName} {{ get; set; }}
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

    private static string GenerateSourceWithTypeOnly(string propertyName, string typeName)
    {
        var csharpType = MapType(typeName);
        return $@"
using Amazon.Lambda.Annotations;
using Amazon.Lambda.Annotations.APIGateway;
using System.Threading.Tasks;

[assembly: Oproto.Lambda.OpenApi.Attributes.OpenApiExampleConfig(GenerateDefaults = true)]

public class TestRequest
{{
    public {csharpType} {propertyName} {{ get; set; }}
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
