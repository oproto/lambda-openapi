using FsCheck;
using FsCheck.Xunit;
using Microsoft.Extensions.Logging;
using Moq;
using Oproto.Lambda.OpenApi.Merge.Lambda.Models;
using Oproto.Lambda.OpenApi.Merge.Lambda.Services;

namespace Oproto.Lambda.OpenApi.Merge.Lambda.Tests;

/// <summary>
/// Property-based tests for auto-discovery filtering.
/// Feature: lambda-merge-tool, Property 3: Auto-Discovery Filtering
/// **Validates: Requirements 3.2**
/// </summary>
public class AutoDiscoveryPropertyTests
{
    /// <summary>
    /// Generators for auto-discovery test data.
    /// </summary>
    private static class AutoDiscoveryGenerators
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
        /// Generates valid JSON filenames.
        /// </summary>
        public static Gen<string> JsonFilenameGen()
        {
            return Gen.Elements(
                "users.json", "orders.json", "products.json", "service1.json",
                "api.json", "openapi.json", "spec.json", "data.json",
                "users-service.json", "orders-api.json", "draft.json");
        }

        /// <summary>
        /// Generates non-JSON filenames.
        /// </summary>
        public static Gen<string> NonJsonFilenameGen()
        {
            return Gen.Elements(
                "readme.md", "config.yaml", "data.xml", "script.js",
                "styles.css", "image.png", "document.txt");
        }

        /// <summary>
        /// Generates output filenames.
        /// </summary>
        public static Gen<string> OutputFilenameGen()
        {
            return Gen.Elements(
                "merged.json", "output.json", "merged-openapi.json", "api-merged.json");
        }

        /// <summary>
        /// Generates exclude patterns.
        /// </summary>
        public static Gen<string> ExcludePatternGen()
        {
            return Gen.Elements(
                "*-draft.json", "*.backup.json", "test-*.json", "*-old.json",
                "temp*.json", "*-dev.json");
        }


        /// <summary>
        /// Generates a set of S3 keys for testing auto-discovery.
        /// </summary>
        public static Gen<AutoDiscoveryTestCase> TestCaseGen()
        {
            return from prefix in PrefixGen()
                   from jsonFileCount in Gen.Choose(1, 5)
                   from jsonFiles in Gen.ListOf(jsonFileCount, JsonFilenameGen())
                   from nonJsonFileCount in Gen.Choose(0, 3)
                   from nonJsonFiles in Gen.ListOf(nonJsonFileCount, NonJsonFilenameGen())
                   from outputFile in OutputFilenameGen()
                   from excludePatternCount in Gen.Choose(0, 2)
                   from excludePatterns in Gen.ListOf(excludePatternCount, ExcludePatternGen())
                   let allKeys = BuildAllKeys(prefix, jsonFiles.Distinct().ToList(), nonJsonFiles.Distinct().ToList())
                   select new AutoDiscoveryTestCase(
                       prefix,
                       allKeys,
                       outputFile,
                       excludePatterns.Distinct().ToList());
        }

