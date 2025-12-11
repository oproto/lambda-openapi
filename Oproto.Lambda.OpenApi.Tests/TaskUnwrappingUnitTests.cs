using System.Text.Json;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Oproto.Lambda.OpenApi.SourceGenerator;

namespace Oproto.Lambda.OpenApi.Tests;

/// <summary>
/// Unit tests for Task&lt;T&gt; unwrapping edge cases.
/// **Validates: Requirements 1.1, 1.2, 1.3, 1.4**
/// </summary>
public class TaskUnwrappingUnitTests
{
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

    /// <summary>
    /// Task&lt;string&gt; should produce a string schema, not a Task wrapper.
    /// **Validates: Requirements 1.1**
    /// </summary>
    [Fact]
    public void Task_String_UnwrapsToString()
    {
        var source = @"
using Amazon.Lambda.Annotations;
using Amazon.Lambda.Annotations.APIGateway;
using System.Threading.Tasks;

public class TestFunctions 
{
    [LambdaFunction]
    [HttpApi(LambdaHttpMethod.Get, ""/items/{id}"")]
    public Task<string> GetItem(string id) => Task.FromResult("""");
}";

        var compilation = CompilerHelper.CreateCompilation(source);
        var generator = new OpenApiSpecGenerator();

        var driver = CSharpGeneratorDriver.Create(generator);
        driver.RunGeneratorsAndUpdateCompilation(compilation,
            out var outputCompilation,
            out var diagnostics);

        Assert.Empty(diagnostics.Where(d => d.Severity == DiagnosticSeverity.Error));

        var jsonContent = ExtractOpenApiJson(outputCompilation);

        // Debug: output the full JSON for inspection
        System.Diagnostics.Debug.WriteLine($"Generated OpenAPI JSON:\n{jsonContent}");

        using var doc = JsonDocument.Parse(jsonContent);
        var root = doc.RootElement;

        var responses = root.GetProperty("paths")
            .GetProperty("/items/{id}")
            .GetProperty("get")
            .GetProperty("responses");

        // The unwrapped Task<string> should produce a 200 response with string schema
        Assert.True(responses.TryGetProperty("200", out var response200),
            $"Expected 200 response. Full JSON: {jsonContent}");

        var schema = response200
            .GetProperty("content")
            .GetProperty("application/json")
            .GetProperty("schema");

        // Should be a string type, not a Task wrapper
        Assert.True(schema.TryGetProperty("type", out var typeValue),
            $"Expected schema to have 'type' property. Schema: {schema.GetRawText()}");
        Assert.Equal("string", typeValue.GetString());

        // Should NOT contain "Task" as a schema reference
        Assert.DoesNotContain("\"$ref\": \"#/components/schemas/Task\"", jsonContent);
    }

    /// <summary>
    /// Task&lt;ComplexType&gt; should produce the ComplexType schema.
    /// **Validates: Requirements 1.1**
    /// </summary>
    [Fact]
    public void Task_ComplexType_UnwrapsToComplexType()
    {
        var source = @"
using Amazon.Lambda.Annotations;
using Amazon.Lambda.Annotations.APIGateway;
using System.Threading.Tasks;

public class TestFunctions 
{
    [LambdaFunction]
    [HttpApi(LambdaHttpMethod.Get, ""/orders/{id}"")]
    public Task<Order> GetOrder(string id) => Task.FromResult<Order>(null);
}

public class Order
{
    public string Id { get; set; }
    public string CustomerName { get; set; }
    public decimal Total { get; set; }
}";

        var compilation = CompilerHelper.CreateCompilation(source);
        var generator = new OpenApiSpecGenerator();

        var driver = CSharpGeneratorDriver.Create(generator);
        driver.RunGeneratorsAndUpdateCompilation(compilation,
            out var outputCompilation,
            out var diagnostics);

        Assert.Empty(diagnostics.Where(d => d.Severity == DiagnosticSeverity.Error));

        var jsonContent = ExtractOpenApiJson(outputCompilation);
        using var doc = JsonDocument.Parse(jsonContent);
        var root = doc.RootElement;

        var schema = root.GetProperty("paths")
            .GetProperty("/orders/{id}")
            .GetProperty("get")
            .GetProperty("responses")
            .GetProperty("200")
            .GetProperty("content")
            .GetProperty("application/json")
            .GetProperty("schema");

        // Should reference the Order schema (either directly or via allOf)
        if (schema.TryGetProperty("$ref", out var refValue))
        {
            Assert.Equal("#/components/schemas/Order", refValue.GetString());
        }
        else if (schema.TryGetProperty("allOf", out var allOf))
        {
            var refs = allOf.EnumerateArray().ToList();
            Assert.Contains(refs, r => r.TryGetProperty("$ref", out var rf) &&
                rf.GetString() == "#/components/schemas/Order");
        }
        else
        {
            // Inline object schema - verify it has the expected properties
            Assert.True(schema.TryGetProperty("properties", out var props));
            Assert.True(props.TryGetProperty("Id", out _));
            Assert.True(props.TryGetProperty("CustomerName", out _));
            Assert.True(props.TryGetProperty("Total", out _));
        }

        // Should NOT contain "Task" as a schema reference
        Assert.DoesNotContain("\"$ref\": \"#/components/schemas/Task\"", jsonContent);
    }

    /// <summary>
    /// Non-generic Task should produce a 204 No Content response.
    /// **Validates: Requirements 1.2**
    /// </summary>
    [Fact]
    public void Task_NonGeneric_ProducesNoContent()
    {
        var source = @"
using Amazon.Lambda.Annotations;
using Amazon.Lambda.Annotations.APIGateway;
using System.Threading.Tasks;

public class TestFunctions 
{
    [LambdaFunction]
    [HttpApi(LambdaHttpMethod.Delete, ""/items/{id}"")]
    public Task DeleteItem(string id) => Task.CompletedTask;
}";

        var compilation = CompilerHelper.CreateCompilation(source);
        var generator = new OpenApiSpecGenerator();

        var driver = CSharpGeneratorDriver.Create(generator);
        driver.RunGeneratorsAndUpdateCompilation(compilation,
            out var outputCompilation,
            out var diagnostics);

        Assert.Empty(diagnostics.Where(d => d.Severity == DiagnosticSeverity.Error));

        var jsonContent = ExtractOpenApiJson(outputCompilation);
        using var doc = JsonDocument.Parse(jsonContent);
        var root = doc.RootElement;

        var responses = root.GetProperty("paths")
            .GetProperty("/items/{id}")
            .GetProperty("delete")
            .GetProperty("responses");

        // Should have 204 No Content, not 200 with Task schema
        Assert.True(responses.TryGetProperty("204", out var noContentResponse));
        Assert.Equal("No Content", noContentResponse.GetProperty("description").GetString());

        // Should NOT have a 200 response with content
        Assert.False(responses.TryGetProperty("200", out _));
    }

    /// <summary>
    /// ValueTask&lt;T&gt; should behave like Task&lt;T&gt;.
    /// **Validates: Requirements 1.3**
    /// </summary>
    [Fact]
    public void ValueTask_UnwrapsCorrectly()
    {
        var source = @"
using Amazon.Lambda.Annotations;
using Amazon.Lambda.Annotations.APIGateway;
using System.Threading.Tasks;

public class TestFunctions 
{
    [LambdaFunction]
    [HttpApi(LambdaHttpMethod.Get, ""/products/{id}"")]
    public ValueTask<Product> GetProduct(string id) => new ValueTask<Product>(new Product());
}

public class Product
{
    public string Id { get; set; }
    public string Name { get; set; }
}";

        var compilation = CompilerHelper.CreateCompilation(source);
        var generator = new OpenApiSpecGenerator();

        var driver = CSharpGeneratorDriver.Create(generator);
        driver.RunGeneratorsAndUpdateCompilation(compilation,
            out var outputCompilation,
            out var diagnostics);

        Assert.Empty(diagnostics.Where(d => d.Severity == DiagnosticSeverity.Error));

        var jsonContent = ExtractOpenApiJson(outputCompilation);
        using var doc = JsonDocument.Parse(jsonContent);
        var root = doc.RootElement;

        var schema = root.GetProperty("paths")
            .GetProperty("/products/{id}")
            .GetProperty("get")
            .GetProperty("responses")
            .GetProperty("200")
            .GetProperty("content")
            .GetProperty("application/json")
            .GetProperty("schema");

        // Should reference the Product schema (either directly or via allOf), not ValueTask
        if (schema.TryGetProperty("$ref", out var refValue))
        {
            Assert.Equal("#/components/schemas/Product", refValue.GetString());
        }
        else if (schema.TryGetProperty("allOf", out var allOf))
        {
            var refs = allOf.EnumerateArray().ToList();
            Assert.Contains(refs, r => r.TryGetProperty("$ref", out var rf) &&
                rf.GetString() == "#/components/schemas/Product");
        }
        else
        {
            // Inline object schema - verify it has the expected properties
            Assert.True(schema.TryGetProperty("properties", out var props),
                $"Expected schema to have properties. Schema: {schema.GetRawText()}");
            Assert.True(props.TryGetProperty("Id", out _));
            Assert.True(props.TryGetProperty("Name", out _));
        }

        // Should NOT contain "ValueTask" as a schema reference
        Assert.DoesNotContain("\"$ref\": \"#/components/schemas/ValueTask\"", jsonContent);
    }

    /// <summary>
    /// Non-async return types should remain unchanged.
    /// **Validates: Requirements 1.4**
    /// </summary>
    [Fact]
    public void NonAsyncType_RemainsUnchanged()
    {
        var source = @"
using Amazon.Lambda.Annotations;
using Amazon.Lambda.Annotations.APIGateway;

public class TestFunctions 
{
    [LambdaFunction]
    [HttpApi(LambdaHttpMethod.Get, ""/items/{id}"")]
    public Item GetItem(string id) => new Item();
}

public class Item
{
    public string Id { get; set; }
    public string Name { get; set; }
}";

        var compilation = CompilerHelper.CreateCompilation(source);
        var generator = new OpenApiSpecGenerator();

        var driver = CSharpGeneratorDriver.Create(generator);
        driver.RunGeneratorsAndUpdateCompilation(compilation,
            out var outputCompilation,
            out var diagnostics);

        Assert.Empty(diagnostics.Where(d => d.Severity == DiagnosticSeverity.Error));

        var jsonContent = ExtractOpenApiJson(outputCompilation);
        using var doc = JsonDocument.Parse(jsonContent);
        var root = doc.RootElement;

        var schema = root.GetProperty("paths")
            .GetProperty("/items/{id}")
            .GetProperty("get")
            .GetProperty("responses")
            .GetProperty("200")
            .GetProperty("content")
            .GetProperty("application/json")
            .GetProperty("schema");

        // Should reference the Item schema (either directly, via allOf, or inline)
        if (schema.TryGetProperty("$ref", out var refValue))
        {
            Assert.Equal("#/components/schemas/Item", refValue.GetString());
        }
        else if (schema.TryGetProperty("allOf", out var allOf))
        {
            var refs = allOf.EnumerateArray().ToList();
            Assert.Contains(refs, r => r.TryGetProperty("$ref", out var rf) &&
                rf.GetString() == "#/components/schemas/Item");
        }
        else
        {
            // Inline object schema - verify it has the expected properties
            Assert.True(schema.TryGetProperty("properties", out var props),
                $"Expected schema to have properties. Schema: {schema.GetRawText()}");
            Assert.True(props.TryGetProperty("Id", out _));
            Assert.True(props.TryGetProperty("Name", out _));
        }
    }
}
