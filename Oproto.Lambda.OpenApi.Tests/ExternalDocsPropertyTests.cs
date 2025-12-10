#nullable enable
using System.Text.Json;
using FsCheck;
using FsCheck.Xunit;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Oproto.Lambda.OpenApi.SourceGenerator;

namespace Oproto.Lambda.OpenApi.Tests;

/// <summary>
/// Property-based tests for external documentation functionality.
/// </summary>
public class ExternalDocsPropertyTests
{
    /// <summary>
    /// **Feature: openapi-completeness, Property 14: Assembly-Level External Docs**
    /// **Validates: Requirements 6.1, 6.3**
    /// 
    /// For any assembly with [OpenApiExternalDocs] attribute, the generated specification SHALL contain
    /// an externalDocs object with URL and description.
    /// </summary>
    [Property(MaxTest = 100)]
    public Property AssemblyExternalDocs_IncludesExternalDocsInSpecification()
    {
        var urlGen = Gen.Elements(
            "https://docs.example.com",
            "https://api.example.com/docs",
            "https://wiki.example.com/api-guide",
            "https://developer.example.com/reference");
        
        var descriptionGen = Gen.OneOf(
            Gen.Constant((string?)null),
            Gen.Elements("Full API documentation", "Developer guide", "API reference", "Getting started guide")
                .Select(s => (string?)s));
        
        var externalDocsGen = Gen.Zip(urlGen, descriptionGen)
            .Select(t => (Url: t.Item1, Description: t.Item2));
        
        return Prop.ForAll(
            externalDocsGen.ToArbitrary(),
            externalDocs =>
            {
                var source = GenerateSourceWithAssemblyExternalDocs(externalDocs.Url, externalDocs.Description);
                var extractedDocs = ExtractExternalDocs(source);
                
                // External docs should be present with correct URL
                var urlMatches = extractedDocs.Url == externalDocs.Url;
                
                // Description should match when provided
                var descriptionMatches = externalDocs.Description == null || 
                                         extractedDocs.Description == externalDocs.Description;
                
                return (urlMatches && descriptionMatches)
                    .Label($"Expected URL '{externalDocs.Url}' with description '{externalDocs.Description}', " +
                           $"but got URL '{extractedDocs.Url}' with description '{extractedDocs.Description}'");
            });
    }


    /// <summary>
    /// **Feature: openapi-completeness, Property 15: Operation-Level External Docs**
    /// **Validates: Requirements 6.2**
    /// 
    /// For any method with [OpenApiExternalDocs] attribute, the generated operation SHALL contain
    /// an externalDocs object.
    /// </summary>
    [Property(MaxTest = 100)]
    public Property MethodExternalDocs_IncludesExternalDocsInOperation()
    {
        var urlGen = Gen.Elements(
            "https://docs.example.com/products",
            "https://api.example.com/docs/get-product",
            "https://wiki.example.com/product-api",
            "https://developer.example.com/products/reference");
        
        var descriptionGen = Gen.OneOf(
            Gen.Constant((string?)null),
            Gen.Elements("Product API documentation", "Get product guide", "Product reference", "Product endpoint docs")
                .Select(s => (string?)s));
        
        var externalDocsGen = Gen.Zip(urlGen, descriptionGen)
            .Select(t => (Url: t.Item1, Description: t.Item2));
        
        return Prop.ForAll(
            externalDocsGen.ToArbitrary(),
            externalDocs =>
            {
                var source = GenerateSourceWithMethodExternalDocs(externalDocs.Url, externalDocs.Description);
                var extractedDocs = ExtractOperationExternalDocs(source, "/items/{id}");
                
                // External docs should be present with correct URL
                var urlMatches = extractedDocs.Url == externalDocs.Url;
                
                // Description should match when provided
                var descriptionMatches = externalDocs.Description == null || 
                                         extractedDocs.Description == externalDocs.Description;
                
                return (urlMatches && descriptionMatches)
                    .Label($"Expected URL '{externalDocs.Url}' with description '{externalDocs.Description}', " +
                           $"but got URL '{extractedDocs.Url}' with description '{extractedDocs.Description}'");
            });
    }

