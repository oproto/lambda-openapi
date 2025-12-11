using System.Text.Json;
using FsCheck;
using FsCheck.Xunit;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Oproto.Lambda.OpenApi.SourceGenerator;

namespace Oproto.Lambda.OpenApi.Tests;

/// <summary>
/// Property-based tests for tag assignment functionality.
/// </summary>
public class TagAssignmentPropertyTests
{
    /// <summary>
    /// **Feature: openapi-completeness, Property 12: Tag Assignment**
    /// **Validates: Requirements 5.1, 5.3, 5.4, 5.5**
    /// 
    /// For any method with [OpenApiTag] attributes, the generated operation SHALL be assigned to all specified tags;
    /// methods without the attribute SHALL be assigned to "Default".
    /// </summary>
    [Property(MaxTest = 100)]
    public Property TagAttribute_AssignsOperationToSpecifiedTags()
    {
        var methodNameGen = Gen.Elements("GetProduct", "CreateOrder", "UpdateUser", "DeleteItem", "ListCustomers");
        var tagNamesGen = Gen.ListOf(Gen.Elements("Products", "Orders", "Users", "Admin", "Public"))
            .Select(tags => tags.Distinct().ToList());
        
        return Prop.ForAll(
            methodNameGen.ToArbitrary(),
            tagNamesGen.ToArbitrary(),
            (methodName, tagNames) =>
            {
                var source = GenerateSourceWithTags(methodName, tagNames);
                var extractedTags = ExtractOperationTags(source);
                
                if (tagNames.Count == 0)
                {
                    // If no tags specified, should default to "Default"
                    var hasDefault = extractedTags.Count == 1 && extractedTags.Contains("Default");
                    return hasDefault
                        .Label($"Expected ['Default'] for method without tags, but got [{string.Join(", ", extractedTags)}]");
                }
                else
                {
                    // All specified tags should be present
                    var allTagsPresent = tagNames.All(t => extractedTags.Contains(t));
                    var correctCount = extractedTags.Count == tagNames.Count;
                    
                    return (allTagsPresent && correctCount)
                        .Label($"Expected tags [{string.Join(", ", tagNames)}], but got [{string.Join(", ", extractedTags)}]");
                }
            });
    }

    /// <summary>
    /// **Feature: openapi-completeness, Property 12: Tag Assignment (Multiple Tags)**
    /// **Validates: Requirements 5.3**
    /// 
    /// For any method with multiple [OpenApiTag] attributes, the generated operation SHALL be assigned to all specified tags.
    /// </summary>
    [Property(MaxTest = 100)]
    public Property MultipleTagAttributes_AssignsOperationToAllTags()
    {
        var methodNameGen = Gen.Elements("GetProduct", "CreateOrder", "UpdateUser");
        // Generate 2-4 distinct tags
        var multipleTagsGen = Gen.ListOf(Gen.Elements("Products", "Orders", "Users", "Admin", "Public", "Internal"))
            .Where(tags => tags.Distinct().Count() >= 2)
            .Select(tags => tags.Distinct().Take(4).ToList());
        
        return Prop.ForAll(
            methodNameGen.ToArbitrary(),
            multipleTagsGen.ToArbitrary(),
            (methodName, tagNames) =>
            {
                var source = GenerateSourceWithTags(methodName, tagNames);
                var extractedTags = ExtractOperationTags(source);
                
                // All specified tags should be present
                var allTagsPresent = tagNames.All(t => extractedTags.Contains(t));
                
                return allTagsPresent
                    .Label($"Expected all tags [{string.Join(", ", tagNames)}] to be present, but got [{string.Join(", ", extractedTags)}]");
            });
    }

    private string GenerateSourceWithTags(string methodName, List<string> tagNames)
    {
        var tagAttributes = tagNames.Count > 0
            ? string.Join("\n    ", tagNames.Select(t => $@"[Oproto.Lambda.OpenApi.Attributes.OpenApiTag(""{t}"")]"))
            : "";
        
        return $@"
using Amazon.Lambda.Annotations;
using Amazon.Lambda.Annotations.APIGateway;
using System.Threading.Tasks;

public class TestFunctions 
{{
    [LambdaFunction]
    [HttpApi(LambdaHttpMethod.Get, ""/items/{{id}}"")]
    {tagAttributes}
    public string {methodName}(string id) => ""test"";
}}";
    }

    private List<string> ExtractOperationTags(string source)
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
                return new List<string>();
            
            var jsonContent = ExtractOpenApiJson(outputCompilation);
            if (string.IsNullOrEmpty(jsonContent))
                return new List<string>();
            
            using var doc = JsonDocument.Parse(jsonContent);
            
