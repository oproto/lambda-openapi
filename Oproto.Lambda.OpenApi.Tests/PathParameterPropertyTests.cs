using System.Text.Json;
using FsCheck;
using FsCheck.Xunit;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Oproto.Lambda.OpenApi.SourceGenerator;

namespace Oproto.Lambda.OpenApi.Tests;

/// <summary>
/// Property-based tests for path parameter generation functionality.
/// </summary>
public class PathParameterPropertyTests
{
    /// <summary>
    /// **Feature: generator-bug-fixes, Property 1: Path Parameter Completeness**
    /// **Validates: Requirements 1.1, 1.4**
    /// 
    /// For any route template containing path parameters (e.g., {id}, {companyId}), 
    /// the generated OpenAPI operation SHALL contain a parameter definition for each 
    /// path parameter with in: path and required: true.
    /// </summary>
    [Property(MaxTest = 100)]
    public Property PathParametersFromTemplate_AreDefinedInOperation()
    {
        // Generate 1-3 path parameter names
        var paramNameGen = Gen.Elements("id", "companyId", "locationId", "userId", "orderId", "productId", "categoryId");
        var paramCountGen = Gen.Choose(1, 3);
        
        var pathParamsGen = paramCountGen.SelectMany(count =>
            Gen.ListOf(count, paramNameGen)
                .Select(names => names.Distinct().ToList()));

        return Prop.ForAll(
            pathParamsGen.ToArbitrary(),
            pathParams =>
            {
                if (pathParams.Count == 0)
                    return true.Label("No path parameters to test");

                var source = GenerateSourceWithPathParams(pathParams);
                var extractedParams = ExtractPathParameters(source);

                // All path parameters from template should be defined
                var allParamsDefined = pathParams.All(p => 
                    extractedParams.Any(ep => 
                        string.Equals(ep.Name, p, StringComparison.OrdinalIgnoreCase)));

                // All defined path parameters should have in: path
                var allInPath = extractedParams
                    .Where(ep => pathParams.Any(p => string.Equals(ep.Name, p, StringComparison.OrdinalIgnoreCase)))
                    .All(ep => ep.In == "path");

                // All defined path parameters should have required: true
                var allRequired = extractedParams
                    .Where(ep => pathParams.Any(p => string.Equals(ep.Name, p, StringComparison.OrdinalIgnoreCase)))
                    .All(ep => ep.Required);

                return (allParamsDefined && allInPath && allRequired)
                    .Label($"Expected path params [{string.Join(", ", pathParams)}] with in:path and required:true, " +
                           $"but got [{string.Join(", ", extractedParams.Select(p => $"{p.Name}(in:{p.In},required:{p.Required})"))}]");
            });
    }

    /// <summary>
    /// **Feature: generator-bug-fixes, Property 2: Path Parameter Type Inference**
    /// **Validates: Requirements 1.2, 1.3**
    /// 
    /// For any path parameter that has a corresponding [FromRoute] method parameter, 
    /// the generated parameter schema SHALL use the method parameter's type. 
    /// For path parameters without a corresponding method parameter, the schema SHALL default to type: string.
    /// </summary>
    [Property(MaxTest = 100)]
    public Property PathParameterTypes_AreInferredFromMethodParameters()
    {
        // Generate parameter name and whether it has a corresponding method parameter
        var paramNameGen = Gen.Elements("id", "companyId", "userId");
        var hasMethodParamGen = Gen.Elements(true, false);
        // Use types that are properly supported by the schema generator
        var typeGen = Gen.Elements("string", "int", "long");

        return Prop.ForAll(
            paramNameGen.ToArbitrary(),
            hasMethodParamGen.ToArbitrary(),
            typeGen.ToArbitrary(),
            (paramName, hasMethodParam, paramType) =>
            {
                var source = GenerateSourceWithTypedPathParam(paramName, hasMethodParam, paramType);
                var extractedParams = ExtractPathParameters(source);

                var pathParam = extractedParams.FirstOrDefault(p => 
                    string.Equals(p.Name, paramName, StringComparison.OrdinalIgnoreCase));

                if (pathParam == null)
                    return false.Label($"Path parameter '{paramName}' not found in generated spec");

                if (hasMethodParam)
                {
                    // Should use the method parameter's type
                    var expectedType = paramType switch
                    {
                        "int" => "integer",
                        "long" => "integer",
                        _ => "string"
                    };
                    return (pathParam.Type == expectedType)
                        .Label($"Expected type '{expectedType}' for {paramName} with [FromRoute] {paramType}, but got '{pathParam.Type}'");
                }
                else
                {
                    // Should default to string
                    return (pathParam.Type == "string")
                        .Label($"Expected type 'string' for {paramName} without method param, but got '{pathParam.Type}'");
                }
            });
    }

