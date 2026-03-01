using FsCheck;
using FsCheck.Xunit;
using Microsoft.Extensions.Logging;
using Moq;
using Oproto.Lambda.OpenApi.Merge;
using Oproto.Lambda.OpenApi.Merge.Lambda.Models;
using Oproto.Lambda.OpenApi.Merge.Lambda.Services;

namespace Oproto.Lambda.OpenApi.Merge.Lambda.Tests;

/// <summary>
/// Property-based tests for explicit sources validation.
/// Feature: lambda-merge-tool, Property 4: Explicit Sources Validation
/// **Validates: Requirements 3.3**
/// </summary>
public class ExplicitSourcesPropertyTests
{
    /// <summary>
    /// Generators for explicit sources test data.
    /// </summary>
    private static class ExplicitSourcesGenerators
    {
        /// <summary>
        /// Generates valid prefix values.
        /// </summary>
        public static Gen<string> PrefixGen()
        {
            return Gen.Elements(
                "publicapi/", "internalapi/", "api/v1/", "services/",
                "gateway/", "admin/", "");
        }

        /// <summary>
        /// Generates valid source file paths.
        /// </summary>
        public static Gen<string> SourcePathGen()
        {
            return Gen.Elements(
                "users.json", "orders.json", "products.json", "service1.json",
                "api.json", "openapi.json", "spec.json", "data.json",
                "users-service.json", "orders-api.json");
        }

        /// <summary>
        /// Generates optional source names.
        /// </summary>
        public static Gen<string?> SourceNameGen()
        {
            return Gen.OneOf(
                Gen.Constant<string?>(null),
                Gen.Elements<string?>("Users", "Orders", "Products", "Service1", "API", "Data"));
        }

        /// <summary>
        /// Generates optional path prefixes.
        /// </summary>
        public static Gen<string?> PathPrefixGen()
        {
            return Gen.OneOf(
                Gen.Constant<string?>(null),
                Gen.Elements<string?>("/users", "/orders", "/products", "/api/v1", "/admin"));
        }

        /// <summary>
        /// Generates a single source configuration.
        /// </summary>
        public static Gen<SourceConfiguration> SourceConfigGen()
        {
            return from path in SourcePathGen()
                   from name in SourceNameGen()
                   from pathPrefix in PathPrefixGen()
                   select new SourceConfiguration
                   {
                       Path = path,
                       Name = name,
                       PathPrefix = pathPrefix
                   };
        }


        /// <summary>
        /// Generates a test case with explicit sources.
        /// </summary>
        public static Gen<ExplicitSourcesTestCase> TestCaseWithSourcesGen()
        {
            return from prefix in PrefixGen()
                   from sourceCount in Gen.Choose(1, 5)
                   from sources in Gen.ListOf(sourceCount, SourceConfigGen())
                   let distinctSources = sources.GroupBy(s => s.Path).Select(g => g.First()).ToList()
                   select new ExplicitSourcesTestCase(prefix, distinctSources);
        }

        /// <summary>
        /// Generates a test case with empty sources (for validation testing).
        /// </summary>
        public static Gen<ExplicitSourcesTestCase> TestCaseWithEmptySourcesGen()
        {
            return from prefix in PrefixGen()
                   select new ExplicitSourcesTestCase(prefix, new List<SourceConfiguration>());
        }

        /// <summary>
        /// Generates a test case with null sources (for validation testing).
        /// </summary>
        public static Gen<ExplicitSourcesTestCase> TestCaseWithNullSourcesGen()
        {
            return from prefix in PrefixGen()
                   select new ExplicitSourcesTestCase(prefix, null);
        }
    }

    /// <summary>
    /// Test case for explicit sources testing.
    /// </summary>
    public record ExplicitSourcesTestCase(
        string Prefix,
        List<SourceConfiguration>? Sources);

    private readonly Mock<IS3Service> _mockS3Service;
    private readonly Mock<ILogger<SourceDiscovery>> _mockLogger;
    private readonly SourceDiscovery _sourceDiscovery;

    public ExplicitSourcesPropertyTests()
    {
        _mockS3Service = new Mock<IS3Service>();
        _mockLogger = new Mock<ILogger<SourceDiscovery>>();
        _sourceDiscovery = new SourceDiscovery(_mockS3Service.Object, _mockLogger.Object);
    }


    /// <summary>
    /// Feature: lambda-merge-tool, Property 4: Explicit Sources Validation
    /// For any LambdaMergeConfig where autoDiscover is false and sources are provided,
    /// the discovered sources SHALL match the explicit sources list.
    /// **Validates: Requirements 3.3**
    /// </summary>
    [Property(MaxTest = 100)]
    public Property ExplicitSources_ReturnsAllConfiguredSources()
    {
        return Prop.ForAll(
            ExplicitSourcesGenerators.TestCaseWithSourcesGen().ToArbitrary(),
            testCase =>
            {
                // Arrange
                var config = new LambdaMergeConfig
                {
                    AutoDiscover = false,
                    Sources = testCase.Sources!,
                    Output = "merged.json",
                    Info = new MergeInfoConfiguration
                    {
                        Title = "Test",
                        Version = "1.0"
                    }
                };

                // Act
                var result = _sourceDiscovery.DiscoverSourcesAsync("test-bucket", testCase.Prefix, config)
                    .GetAwaiter().GetResult();

                // Assert - should return same number of sources
                var countMatches = result.Count == testCase.Sources!.Count;

                return countMatches.Label($"Expected {testCase.Sources.Count} sources, got {result.Count}");
            });
    }