            // Navigate to the first operation and get its tags
            if (doc.RootElement.TryGetProperty("paths", out var paths))
            {
                foreach (var path in paths.EnumerateObject())
                {
                    foreach (var operation in path.Value.EnumerateObject())
                    {
                        // Skip non-operation properties
                        if (operation.Name.StartsWith("x-") || operation.Name == "parameters")
                            continue;
                        
                        if (operation.Value.TryGetProperty("tags", out var tagsArray))
                        {
                            return tagsArray.EnumerateArray()
                                .Select(t => t.GetString())
                                .Where(t => t != null)
                                .ToList();
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


/// <summary>
/// Property-based tests for tag definition functionality.
/// </summary>
public class TagDefinitionPropertyTests
{
    /// <summary>
    /// **Feature: openapi-completeness, Property 13: Tag Definitions**
    /// **Validates: Requirements 5.2**
    /// 
    /// For any assembly with [OpenApiTagDefinition] attributes, the generated specification SHALL contain
    /// tag definitions with names and descriptions in the tags array.
    /// </summary>
    [Property(MaxTest = 100)]
    public Property TagDefinitionAttribute_IncludesTagsInSpecification()
    {
        var tagNameGen = Gen.Elements("Products", "Orders", "Users", "Admin", "Public");
        var descriptionGen = Gen.Elements(
            "Product management operations",
            "Order processing endpoints",
            "User account operations",
            "Administrative functions",
            "Public API endpoints");

        // Generate tag definitions as tuples
        var tagDefGen = Gen.Zip(tagNameGen, descriptionGen)
            .Select(t => (Name: t.Item1, Description: t.Item2));

        var tagDefsGen = Gen.ListOf(tagDefGen)
            .Select(defs => defs.DistinctBy(d => d.Name).Take(3).ToList());

        return Prop.ForAll(
            tagDefsGen.ToArbitrary(),
            tagDefs =>
            {
                var source = GenerateSourceWithTagDefinitions(tagDefs);
                var extractedTags = ExtractTagDefinitions(source);

                if (tagDefs.Count == 0)
                {
                    // No tag definitions, tags array may be empty or contain only operation tags
                    return true.Label("No tag definitions specified");
                }

                // All specified tag definitions should be present with descriptions
                var allTagsPresent = tagDefs.All(td =>
                    extractedTags.Any(et => et.Name == td.Name && et.Description == td.Description));

                return allTagsPresent
                    .Label($"Expected tag definitions [{string.Join(", ", tagDefs.Select(t => $"{t.Name}:{t.Description}"))}], " +
                           $"but got [{string.Join(", ", extractedTags.Select(t => $"{t.Name}:{t.Description}"))}]");
            });
    }

    private string GenerateSourceWithTagDefinitions(List<(string Name, string Description)> tagDefs)
    {
        var tagDefAttributes = tagDefs.Count > 0
            ? string.Join("\n", tagDefs.Select(td =>
                $@"[assembly: Oproto.Lambda.OpenApi.Attributes.OpenApiTagDefinition(""{td.Name}"", Description = ""{td.Description}"")]"))
            : "";

        // Use the first tag name for the operation if available
        var operationTag = tagDefs.Count > 0
            ? $@"[Oproto.Lambda.OpenApi.Attributes.OpenApiTag(""{tagDefs[0].Name}"")]"
            : "";

        return $@"
using Amazon.Lambda.Annotations;
using Amazon.Lambda.Annotations.APIGateway;
using System.Threading.Tasks;

{tagDefAttributes}

public class TestFunctions 
{{
    [LambdaFunction]
    [HttpApi(LambdaHttpMethod.Get, ""/items/{{id}}"")]
    {operationTag}
    public string GetItem(string id) => ""test"";
}}";
    }

    private List<(string Name, string Description)> ExtractTagDefinitions(string source)
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
                return new List<(string, string)>();

            var jsonContent = ExtractOpenApiJson(outputCompilation);
            if (string.IsNullOrEmpty(jsonContent))
                return new List<(string, string)>();

            using var doc = JsonDocument.Parse(jsonContent);

            var tags = new List<(string Name, string Description)>();

            // Get tags from the root tags array
            if (doc.RootElement.TryGetProperty("tags", out var tagsArray))
            {
                foreach (var tag in tagsArray.EnumerateArray())
                {
                    var name = tag.TryGetProperty("name", out var nameProp) ? nameProp.GetString() : null;
                    var description = tag.TryGetProperty("description", out var descProp) ? descProp.GetString() : null;

                    if (name != null)
                    {
                        tags.Add((name, description ?? ""));
                    }
                }
            }

            return tags;
        }
        catch
        {
            return new List<(string, string)>();
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
