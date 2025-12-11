#nullable enable
using System.Text.Json;
using FsCheck;
using FsCheck.Xunit;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Oproto.Lambda.OpenApi.SourceGenerator;

namespace Oproto.Lambda.OpenApi.Tests;

/// <summary>
/// Property-based tests for server definition functionality.
/// </summary>
public class ServerDefinitionPropertyTests
{
    /// <summary>
    /// **Feature: openapi-completeness, Property 10: Server Definitions from Assembly**
    /// **Validates: Requirements 4.1, 4.2**
    /// 
    /// For any assembly with [OpenApiServer] attributes, the generated specification SHALL contain
    /// a servers array with those URLs and descriptions.
    /// </summary>
    [Property(MaxTest = 100)]
    public Property ServerAttribute_IncludesServersInSpecification()
    {
        var serverUrlGen = Gen.Elements(
            "https://api.example.com",
            "https://staging.example.com",
            "https://dev.example.com",
            "https://localhost:5000",
            "https://api.prod.example.com/v1");

        var descriptionGen = Gen.OneOf(
            Gen.Constant((string?)null),
            Gen.Elements("Production server", "Staging server", "Development server", "Local development")
                .Select(s => (string?)s));

        var serverGen = Gen.Zip(serverUrlGen, descriptionGen)
            .Select(t => (Url: t.Item1, Description: t.Item2));
        var serversGen = Gen.ListOf(serverGen)
            .Select(servers => servers.DistinctBy(s => s.Url).Take(3).ToList());

        return Prop.ForAll(
            serversGen.ToArbitrary(),
            servers =>
            {
                var source = GenerateSourceWithServers(servers);
                var extractedServers = ExtractServers(source);

                if (servers.Count == 0)
                {
                    // If no servers specified, servers array should be empty or null
                    var noServers = extractedServers.Count == 0;
                    return noServers
                        .Label($"Expected no servers, but got {extractedServers.Count}");
                }
                else
                {
                    // All specified servers should be present with correct URLs
                    var allUrlsPresent = servers.All(s =>
                        extractedServers.Any(es => es.Url == s.Url));

                    // Descriptions should match when provided
                    var descriptionsMatch = servers.All(s =>
                    {
                        var extracted = extractedServers.FirstOrDefault(es => es.Url == s.Url);
                        if (extracted.Url == null) return false;
                        if (s.Description == null) return true; // No description expected
                        return extracted.Description == s.Description;
                    });

                    return (allUrlsPresent && descriptionsMatch)
                        .Label($"Expected servers [{string.Join(", ", servers.Select(s => s.Url))}], " +
                               $"but got [{string.Join(", ", extractedServers.Select(s => s.Url))}]");
                }
            });
    }

    /// <summary>
    /// **Feature: openapi-completeness, Property 11: No Servers When Not Defined**
    /// **Validates: Requirements 4.4**
    /// 
    /// For any assembly without [OpenApiServer] attributes, the generated specification SHALL NOT contain a servers array.
    /// </summary>
    [Property(MaxTest = 100)]
    public Property NoServerAttribute_OmitsServersFromSpecification()
    {
        var methodNameGen = Gen.Elements("GetProduct", "CreateOrder", "UpdateUser", "DeleteItem");

        return Prop.ForAll(
            methodNameGen.ToArbitrary(),
            methodName =>
            {
                var source = GenerateSourceWithoutServers(methodName);
                var extractedServers = ExtractServers(source);

                var noServers = extractedServers.Count == 0;
                return noServers
                    .Label($"Expected no servers when none defined, but got {extractedServers.Count}");
            });
    }

    private static string GenerateSourceWithServers(List<(string Url, string? Description)> servers)
    {
        var serverAttributes = servers.Count > 0
            ? string.Join("\n", servers.Select(s =>
                s.Description != null
                    ? $@"[assembly: Oproto.Lambda.OpenApi.Attributes.OpenApiServer(""{s.Url}"", Description = ""{s.Description}"")]"
                    : $@"[assembly: Oproto.Lambda.OpenApi.Attributes.OpenApiServer(""{s.Url}"")]"))
            : "";

        return $@"
{serverAttributes}

using Amazon.Lambda.Annotations;
using Amazon.Lambda.Annotations.APIGateway;
using System.Threading.Tasks;

public class TestFunctions 
{{
    [LambdaFunction]
    [HttpApi(LambdaHttpMethod.Get, ""/items/{{id}}"")]
    public string GetItem(string id) => ""test"";
}}";
    }

    private string GenerateSourceWithoutServers(string methodName)
    {
        return $@"
using Amazon.Lambda.Annotations;
using Amazon.Lambda.Annotations.APIGateway;
using System.Threading.Tasks;

public class TestFunctions 
{{
    [LambdaFunction]
    [HttpApi(LambdaHttpMethod.Get, ""/items/{{id}}"")]
    public string {methodName}(string id) => ""test"";
}}";
    }

    private List<(string Url, string? Description)> ExtractServers(string source)
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
                return new List<(string, string?)>();

            var jsonContent = ExtractOpenApiJson(outputCompilation);
            if (string.IsNullOrEmpty(jsonContent))
                return new List<(string, string?)>();

            using var doc = JsonDocument.Parse(jsonContent);

            var servers = new List<(string Url, string? Description)>();

            if (doc.RootElement.TryGetProperty("servers", out var serversArray))
            {
                foreach (var server in serversArray.EnumerateArray())
                {
                    var url = server.GetProperty("url").GetString() ?? "";
                    string? description = null;
                    if (server.TryGetProperty("description", out var descProp))
                    {
                        description = descProp.GetString();
                    }
                    servers.Add((url, description));
                }
            }

            return servers;
        }
        catch
        {
            // Return empty on error
        }

        return new List<(string, string?)>();
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
