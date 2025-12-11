using System.Text.Json;
using FsCheck;
using FsCheck.Xunit;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Oproto.Lambda.OpenApi.SourceGenerator;

namespace Oproto.Lambda.OpenApi.Tests;

/// <summary>
/// Property-based tests for OpenApiOperation attribute functionality.
/// </summary>
public class OpenApiOperationAttributePropertyTests
{
    /// <summary>
    /// **Feature: openapi-completeness, Property: OpenApiOperation Summary and Description**
    /// **Validates: OpenApiOperation attribute Summary and Description are applied**
    /// 
    /// For any method with [OpenApiOperation] attribute, the generated operation SHALL have
    /// the specified Summary and Description values.
    /// </summary>
    [Property(MaxTest = 100)]
    public Property OpenApiOperationAttribute_AppliesSummaryAndDescription()
    {
        var summaryGen = Gen.Elements("Get a product", "Create new order", "Update user profile");
        var descriptionGen = Gen.Elements("Retrieves a product by ID", "Creates a new order", "Updates user data");

        return Prop.ForAll(
            summaryGen.ToArbitrary(),
            descriptionGen.ToArbitrary(),
            (summary, description) =>
            {
                var source = GenerateSourceWithOpenApiOperation(summary, description, false, null);
                var result = ExtractOperationInfo(source);

                if (result == null)
                    return false.Label("Failed to extract operation info");

                var summaryCorrect = result.Summary == summary;
                var descriptionCorrect = result.Description == description;

                return (summaryCorrect && descriptionCorrect)
                    .Label($"Summary: expected '{summary}' got '{result.Summary}', " +
                           $"Description: expected '{description}' got '{result.Description}'");
            });
    }

    /// <summary>
    /// **Feature: openapi-completeness, Property: OpenApiOperation Deprecated and OperationId**
    /// **Validates: OpenApiOperation attribute Deprecated and OperationId are applied**
    /// 
    /// For any method with [OpenApiOperation] attribute, the generated operation SHALL have
    /// the specified Deprecated and OperationId values.
    /// </summary>
    [Property(MaxTest = 100)]
    public Property OpenApiOperationAttribute_AppliesDeprecatedAndOperationId()
    {
        var deprecatedGen = Arb.Generate<bool>();
        var operationIdGen = Gen.Elements("getProductById", "createNewOrder", "updateUserProfile");

        return Prop.ForAll(
            deprecatedGen.ToArbitrary(),
            operationIdGen.ToArbitrary(),
            (deprecated, operationId) =>
            {
                var source = GenerateSourceWithOpenApiOperation(null, null, deprecated, operationId);
                var result = ExtractOperationInfo(source);

                if (result == null)
                    return false.Label("Failed to extract operation info");

                var deprecatedCorrect = result.IsDeprecated == deprecated;
                var operationIdCorrect = result.OperationId == operationId;

                return (deprecatedCorrect && operationIdCorrect)
                    .Label($"Deprecated: expected {deprecated} got {result.IsDeprecated}, " +
                           $"OperationId: expected '{operationId}' got '{result.OperationId}'");
            });
    }

    /// <summary>
    /// **Feature: openapi-completeness, Property: OpenApiOperation Overrides XML Docs**
    /// **Validates: Attribute values take precedence over XML documentation**
    /// 
    /// For any method with both XML docs and [OpenApiOperation] attribute,
    /// the attribute values SHALL override the XML documentation values.
    /// </summary>
    [Property(MaxTest = 100)]
    public Property OpenApiOperationAttribute_OverridesXmlDocs()
    {
        var attrSummaryGen = Gen.Elements("Attribute Summary", "Custom Summary", "Override Summary");
        var attrDescriptionGen = Gen.Elements("Attribute Description", "Custom Description", "Override Description");

        return Prop.ForAll(
            attrSummaryGen.ToArbitrary(),
            attrDescriptionGen.ToArbitrary(),
            (attrSummary, attrDescription) =>
            {
                var source = GenerateSourceWithBothXmlAndAttribute(attrSummary, attrDescription);
                var result = ExtractOperationInfo(source);

                if (result == null)
                    return false.Label("Failed to extract operation info");

                // Attribute values should override XML docs
                var summaryCorrect = result.Summary == attrSummary;
                var descriptionCorrect = result.Description == attrDescription;

                return (summaryCorrect && descriptionCorrect)
                    .Label($"Expected attribute values to override XML docs. " +
                           $"Summary: expected '{attrSummary}' got '{result.Summary}', " +
                           $"Description: expected '{attrDescription}' got '{result.Description}'");
            });
    }

    private string GenerateSourceWithOpenApiOperation(string summary, string description, bool deprecated, string operationId)
    {
        var attrParts = new List<string>();
        if (!string.IsNullOrEmpty(summary))
            attrParts.Add($@"Summary = ""{summary}""");
        if (!string.IsNullOrEmpty(description))
            attrParts.Add($@"Description = ""{description}""");
        if (deprecated)
            attrParts.Add("Deprecated = true");
        if (!string.IsNullOrEmpty(operationId))
            attrParts.Add($@"OperationId = ""{operationId}""");

        var attrContent = attrParts.Count > 0 ? string.Join(", ", attrParts) : "";
        var openApiOperationAttr = attrParts.Count > 0 ? $"[OpenApiOperation({attrContent})]" : "";

        return $@"
using Amazon.Lambda.Annotations;
using Amazon.Lambda.Annotations.APIGateway;
using Oproto.Lambda.OpenApi.Attributes;
using System.Threading.Tasks;

public class TestFunctions 
{{
    [LambdaFunction]
    [HttpApi(LambdaHttpMethod.Get, ""/items/{{id}}"")]
    {openApiOperationAttr}
    public string GetItem(string id) => ""test"";
}}";
    }

    private string GenerateSourceWithBothXmlAndAttribute(string attrSummary, string attrDescription)
    {
        return $@"
using Amazon.Lambda.Annotations;
using Amazon.Lambda.Annotations.APIGateway;
using Oproto.Lambda.OpenApi.Attributes;
using System.Threading.Tasks;

public class TestFunctions 
{{
    /// <summary>
    /// XML Summary that should be overridden
    /// </summary>
    /// <remarks>
    /// XML Description that should be overridden
    /// </remarks>
    [LambdaFunction]
    [HttpApi(LambdaHttpMethod.Get, ""/items/{{id}}"")]
    [OpenApiOperation(Summary = ""{attrSummary}"", Description = ""{attrDescription}"")]
    public string GetItem(string id) => ""test"";
}}";
    }

    private OperationInfo ExtractOperationInfo(string source)
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

                        var info = new OperationInfo();

                        if (operation.Value.TryGetProperty("summary", out var summaryProp))
                            info.Summary = summaryProp.GetString();

                        if (operation.Value.TryGetProperty("description", out var descProp))
                            info.Description = descProp.GetString();

                        if (operation.Value.TryGetProperty("deprecated", out var deprecatedProp))
                            info.IsDeprecated = deprecatedProp.GetBoolean();

                        if (operation.Value.TryGetProperty("operationId", out var opIdProp))
                            info.OperationId = opIdProp.GetString();

                        return info;
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

    private class OperationInfo
    {
        public string Summary { get; set; }
        public string Description { get; set; }
        public bool IsDeprecated { get; set; }
        public string OperationId { get; set; }
    }
}
