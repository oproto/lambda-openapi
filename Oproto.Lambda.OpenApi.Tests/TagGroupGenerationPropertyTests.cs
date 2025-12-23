using System.Text.Json;
using FsCheck;
using FsCheck.Xunit;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Oproto.Lambda.OpenApi.SourceGenerator;

namespace Oproto.Lambda.OpenApi.Tests;

/// <summary>
/// Property-based tests for tag group generation functionality.
/// </summary>
public class TagGroupGenerationPropertyTests
{
    /// <summary>
    /// **Feature: tag-groups-extension, Property 1: Tag group attribute parsing**
    /// **Validates: Requirements 1.2**
    /// 
    /// For any OpenApiTagGroupAttribute with a valid name and array of tags, 
    /// the Source_Generator SHALL correctly extract the group name and all associated tags.
    /// </summary>
    [Property(MaxTest = 100)]
    public Property TagGroupAttribute_ParsesNameAndTags()
    {
        var groupNameGen = Gen.Elements("User Management", "Products", "Orders", "Admin", "Public API");
        var tagNamesGen = Gen.ListOf(Gen.Elements("Users", "Auth", "Products", "Orders", "Admin", "Public"))
            .Select(tags => tags.Distinct().Take(4).ToList());

        return Prop.ForAll(
            groupNameGen.ToArbitrary(),
            tagNamesGen.ToArbitrary(),
            (groupName, tagNames) =>
            {
                var source = GenerateSourceWithTagGroup(groupName, tagNames);
                var extractedGroups = ExtractTagGroups(source);

                if (extractedGroups.Count == 0)
                    return false.Label("No tag groups extracted");

                var group = extractedGroups[0];
                var nameMatches = group.Name == groupName;
                var tagsMatch = tagNames.All(t => group.Tags.Contains(t)) && group.Tags.Count == tagNames.Count;

                return (nameMatches && tagsMatch)
                    .Label($"Expected group '{groupName}' with tags [{string.Join(", ", tagNames)}], " +
                           $"but got '{group.Name}' with tags [{string.Join(", ", group.Tags)}]");
            });
    }

    /// <summary>
    /// **Feature: tag-groups-extension, Property 2: Tag group order preservation**
    /// **Validates: Requirements 1.3, 2.4**
    /// 
    /// For any sequence of OpenApiTagGroupAttribute attributes applied to an assembly, 
    /// the Source_Generator SHALL output the tag groups in the same order as they are defined.
    /// </summary>
    [Property(MaxTest = 100)]
    public Property TagGroupAttributes_PreserveOrder()
    {
        // Generate 2-4 distinct group names
        var groupNamesGen = Gen.ListOf(Gen.Elements("Group A", "Group B", "Group C", "Group D", "Group E"))
            .Where(names => names.Distinct().Count() >= 2)
            .Select(names => names.Distinct().Take(4).ToList());

        return Prop.ForAll(
            groupNamesGen.ToArbitrary(),
            groupNames =>
            {
                var source = GenerateSourceWithMultipleTagGroups(groupNames);
                var extractedGroups = ExtractTagGroups(source);

                if (extractedGroups.Count != groupNames.Count)
                    return false.Label($"Expected {groupNames.Count} groups, but got {extractedGroups.Count}");

                // Check order is preserved
                var orderPreserved = true;
                for (int i = 0; i < groupNames.Count; i++)
                {
                    if (extractedGroups[i].Name != groupNames[i])
                    {
                        orderPreserved = false;
                        break;
                    }
                }

                return orderPreserved
                    .Label($"Expected order [{string.Join(", ", groupNames)}], " +
                           $"but got [{string.Join(", ", extractedGroups.Select(g => g.Name))}]");
            });
    }

    /// <summary>
    /// **Feature: tag-groups-extension, Property 3: Tag group output presence**
    /// **Validates: Requirements 2.1**
    /// 
    /// For any assembly with one or more OpenApiTagGroupAttribute attributes, 
    /// the generated OpenAPI document SHALL contain an x-tagGroups extension at the root level.
    /// </summary>
    [Property(MaxTest = 100)]
    public Property TagGroupAttribute_ProducesExtension()
    {
        var groupNameGen = Gen.Elements("User Management", "Products", "Orders");
        var tagNamesGen = Gen.ListOf(Gen.Elements("Users", "Auth", "Products"))
            .Select(tags => tags.Distinct().Take(3).ToList());

        return Prop.ForAll(
            groupNameGen.ToArbitrary(),
            tagNamesGen.ToArbitrary(),
            (groupName, tagNames) =>
            {
                var source = GenerateSourceWithTagGroup(groupName, tagNames);
                var hasExtension = HasTagGroupsExtension(source);

                return hasExtension
                    .Label($"Expected x-tagGroups extension to be present for group '{groupName}'");
            });
    }

    /// <summary>
    /// **Feature: tag-groups-extension, Property 4: Tag group JSON structure**
    /// **Validates: Requirements 2.2, 5.1, 5.2**
    /// 
    /// For any tag group in the output, the JSON object SHALL contain a name property (string) 
    /// and a tags property (array of strings).
    /// </summary>
    [Property(MaxTest = 100)]
    public Property TagGroupOutput_HasCorrectJsonStructure()
    {
        var groupNameGen = Gen.Elements("User Management", "Products", "Orders");
        var tagNamesGen = Gen.ListOf(Gen.Elements("Users", "Auth", "Products", "Orders"))
            .Where(tags => tags.Count() > 0)
            .Select(tags => tags.Distinct().Take(3).ToList());

        return Prop.ForAll(
            groupNameGen.ToArbitrary(),
            tagNamesGen.ToArbitrary(),
            (groupName, tagNames) =>
            {
                var source = GenerateSourceWithTagGroup(groupName, tagNames);
                var structureValid = ValidateTagGroupJsonStructure(source);

                return structureValid
                    .Label($"Tag group JSON structure is invalid for group '{groupName}'");
            });
    }