    /// <summary>
    /// Feature: lambda-merge-tool, Property 4: Explicit Sources Validation
    /// For any LambdaMergeConfig where autoDiscover is false,
    /// each discovered source SHALL have the correct S3 key constructed from prefix + path.
    /// **Validates: Requirements 3.3**
    /// </summary>
    [Property(MaxTest = 100)]
    public Property ExplicitSources_ConstructsCorrectS3Keys()
    {
        return Prop.ForAll(
            ExplicitSourcesGenerators.TestCaseWithSourcesGen().ToArbitrary(),
            testCase =>
            {
                // Arrange
                var config = new LambdaMergeConfig
                {
                    AutoDiscover = false,
                    Sources = testCase.Sources!,
                    Output = "merged.json",
                    Info = new MergeInfoConfiguration
                    {
                        Title = "Test",
                        Version = "1.0"
                    }
                };

                // Act
                var result = _sourceDiscovery.DiscoverSourcesAsync("test-bucket", testCase.Prefix, config)
                    .GetAwaiter().GetResult();

                // Assert - each source key should be prefix + path
                var allKeysCorrect = true;
                for (int i = 0; i < testCase.Sources!.Count; i++)
                {
                    var expectedKey = BuildExpectedKey(testCase.Prefix, testCase.Sources[i].Path);
                    if (result[i].Key != expectedKey)
                    {
                        allKeysCorrect = false;
                        break;
                    }
                }

                return allKeysCorrect.Label($"All source keys should be correctly constructed from prefix + path");
            });
    }


    /// <summary>
    /// Feature: lambda-merge-tool, Property 4: Explicit Sources Validation
    /// For any LambdaMergeConfig where autoDiscover is false,
    /// each discovered source SHALL preserve the explicit configuration.
    /// **Validates: Requirements 3.3**
    /// </summary>
    [Property(MaxTest = 100)]
    public Property ExplicitSources_PreservesExplicitConfiguration()
    {
        return Prop.ForAll(
            ExplicitSourcesGenerators.TestCaseWithSourcesGen().ToArbitrary(),
            testCase =>
            {
                // Arrange
                var config = new LambdaMergeConfig
                {
                    AutoDiscover = false,
                    Sources = testCase.Sources!,
                    Output = "merged.json",
                    Info = new MergeInfoConfiguration
                    {
                        Title = "Test",
                        Version = "1.0"
                    }
                };

                // Act
                var result = _sourceDiscovery.DiscoverSourcesAsync("test-bucket", testCase.Prefix, config)
                    .GetAwaiter().GetResult();

                // Assert - each discovered source should have the explicit config attached
                var allConfigsPreserved = true;
                for (int i = 0; i < testCase.Sources!.Count; i++)
                {
                    var discoveredSource = result[i];
                    var originalConfig = testCase.Sources[i];

                    if (discoveredSource.ExplicitConfig == null ||
                        discoveredSource.ExplicitConfig.Path != originalConfig.Path ||
                        discoveredSource.ExplicitConfig.PathPrefix != originalConfig.PathPrefix)
                    {
                        allConfigsPreserved = false;
                        break;
                    }
                }

                return allConfigsPreserved.Label($"All explicit configurations should be preserved in discovered sources");
            });
    }

    /// <summary>
    /// Feature: lambda-merge-tool, Property 4: Explicit Sources Validation
    /// For any LambdaMergeConfig where autoDiscover is false,
    /// the source name SHALL be the explicit name if provided, or derived from filename.
    /// **Validates: Requirements 3.3**
    /// </summary>
    [Property(MaxTest = 100)]
    public Property ExplicitSources_UsesExplicitNameOrDerivedName()
    {
        return Prop.ForAll(
            ExplicitSourcesGenerators.TestCaseWithSourcesGen().ToArbitrary(),
            testCase =>
            {
                // Arrange
                var config = new LambdaMergeConfig
                {
                    AutoDiscover = false,
                    Sources = testCase.Sources!,
                    Output = "merged.json",
                    Info = new MergeInfoConfiguration
                    {
                        Title = "Test",
                        Version = "1.0"
                    }
                };

                // Act
                var result = _sourceDiscovery.DiscoverSourcesAsync("test-bucket", testCase.Prefix, config)
                    .GetAwaiter().GetResult();

                // Assert - name should be explicit name or derived from filename
                var allNamesCorrect = true;
                for (int i = 0; i < testCase.Sources!.Count; i++)
                {
                    var discoveredSource = result[i];
                    var originalConfig = testCase.Sources[i];

                    var expectedName = originalConfig.Name ?? GetNameFromFilename(originalConfig.Path);
                    if (discoveredSource.Name != expectedName)
                    {
                        allNamesCorrect = false;
                        break;
                    }
                }

                return allNamesCorrect.Label($"Source names should be explicit name or derived from filename");
            });
    }

    private static string BuildExpectedKey(string prefix, string path)
    {
        if (string.IsNullOrEmpty(prefix))
        {
            return path;
        }

        if (!prefix.EndsWith("/"))
        {
            prefix += "/";
        }

        return prefix + path;
    }

    private static string GetNameFromFilename(string filename)
    {
        if (filename.EndsWith(".json", StringComparison.OrdinalIgnoreCase))
        {
            return filename.Substring(0, filename.Length - 5);
        }
        return filename;
    }
}
