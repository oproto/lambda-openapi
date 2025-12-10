using System.Text.Json;
using FsCheck;
using FsCheck.Xunit;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Oproto.Lambda.OpenApi.SourceGenerator;

namespace Oproto.Lambda.OpenApi.Tests;

/// <summary>
/// Property-based tests for Task&lt;T&gt; unwrapping functionality.
/// </summary>
public class TaskUnwrappingPropertyTests
{
    /// <summary>
    /// **Feature: pre-release-fixes, Property 1: Task&lt;T&gt; Unwrapping Preserves Inner Type**
    /// **Validates: Requirements 1.1, 1.3, 1.4**
    /// 
    /// For any Lambda function with return type Task&lt;T&gt; or ValueTask&lt;T&gt;, 
    /// the generated OpenAPI response schema SHALL be equivalent to the schema 
    /// that would be generated for return type T directly.
    /// </summary>
    [Property(MaxTest = 100)]
    public Property TaskT_UnwrappingPreservesInnerType()
    {
        // Generate random type names for testing
        var typeNameGen = Gen.Elements("string", "int", "bool", "Product", "Order", "Customer");
        
        return Prop.ForAll(typeNameGen.ToArbitrary(), typeName =>
        {
            // Generate source with Task<T> return type
            var taskSource = GenerateSourceWithReturnType($"Task<{typeName}>", typeName);
            var directSource = GenerateSourceWithReturnType(typeName, typeName);
            
            // Compile and generate OpenAPI for both
            var taskSchema = ExtractResponseSchema(taskSource);
            var directSchema = ExtractResponseSchema(directSource);
            
            // The schemas should be equivalent (both should reference the inner type)
            return SchemasAreEquivalent(taskSchema, directSchema)
                .Label($"Task<{typeName}> schema should equal {typeName} schema");
        });
    }

    /// <summary>
    /// **Feature: pre-release-fixes, Property 1: Task&lt;T&gt; Unwrapping Preserves Inner Type**
    /// **Validates: Requirements 1.1, 1.3, 1.4**
    /// 
    /// ValueTask&lt;T&gt; should behave identically to Task&lt;T&gt;.
    /// </summary>
    [Property(MaxTest = 100)]
    public Property ValueTaskT_UnwrappingPreservesInnerType()
    {
        var typeNameGen = Gen.Elements("string", "int", "bool", "Product", "Order", "Customer");
        
        return Prop.ForAll(typeNameGen.ToArbitrary(), typeName =>
        {
            var valueTaskSource = GenerateSourceWithReturnType($"ValueTask<{typeName}>", typeName);
            var directSource = GenerateSourceWithReturnType(typeName, typeName);
            
            var valueTaskSchema = ExtractResponseSchema(valueTaskSource);
            var directSchema = ExtractResponseSchema(directSource);
            
            return SchemasAreEquivalent(valueTaskSchema, directSchema)
                .Label($"ValueTask<{typeName}> schema should equal {typeName} schema");
        });
    }

    private string GenerateSourceWithReturnType(string returnType, string modelType)
    {
        var modelClass = modelType switch
        {
            "string" or "int" or "bool" => "",
            _ => $@"
    public class {modelType}
    {{
        public string Id {{ get; set; }}
        public string Name {{ get; set; }}
    }}"
        };

        return $@"
using Amazon.Lambda.Annotations;
using Amazon.Lambda.Annotations.APIGateway;
using System.Threading.Tasks;

public class TestFunctions 
{{
    [LambdaFunction]
    [HttpApi(LambdaHttpMethod.Get, ""/items/{{id}}"")]
    public {returnType} GetItem(string id) => default;
}}
{modelClass}";
    }

    private JsonElement? ExtractResponseSchema(string source)
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
                return null;
            
            var jsonContent = ExtractOpenApiJson(outputCompilation);
            if (string.IsNullOrEmpty(jsonContent))
                return null;
            
            using var doc = JsonDocument.Parse(jsonContent);
            
            // Navigate to the response schema
            if (doc.RootElement.TryGetProperty("paths", out var paths) &&
                paths.TryGetProperty("/items/{id}", out var path) &&
                path.TryGetProperty("get", out var operation) &&
                operation.TryGetProperty("responses", out var responses))
            {
                // Check for 200 response first, then 204
                if (responses.TryGetProperty("200", out var response200) &&
                    response200.TryGetProperty("content", out var content) &&
                    content.TryGetProperty("application/json", out var mediaType) &&
                    mediaType.TryGetProperty("schema", out var schema))
                {
                    return schema.Clone();
                }
                
                // For void returns (Task without T), there's no content schema
                if (responses.TryGetProperty("204", out _))
                {
                    return JsonDocument.Parse("{}").RootElement.Clone();
                }
            }
            
            return null;
        }
        catch
        {
            return null;
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

    private bool SchemasAreEquivalent(JsonElement? schema1, JsonElement? schema2)
    {
        if (schema1 == null && schema2 == null)
            return true;
        if (schema1 == null || schema2 == null)
            return false;
        
        // Compare the JSON representations
        return schema1.Value.GetRawText() == schema2.Value.GetRawText();
    }
}
