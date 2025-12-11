using System.Text.Json;
using FsCheck;
using FsCheck.Xunit;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Oproto.Lambda.OpenApi.SourceGenerator;

namespace Oproto.Lambda.OpenApi.Tests;

/// <summary>
/// Property-based tests for operationId generation functionality.
/// </summary>
public class OperationIdPropertyTests
{
    /// <summary>
    /// **Feature: openapi-completeness, Property 17: OperationId Uniqueness**
    /// **Validates: Requirements 7.3**
    /// 
    /// For any set of operations, all operationIds in the generated specification SHALL be unique.
    /// </summary>
    [Property(MaxTest = 100)]
    public Property AllOperationIds_AreUnique()
    {
        // Generate random number of methods (2-10) with potentially duplicate names
        var methodCountGen = Gen.Choose(2, 10);
        var methodNameGen = Gen.Elements("GetItem", "CreateItem", "UpdateItem", "DeleteItem", "ListItems");

        return Prop.ForAll(
            methodCountGen.ToArbitrary(),
            methodNameGen.ToArbitrary(),
            (methodCount, baseName) =>
            {
                // Generate source with multiple methods that could have duplicate names
                var source = GenerateSourceWithMultipleMethods(methodCount, baseName);

                // Extract all operationIds from the generated OpenAPI spec
                var operationIds = ExtractAllOperationIds(source);

                // All operationIds should be unique
                var uniqueIds = operationIds.Distinct().ToList();

                return (operationIds.Count == uniqueIds.Count)
                    .Label($"Expected {operationIds.Count} unique operationIds, but found {uniqueIds.Count} unique out of: [{string.Join(", ", operationIds)}]");
            });
    }

    /// <summary>
    /// **Feature: openapi-completeness, Property 16: OperationId Generation**
    /// **Validates: Requirements 7.1, 7.2**
    /// 
    /// For any operation, the generated OpenAPI SHALL include an operationId based on 
    /// the method name or [OpenApiOperationId] attribute value.
    /// </summary>
    [Property(MaxTest = 100)]
    public Property OperationId_MatchesMethodNameOrAttribute()
    {
        var methodNameGen = Gen.Elements("GetProduct", "CreateOrder", "UpdateUser", "DeleteItem", "ListCustomers");
        var useAttributeGen = Arb.Generate<bool>();
        var customIdGen = Gen.Elements("customGetOp", "customCreateOp", "customUpdateOp", "customDeleteOp", "customListOp");

        return Prop.ForAll(
            methodNameGen.ToArbitrary(),
            useAttributeGen.ToArbitrary(),
            customIdGen.ToArbitrary(),
            (methodName, useAttribute, customId) =>
            {
                var source = GenerateSourceWithOperationId(methodName, useAttribute ? customId : null);
                var operationIds = ExtractAllOperationIds(source);

                if (operationIds.Count == 0)
                    return false.Label("No operationIds found in generated spec");

                var operationId = operationIds.First();
                var expectedId = useAttribute ? customId : methodName;

                return (operationId == expectedId)
                    .Label($"Expected operationId '{expectedId}', but got '{operationId}'");
            });
    }

    private string GenerateSourceWithMultipleMethods(int methodCount, string baseName)
    {
        var methods = new List<string>();
        var routes = new List<string>();

        for (int i = 0; i < methodCount; i++)
        {
            var route = $"/items{i}/{{id}}";
            routes.Add(route);

            // Use the same base name for some methods to test uniqueness
            var methodName = i % 2 == 0 ? baseName : $"{baseName}{i}";

            methods.Add($@"
    [LambdaFunction]
    [HttpApi(LambdaHttpMethod.Get, ""{route}"")]
    public string {methodName}_{i}(string id) => ""test"";");
        }

        return $@"
using Amazon.Lambda.Annotations;
using Amazon.Lambda.Annotations.APIGateway;
using System.Threading.Tasks;

public class TestFunctions 
{{
{string.Join("\n", methods)}
}}";
    }

    private string GenerateSourceWithOperationId(string methodName, string customOperationId)
    {
        var operationIdAttr = customOperationId != null
            ? $@"[Oproto.Lambda.OpenApi.Attributes.OpenApiOperationId(""{customOperationId}"")]"
            : "";

        return $@"
using Amazon.Lambda.Annotations;
using Amazon.Lambda.Annotations.APIGateway;
using System.Threading.Tasks;

public class TestFunctions 
{{
    [LambdaFunction]
    [HttpApi(LambdaHttpMethod.Get, ""/items/{{id}}"")]
    {operationIdAttr}
    public string {methodName}(string id) => ""test"";
}}";
    }

    private List<string> ExtractAllOperationIds(string source)
    {
        var operationIds = new List<string>();

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
                return operationIds;

            var jsonContent = ExtractOpenApiJson(outputCompilation);
            if (string.IsNullOrEmpty(jsonContent))
                return operationIds;

            using var doc = JsonDocument.Parse(jsonContent);

            // Navigate through all paths and operations to collect operationIds
            if (doc.RootElement.TryGetProperty("paths", out var paths))
            {
                foreach (var path in paths.EnumerateObject())
                {
                    foreach (var operation in path.Value.EnumerateObject())
                    {
                        // Skip non-operation properties like "parameters"
                        if (operation.Name.StartsWith("x-") || operation.Name == "parameters")
                            continue;

                        if (operation.Value.TryGetProperty("operationId", out var operationId))
                        {
                            operationIds.Add(operationId.GetString() ?? "");
                        }
                    }
                }
            }
        }
        catch
        {
            // Return empty list on error
        }

        return operationIds;
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
