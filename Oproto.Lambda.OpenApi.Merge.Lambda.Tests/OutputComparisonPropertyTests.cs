using FsCheck;
using FsCheck.Xunit;
using Microsoft.Extensions.Logging;
using Moq;
using Oproto.Lambda.OpenApi.Merge.Lambda.Services;
using System.Text.Json;
using System.Text.Json.Nodes;

namespace Oproto.Lambda.OpenApi.Merge.Lambda.Tests;

/// <summary>
/// Property-based tests for output comparison normalization.
/// Feature: lambda-merge-tool, Property 5: Output Comparison Normalization
/// **Validates: Requirements 5.2, 5.5**
/// </summary>
public class OutputComparisonPropertyTests
{
    /// <summary>
    /// Generators for output comparison test data.
    /// </summary>
    private static class OutputComparisonGenerators
    {
        /// <summary>
        /// Generates valid OpenAPI-like JSON objects with various structures.
        /// </summary>
        public static Gen<JsonObject> OpenApiJsonGen()
        {
            return from title in Gen.Elements("Test API", "My API", "Sample API", "Product API")
                   from version in Gen.Elements("1.0.0", "2.0.0", "1.1.0", "3.0.0")
                   from pathCount in Gen.Choose(1, 3)
                   from paths in Gen.ListOf(pathCount, PathGen())
                   from schemaCount in Gen.Choose(0, 2)
                   from schemas in Gen.ListOf(schemaCount, SchemaGen())
                   select BuildOpenApiJson(title, version, paths.ToList(), schemas.ToList());
        }

        /// <summary>
        /// Generates path entries.
        /// </summary>
        private static Gen<(string Path, string Method, string Summary)> PathGen()
        {
            return from path in Gen.Elements("/users", "/products", "/orders", "/items", "/api/v1/data")
                   from method in Gen.Elements("get", "post", "put", "delete")
                   from summary in Gen.Elements("Get resource", "Create resource", "Update resource", "Delete resource")
                   select (path, method, summary);
        }

        /// <summary>
        /// Generates schema entries.
        /// </summary>
        private static Gen<(string Name, string Type)> SchemaGen()
        {
            return from name in Gen.Elements("User", "Product", "Order", "Item", "Response")
                   from type in Gen.Elements("object", "string", "integer", "array")
                   select (name, type);
        }

        private static JsonObject BuildOpenApiJson(
            string title,
            string version,
            List<(string Path, string Method, string Summary)> paths,
            List<(string Name, string Type)> schemas)
        {
            var doc = new JsonObject
            {
                ["openapi"] = "3.0.0",
                ["info"] = new JsonObject
                {
                    ["title"] = title,
                    ["version"] = version
                }
            };

            // Add paths
            var pathsObj = new JsonObject();
            foreach (var (path, method, summary) in paths)
            {
                if (!pathsObj.ContainsKey(path))
                {
                    pathsObj[path] = new JsonObject();
                }
                var pathItem = pathsObj[path]!.AsObject();
                pathItem[method] = new JsonObject
                {
                    ["summary"] = summary,
                    ["responses"] = new JsonObject
                    {
                        ["200"] = new JsonObject
                        {
                            ["description"] = "Success"
                        }
                    }
                };
            }
            doc["paths"] = pathsObj;

            // Add schemas if any
            if (schemas.Count > 0)
            {
                var schemasObj = new JsonObject();
                foreach (var (name, type) in schemas)
                {
                    schemasObj[name] = new JsonObject
                    {
                        ["type"] = type
                    };
                }
                doc["components"] = new JsonObject
                {
                    ["schemas"] = schemasObj
                };
            }

            return doc;
        }