        private static List<string> BuildAllKeys(string prefix, List<string> jsonFiles, List<string> nonJsonFiles)
        {
            var keys = new List<string>();

            // Always add config.json
            keys.Add(prefix + "config.json");

            // Add JSON files
            foreach (var file in jsonFiles)
            {
                keys.Add(prefix + file);
            }

            // Add non-JSON files
            foreach (var file in nonJsonFiles)
            {
                keys.Add(prefix + file);
            }

            return keys;
        }
    }

    /// <summary>
    /// Test case for auto-discovery testing.
    /// </summary>
    public record AutoDiscoveryTestCase(
        string Prefix,
        List<string> AllKeys,
        string OutputFile,
        List<string> ExcludePatterns);


    private readonly Mock<IS3Service> _mockS3Service;
    private readonly Mock<ILogger<SourceDiscovery>> _mockLogger;
    private readonly SourceDiscovery _sourceDiscovery;

    public AutoDiscoveryPropertyTests()
    {
        _mockS3Service = new Mock<IS3Service>();
        _mockLogger = new Mock<ILogger<SourceDiscovery>>();
        _sourceDiscovery = new SourceDiscovery(_mockS3Service.Object, _mockLogger.Object);
    }

    /// <summary>
    /// Feature: lambda-merge-tool, Property 3: Auto-Discovery Filtering
    /// For any set of S3 keys within a prefix, when autoDiscover is true,
    /// the discovered sources SHALL include only .json files.
    /// **Validates: Requirements 3.2**
    /// </summary>
    [Property(MaxTest = 100)]
    public Property AutoDiscover_OnlyIncludesJsonFiles()
    {
        return Prop.ForAll(
            AutoDiscoveryGenerators.TestCaseGen().ToArbitrary(),
            testCase =>
            {
                // Arrange
                _mockS3Service
                    .Setup(x => x.ListObjectsAsync(It.IsAny<string>(), testCase.Prefix, It.IsAny<CancellationToken>()))
                    .ReturnsAsync(testCase.AllKeys);

                var config = new LambdaMergeConfig
                {
                    AutoDiscover = true,
                    Output = testCase.OutputFile,
                    ExcludePatterns = testCase.ExcludePatterns,
                    Info = new Oproto.Lambda.OpenApi.Merge.MergeInfoConfiguration
                    {
                        Title = "Test",
                        Version = "1.0"
                    }
                };

                // Act
                var result = _sourceDiscovery.DiscoverSourcesAsync("test-bucket", testCase.Prefix, config)
                    .GetAwaiter().GetResult();

                // Assert - all discovered sources should be JSON files
                var allJson = result.All(s => s.Key.EndsWith(".json", StringComparison.OrdinalIgnoreCase));

                return allJson.Label($"All discovered sources should be JSON files. Found: {string.Join(", ", result.Select(s => s.Key))}");
            });
    }

    /// <summary>
    /// Feature: lambda-merge-tool, Property 3: Auto-Discovery Filtering
    /// For any set of S3 keys within a prefix, when autoDiscover is true,
    /// the discovered sources SHALL exclude config.json.
    /// **Validates: Requirements 3.2**
    /// </summary>
    [Property(MaxTest = 100)]
    public Property AutoDiscover_ExcludesConfigJson()
    {
        return Prop.ForAll(
            AutoDiscoveryGenerators.TestCaseGen().ToArbitrary(),
            testCase =>
            {
                // Arrange
                _mockS3Service
                    .Setup(x => x.ListObjectsAsync(It.IsAny<string>(), testCase.Prefix, It.IsAny<CancellationToken>()))
                    .ReturnsAsync(testCase.AllKeys);

                var config = new LambdaMergeConfig
                {
                    AutoDiscover = true,
                    Output = testCase.OutputFile,
                    ExcludePatterns = testCase.ExcludePatterns,
                    Info = new Oproto.Lambda.OpenApi.Merge.MergeInfoConfiguration
                    {
                        Title = "Test",
                        Version = "1.0"
                    }
                };

                // Act
                var result = _sourceDiscovery.DiscoverSourcesAsync("test-bucket", testCase.Prefix, config)
                    .GetAwaiter().GetResult();

                // Assert - config.json should not be in results
                var configExcluded = !result.Any(s => 
                    s.Key.EndsWith("/config.json", StringComparison.OrdinalIgnoreCase) ||
                    s.Key.Equals("config.json", StringComparison.OrdinalIgnoreCase));

                return configExcluded.Label($"config.json should be excluded. Found keys: {string.Join(", ", result.Select(s => s.Key))}");
            });
    }


    /// <summary>
    /// Feature: lambda-merge-tool, Property 3: Auto-Discovery Filtering
    /// For any set of S3 keys within a prefix, when autoDiscover is true,
    /// the discovered sources SHALL exclude the configured output file.
    /// **Validates: Requirements 3.2**
    /// </summary>
    [Property(MaxTest = 100)]
    public Property AutoDiscover_ExcludesOutputFile()
    {
        return Prop.ForAll(
            AutoDiscoveryGenerators.TestCaseGen().ToArbitrary(),
            testCase =>
            {
                // Arrange - add the output file to the list of keys
                var keysWithOutput = new List<string>(testCase.AllKeys);
                var outputKey = string.IsNullOrEmpty(testCase.Prefix) 
                    ? testCase.OutputFile 
                    : testCase.Prefix + testCase.OutputFile;
                if (!keysWithOutput.Contains(outputKey))
                {
                    keysWithOutput.Add(outputKey);
                }

                _mockS3Service
                    .Setup(x => x.ListObjectsAsync(It.IsAny<string>(), testCase.Prefix, It.IsAny<CancellationToken>()))
                    .ReturnsAsync(keysWithOutput);

                var config = new LambdaMergeConfig
                {
                    AutoDiscover = true,
                    Output = testCase.OutputFile,
                    ExcludePatterns = testCase.ExcludePatterns,
                    Info = new Oproto.Lambda.OpenApi.Merge.MergeInfoConfiguration
                    {
                        Title = "Test",
                        Version = "1.0"
                    }
                };

                // Act
                var result = _sourceDiscovery.DiscoverSourcesAsync("test-bucket", testCase.Prefix, config)
                    .GetAwaiter().GetResult();

                // Assert - output file should not be in results
                var outputExcluded = !result.Any(s => 
                    s.Key.Equals(outputKey, StringComparison.OrdinalIgnoreCase));

                return outputExcluded.Label($"Output file '{outputKey}' should be excluded. Found keys: {string.Join(", ", result.Select(s => s.Key))}");
            });
    }

    /// <summary>
    /// Feature: lambda-merge-tool, Property 3: Auto-Discovery Filtering
    /// For any set of S3 keys within a prefix, when autoDiscover is true,
    /// the discovered sources SHALL exclude files matching excludePatterns.
    /// **Validates: Requirements 3.2**
    /// </summary>
    [Property(MaxTest = 100)]
    public Property AutoDiscover_ExcludesFilesMatchingExcludePatterns()
    {
        return Prop.ForAll(
            AutoDiscoveryGenerators.TestCaseGen().ToArbitrary(),
            testCase =>
            {
                // Arrange
                _mockS3Service
                    .Setup(x => x.ListObjectsAsync(It.IsAny<string>(), testCase.Prefix, It.IsAny<CancellationToken>()))
                    .ReturnsAsync(testCase.AllKeys);

                var config = new LambdaMergeConfig
                {
                    AutoDiscover = true,
                    Output = testCase.OutputFile,
                    ExcludePatterns = testCase.ExcludePatterns,
                    Info = new Oproto.Lambda.OpenApi.Merge.MergeInfoConfiguration
                    {
                        Title = "Test",
                        Version = "1.0"
                    }
                };

                // Act
                var result = _sourceDiscovery.DiscoverSourcesAsync("test-bucket", testCase.Prefix, config)
                    .GetAwaiter().GetResult();

                // Assert - no discovered source should match any exclude pattern
                var noExcludedFiles = true;
                foreach (var source in result)
                {
                    var filename = GetFilename(source.Key);
                    foreach (var pattern in testCase.ExcludePatterns)
                    {
                        if (MatchesGlobPattern(filename, pattern))
                        {
                            noExcludedFiles = false;
                            break;
                        }
                    }
                    if (!noExcludedFiles) break;
                }

                return noExcludedFiles.Label($"No discovered source should match exclude patterns. Patterns: [{string.Join(", ", testCase.ExcludePatterns)}], Found: [{string.Join(", ", result.Select(s => s.Key))}]");
            });
    }

    private static string GetFilename(string key)
    {
        var lastSlash = key.LastIndexOf('/');
        return lastSlash >= 0 ? key.Substring(lastSlash + 1) : key;
    }

    private static bool MatchesGlobPattern(string filename, string pattern)
    {
        if (string.IsNullOrEmpty(pattern))
        {
            return false;
        }

        var regexPattern = "^" + System.Text.RegularExpressions.Regex.Escape(pattern)
            .Replace("\\*", ".*")
            .Replace("\\?", ".") + "$";

        return System.Text.RegularExpressions.Regex.IsMatch(
            filename,
            regexPattern,
            System.Text.RegularExpressions.RegexOptions.IgnoreCase);
    }
}
