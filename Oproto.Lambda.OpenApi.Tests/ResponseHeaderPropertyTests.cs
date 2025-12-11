#nullable enable
using System.Text.Json;
using FsCheck;
using FsCheck.Xunit;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Oproto.Lambda.OpenApi.SourceGenerator;

namespace Oproto.Lambda.OpenApi.Tests;

/// <summary>
/// Property-based tests for response header functionality.
/// </summary>
public class ResponseHeaderPropertyTests
{
    /// <summary>
    /// **Feature: openapi-completeness, Property 9: Response Header Inclusion**
    /// **Validates: Requirements 3.1, 3.2, 3.3, 3.4**
    /// 
    /// For any method with [OpenApiResponseHeader] attributes, the generated response SHALL contain
    /// those headers with correct names, types, and required flags.
    /// </summary>
    [Property(MaxTest = 100)]
    public Property ResponseHeaderAttribute_IncludesHeadersInResponse()
    {
        var headerNameGen = Gen.Elements(
            "X-Request-Id",
            "X-Rate-Limit-Remaining",
            "X-Correlation-Id",
            "X-Total-Count",
            "X-Page-Number",
            "ETag",
            "Last-Modified");

        var statusCodeGen = Gen.Elements(200, 201, 400, 404, 500);

        var descriptionGen = Gen.OneOf(
            Gen.Constant((string?)null),
            Gen.Elements("Unique request identifier", "Rate limit remaining", "Total count of items")
                .Select(s => (string?)s));

        var typeGen = Gen.Elements("string", "int", "bool");

        var requiredGen = Arb.Generate<bool>();

        // Combine generators step by step since Zip only takes 2-3 arguments
        var headerGen = Gen.Zip(headerNameGen, statusCodeGen)
            .SelectMany(t1 => Gen.Zip(descriptionGen, typeGen)
                .SelectMany(t2 => requiredGen
                    .Select(req => new HeaderConfig(t1.Item1, t1.Item2, t2.Item1, t2.Item2, req))));

        var headersGen = Gen.ListOf(headerGen)
            .Select(headers => headers.DistinctBy(h => (h.Name, h.StatusCode)).Take(3).ToList());

        return Prop.ForAll(
            headersGen.ToArbitrary(),
            headers =>
            {
                var source = GenerateSourceWithHeaders(headers);
                var extractedHeaders = ExtractResponseHeaders(source);

                if (headers.Count == 0)
                {
                    // If no headers specified, no custom headers should be present
                    var noHeaders = extractedHeaders.Count == 0;
                    return noHeaders
                        .Label($"Expected no headers, but got {extractedHeaders.Count}");
                }
                else
                {
                    // All specified headers should be present with correct properties
                    var allHeadersPresent = headers.All(h =>
                    {
                        var extracted = extractedHeaders.FirstOrDefault(eh =>
                            eh.Name == h.Name && eh.StatusCode == h.StatusCode);

                        if (extracted is null) return false;

                        // Check required flag matches
                        if (h.Required != extracted.Required) return false;

                        // Check type matches (map to OpenAPI type)
                        var expectedType = h.Type switch
                        {
                            "int" => "integer",
                            "bool" => "boolean",
                            _ => "string"
                        };
                        if (extracted.SchemaType != expectedType) return false;

                        // Check description if provided
                        if (h.Description != null && extracted.Description != h.Description) return false;

                        return true;
                    });

                    return allHeadersPresent
                        .Label($"Expected headers [{string.Join(", ", headers.Select(h => $"{h.Name}@{h.StatusCode}"))}], " +
                               $"but got [{string.Join(", ", extractedHeaders.Select(h => $"{h.Name}@{h.StatusCode}"))}]");
                }
            });
    }


