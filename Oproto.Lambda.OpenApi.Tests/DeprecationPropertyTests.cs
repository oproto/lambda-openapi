using System.Text.Json;
using FsCheck;
using FsCheck.Xunit;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Oproto.Lambda.OpenApi.SourceGenerator;

namespace Oproto.Lambda.OpenApi.Tests;

/// <summary>
/// Property-based tests for deprecation functionality.
/// </summary>
public class DeprecationPropertyTests
{
    /// <summary>
    /// **Feature: openapi-completeness, Property 6: Obsolete Attribute Maps to Deprecated**
    /// **Validates: Requirements 2.1**
    /// 
    /// For any method with [Obsolete] attribute, the generated operation SHALL have deprecated: true.
    /// </summary>
    [Property(MaxTest = 100)]
    public Property ObsoleteAttribute_MapsToDeprecated()
    {
        var methodNameGen = Gen.Elements("GetProduct", "CreateOrder", "UpdateUser", "DeleteItem", "ListCustomers");
        var hasObsoleteGen = Arb.Generate<bool>();
        var obsoleteMessageGen = Gen.Elements(
            (string)null, 
            "This endpoint is deprecated", 
            "Use v2 API instead", 
            "Will be removed in next version");
        
        return Prop.ForAll(
            methodNameGen.ToArbitrary(),
            hasObsoleteGen.ToArbitrary(),
            obsoleteMessageGen.ToArbitrary(),
            (methodName, hasObsolete, obsoleteMessage) =>
            {
                var source = GenerateSourceWithObsolete(methodName, hasObsolete, obsoleteMessage);
                var (isDeprecated, description) = ExtractDeprecationInfo(source);
                
                if (hasObsolete)
                {
                    // If method has [Obsolete], operation should be deprecated
                    var deprecatedCorrect = isDeprecated;
                    
                    // If there's a message, it should appear in the description
                    var messageCorrect = string.IsNullOrEmpty(obsoleteMessage) || 
                                         (description?.Contains(obsoleteMessage) ?? false);
                    
                    return (deprecatedCorrect && messageCorrect)
                        .Label($"Expected deprecated=true (got {isDeprecated}) and message '{obsoleteMessage}' in description '{description}'");
                }
                else
                {
                    // If method doesn't have [Obsolete], operation should NOT be deprecated
                    return (!isDeprecated)
                        .Label($"Expected deprecated=false for non-obsolete method, but got deprecated={isDeprecated}");
                }
            });
    }

    private string GenerateSourceWithObsolete(string methodName, bool hasObsolete, string obsoleteMessage)
    {
        var obsoleteAttr = "";
        if (hasObsolete)
        {
            obsoleteAttr = string.IsNullOrEmpty(obsoleteMessage)
                ? "[System.Obsolete]"
                : $@"[System.Obsolete(""{obsoleteMessage}"")]";
        }
        
        return $@"
using Amazon.Lambda.Annotations;
using Amazon.Lambda.Annotations.APIGateway;
using System.Threading.Tasks;

public class TestFunctions 
{{
    [LambdaFunction]
    [HttpApi(LambdaHttpMethod.Get, ""/items/{{id}}"")]
    {obsoleteAttr}
    public string {methodName}(string id) => ""test"";
}}";
    }

    private (bool IsDeprecated, string Description) ExtractDeprecationInfo(string source)
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
                return (false, null);
            
            var jsonContent = ExtractOpenApiJson(outputCompilation);
            if (string.IsNullOrEmpty(jsonContent))
                return (false, null);
            
            using var doc = JsonDocument.Parse(jsonContent);
            
            // Navigate to the first operation
            if (doc.RootElement.TryGetProperty("paths", out var paths))
            {
                foreach (var path in paths.EnumerateObject())
                {
                    foreach (var operation in path.Value.EnumerateObject())
                    {
                        // Skip non-operation properties
                        if (operation.Name.StartsWith("x-") || operation.Name == "parameters")
                            continue;
                        
                        var isDeprecated = false;
                        string description = null;
                        
                        if (operation.Value.TryGetProperty("deprecated", out var deprecatedProp))
                        {
                            isDeprecated = deprecatedProp.GetBoolean();
                        }
                        
                        if (operation.Value.TryGetProperty("description", out var descProp))
                        {
                            description = descProp.GetString();
                        }
                        
                        return (isDeprecated, description);
                    }
                }
            }
        }
        catch
        {
            // Return default on error
        }
        
        return (false, null);
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