        /// <summary>
        /// Generates a test case with original JSON and a formatting variation.
        /// </summary>
        public static Gen<OutputComparisonTestCase> TestCaseGen()
        {
            return from jsonObj in OpenApiJsonGen()
                   from variationType in Gen.Elements(
                       FormattingVariation.ReorderProperties,
                       FormattingVariation.ChangeWhitespace,
                       FormattingVariation.MinifyJson,
                       FormattingVariation.PrettyPrint)
                   let originalJson = jsonObj.ToJsonString(new JsonSerializerOptions { WriteIndented = true })
                   let variedJson = ApplyVariation(jsonObj, variationType)
                   select new OutputComparisonTestCase(originalJson, variedJson, variationType);
        }

        private static string ApplyVariation(JsonObject original, FormattingVariation variation)
        {
            return variation switch
            {
                FormattingVariation.ReorderProperties => ReorderProperties(original),
                FormattingVariation.ChangeWhitespace => ChangeWhitespace(original),
                FormattingVariation.MinifyJson => original.ToJsonString(new JsonSerializerOptions { WriteIndented = false }),
                FormattingVariation.PrettyPrint => original.ToJsonString(new JsonSerializerOptions { WriteIndented = true }),
                _ => original.ToJsonString()
            };
        }

        private static string ReorderProperties(JsonObject original)
        {
            // Reverse the order of properties at the top level
            var reversed = new JsonObject();
            var properties = original.ToList();
            properties.Reverse();
            foreach (var kvp in properties)
            {
                reversed[kvp.Key] = JsonNode.Parse(kvp.Value?.ToJsonString() ?? "null");
            }
            return reversed.ToJsonString(new JsonSerializerOptions { WriteIndented = true });
        }

        private static string ChangeWhitespace(JsonObject original)
        {
            // Add extra whitespace by using different indentation
            var json = original.ToJsonString(new JsonSerializerOptions { WriteIndented = true });
            // Add extra newlines and spaces
            return json.Replace("\n", "\n\n").Replace("  ", "    ");
        }
    }

    /// <summary>
    /// Types of formatting variations to apply.
    /// </summary>
    public enum FormattingVariation
    {
        ReorderProperties,
        ChangeWhitespace,
        MinifyJson,
        PrettyPrint
    }

    /// <summary>
    /// Test case for output comparison testing.
    /// </summary>
    public record OutputComparisonTestCase(
        string OriginalJson,
        string VariedJson,
        FormattingVariation VariationType);


    private readonly Mock<ILogger<OutputComparer>> _mockLogger;
    private readonly OutputComparer _outputComparer;

    public OutputComparisonPropertyTests()
    {
        _mockLogger = new Mock<ILogger<OutputComparer>>();
        _outputComparer = new OutputComparer(_mockLogger.Object);
    }

    /// <summary>
    /// Feature: lambda-merge-tool, Property 5: Output Comparison Normalization
    /// For any two OpenAPI documents that are semantically equivalent but differ only
    /// in JSON formatting (whitespace, property order), the comparison function SHALL return true.
    /// **Validates: Requirements 5.2, 5.5**
    /// </summary>
    [Property(MaxTest = 100)]
    public Property SemanticallyEquivalentDocuments_WithFormattingDifferences_AreEqual()
    {
        return Prop.ForAll(
            OutputComparisonGenerators.TestCaseGen().ToArbitrary(),
            testCase =>
            {
                // Act
                var areEquivalent = _outputComparer.AreEquivalent(testCase.OriginalJson, testCase.VariedJson);

                // Assert - semantically equivalent documents should be equal
                return areEquivalent.Label(
                    $"Documents with {testCase.VariationType} variation should be equivalent.\n" +
                    $"Original (first 200 chars): {Truncate(testCase.OriginalJson, 200)}\n" +
                    $"Varied (first 200 chars): {Truncate(testCase.VariedJson, 200)}");
            });
    }

