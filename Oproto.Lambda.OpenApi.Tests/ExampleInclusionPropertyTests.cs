#nullable enable
using System.Text.Json;
using FsCheck;
using FsCheck.Xunit;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Oproto.Lambda.OpenApi.SourceGenerator;

namespace Oproto.Lambda.OpenApi.Tests;

/// <summary>
/// Property-based tests for operation example functionality.
/// </summary>
public class ExampleInclusionPropertyTests
{
    /// <summary>
    /// **Feature: openapi-completeness, Property 2: Attribute Example Inclusion**
    /// **Validates: Requirements 1.2**
    /// 
    /// For any method with [OpenApiExample] attributes, the generated OpenAPI operation SHALL contain
    /// the parsed JSON examples in the appropriate location (request body or response).
    /// Note: OpenAPI only supports one example per media type, so we verify that at least one example
    /// is present for each unique location (request body, or response status code).
    /// </summary>
    [Property(MaxTest = 100)]
    public Property OpenApiExampleAttribute_IncludesExamplesInOperation()
    {
        var exampleNameGen = Gen.Elements(
            "Basic Example",
            "Success Response",
            "Error Response",
            "Sample Request",
            "Test Data");

        // Generate simple JSON objects that are valid
        var jsonValueGen = Gen.Elements(
            "{\"id\": 1, \"name\": \"Test\"}",
            "{\"message\": \"Success\"}",
            "{\"error\": \"Not Found\"}",
            "{\"items\": [1, 2, 3]}",
            "{\"value\": 42}");

        var statusCodeGen = Gen.Elements(200, 201, 400, 404, 500);

        var isRequestGen = Arb.Generate<bool>();

        // Combine generators
        var exampleGen = Gen.Zip(exampleNameGen, jsonValueGen)
            .SelectMany(t1 => Gen.Zip(statusCodeGen, isRequestGen)
                .Select(t2 => new ExampleConfig(t1.Item1, t1.Item2, t2.Item1, t2.Item2)));

        // Generate a list of examples, ensuring uniqueness by (IsRequest, StatusCode) combination
        // This reflects that OpenAPI only supports one example per location
        var examplesGen = Gen.ListOf(exampleGen)
            .Select(examples => examples.DistinctBy(e => (e.IsRequest, e.StatusCode)).Take(3).ToList());

        return Prop.ForAll(
            examplesGen.ToArbitrary(),
            examples =>
            {
                var source = GenerateSourceWithExamples(examples);
                var extractedExamples = ExtractExamples(source);

                if (examples.Count == 0)
                {
                    // If no examples specified, no custom examples should be present
                    var noExamples = extractedExamples.Count == 0;
                    return noExamples
                        .Label($"Expected no examples, but got {extractedExamples.Count}");
                }
                else
                {
                    // Get unique locations from input examples
                    var hasRequestExample = examples.Any(e => e.IsRequest);
                    var responseStatusCodes = examples.Where(e => !e.IsRequest).Select(e => e.StatusCode).Distinct().ToList();

                    // Verify request example is present if any request examples were specified
                    var requestExamplePresent = !hasRequestExample || extractedExamples.Any(ex => ex.IsRequest);

                    // Verify response examples are present for each unique status code
                    var responseExamplesPresent = responseStatusCodes.All(statusCode =>
                        extractedExamples.Any(ex => !ex.IsRequest && ex.StatusCode == statusCode));

                    var allExamplesPresent = requestExamplePresent && responseExamplesPresent;

                    return allExamplesPresent
                        .Label($"Expected examples for [{(hasRequestExample ? "request, " : "")}{string.Join(", ", responseStatusCodes.Select(s => $"response@{s}"))}], " +
                               $"but got [{string.Join(", ", extractedExamples.Select(e => $"{(e.IsRequest ? "request" : $"response@{e.StatusCode}")}"))}]");
                }
            });
    }