    /// <summary>
    /// Tests that path parameters with constraints (e.g., {id:int}) are properly extracted.
    /// </summary>
    [Property(MaxTest = 100)]
    public Property PathParametersWithConstraints_AreProperlyExtracted()
    {
        var paramNameGen = Gen.Elements("id", "companyId", "userId");
        var constraintGen = Gen.Elements("int", "guid", "long", "");

        return Prop.ForAll(
            paramNameGen.ToArbitrary(),
            constraintGen.ToArbitrary(),
            (paramName, constraint) =>
            {
                var routeParam = string.IsNullOrEmpty(constraint) 
                    ? $"{{{paramName}}}" 
                    : $"{{{paramName}:{constraint}}}";
                    
                var source = GenerateSourceWithRoute($"/items/{routeParam}");
                var extractedParams = ExtractPathParameters(source);

                // The parameter should be extracted with just the name (constraint stripped)
                var hasParam = extractedParams.Any(p => 
                    string.Equals(p.Name, paramName, StringComparison.OrdinalIgnoreCase));

                return hasParam
                    .Label($"Expected path parameter '{paramName}' to be extracted from route with constraint '{constraint}'");
            });
    }

    private string GenerateSourceWithPathParams(List<string> pathParams)
    {
        var routeParts = pathParams.Select(p => $"{{{p}}}");
        var route = "/items/" + string.Join("/", routeParts);

        return $@"
using Amazon.Lambda.Annotations;
using Amazon.Lambda.Annotations.APIGateway;
using System.Threading.Tasks;

public class TestFunctions 
{{
    [LambdaFunction]
    [HttpApi(LambdaHttpMethod.Get, ""{route}"")]
    public string GetItem() => ""test"";
}}";
    }

    private string GenerateSourceWithTypedPathParam(string paramName, bool hasMethodParam, string paramType)
    {
        var route = $"/items/{{{paramName}}}";
        var methodParam = hasMethodParam 
            ? $"[FromRoute] {paramType} {paramName}" 
            : "";

        return $@"
using Amazon.Lambda.Annotations;
using Amazon.Lambda.Annotations.APIGateway;
using System;
using System.Threading.Tasks;

public class TestFunctions 
{{
    [LambdaFunction]
    [HttpApi(LambdaHttpMethod.Get, ""{route}"")]
    public string GetItem({methodParam}) => ""test"";
}}";
    }

    private string GenerateSourceWithRoute(string route)
    {
        return $@"
using Amazon.Lambda.Annotations;
using Amazon.Lambda.Annotations.APIGateway;
using System.Threading.Tasks;

public class TestFunctions 
{{
    [LambdaFunction]
    [HttpApi(LambdaHttpMethod.Get, ""{route}"")]
    public string GetItem() => ""test"";
}}";
    }

    private List<PathParameterInfo> ExtractPathParameters(string source)
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
                return new List<PathParameterInfo>();

            var jsonContent = ExtractOpenApiJson(outputCompilation);
            if (string.IsNullOrEmpty(jsonContent))
                return new List<PathParameterInfo>();

            using var doc = JsonDocument.Parse(jsonContent);

            var parameters = new List<PathParameterInfo>();

            // Navigate to the first operation and get its parameters
            if (doc.RootElement.TryGetProperty("paths", out var paths))
            {
                foreach (var path in paths.EnumerateObject())
                {
                    foreach (var operation in path.Value.EnumerateObject())
                    {
                        // Skip non-operation properties
                        if (operation.Name.StartsWith("x-") || operation.Name == "parameters")
                            continue;

                        if (operation.Value.TryGetProperty("parameters", out var paramsArray))
                        {
                            foreach (var param in paramsArray.EnumerateArray())
                            {
                                var name = param.TryGetProperty("name", out var nameProp) 
                                    ? nameProp.GetString() : null;
                                var inValue = param.TryGetProperty("in", out var inProp) 
                                    ? inProp.GetString() : null;
                                var required = param.TryGetProperty("required", out var reqProp) 
                                    && reqProp.GetBoolean();
                                var type = "string";
                                if (param.TryGetProperty("schema", out var schema) &&
                                    schema.TryGetProperty("type", out var typeProp))
                                {
                                    type = typeProp.GetString() ?? "string";
                                }

                                if (name != null)
                                {
                                    parameters.Add(new PathParameterInfo
                                    {
                                        Name = name,
                                        In = inValue ?? "",
                                        Required = required,
                                        Type = type
                                    });
                                }
                            }
                        }

                        return parameters; // Return after first operation
                    }
                }
            }
        }
        catch
        {
            // Return empty on error
        }

        return new List<PathParameterInfo>();
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

    private class PathParameterInfo
    {
        public string Name { get; set; } = "";
        public string In { get; set; } = "";
        public bool Required { get; set; }
        public string Type { get; set; } = "string";
    }
}