    /// <summary>
    /// **Feature: openapi-completeness, Property 9 (continued): Multiple Headers Per Status Code**
    /// **Validates: Requirements 3.2**
    /// 
    /// For any method with multiple [OpenApiResponseHeader] attributes for the same status code,
    /// all headers SHALL be included in that response.
    /// </summary>
    [Property(MaxTest = 100)]
    public Property MultipleHeadersPerStatusCode_AllIncluded()
    {
        var headerNamesGen = Gen.Shuffle(new[] { "X-Request-Id", "X-Rate-Limit", "X-Total-Count" })
            .Select(names => names.Take(2).ToList());

        return Prop.ForAll(
            headerNamesGen.ToArbitrary(),
            headerNames =>
            {
                // Create multiple headers for status code 200
                var headers = headerNames.Select(name => new HeaderConfig(name, 200, null, "string", false)).ToList();

                var source = GenerateSourceWithHeaders(headers);
                var extractedHeaders = ExtractResponseHeaders(source);

                // Filter to only 200 status code headers
                var headers200 = extractedHeaders.Where(h => h.StatusCode == 200).ToList();

                var allPresent = headerNames.All(name =>
                    headers200.Any(h => h.Name == name));

                return allPresent
                    .Label($"Expected all headers [{string.Join(", ", headerNames)}] for status 200, " +
                           $"but got [{string.Join(", ", headers200.Select(h => h.Name))}]");
            });
    }

    private record HeaderConfig(string Name, int StatusCode, string? Description, string Type, bool Required);

    private record ExtractedHeader(string Name, int StatusCode, string? Description, string SchemaType, bool Required);

    private static string GenerateSourceWithHeaders(List<HeaderConfig> headers)
    {
        var headerAttributes = headers.Count > 0
            ? string.Join("\n    ", headers.Select(h =>
            {
                var parts = new List<string>();
                if (h.StatusCode != 200) parts.Add($"StatusCode = {h.StatusCode}");
                if (h.Description != null) parts.Add($@"Description = ""{h.Description}""");
                if (h.Type != "string") parts.Add($"Type = typeof({h.Type})");
                if (h.Required) parts.Add("Required = true");

                var namedArgs = parts.Count > 0 ? ", " + string.Join(", ", parts) : "";
                return $@"[Oproto.Lambda.OpenApi.Attributes.OpenApiResponseHeader(""{h.Name}""{namedArgs})]";
            }))
            : "";

        return $@"
using Amazon.Lambda.Annotations;
using Amazon.Lambda.Annotations.APIGateway;
using System.Threading.Tasks;

public class TestFunctions 
{{
    [LambdaFunction]
    [HttpApi(LambdaHttpMethod.Get, ""/items/{{id}}"")]
    {headerAttributes}
    public string GetItem(string id) => ""test"";
}}";
    }

    private List<ExtractedHeader> ExtractResponseHeaders(string source)
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
                return new List<ExtractedHeader>();

            var jsonContent = ExtractOpenApiJson(outputCompilation);
            if (string.IsNullOrEmpty(jsonContent))
                return new List<ExtractedHeader>();

            using var doc = JsonDocument.Parse(jsonContent);

            var headers = new List<ExtractedHeader>();

            if (doc.RootElement.TryGetProperty("paths", out var paths))
            {
                foreach (var path in paths.EnumerateObject())
                {
                    foreach (var operation in path.Value.EnumerateObject())
                    {
                        // Skip non-operation properties
                        if (operation.Name.StartsWith("x-") || operation.Name == "parameters")
                            continue;

                        if (operation.Value.TryGetProperty("responses", out var responses))
                        {
                            foreach (var response in responses.EnumerateObject())
                            {
                                if (!int.TryParse(response.Name, out var statusCode))
                                    continue;

                                if (response.Value.TryGetProperty("headers", out var headersObj))
                                {
                                    foreach (var header in headersObj.EnumerateObject())
                                    {
                                        string? description = null;
                                        string schemaType = "string";
                                        bool required = false;

                                        if (header.Value.TryGetProperty("description", out var descProp))
                                        {
                                            description = descProp.GetString();
                                        }

                                        if (header.Value.TryGetProperty("required", out var reqProp))
                                        {
                                            required = reqProp.GetBoolean();
                                        }

                                        if (header.Value.TryGetProperty("schema", out var schemaProp) &&
                                            schemaProp.TryGetProperty("type", out var typeProp))
                                        {
                                            schemaType = typeProp.GetString() ?? "string";
                                        }

                                        headers.Add(new ExtractedHeader(header.Name, statusCode, description, schemaType, required));
                                    }
                                }
                            }
                        }
                    }
                }
            }

            return headers;
        }
        catch
        {
            // Return empty on error
        }

        return new List<ExtractedHeader>();
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
