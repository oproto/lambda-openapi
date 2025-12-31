using System.Text.Json;
using FsCheck;
using FsCheck.Xunit;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Oproto.Lambda.OpenApi.SourceGenerator;

namespace Oproto.Lambda.OpenApi.Tests;

/// <summary>
/// Property-based tests for AWS Lambda type exclusion from OpenAPI specifications.
/// </summary>
public class AwsTypeExclusionPropertyTests
{
    /// <summary>
    /// **Feature: generator-bug-fixes, Property 6: AWS Type Exclusion**
    /// **Validates: Requirements 4.2, 4.3**
    /// 
    /// For any generated OpenAPI specification, the components/schemas section SHALL NOT contain
    /// AWS Lambda types (types from Amazon.Lambda.* namespaces or common Lambda type names like
    /// APIGatewayProxyRequest).
    /// </summary>
    [Property(MaxTest = 100)]
    public Property AwsLambdaTypes_NotInComponentsSchemas()
    {
        var awsTypeGen = Gen.Elements(
            "APIGatewayProxyRequest",
            "APIGatewayProxyResponse",
            "APIGatewayHttpApiV2ProxyRequest",
            "APIGatewayHttpApiV2ProxyResponse",
            "ILambdaContext"
        );
        var methodNameGen = Gen.Elements("HandleRequest", "ProcessEvent", "Execute", "Run");

        return Prop.ForAll(
            awsTypeGen.ToArbitrary(),
            methodNameGen.ToArbitrary(),
            (awsTypeName, methodName) =>
            {
                var source = GenerateSourceWithAwsType(methodName, awsTypeName);
                var schemas = ExtractComponentSchemas(source);

                // AWS types should NOT appear in components/schemas
                var hasAwsType = schemas.Any(s => 
                    s.Contains("APIGateway", StringComparison.OrdinalIgnoreCase) ||
                    s.Contains("LambdaContext", StringComparison.OrdinalIgnoreCase));

                return (!hasAwsType)
                    .Label($"Expected no AWS types in schemas, but found: [{string.Join(", ", schemas)}]");
            });
    }

    /// <summary>
    /// **Feature: generator-bug-fixes, Property 6: AWS Type Exclusion (Parameters)**
    /// **Validates: Requirements 4.3**
    /// 
    /// For any method with AWS Lambda type parameters without [FromBody], those parameters
    /// SHALL NOT appear in the operation's parameters list.
    /// </summary>
    [Property(MaxTest = 100)]
    public Property AwsLambdaTypeParameters_ExcludedFromOperation()
    {
        var awsTypeGen = Gen.Elements(
            "APIGatewayProxyRequest",
            "APIGatewayHttpApiV2ProxyRequest",
            "ILambdaContext"
        );
        var methodNameGen = Gen.Elements("HandleRequest", "ProcessEvent", "Execute");

        return Prop.ForAll(
            awsTypeGen.ToArbitrary(),
            methodNameGen.ToArbitrary(),
            (awsTypeName, methodName) =>
            {
                var source = GenerateSourceWithAwsTypeParameter(methodName, awsTypeName);
                var parameters = ExtractOperationParameters(source);

                // AWS type parameters should NOT appear in operation parameters
                var hasAwsParam = parameters.Any(p => 
                    p.Contains("request", StringComparison.OrdinalIgnoreCase) ||
                    p.Contains("context", StringComparison.OrdinalIgnoreCase));

                return (!hasAwsParam)
                    .Label($"Expected no AWS type parameters, but found: [{string.Join(", ", parameters)}]");
            });
    }

    /// <summary>
    /// **Feature: generator-bug-fixes, Property 7: POST Without Body**
    /// **Validates: Requirements 4.1**
    /// 
    /// For any POST operation where no parameter has the [FromBody] attribute,
    /// the operation SHALL NOT have a requestBody defined.
    /// </summary>
    [Property(MaxTest = 100)]
    public Property PostWithoutFromBody_NoRequestBody()
    {
        var methodNameGen = Gen.Elements("CreateItem", "ProcessData", "SubmitForm", "HandlePost");

        return Prop.ForAll(
            methodNameGen.ToArbitrary(),
            methodName =>
            {
                var source = GeneratePostWithoutFromBody(methodName);
                var hasRequestBody = CheckHasRequestBody(source);

                return (!hasRequestBody)
                    .Label($"Expected no requestBody for POST without [FromBody], but requestBody was present");
            });
    }

    /// <summary>
    /// Tests that POST with explicit [FromBody] on a business type DOES have a requestBody.
    /// </summary>
    [Property(MaxTest = 100)]
    public Property PostWithFromBody_HasRequestBody()
    {
        var methodNameGen = Gen.Elements("CreateItem", "ProcessData", "SubmitForm");

        return Prop.ForAll(
            methodNameGen.ToArbitrary(),
            methodName =>
            {
                var source = GeneratePostWithFromBody(methodName);
                var hasRequestBody = CheckHasRequestBody(source);

                return hasRequestBody
                    .Label($"Expected requestBody for POST with [FromBody], but requestBody was not present");
            });
    }