    /// <summary>
    /// Verifies that when no tag groups are defined, the x-tagGroups extension is not present.
    /// **Validates: Requirements 2.3**
    /// </summary>
    [Fact]
    public void NoTagGroups_NoExtension()
    {
        var source = @"
using Amazon.Lambda.Annotations;
using Amazon.Lambda.Annotations.APIGateway;

[assembly: Oproto.Lambda.OpenApi.Attributes.OpenApiInfo(""Test API"", ""1.0.0"")]

public class TestFunctions 
{
    [LambdaFunction]
    [HttpApi(LambdaHttpMethod.Get, ""/items/{id}"")]
    public string GetItem(string id) => ""test"";
}";

        var hasExtension = HasTagGroupsExtension(source);
        Assert.False(hasExtension, "x-tagGroups extension should not be present when no tag groups are defined");
    }

    private string GenerateSourceWithTagGroup(string groupName, List<string> tagNames)
    {
        var tagsParam = tagNames.Count > 0
            ? ", " + string.Join(", ", tagNames.Select(t => $@"""{t}"""))
            : "";

        return $@"
using Amazon.Lambda.Annotations;
using Amazon.Lambda.Annotations.APIGateway;

[assembly: Oproto.Lambda.OpenApi.Attributes.OpenApiInfo(""Test API"", ""1.0.0"")]
[assembly: Oproto.Lambda.OpenApi.Attributes.OpenApiTagGroup(""{groupName}""{tagsParam})]

public class TestFunctions 
{{
    [LambdaFunction]
    [HttpApi(LambdaHttpMethod.Get, ""/items/{{id}}"")]
    public string GetItem(string id) => ""test"";
}}";
    }

    private string GenerateSourceWithMultipleTagGroups(List<string> groupNames)
    {
        var tagGroupAttributes = string.Join("\n", groupNames.Select((name, index) =>
            $@"[assembly: Oproto.Lambda.OpenApi.Attributes.OpenApiTagGroup(""{name}"", ""Tag{index}A"", ""Tag{index}B"")]"));

        return $@"
using Amazon.Lambda.Annotations;
using Amazon.Lambda.Annotations.APIGateway;

[assembly: Oproto.Lambda.OpenApi.Attributes.OpenApiInfo(""Test API"", ""1.0.0"")]
{tagGroupAttributes}

public class TestFunctions 
{{
    [LambdaFunction]
    [HttpApi(LambdaHttpMethod.Get, ""/items/{{id}}"")]
    public string GetItem(string id) => ""test"";
}}";
    }

    private List<(string Name, List<string> Tags)> ExtractTagGroups(string source)
    {
        try
        {
            var jsonContent = GenerateAndExtractJson(source);
            if (string.IsNullOrEmpty(jsonContent))
                return new List<(string, List<string>)>();

            using var doc = JsonDocument.Parse(jsonContent);

            var groups = new List<(string Name, List<string> Tags)>();

            if (doc.RootElement.TryGetProperty("x-tagGroups", out var tagGroupsArray))
            {
                foreach (var group in tagGroupsArray.EnumerateArray())
                {
                    var name = group.TryGetProperty("name", out var nameProp) ? nameProp.GetString() : null;
                    var tags = new List<string>();

                    if (group.TryGetProperty("tags", out var tagsArray))
                    {
                        tags.AddRange(tagsArray.EnumerateArray()
                            .Select(t => t.GetString())
                            .Where(t => t != null)!);
                    }

                    if (name != null)
                    {
                        groups.Add((name, tags));
                    }
                }
            }

            return groups;
        }
        catch
        {
            return new List<(string, List<string>)>();
        }
    }

    private bool HasTagGroupsExtension(string source)
    {
        try
        {
            var jsonContent = GenerateAndExtractJson(source);
            if (string.IsNullOrEmpty(jsonContent))
                return false;

            using var doc = JsonDocument.Parse(jsonContent);
            return doc.RootElement.TryGetProperty("x-tagGroups", out _);
        }
        catch
        {
            return false;
        }
    }

    private bool ValidateTagGroupJsonStructure(string source)
    {
        try
        {
            var jsonContent = GenerateAndExtractJson(source);
            if (string.IsNullOrEmpty(jsonContent))
                return false;

            using var doc = JsonDocument.Parse(jsonContent);

            if (!doc.RootElement.TryGetProperty("x-tagGroups", out var tagGroupsArray))
                return false;

            foreach (var group in tagGroupsArray.EnumerateArray())
            {
                // Must have "name" as string
                if (!group.TryGetProperty("name", out var nameProp) || nameProp.ValueKind != JsonValueKind.String)
                    return false;

                // Must have "tags" as array
                if (!group.TryGetProperty("tags", out var tagsArray) || tagsArray.ValueKind != JsonValueKind.Array)
                    return false;

                // All tags must be strings
                foreach (var tag in tagsArray.EnumerateArray())
                {
                    if (tag.ValueKind != JsonValueKind.String)
                        return false;
                }
            }

            return true;
        }
        catch
        {
            return false;
        }
    }

    private string GenerateAndExtractJson(string source)
    {
        var compilation = CompilerHelper.CreateCompilation(source);
        var generator = new OpenApiSpecGenerator();

        var driver = CSharpGeneratorDriver.Create(generator);
        driver.RunGeneratorsAndUpdateCompilation(compilation,
            out var outputCompilation,
            out var diagnostics);

        if (diagnostics.Any(d => d.Severity == DiagnosticSeverity.Error))
            return string.Empty;

        return ExtractOpenApiJson(outputCompilation);
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
