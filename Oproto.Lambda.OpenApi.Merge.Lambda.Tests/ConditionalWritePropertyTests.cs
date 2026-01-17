using FsCheck;
using FsCheck.Xunit;
using Microsoft.Extensions.Logging;
using Moq;
using Oproto.Lambda.OpenApi.Merge.Lambda.Services;
using System.Text.Json;
using System.Text.Json.Nodes;

namespace Oproto.Lambda.OpenApi.Merge.Lambda.Tests;

/// <summary>
/// Property-based tests for conditional write correctness.
/// Feature: lambda-merge-tool, Property 6: Conditional Write Correctness
/// **Validates: Requirements 5.3, 5.4**
/// </summary>
public class ConditionalWritePropertyTests
{
    /// <summary>
    /// Generators for conditional write test data.
    /// </summary>
    private static class ConditionalWriteGenerators
    {
        /// <summary>
        /// Generates valid OpenAPI-like JSON objects.
        /// </summary>
        public static Gen<JsonObject> OpenApiJsonGen()
        {
            return from title in Gen.Elements("Test API", "My API", "Sample API", "Product API")
                   from version in Gen.Elements("1.0.0", "2.0.0", "1.1.0", "3.0.0")
                   from pathCount in Gen.Choose(1, 3)
                   from paths in Gen.ListOf(pathCount, PathGen())
                   select BuildOpenApiJson(title, version, paths.ToList());
        }

        private static Gen<(string Path, string Method, string Summary)> PathGen()
        {
            return from path in Gen.Elements("/users", "/products", "/orders", "/items", "/api/v1/data")
                   from method in Gen.Elements("get", "post", "put", "delete")
                   from summary in Gen.Elements("Get resource", "Create resource", "Update resource", "Delete resource")
                   select (path, method, summary);
        }

        private static JsonObject BuildOpenApiJson(
            string title,
            string version,
            List<(string Path, string Method, string Summary)> paths)
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

            return doc;
        }

        /// <summary>
        /// Generates a test case for conditional write testing.
        /// </summary>
        public static Gen<ConditionalWriteTestCase> IdenticalContentTestCaseGen()
        {
            return from jsonObj in OpenApiJsonGen()
                   from bucket in Gen.Elements("test-bucket", "my-bucket", "api-bucket")
                   from key in Gen.Elements("api/merged.json", "output/openapi.json", "specs/api.json")
                   let originalJson = jsonObj.ToJsonString(new JsonSerializerOptions { WriteIndented = true })
                   // Create a formatting variation that is semantically identical
                   let variedJson = CreateFormattingVariation(jsonObj)
                   select new ConditionalWriteTestCase(bucket, key, originalJson, variedJson, true);
        }

        /// <summary>
        /// Generates a test case where content differs.
        /// </summary>
        public static Gen<ConditionalWriteTestCase> DifferentContentTestCaseGen()
        {
            return from jsonObj1 in OpenApiJsonGen()
                   from jsonObj2 in OpenApiJsonGen()
                   from bucket in Gen.Elements("test-bucket", "my-bucket", "api-bucket")
                   from key in Gen.Elements("api/merged.json", "output/openapi.json", "specs/api.json")
                   let json1 = jsonObj1.ToJsonString(new JsonSerializerOptions { WriteIndented = true })
                   let json2 = jsonObj2.ToJsonString(new JsonSerializerOptions { WriteIndented = true })
                   // Only use if they're actually different
                   where json1 != json2
                   select new ConditionalWriteTestCase(bucket, key, json1, json2, false);
        }

        /// <summary>
        /// Generates a test case where no existing content exists.
        /// </summary>
        public static Gen<ConditionalWriteTestCase> NoExistingContentTestCaseGen()
        {
            return from jsonObj in OpenApiJsonGen()
                   from bucket in Gen.Elements("test-bucket", "my-bucket", "api-bucket")
                   from key in Gen.Elements("api/merged.json", "output/openapi.json", "specs/api.json")
                   let newJson = jsonObj.ToJsonString(new JsonSerializerOptions { WriteIndented = true })
                   select new ConditionalWriteTestCase(bucket, key, null, newJson, false);
        }