    /// <summary>
    /// Feature: lambda-merge-tool, Property 5: Output Comparison Normalization
    /// For any JSON document, normalizing it twice should produce the same result (idempotence).
    /// **Validates: Requirements 5.2, 5.5**
    /// </summary>
    [Property(MaxTest = 100)]
    public Property NormalizationIsIdempotent()
    {
        return Prop.ForAll(
            OutputComparisonGenerators.OpenApiJsonGen().ToArbitrary(),
            jsonObj =>
            {
                // Arrange
                var originalJson = jsonObj.ToJsonString(new JsonSerializerOptions { WriteIndented = true });

                // Act
                var normalized1 = _outputComparer.NormalizeJson(originalJson);
                var normalized2 = _outputComparer.NormalizeJson(normalized1);

                // Assert - normalizing twice should produce the same result
                var isIdempotent = normalized1 == normalized2;

                return isIdempotent.Label(
                    $"Normalization should be idempotent.\n" +
                    $"First normalization (first 200 chars): {Truncate(normalized1, 200)}\n" +
                    $"Second normalization (first 200 chars): {Truncate(normalized2, 200)}");
            });
    }

    /// <summary>
    /// Feature: lambda-merge-tool, Property 5: Output Comparison Normalization
    /// For any JSON document, the normalized output should have properties sorted alphabetically.
    /// **Validates: Requirements 5.2, 5.5**
    /// </summary>
    [Property(MaxTest = 100)]
    public Property NormalizedJsonHasSortedProperties()
    {
        return Prop.ForAll(
            OutputComparisonGenerators.OpenApiJsonGen().ToArbitrary(),
            jsonObj =>
            {
                // Arrange
                var originalJson = jsonObj.ToJsonString(new JsonSerializerOptions { WriteIndented = true });

                // Act
                var normalized = _outputComparer.NormalizeJson(originalJson);
                var parsedNormalized = JsonNode.Parse(normalized)?.AsObject();

                if (parsedNormalized == null)
                {
                    return false.Label("Failed to parse normalized JSON");
                }

                // Assert - properties should be sorted alphabetically
                var properties = parsedNormalized.Select(kvp => kvp.Key).ToList();
                var sortedProperties = properties.OrderBy(p => p, StringComparer.Ordinal).ToList();
                var isSorted = properties.SequenceEqual(sortedProperties);

                return isSorted.Label(
                    $"Properties should be sorted alphabetically.\n" +
                    $"Actual order: [{string.Join(", ", properties)}]\n" +
                    $"Expected order: [{string.Join(", ", sortedProperties)}]");
            });
    }

    /// <summary>
    /// Feature: lambda-merge-tool, Property 5: Output Comparison Normalization
    /// For any two different JSON documents, the comparison function SHALL return false.
    /// **Validates: Requirements 5.2, 5.5**
    /// </summary>
    [Property(MaxTest = 100)]
    public Property DifferentDocuments_AreNotEqual()
    {
        return Prop.ForAll(
            OutputComparisonGenerators.OpenApiJsonGen().ToArbitrary(),
            OutputComparisonGenerators.OpenApiJsonGen().ToArbitrary(),
            (jsonObj1, jsonObj2) =>
            {
                // Arrange
                var json1 = jsonObj1.ToJsonString();
                var json2 = jsonObj2.ToJsonString();

                // Skip if they happen to be the same
                if (json1 == json2)
                {
                    return true.Label("Skipped - documents are identical");
                }

                // Act
                var areEquivalent = _outputComparer.AreEquivalent(json1, json2);

                // Assert - different documents should not be equal
                // Note: This may occasionally fail if two different generated documents
                // happen to be semantically equivalent, which is acceptable
                return (!areEquivalent).Label(
                    $"Different documents should not be equivalent.\n" +
                    $"Doc1 (first 100 chars): {Truncate(json1, 100)}\n" +
                    $"Doc2 (first 100 chars): {Truncate(json2, 100)}");
            });
    }

    private static string Truncate(string s, int maxLength)
    {
        if (string.IsNullOrEmpty(s)) return s;
        return s.Length <= maxLength ? s : s.Substring(0, maxLength) + "...";
    }
}