    /// <summary>
    /// Tests that when no [OpenApiExternalDocs] attribute is present at assembly level,
    /// the specification does not contain an externalDocs field.
    /// </summary>
    [Property(MaxTest = 100)]
    public Property NoAssemblyExternalDocs_OmitsExternalDocsFromSpecification()
    {
        var methodNameGen = Gen.Elements("GetProduct", "CreateOrder", "UpdateUser", "DeleteItem");
        
        return Prop.ForAll(
            methodNameGen.ToArbitrary(),
            methodName =>
            {
                var source = GenerateSourceWithoutExternalDocs(methodName);
                var extractedDocs = ExtractExternalDocs(source);
                
                var noExternalDocs = extractedDocs.Url == null;
                return noExternalDocs
                    .Label($"Expected no external docs when none defined, but got URL '{extractedDocs.Url}'");
            });
    }

    private static string GenerateSourceWithAssemblyExternalDocs(string url, string? description)
    {
        var descriptionPart = description != null ? $@", Description = ""{description}""" : "";
        
        return $@"
[assembly: Oproto.Lambda.OpenApi.Attributes.OpenApiExternalDocs(""{url}""{descriptionPart})]

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

    private static string GenerateSourceWithMethodExternalDocs(string url, string? description)
    {
        var descriptionPart = description != null ? $@", Description = ""{description}""" : "";
        
        return $@"
using Amazon.Lambda.Annotations;
using Amazon.Lambda.Annotations.APIGateway;
using Oproto.Lambda.OpenApi.Attributes;
using System.Threading.Tasks;

public class TestFunctions 
{{
    [LambdaFunction]
    [HttpApi(LambdaHttpMethod.Get, ""/items/{{id}}"")]
    [OpenApiExternalDocs(""{url}""{descriptionPart})]
    public string GetItem(string id) => ""test"";
}}";
    }

    private string GenerateSourceWithoutExternalDocs(string methodName)
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

    private (string? Url, string? Description) ExtractExternalDocs(string source)
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
                return (null, null);
            
            var jsonContent = ExtractOpenApiJson(outputCompilation);
            if (string.IsNullOrEmpty(jsonContent))
                return (null, null);
            
            using var doc = JsonDocument.Parse(jsonContent);
            
            if (doc.RootElement.TryGetProperty("externalDocs", out var externalDocs))
            {
                var url = externalDocs.TryGetProperty("url", out var urlProp) 
                    ? urlProp.GetString() 
                    : null;
                var description = externalDocs.TryGetProperty("description", out var descProp) 
                    ? descProp.GetString() 
                    : null;
                return (url, description);
            }
            
            return (null, null);
        }
        catch
        {
            return (null, null);
        }
    }

    private (string? Url, string? Description) ExtractOperationExternalDocs(string source, string path)
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
                return (null, null);
            
            var jsonContent = ExtractOpenApiJson(outputCompilation);
            if (string.IsNullOrEmpty(jsonContent))
                return (null, null);
            
            using var doc = JsonDocument.Parse(jsonContent);
            
            if (doc.RootElement.TryGetProperty("paths", out var paths) &&
                paths.TryGetProperty(path, out var pathItem) &&
                pathItem.TryGetProperty("get", out var operation) &&
                operation.TryGetProperty("externalDocs", out var externalDocs))
            {
                var url = externalDocs.TryGetProperty("url", out var urlProp) 
                    ? urlProp.GetString() 
                    : null;
                var description = externalDocs.TryGetProperty("description", out var descProp) 
                    ? descProp.GetString() 
                    : null;
                return (url, description);
            }
            
            return (null, null);
        }
        catch
        {
            return (null, null);
        }
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
