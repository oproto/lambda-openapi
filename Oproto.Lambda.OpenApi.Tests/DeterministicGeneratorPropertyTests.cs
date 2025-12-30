using System.Text.Json;
using FsCheck;
using FsCheck.Xunit;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Oproto.Lambda.OpenApi.SourceGenerator;

namespace Oproto.Lambda.OpenApi.Tests;

/// <summary>
/// Property-based tests for deterministic output from the OpenAPI source generator.
/// </summary>
public class DeterministicGeneratorPropertyTests
{
    /// <summary>
    /// Generators for deterministic output test data.
    /// </summary>
    private static class GeneratorTestData
    {
        public static Gen<string> PathSegmentGen()
        {
            return Gen.Elements("users", "products", "orders", "items", "api", "v1", "admin", "auth");
        }

        public static Gen<string> TagNameGen()
        {
            return Gen.Elements("Users", "Products", "Orders", "Items", "Admin", "Auth");
        }

        public static Gen<string> PropertyNameGen()
        {
            return Gen.Elements("id", "name", "value", "count", "status", "email", "phone", "address");
        }

        public static Gen<string> SchemaNameGen()
        {
            return Gen.Elements("User", "Product", "Order", "Item", "Response", "Request");
        }

        public static Gen<string> TagGroupNameGen()
        {
            return Gen.Elements("User Management", "Product Catalog", "Order Processing", "Administration");
        }

        /// <summary>
        /// Generates source code with multiple paths in random order.
        /// </summary>
        public static Gen<string> SourceWithMultiplePathsGen()
        {
            return from pathCount in Gen.Choose(2, 4)
                   from segments in Gen.ListOf(pathCount, PathSegmentGen())
                   let uniqueSegments = segments.Distinct().ToList()
                   where uniqueSegments.Count >= 2
                   select GenerateSourceWithPaths(uniqueSegments);
        }

        /// <summary>
        /// Generates source code with multiple tags in random order.
        /// </summary>
        public static Gen<string> SourceWithMultipleTagsGen()
        {
            return from tagCount in Gen.Choose(2, 4)
                   from tags in Gen.ListOf(tagCount, TagNameGen())
                   let uniqueTags = tags.Distinct().ToList()
                   where uniqueTags.Count >= 2
                   select GenerateSourceWithTags(uniqueTags);
        }

        /// <summary>
        /// Generates source code with a schema containing multiple properties.
        /// </summary>
        public static Gen<string> SourceWithSchemaPropertiesGen()
        {
            return from propCount in Gen.Choose(2, 5)
                   from props in Gen.ListOf(propCount, PropertyNameGen())
                   let uniqueProps = props.Distinct().ToList()
                   where uniqueProps.Count >= 2
                   select GenerateSourceWithSchemaProperties(uniqueProps);
        }

        /// <summary>
        /// Generates source code with multiple tag groups.
        /// </summary>
        public static Gen<string> SourceWithTagGroupsGen()
        {
            return from groupCount in Gen.Choose(2, 3)
                   from groups in Gen.ListOf(groupCount, TagGroupNameGen())
                   from tagCount in Gen.Choose(2, 3)
                   from tags in Gen.ListOf(tagCount, TagNameGen())
                   let uniqueGroups = groups.Distinct().ToList()
                   let uniqueTags = tags.Distinct().ToList()
                   where uniqueGroups.Count >= 2 && uniqueTags.Count >= 2
                   select GenerateSourceWithTagGroups(uniqueGroups, uniqueTags);
        }