    /// <summary>
    /// **Feature: openapi-completeness, Property 4: Request Example Placement**
    /// **Validates: Requirements 1.4**
    /// 
    /// For any request example, the example SHALL appear at requestBody.content.application/json.example.
    /// </summary>
    [Property(MaxTest = 100)]
    public Property RequestExample_PlacedInRequestBody()
    {
        var jsonValueGen = Gen.Elements(
            "{\"name\": \"Test Product\", \"price\": 9.99}",
            "{\"title\": \"Sample\", \"description\": \"A sample item\"}",
            "{\"data\": [1, 2, 3]}");

        return Prop.ForAll(
            jsonValueGen.ToArbitrary(),
            jsonValue =>
            {
                var examples = new List<ExampleConfig>
                {
                    new ExampleConfig("Request Example", jsonValue, 200, true)
                };

                var source = GenerateSourceWithExamples(examples);
                var extractedExamples = ExtractExamples(source);

                var requestExample = extractedExamples.FirstOrDefault(e => e.IsRequest);
                var hasRequestExample = requestExample != null;

                return hasRequestExample
                    .Label($"Expected request example in requestBody, but none found. Extracted: [{string.Join(", ", extractedExamples.Select(e => $"{(e.IsRequest ? "request" : $"response@{e.StatusCode}")}"))}]");
            });
    }

    /// <summary>
    /// **Feature: openapi-completeness, Property 5: Response Example Placement**
    /// **Validates: Requirements 1.5**
    /// 
    /// For any response example with a given status code, the example SHALL appear at 
    /// responses.{statusCode}.content.application/json.example.
    /// </summary>
    [Property(MaxTest = 100)]
    public Property ResponseExample_PlacedInCorrectStatusCode()
    {
        var statusCodeGen = Gen.Elements(200, 201, 400, 404, 500);

        var jsonValueGen = Gen.Elements(
            "{\"id\": 1, \"name\": \"Result\"}",
            "{\"message\": \"Created successfully\"}",
            "{\"error\": \"Bad request\"}");

        return Prop.ForAll(
            Gen.Zip(statusCodeGen, jsonValueGen).ToArbitrary(),
            tuple =>
            {
                var (statusCode, jsonValue) = tuple;
                var examples = new List<ExampleConfig>
                {
                    new ExampleConfig("Response Example", jsonValue, statusCode, false)
                };

                var source = GenerateSourceWithExamples(examples);
                var extractedExamples = ExtractExamples(source);

                var responseExample = extractedExamples.FirstOrDefault(e =>
                    !e.IsRequest && e.StatusCode == statusCode);
                var hasCorrectExample = responseExample != null;

                return hasCorrectExample
                    .Label($"Expected response example at status {statusCode}, but none found. Extracted: [{string.Join(", ", extractedExamples.Select(e => $"{(e.IsRequest ? "request" : $"response@{e.StatusCode}")}"))}]");
            });
    }

    private record ExampleConfig(string Name, string JsonValue, int StatusCode, bool IsRequest);

    private record ExtractedExample(bool IsRequest, int StatusCode, string JsonValue);

    private static string GenerateSourceWithExamples(List<ExampleConfig> examples)
    {
        var exampleAttributes = examples.Count > 0
            ? string.Join("\n    ", examples.Select(e =>
            {
                var escapedJson = e.JsonValue.Replace("\"", "\\\"");
                var parts = new List<string>();
                if (e.StatusCode != 200) parts.Add($"StatusCode = {e.StatusCode}");
                if (e.IsRequest) parts.Add("IsRequestExample = true");

                var namedArgs = parts.Count > 0 ? ", " + string.Join(", ", parts) : "";
                return $@"[Oproto.Lambda.OpenApi.Attributes.OpenApiExample(""{e.Name}"", ""{escapedJson}""{namedArgs})]";
            }))
            : "";

        return $@"
using Amazon.Lambda.Annotations;
using Amazon.Lambda.Annotations.APIGateway;
using System.Threading.Tasks;

public class TestRequest
{{
    public string Name {{ get; set; }}
    public decimal Price {{ get; set; }}
}}

public class TestResponse
{{
    public int Id {{ get; set; }}
    public string Name {{ get; set; }}
}}

public class TestFunctions 
{{
    [LambdaFunction]
    [HttpApi(LambdaHttpMethod.Post, ""/items"")]
    {exampleAttributes}
    public TestResponse CreateItem([FromBody] TestRequest request) => new TestResponse {{ Id = 1, Name = request.Name }};
}}";
    }

