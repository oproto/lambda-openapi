#nullable enable
using System.Text.Json;
using FsCheck;
using FsCheck.Xunit;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Oproto.Lambda.OpenApi.SourceGenerator;

namespace Oproto.Lambda.OpenApi.Tests;

/// <summary>
/// Property-based tests for example generation configuration functionality.
/// </summary>
public class ExampleConfigurationPropertyTests
{
    /// <summary>
    /// **Feature: tag-groups-extension, Property 22: Config disables auto-generation**
    /// **Validates: Requirements 9.3**
    /// 
    /// For any assembly where automatic example generation is disabled via configuration,
    /// the Source_Generator SHALL NOT compose examples from property-level values
    /// and SHALL only use explicit [OpenApiExample] attributes.
    /// </summary>
    [Property(MaxTest = 100)]
    public Property ConfigDisablesAutoGeneration_NoComposedExamplesWhenDisabled()
    {
        // Generate property configurations with examples
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
                // Generate source with ComposeFromProperties = false
                var source = GenerateSourceWithConfigDisabled(properties);
                var composedExample = ExtractComposedExample(source);

                // When ComposeFromProperties is false, there should be no composed example
                var noComposedExample = composedExample == null;

                return noComposedExample
                    .Label($"Expected no composed example when ComposeFromProperties=false, but found one. Properties: [{string.Join(", ", properties.Select(p => p.Name))}]");
            });
    }

    /// <summary>
    /// **Feature: tag-groups-extension, Property 23: Independent config options**
    /// **Validates: Requirements 9.5**
    /// 
    /// For any configuration, the ComposeFromProperties and GenerateDefaults options
    /// SHALL operate independently, allowing example composition without default generation.
    /// </summary>
    [Property(MaxTest = 100)]
    public Property IndependentConfigOptions_ComposeWithoutDefaults()
    {
        // Generate properties - some with examples, some without
        var withExampleGen = Gen.Elements(
            ("Name", "TestName"),
            ("Title", "TestTitle"),
            ("Description", "TestDescription")
        );

        var withoutExampleGen = Gen.Elements("Id", "Status", "Count");

        return Prop.ForAll(
            Gen.Zip(
                Gen.ListOf(withExampleGen).Select(l => l.Take(2).ToList()),
                Gen.ListOf(withoutExampleGen).Select(l => l.Take(2).ToList())
            ).Where(t => t.Item1.Count > 0 && t.Item2.Count > 0).ToArbitrary(),
            tuple =>
            {
                var (withExamples, withoutExamples) = tuple;

                // Generate source with ComposeFromProperties=true but GenerateDefaults=false
                var source = GenerateSourceWithComposeOnlyNoDefaults(withExamples, withoutExamples);
                var composedExample = ExtractComposedExample(source);

                if (composedExample == null)
                    return false.Label("Expected composed example when ComposeFromProperties=true");

                var example = composedExample.Value;

                // Properties WITH explicit examples should be present
                var explicitExamplesPresent = withExamples.All(prop =>
                {
                    var jsonName = ToCamelCase(prop.Item1);
                    return example.TryGetProperty(jsonName, out _);
                });

                // Properties WITHOUT explicit examples should NOT be present (since GenerateDefaults=false)
                var noDefaultsGenerated = withoutExamples.All(prop =>
                {
                    var jsonName = ToCamelCase(prop);
                    return !example.TryGetProperty(jsonName, out _);
                });

                return (explicitExamplesPresent && noDefaultsGenerated)
                    .Label($"Expected only explicit examples (no defaults). With examples: [{string.Join(", ", withExamples.Select(p => p.Item1))}], " +
                           $"Without examples: [{string.Join(", ", withoutExamples)}]");
            });
    }

    /// <summary>
    /// Additional test: Verify that GenerateDefaults=true generates defaults for properties without examples.
    /// </summary>
    [Property(MaxTest = 100)]
    public Property IndependentConfigOptions_DefaultsGeneratedWhenEnabled()
    {
        // Generate properties without explicit examples
        var propertyNameGen = Gen.Elements("Name", "Title", "Description", "Value");

        return Prop.ForAll(
            Gen.ListOf(propertyNameGen)
                .Select(l => l.Distinct().Take(3).ToList())
                .Where(l => l.Count > 0)
                .ToArbitrary(),
            propertyNames =>
            {
                // Generate source with both ComposeFromProperties=true and GenerateDefaults=true
                var source = GenerateSourceWithDefaultsEnabled(propertyNames);
                var composedExample = ExtractComposedExample(source);

                if (composedExample == null)
                    return false.Label("Expected composed example when GenerateDefaults=true");

                var example = composedExample.Value;

                // All properties should have default values generated
                var allPropertiesPresent = propertyNames.All(prop =>
                {
                    var jsonName = ToCamelCase(prop);
                    return example.TryGetProperty(jsonName, out _);
                });

                return allPropertiesPresent
                    .Label($"Expected all properties to have defaults generated. Properties: [{string.Join(", ", propertyNames)}]");
            });
    }

    private record PropertyConfig(string Name, string Type, string? Example);

    private static string ToCamelCase(string value)
    {
        if (string.IsNullOrEmpty(value)) return value;
        if (value.Length == 1) return value.ToLowerInvariant();
        return char.ToLowerInvariant(value[0]) + value.Substring(1);
    }

    private static string GenerateSourceWithConfigDisabled(List<PropertyConfig> properties)
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

[assembly: Oproto.Lambda.OpenApi.Attributes.OpenApiExampleConfig(ComposeFromProperties = false)]

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

    private static string GenerateSourceWithComposeOnlyNoDefaults(
        List<(string Name, string Example)> withExamples,
        List<string> withoutExamples)
    {
        var withExampleDeclarations = string.Join("\n    ", withExamples.Select(p =>
            $@"[Oproto.Lambda.OpenApi.Attributes.OpenApiSchema(Example = ""{p.Example}"")]
    public string {p.Name} {{ get; set; }}"));

        var withoutExampleDeclarations = string.Join("\n    ", withoutExamples.Select(p =>
            $@"public string {p} {{ get; set; }}"));

        return $@"
using Amazon.Lambda.Annotations;
using Amazon.Lambda.Annotations.APIGateway;
using System.Threading.Tasks;

[assembly: Oproto.Lambda.OpenApi.Attributes.OpenApiExampleConfig(ComposeFromProperties = true, GenerateDefaults = false)]

public class TestRequest
{{
    {withExampleDeclarations}
    {withoutExampleDeclarations}
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

    private static string GenerateSourceWithDefaultsEnabled(List<string> propertyNames)
    {
        var propertyDeclarations = string.Join("\n    ", propertyNames.Select(p =>
            $@"public string {p} {{ get; set; }}"));

        return $@"
using Amazon.Lambda.Annotations;
using Amazon.Lambda.Annotations.APIGateway;
using System.Threading.Tasks;

[assembly: Oproto.Lambda.OpenApi.Attributes.OpenApiExampleConfig(ComposeFromProperties = true, GenerateDefaults = true)]

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