        private static string CreateFormattingVariation(JsonObject original)
        {
            // Reverse property order to create a formatting variation
            var reversed = new JsonObject();
            var properties = original.ToList();
            properties.Reverse();
            foreach (var kvp in properties)
            {
                reversed[kvp.Key] = JsonNode.Parse(kvp.Value?.ToJsonString() ?? "null");
            }
            return reversed.ToJsonString(new JsonSerializerOptions { WriteIndented = false });
        }
    }

    /// <summary>
    /// Test case for conditional write testing.
    /// </summary>
    public record ConditionalWriteTestCase(
        string Bucket,
        string Key,
        string? ExistingContent,
        string NewContent,
        bool ShouldBeIdentical);


    private readonly Mock<IS3Service> _mockS3Service;
    private readonly Mock<ILogger<OutputComparer>> _mockOutputComparerLogger;
    private readonly Mock<ILogger<ConditionalWriter>> _mockConditionalWriterLogger;
    private readonly OutputComparer _outputComparer;
    private readonly ConditionalWriter _conditionalWriter;

    public ConditionalWritePropertyTests()
    {
        _mockS3Service = new Mock<IS3Service>();
        _mockOutputComparerLogger = new Mock<ILogger<OutputComparer>>();
        _mockConditionalWriterLogger = new Mock<ILogger<ConditionalWriter>>();
        _outputComparer = new OutputComparer(_mockOutputComparerLogger.Object);
        _conditionalWriter = new ConditionalWriter(
            _mockS3Service.Object,
            _outputComparer,
            _mockConditionalWriterLogger.Object);
    }

    /// <summary>
    /// Feature: lambda-merge-tool, Property 6: Conditional Write Correctness
    /// For any merge operation, if the new merged spec is semantically identical to the
    /// existing output spec, the write operation SHALL be skipped.
    /// **Validates: Requirements 5.3, 5.4**
    /// </summary>
    [Property(MaxTest = 100)]
    public Property IdenticalContent_SkipsWrite()
    {
        return Prop.ForAll(
            ConditionalWriteGenerators.IdenticalContentTestCaseGen().ToArbitrary(),
            testCase =>
            {
                // Arrange
                _mockS3Service.Reset();
                _mockS3Service
                    .Setup(x => x.ReadTextAsync(testCase.Bucket, testCase.Key, It.IsAny<CancellationToken>()))
                    .ReturnsAsync(testCase.ExistingContent);

                // Act
                var result = _conditionalWriter.WriteIfChangedAsync(
                    testCase.Bucket,
                    testCase.Key,
                    testCase.NewContent)
                    .GetAwaiter().GetResult();

                // Assert - write should be skipped for identical content
                var writeSkipped = !result.WasWritten;
                var reasonCorrect = result.Reason == "Content unchanged";

                // Verify WriteTextAsync was NOT called
                _mockS3Service.Verify(
                    x => x.WriteTextAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>(), It.IsAny<CancellationToken>()),
                    Times.Never);

                return (writeSkipped && reasonCorrect).Label(
                    $"Write should be skipped for identical content.\n" +
                    $"WasWritten: {result.WasWritten}, Reason: {result.Reason}");
            });
    }

    /// <summary>
    /// Feature: lambda-merge-tool, Property 6: Conditional Write Correctness
    /// For any merge operation, if the new merged spec differs from the existing output spec,
    /// the write operation SHALL occur.
    /// **Validates: Requirements 5.3, 5.4**
    /// </summary>
    [Property(MaxTest = 100)]
    public Property DifferentContent_PerformsWrite()
    {
        return Prop.ForAll(
            ConditionalWriteGenerators.DifferentContentTestCaseGen().ToArbitrary(),
            testCase =>
            {
                // Arrange
                _mockS3Service.Reset();
                _mockS3Service
                    .Setup(x => x.ReadTextAsync(testCase.Bucket, testCase.Key, It.IsAny<CancellationToken>()))
                    .ReturnsAsync(testCase.ExistingContent);
                _mockS3Service
                    .Setup(x => x.WriteTextAsync(testCase.Bucket, testCase.Key, testCase.NewContent, "application/json", It.IsAny<CancellationToken>()))
                    .Returns(Task.CompletedTask);

                // Act
                var result = _conditionalWriter.WriteIfChangedAsync(
                    testCase.Bucket,
                    testCase.Key,
                    testCase.NewContent)
                    .GetAwaiter().GetResult();

                // Assert - write should occur for different content
                var writeOccurred = result.WasWritten;
                var reasonCorrect = result.Reason == "Content changed";

                // Verify WriteTextAsync WAS called
                _mockS3Service.Verify(
                    x => x.WriteTextAsync(testCase.Bucket, testCase.Key, testCase.NewContent, "application/json", It.IsAny<CancellationToken>()),
                    Times.Once);

                return (writeOccurred && reasonCorrect).Label(
                    $"Write should occur for different content.\n" +
                    $"WasWritten: {result.WasWritten}, Reason: {result.Reason}");
            });
    }

    /// <summary>
    /// Feature: lambda-merge-tool, Property 6: Conditional Write Correctness
    /// For any merge operation where no existing output exists, the write operation SHALL occur.
    /// **Validates: Requirements 5.3, 5.4**
    /// </summary>
    [Property(MaxTest = 100)]
    public Property NoExistingContent_PerformsWrite()
    {
        return Prop.ForAll(
            ConditionalWriteGenerators.NoExistingContentTestCaseGen().ToArbitrary(),
            testCase =>
            {
                // Arrange
                _mockS3Service.Reset();
                _mockS3Service
                    .Setup(x => x.ReadTextAsync(testCase.Bucket, testCase.Key, It.IsAny<CancellationToken>()))
                    .ReturnsAsync((string?)null);
                _mockS3Service
                    .Setup(x => x.WriteTextAsync(testCase.Bucket, testCase.Key, testCase.NewContent, "application/json", It.IsAny<CancellationToken>()))
                    .Returns(Task.CompletedTask);

                // Act
                var result = _conditionalWriter.WriteIfChangedAsync(
                    testCase.Bucket,
                    testCase.Key,
                    testCase.NewContent)
                    .GetAwaiter().GetResult();

                // Assert - write should occur when no existing content
                var writeOccurred = result.WasWritten;
                var reasonCorrect = result.Reason == "File did not exist";

                // Verify WriteTextAsync WAS called
                _mockS3Service.Verify(
                    x => x.WriteTextAsync(testCase.Bucket, testCase.Key, testCase.NewContent, "application/json", It.IsAny<CancellationToken>()),
                    Times.Once);

                return (writeOccurred && reasonCorrect).Label(
                    $"Write should occur when no existing content.\n" +
                    $"WasWritten: {result.WasWritten}, Reason: {result.Reason}");
            });
    }

    /// <summary>
    /// Feature: lambda-merge-tool, Property 6: Conditional Write Correctness
    /// For any conditional write operation, the output key in the result SHALL match the input key.
    /// **Validates: Requirements 5.3, 5.4**
    /// </summary>
    [Property(MaxTest = 100)]
    public Property OutputKey_MatchesInputKey()
    {
        return Prop.ForAll(
            ConditionalWriteGenerators.IdenticalContentTestCaseGen().ToArbitrary(),
            testCase =>
            {
                // Arrange
                _mockS3Service.Reset();
                _mockS3Service
                    .Setup(x => x.ReadTextAsync(testCase.Bucket, testCase.Key, It.IsAny<CancellationToken>()))
                    .ReturnsAsync(testCase.ExistingContent);

                // Act
                var result = _conditionalWriter.WriteIfChangedAsync(
                    testCase.Bucket,
                    testCase.Key,
                    testCase.NewContent)
                    .GetAwaiter().GetResult();

                // Assert - output key should match input key
                var keyMatches = result.OutputKey == testCase.Key;

                return keyMatches.Label(
                    $"Output key should match input key.\n" +
                    $"Expected: {testCase.Key}, Actual: {result.OutputKey}");
            });
    }
}