    private string GenerateSourceWithAwsType(string methodName, string awsTypeName)
    {
        return $@"
using Amazon.Lambda.Annotations;
using Amazon.Lambda.Annotations.APIGateway;
using System.Threading.Tasks;

public class {awsTypeName} {{ }}

public class TestFunctions 
{{
    [LambdaFunction]
    [HttpApi(LambdaHttpMethod.Post, ""/items"")]
    public string {methodName}({awsTypeName} request) => ""test"";
}}";
    }

    private string GenerateSourceWithAwsTypeParameter(string methodName, string awsTypeName)
    {
        return $@"
using Amazon.Lambda.Annotations;
using Amazon.Lambda.Annotations.APIGateway;
using System.Threading.Tasks;

public class {awsTypeName} {{ }}

public class TestFunctions 
{{
    [LambdaFunction]
    [HttpApi(LambdaHttpMethod.Get, ""/items"")]
    public string {methodName}({awsTypeName} request) => ""test"";
}}";
    }

    private string GeneratePostWithoutFromBody(string methodName)
    {
        // POST method with AWS type parameter (no [FromBody])
        return $@"
using Amazon.Lambda.Annotations;
using Amazon.Lambda.Annotations.APIGateway;
using System.Threading.Tasks;

public class APIGatewayProxyRequest {{ }}

public class TestFunctions 
{{
    [LambdaFunction]
    [HttpApi(LambdaHttpMethod.Post, ""/items"")]
    public string {methodName}(APIGatewayProxyRequest request) => ""test"";
}}";
    }

    private string GeneratePostWithFromBody(string methodName)
    {
        // POST method with explicit [FromBody] on a business type
        return $@"
using Amazon.Lambda.Annotations;
using Amazon.Lambda.Annotations.APIGateway;
using System.Threading.Tasks;

public class CreateItemRequest 
{{ 
    public string Name {{ get; set; }}
    public decimal Price {{ get; set; }}
}}

public class TestFunctions 
{{
    [LambdaFunction]
    [HttpApi(LambdaHttpMethod.Post, ""/items"")]
    public string {methodName}([FromBody] CreateItemRequest request) => ""test"";
}}";
    }

    private List<string> ExtractComponentSchemas(string source)
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
                return new List<string>();

            var jsonContent = ExtractOpenApiJson(outputCompilation);
            if (string.IsNullOrEmpty(jsonContent))
                return new List<string>();

            using var doc = JsonDocument.Parse(jsonContent);

            if (doc.RootElement.TryGetProperty("components", out var components) &&
                components.TryGetProperty("schemas", out var schemas))
            {
                return schemas.EnumerateObject()
                    .Select(s => s.Name)
                    .ToList();
            }
        }
        catch
        {
            // Return empty on error
        }

        return new List<string>();
    }

    private List<string> ExtractOperationParameters(string source)
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
                return new List<string>();

            var jsonContent = ExtractOpenApiJson(outputCompilation);
            if (string.IsNullOrEmpty(jsonContent))
                return new List<string>();

            using var doc = JsonDocument.Parse(jsonContent);

            if (doc.RootElement.TryGetProperty("paths", out var paths))
            {
                foreach (var path in paths.EnumerateObject())
                {
                    foreach (var operation in path.Value.EnumerateObject())
                    {
                        if (operation.Name.StartsWith("x-") || operation.Name == "parameters")
                            continue;

                        if (operation.Value.TryGetProperty("parameters", out var parameters))
                        {
                            return parameters.EnumerateArray()
                                .Where(p => p.TryGetProperty("name", out _))
                                .Select(p => p.GetProperty("name").GetString())
                                .Where(n => n != null)
                                .ToList()!;
                        }

                        return new List<string>();
                    }
                }
            }
        }
        catch
        {
            // Return empty on error
        }

        return new List<string>();
    }

    private bool CheckHasRequestBody(string source)
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
                return false;

            var jsonContent = ExtractOpenApiJson(outputCompilation);
            if (string.IsNullOrEmpty(jsonContent))
                return false;

            using var doc = JsonDocument.Parse(jsonContent);

            if (doc.RootElement.TryGetProperty("paths", out var paths))
            {
                foreach (var path in paths.EnumerateObject())
                {
                    foreach (var operation in path.Value.EnumerateObject())
                    {
                        if (operation.Name.StartsWith("x-") || operation.Name == "parameters")
                            continue;

                        return operation.Value.TryGetProperty("requestBody", out _);
                    }
                }
            }
        }
        catch
        {
            // Return false on error
        }

        return false;
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
