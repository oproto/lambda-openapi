using System.Text.Json;
using FsCheck;
using FsCheck.Xunit;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Oproto.Lambda.OpenApi.SourceGenerator;

namespace Oproto.Lambda.OpenApi.Tests;

/// <summary>
/// Property-based tests for class-level OpenApiTag attribute support.
/// </summary>
public class ClassLevelTagPropertyTests
{
    /// <summary>
    /// **Feature: generator-bug-fixes, Property 4: Class-Level Tag Inheritance**
    /// **Validates: Requirements 3.1, 3.3**
    /// 
    /// For any operation method without method-level [OpenApiTag] attributes, if the containing class
    /// has [OpenApiTag] attributes, the operation SHALL be assigned all class-level tags.
    /// </summary>
    [Property(MaxTest = 100)]
    public Property ClassLevelTag_InheritedByMethodsWithoutTags()
    {
        var methodNameGen = Gen.Elements("GetProduct", "CreateOrder", "UpdateUser", "DeleteItem", "ListCustomers");
        var classTagsGen = Gen.ListOf(Gen.Elements("Products", "Orders", "Users", "Admin", "Public"))
            .Select(tags => tags.Distinct().ToList())
            .Where(tags => tags.Count > 0); // Ensure at least one class-level tag

        return Prop.ForAll(
            methodNameGen.ToArbitrary(),
            classTagsGen.ToArbitrary(),
            (methodName, classTags) =>
            {
                var source = GenerateSourceWithClassLevelTags(methodName, classTags, methodTags: new List<string>());
                var extractedTags = ExtractOperationTags(source);

                // All class-level tags should be inherited
                var allTagsPresent = classTags.All(t => extractedTags.Contains(t));
                var correctCount = extractedTags.Count == classTags.Count;

                return (allTagsPresent && correctCount)
                    .Label($"Expected class tags [{string.Join(", ", classTags)}] to be inherited, but got [{string.Join(", ", extractedTags)}]");
            });
    }

    /// <summary>
    /// **Feature: generator-bug-fixes, Property 4: Class-Level Tag Inheritance (Multiple Tags)**
    /// **Validates: Requirements 3.3**
    /// 
    /// For any class with multiple [OpenApiTag] attributes, all class-level tags SHALL be applied
    /// to operations without method-level tags.
    /// </summary>
    [Property(MaxTest = 100)]
    public Property MultipleClassLevelTags_AllInheritedByMethods()
    {
        var methodNameGen = Gen.Elements("GetProduct", "CreateOrder", "UpdateUser");
        // Generate 2-4 distinct class-level tags
        var multipleClassTagsGen = Gen.ListOf(Gen.Elements("Products", "Orders", "Users", "Admin", "Public", "Internal"))
            .Where(tags => tags.Distinct().Count() >= 2)
            .Select(tags => tags.Distinct().Take(4).ToList());

        return Prop.ForAll(
            methodNameGen.ToArbitrary(),
            multipleClassTagsGen.ToArbitrary(),
            (methodName, classTags) =>
            {
                var source = GenerateSourceWithClassLevelTags(methodName, classTags, methodTags: new List<string>());
                var extractedTags = ExtractOperationTags(source);

                // All class-level tags should be present
                var allTagsPresent = classTags.All(t => extractedTags.Contains(t));

                return allTagsPresent
                    .Label($"Expected all class tags [{string.Join(", ", classTags)}] to be present, but got [{string.Join(", ", extractedTags)}]");
            });
    }

    /// <summary>
    /// **Feature: generator-bug-fixes, Property 5: Method-Level Tag Precedence**
    /// **Validates: Requirements 3.2**
    /// 
    /// For any operation method with method-level [OpenApiTag] attributes, the operation SHALL use
    /// only the method-level tags, ignoring any class-level tags.
    /// </summary>
    [Property(MaxTest = 100)]
    public Property MethodLevelTags_TakePrecedenceOverClassTags()
    {
        var methodNameGen = Gen.Elements("GetProduct", "CreateOrder", "UpdateUser", "DeleteItem");
        var classTagsGen = Gen.ListOf(Gen.Elements("ClassTag1", "ClassTag2", "ClassTag3"))
            .Select(tags => tags.Distinct().ToList())
            .Where(tags => tags.Count > 0);
        var methodTagsGen = Gen.ListOf(Gen.Elements("MethodTag1", "MethodTag2", "MethodTag3"))
            .Select(tags => tags.Distinct().ToList())
            .Where(tags => tags.Count > 0);

        return Prop.ForAll(
            methodNameGen.ToArbitrary(),
            classTagsGen.ToArbitrary(),
            methodTagsGen.ToArbitrary(),
            (methodName, classTags, methodTags) =>
            {
                var source = GenerateSourceWithClassLevelTags(methodName, classTags, methodTags);
                var extractedTags = ExtractOperationTags(source);

                // Only method-level tags should be present
                var allMethodTagsPresent = methodTags.All(t => extractedTags.Contains(t));
                var noClassTagsPresent = !classTags.Any(t => extractedTags.Contains(t));
                var correctCount = extractedTags.Count == methodTags.Count;

                return (allMethodTagsPresent && noClassTagsPresent && correctCount)
                    .Label($"Expected only method tags [{string.Join(", ", methodTags)}], " +
                           $"but got [{string.Join(", ", extractedTags)}]. " +
                           $"Class tags [{string.Join(", ", classTags)}] should NOT be present.");
            });
    }

    /// <summary>
    /// Tests that when neither class nor method has tags, the operation defaults to "Default" tag.
    /// </summary>
    [Property(MaxTest = 100)]
    public Property NoTags_DefaultsToDefaultTag()
    {
        var methodNameGen = Gen.Elements("GetProduct", "CreateOrder", "UpdateUser", "DeleteItem", "ListCustomers");

        return Prop.ForAll(
            methodNameGen.ToArbitrary(),
            methodName =>
            {
                var source = GenerateSourceWithClassLevelTags(methodName, classTags: new List<string>(), methodTags: new List<string>());
                var extractedTags = ExtractOperationTags(source);

                var hasDefault = extractedTags.Count == 1 && extractedTags.Contains("Default");

                return hasDefault
                    .Label($"Expected ['Default'] when no tags specified, but got [{string.Join(", ", extractedTags)}]");
            });
    }

    private string GenerateSourceWithClassLevelTags(string methodName, List<string> classTags, List<string> methodTags)
    {
        var classTagAttributes = classTags.Count > 0
            ? string.Join("\n", classTags.Select(t => $@"[Oproto.Lambda.OpenApi.Attributes.OpenApiTag(""{t}"")]"))
            : "";

        var methodTagAttributes = methodTags.Count > 0
            ? string.Join("\n    ", methodTags.Select(t => $@"[Oproto.Lambda.OpenApi.Attributes.OpenApiTag(""{t}"")]"))
            : "";

        return $@"
using Amazon.Lambda.Annotations;
using Amazon.Lambda.Annotations.APIGateway;
using System.Threading.Tasks;

{classTagAttributes}
public class TestFunctions 
{{
    [LambdaFunction]
    [HttpApi(LambdaHttpMethod.Get, ""/items/{{id}}"")]
    {methodTagAttributes}
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