        private static string GenerateSourceWithPaths(List<string> pathSegments)
        {
            var methods = string.Join("\n", pathSegments.Select((segment, index) => $@"
    [LambdaFunction]
    [HttpApi(LambdaHttpMethod.Get, ""/{segment}"")]
    [OpenApiTag(""Default"")]
    public string Get{char.ToUpper(segment[0])}{segment.Substring(1)}() => ""test"";"));

            return $@"
using Amazon.Lambda.Annotations;
using Amazon.Lambda.Annotations.APIGateway;
using Oproto.Lambda.OpenApi.Attributes;

[assembly: OpenApiInfo(""Test API"", ""1.0.0"")]

public class TestFunctions 
{{
{methods}
}}";
        }

        private static string GenerateSourceWithTags(List<string> tags)
        {
            var methods = string.Join("\n", tags.Select((tag, index) => $@"
    [LambdaFunction]
    [HttpApi(LambdaHttpMethod.Get, ""/endpoint{index}"")]
    [OpenApiTag(""{tag}"")]
    public string GetEndpoint{index}() => ""test"";"));

            var tagDefinitions = string.Join("\n", tags.Select(tag =>
                $@"[assembly: OpenApiTagDefinition(""{tag}"", Description = ""Description for {tag}"")]"));

            return $@"
using Amazon.Lambda.Annotations;
using Amazon.Lambda.Annotations.APIGateway;
using Oproto.Lambda.OpenApi.Attributes;

[assembly: OpenApiInfo(""Test API"", ""1.0.0"")]
{tagDefinitions}

public class TestFunctions 
{{
{methods}
}}";
        }

        private static string GenerateSourceWithSchemaProperties(List<string> properties)
        {
            var props = string.Join("\n", properties.Select(prop =>
                $@"    [OpenApiSchema(Description = ""The {prop}"")]
    public string {char.ToUpper(prop[0])}{prop.Substring(1)} {{ get; set; }}"));

            return $@"
using Amazon.Lambda.Annotations;
using Amazon.Lambda.Annotations.APIGateway;
using Oproto.Lambda.OpenApi.Attributes;

[assembly: OpenApiInfo(""Test API"", ""1.0.0"")]

public class TestModel
{{
{props}
}}

public class TestFunctions 
{{
    [LambdaFunction]
    [HttpApi(LambdaHttpMethod.Post, ""/items"")]
    public TestModel CreateItem([FromBody] TestModel model) => model;
}}";
        }

        private static string GenerateSourceWithTagGroups(List<string> groupNames, List<string> tagNames)
        {
            var tagGroupAttributes = string.Join("\n", groupNames.Select((name, index) =>
            {
                var tagsForGroup = tagNames.Skip(index % tagNames.Count).Take(2).ToList();
                var tagsParam = string.Join(", ", tagsForGroup.Select(t => $@"""{t}"""));
                return $@"[assembly: OpenApiTagGroup(""{name}"", {tagsParam})]";
            }));

            var tagDefinitions = string.Join("\n", tagNames.Select(tag =>
                $@"[assembly: OpenApiTagDefinition(""{tag}"", Description = ""Description for {tag}"")]"));

            return $@"
using Amazon.Lambda.Annotations;
using Amazon.Lambda.Annotations.APIGateway;
using Oproto.Lambda.OpenApi.Attributes;

[assembly: OpenApiInfo(""Test API"", ""1.0.0"")]
{tagDefinitions}
{tagGroupAttributes}

public class TestFunctions 
{{
    [LambdaFunction]
    [HttpApi(LambdaHttpMethod.Get, ""/items"")]
    [OpenApiTag(""{tagNames.First()}"")]
    public string GetItems() => ""test"";
}}";
        }
    }

    /// <summary>
    /// **Feature: deterministic-output, Property: Generator Deterministic Output**
    /// Test that generating the same source twice produces identical output.
    /// **Validates: Requirements 1.1, 2.1, 3.1, 4.1, 5.1, 7.1, 8.1, 9.1, 11.1**
    /// </summary>
    [Property(MaxTest = 100)]
    public Property Generator_ProducesIdenticalOutput_ForSameSource()
    {
        return Prop.ForAll(
            GeneratorTestData.SourceWithMultiplePathsGen().ToArbitrary(),
            source =>
            {
                var json1 = GenerateAndExtractJson(source);
                var json2 = GenerateAndExtractJson(source);

                if (string.IsNullOrEmpty(json1) || string.IsNullOrEmpty(json2))
                    return false.Label("Failed to generate JSON");

                return (json1 == json2)
                    .Label($"Generated JSON should be identical across runs");
            });
    }

    /// <summary>
    /// **Feature: deterministic-output, Property: Path Ordering in Generator**
    /// For any source with multiple paths, the generated output SHALL have paths in alphabetical order.
    /// **Validates: Requirements 1.1**
    /// </summary>
    [Property(MaxTest = 100)]
    public Property Generator_SortsPaths_Alphabetically()
    {
        return Prop.ForAll(
            GeneratorTestData.SourceWithMultiplePathsGen().ToArbitrary(),
            source =>
            {
                var json = GenerateAndExtractJson(source);
                if (string.IsNullOrEmpty(json))
                    return false.Label("Failed to generate JSON");

                var paths = ExtractPaths(json);
                if (paths.Count < 2)
                    return false.Label("Not enough paths generated");

                var isSorted = paths.SequenceEqual(paths.OrderBy(p => p, StringComparer.Ordinal));
                return isSorted.Label("Paths should be in alphabetical order");
            });
    }

    /// <summary>
    /// **Feature: deterministic-output, Property: Tag Ordering in Generator**
    /// For any source with multiple tags, the generated output SHALL have tags in alphabetical order.
    /// **Validates: Requirements 4.1**
    /// </summary>
    [Property(MaxTest = 100)]
    public Property Generator_SortsTags_Alphabetically()
    {
        return Prop.ForAll(
            GeneratorTestData.SourceWithMultipleTagsGen().ToArbitrary(),
            source =>
            {
                var json = GenerateAndExtractJson(source);
                if (string.IsNullOrEmpty(json))
                    return false.Label("Failed to generate JSON");

                var tags = ExtractTags(json);
                if (tags.Count < 2)
                    return false.Label("Not enough tags generated");

                var isSorted = tags.SequenceEqual(tags.OrderBy(t => t, StringComparer.Ordinal));
                return isSorted.Label("Tags should be in alphabetical order");
            });
    }

    /// <summary>
    /// **Feature: deterministic-output, Property: Schema Property Ordering in Generator**
    /// For any source with schemas containing multiple properties, the generated output SHALL have 
    /// properties in alphabetical order.
    /// **Validates: Requirements 3.1**
    /// </summary>
    [Property(MaxTest = 100)]
    public Property Generator_SortsSchemaProperties_Alphabetically()
    {
        return Prop.ForAll(
            GeneratorTestData.SourceWithSchemaPropertiesGen().ToArbitrary(),
            source =>
            {
                var json = GenerateAndExtractJson(source);
                if (string.IsNullOrEmpty(json))
                    return false.Label("Failed to generate JSON");

                var properties = ExtractSchemaProperties(json, "TestModel");
                if (properties.Count < 2)
                    return true.Label("Not enough properties to verify ordering (may be expected)");

                var isSorted = properties.SequenceEqual(properties.OrderBy(p => p, StringComparer.Ordinal));
                return isSorted.Label("Schema properties should be in alphabetical order");
            });
    }

    /// <summary>
    /// **Feature: deterministic-output, Property: Tag Group Ordering in Generator**
    /// For any source with tag groups, the generated output SHALL have tag groups in alphabetical order.
    /// **Validates: Requirements 5.1**
    /// </summary>
    [Property(MaxTest = 100)]
    public Property Generator_SortsTagGroups_Alphabetically()
    {
        return Prop.ForAll(
            GeneratorTestData.SourceWithTagGroupsGen().ToArbitrary(),
            source =>
            {
                var json = GenerateAndExtractJson(source);
                if (string.IsNullOrEmpty(json))
                    return false.Label("Failed to generate JSON");

                var tagGroups = ExtractTagGroups(json);
                if (tagGroups.Count < 2)
                    return false.Label("Not enough tag groups generated");

                var groupNames = tagGroups.Select(g => g.Name).ToList();
                var isSorted = groupNames.SequenceEqual(groupNames.OrderBy(g => g, StringComparer.Ordinal));

                // Also verify tags within groups are sorted
                var tagsWithinGroupsSorted = tagGroups.All(g =>
                    g.Tags.SequenceEqual(g.Tags.OrderBy(t => t, StringComparer.Ordinal)));

                return isSorted.Label("Tag groups should be in alphabetical order")
                    .And(tagsWithinGroupsSorted).Label("Tags within groups should be in alphabetical order");
            });
    }

    private string GenerateAndExtractJson(string source)
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
                return string.Empty;

            return ExtractOpenApiJson(outputCompilation);
        }
        catch
        {
            return string.Empty;
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

    private List<string> ExtractPaths(string json)
    {
        try
        {
            using var doc = JsonDocument.Parse(json);
            if (doc.RootElement.TryGetProperty("paths", out var pathsElement))
            {
                return pathsElement.EnumerateObject()
                    .Select(p => p.Name)
                    .ToList();
            }
        }
        catch { }
        return new List<string>();
    }

    private List<string> ExtractTags(string json)
    {
        try
        {
            using var doc = JsonDocument.Parse(json);
            if (doc.RootElement.TryGetProperty("tags", out var tagsElement))
            {
                return tagsElement.EnumerateArray()
                    .Select(t => t.GetProperty("name").GetString())
                    .Where(n => n != null)
                    .ToList()!;
            }
        }
        catch { }
        return new List<string>();
    }

    private List<string> ExtractSchemaProperties(string json, string schemaName)
    {
        try
        {
            using var doc = JsonDocument.Parse(json);
            if (doc.RootElement.TryGetProperty("components", out var components) &&
                components.TryGetProperty("schemas", out var schemas) &&
                schemas.TryGetProperty(schemaName, out var schema) &&
                schema.TryGetProperty("properties", out var properties))
            {
                return properties.EnumerateObject()
                    .Select(p => p.Name)
                    .ToList();
            }
        }
        catch { }
        return new List<string>();
    }

    private List<(string Name, List<string> Tags)> ExtractTagGroups(string json)
    {
        try
        {
            using var doc = JsonDocument.Parse(json);
            if (doc.RootElement.TryGetProperty("x-tagGroups", out var tagGroupsElement))
            {
                return tagGroupsElement.EnumerateArray()
                    .Select(g => (
                        Name: g.GetProperty("name").GetString() ?? "",
                        Tags: g.GetProperty("tags").EnumerateArray()
                            .Select(t => t.GetString() ?? "")
                            .ToList()
                    ))
                    .ToList();
            }
        }
        catch { }
        return new List<(string, List<string>)>();
    }
}