    private List<ExtractedExample> ExtractExamples(string source)
    {
        try
        {
            var compilation = CompilerHelper.CreateCompilation(source);
            var generator = new OpenApiSpecGenerator();

            var driver = CSharpGeneratorDriver.Create(generator);
            driver.RunGeneratorsAndUpdateCompilation(compilation,
                out var outputCompilation,
                out var diagnostics);

            // Check for errors
            if (diagnostics.Any(d => d.Severity == DiagnosticSeverity.Error))
                return new List<ExtractedExample>();

            var jsonContent = ExtractOpenApiJson(outputCompilation);
            if (string.IsNullOrEmpty(jsonContent))
                return new List<ExtractedExample>();

            using var doc = JsonDocument.Parse(jsonContent);

            var examples = new List<ExtractedExample>();

            if (doc.RootElement.TryGetProperty("paths", out var paths))
            {
                foreach (var path in paths.EnumerateObject())
                {
                    foreach (var operation in path.Value.EnumerateObject())
                    {
                        // Skip non-operation properties
                        if (operation.Name.StartsWith("x-") || operation.Name == "parameters")
                            continue;

                        // Check for request body example
                        if (operation.Value.TryGetProperty("requestBody", out var requestBody) &&
                            requestBody.TryGetProperty("content", out var requestContent) &&
                            requestContent.TryGetProperty("application/json", out var requestMediaType) &&
                            requestMediaType.TryGetProperty("example", out var requestExample))
                        {
                            examples.Add(new ExtractedExample(true, 0, requestExample.ToString()));
                        }

                        // Check for response examples
                        if (operation.Value.TryGetProperty("responses", out var responses))
                        {
                            foreach (var response in responses.EnumerateObject())
                            {
                                if (!int.TryParse(response.Name, out var statusCode))
                                    continue;

                                if (response.Value.TryGetProperty("content", out var responseContent) &&
                                    responseContent.TryGetProperty("application/json", out var responseMediaType) &&
                                    responseMediaType.TryGetProperty("example", out var responseExample))
                                {
                                    examples.Add(new ExtractedExample(false, statusCode, responseExample.ToString()));
                                }
                            }
                        }
                    }
                }
            }

            return examples;
        }
        catch
        {
            // Return empty on error
        }

        return new List<ExtractedExample>();
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
/// Property-based tests for example precedence functionality.
/// </summary>
public class ExamplePrecedencePropertyTests
{
    /// <summary>
    /// **Feature: openapi-completeness, Property 3: Example Attribute Precedence**
    /// **Validates: Requirements 1.3**
    /// 
    /// For any method with both XML example and [OpenApiExample] attribute,
    /// the attribute value SHALL appear in the output (not the XML value).
    /// </summary>
    [Property(MaxTest = 100)]
    public Property AttributeExample_TakesPrecedenceOverXmlExample()
    {
        var attributeJsonGen = Gen.Elements(
            "{\"source\": \"attribute\", \"id\": 1}",
            "{\"from\": \"attribute\", \"value\": 42}",
            "{\"type\": \"attribute\", \"data\": true}");

        return Prop.ForAll(
            attributeJsonGen.ToArbitrary(),
            attributeJson =>
            {
                // XML example has different content than attribute
                var xmlJson = "{\"source\": \"xml\", \"id\": 999}";
                var source = GenerateSourceWithBothExamples(attributeJson, xmlJson);
                var extractedExample = ExtractResponseExample(source);

                if (extractedExample == null)
                    return false.Label("No example found in response");

                // The attribute example should be present, not the XML example
                var hasAttributeContent = extractedExample.Contains("attribute");
                var noXmlContent = !extractedExample.Contains("xml") && !extractedExample.Contains("999");

                return (hasAttributeContent && noXmlContent)
                    .Label($"Expected attribute example content, but got: {extractedExample}");
            });
    }

    private string GenerateSourceWithBothExamples(string attributeJson, string xmlJson)
    {
        var escapedAttributeJson = attributeJson.Replace("\"", "\\\"");

        return $@"
using Amazon.Lambda.Annotations;
using Amazon.Lambda.Annotations.APIGateway;
using System.Threading.Tasks;

public class TestResponse
{{
    public string Source {{ get; set; }}
    public int Id {{ get; set; }}
}}

public class TestFunctions 
{{
    /// <summary>
    /// Test method with XML example
    /// </summary>
    /// <example>
    /// {xmlJson}
    /// </example>
    [LambdaFunction]
    [HttpApi(LambdaHttpMethod.Get, ""/items/{{id}}"")]
    [Oproto.Lambda.OpenApi.Attributes.OpenApiExample(""Attribute Example"", ""{escapedAttributeJson}"", StatusCode = 200)]
    public TestResponse GetItem(string id) => new TestResponse {{ Source = ""test"", Id = 1 }};
}}";
    }

    private string? ExtractResponseExample(string source)
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

                        if (operation.Value.TryGetProperty("responses", out var responses) &&
                            responses.TryGetProperty("200", out var response200) &&
                            response200.TryGetProperty("content", out var content) &&
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
