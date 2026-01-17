using FsCheck;
using FsCheck.Xunit;
using Microsoft.Extensions.Logging;
using Moq;
using Oproto.Lambda.OpenApi.Merge.Lambda.Services;

namespace Oproto.Lambda.OpenApi.Merge.Lambda.Tests;

/// <summary>
/// Property-based tests for prefix extraction from S3 keys.
/// Feature: lambda-merge-tool, Property 1: Prefix Extraction Consistency
/// **Validates: Requirements 1.1, 1.2, 1.3, 1.4**
/// </summary>
public class PrefixExtractionPropertyTests
{
    /// <summary>
    /// Generators for S3 key test data.
    /// </summary>
    private static class S3KeyGenerators
    {
        /// <summary>
        /// Generates valid prefix segments (directory names).
        /// </summary>
        public static Gen<string> PrefixSegmentGen()
        {
            return Gen.Elements(
                "publicapi", "internalapi", "api", "v1", "v2", "v3",
                "users", "orders", "products", "services", "gateway",
                "admin", "public", "private", "staging", "production");
        }

        /// <summary>
        /// Generates valid filenames.
        /// </summary>
        public static Gen<string> FilenameGen()
        {
            return Gen.Elements(
                "config.json", "openapi.json", "api.json", "merged.json",
                "users-service.json", "orders-service.json", "products.json",
                "service1.json", "service2.json", "spec.json");
        }

        /// <summary>
        /// Generates a single-level prefix S3 key (e.g., "publicapi/config.json").
        /// </summary>
        public static Gen<(string Key, string ExpectedPrefix, string ExpectedFilename)> SingleLevelKeyGen()
        {
            return from prefix in PrefixSegmentGen()
                   from filename in FilenameGen()
                   let key = $"{prefix}/{filename}"
                   let expectedPrefix = $"{prefix}/"
                   select (key, expectedPrefix, filename);
        }

        /// <summary>
        /// Generates a multi-level prefix S3 key (e.g., "internal/v2/service.json").
        /// </summary>
        public static Gen<(string Key, string ExpectedPrefix, string ExpectedFilename)> MultiLevelKeyGen()
        {
            return from segmentCount in Gen.Choose(2, 4)
                   from segments in Gen.ListOf(segmentCount, PrefixSegmentGen())
                   from filename in FilenameGen()
                   let prefix = string.Join("/", segments)
                   let key = $"{prefix}/{filename}"
                   let expectedPrefix = $"{prefix}/"
                   select (key, expectedPrefix, filename);
        }

        /// <summary>
        /// Generates root-level S3 keys (no prefix, e.g., "config.json").
        /// </summary>
        public static Gen<(string Key, string ExpectedPrefix, string ExpectedFilename)> RootLevelKeyGen()
        {
            return from filename in FilenameGen()
                   select (filename, string.Empty, filename);
        }

        /// <summary>
        /// Generates any valid S3 key with its expected prefix.
        /// </summary>
        public static Gen<(string Key, string ExpectedPrefix, string ExpectedFilename)> AnyValidKeyGen()
        {
            return Gen.OneOf(
                SingleLevelKeyGen(),
                MultiLevelKeyGen(),
                RootLevelKeyGen());
        }
    }

    private readonly ConfigLoader _configLoader;

    public PrefixExtractionPropertyTests()
    {
        var mockS3Service = new Mock<IS3Service>();
        var mockLogger = new Mock<ILogger<ConfigLoader>>();
        _configLoader = new ConfigLoader(mockS3Service.Object, mockLogger.Object);
    }

    /// <summary>
    /// Feature: lambda-merge-tool, Property 1: Prefix Extraction Consistency
    /// For any valid S3 key, extracting the prefix SHALL return the directory path portion
    /// before the filename.
    /// **Validates: Requirements 1.1, 1.2, 1.3, 1.4**
    /// </summary>
    [Property(MaxTest = 100)]
    public Property ExtractPrefix_ReturnsDirectoryPathBeforeFilename()
    {
        return Prop.ForAll(
            S3KeyGenerators.AnyValidKeyGen().ToArbitrary(),
            testCase =>
            {
                var (key, expectedPrefix, _) = testCase;

                // Act
                var actualPrefix = _configLoader.ExtractPrefix(key);

                // Assert
                return (actualPrefix == expectedPrefix)
                    .Label($"Key: '{key}' -> Expected: '{expectedPrefix}', Actual: '{actualPrefix}'");
            });
    }

    /// <summary>
    /// Feature: lambda-merge-tool, Property 1: Prefix Extraction Consistency
    /// For any valid S3 key, prefix extraction SHALL be deterministic (same input always
    /// produces same output).
    /// **Validates: Requirements 1.1, 1.2, 1.3, 1.4**
    /// </summary>
    [Property(MaxTest = 100)]
    public Property ExtractPrefix_IsDeterministic()
    {
        return Prop.ForAll(
            S3KeyGenerators.AnyValidKeyGen().ToArbitrary(),
            testCase =>
            {
                var (key, _, _) = testCase;

                // Act - extract prefix multiple times
                var result1 = _configLoader.ExtractPrefix(key);
                var result2 = _configLoader.ExtractPrefix(key);
                var result3 = _configLoader.ExtractPrefix(key);

                // Assert - all results should be identical
                return (result1 == result2 && result2 == result3)
                    .Label($"Extraction should be deterministic for key: '{key}'");
            });
    }

    /// <summary>
    /// Feature: lambda-merge-tool, Property 1: Prefix Extraction Consistency
    /// For any valid S3 key with a prefix, the extracted prefix combined with the filename
    /// SHALL reconstruct the original key.
    /// **Validates: Requirements 1.1, 1.2, 1.3, 1.4**
    /// </summary>
    [Property(MaxTest = 100)]
    public Property ExtractPrefix_PlusFIlename_ReconstructsOriginalKey()
    {
        return Prop.ForAll(
            S3KeyGenerators.AnyValidKeyGen().ToArbitrary(),
            testCase =>
            {
                var (key, _, expectedFilename) = testCase;

                // Act
                var prefix = _configLoader.ExtractPrefix(key);
                var reconstructedKey = prefix + expectedFilename;

                // Assert
                return (reconstructedKey == key)
                    .Label($"Reconstructed key '{reconstructedKey}' should equal original '{key}'");
            });
    }

    /// <summary>
    /// Feature: lambda-merge-tool, Property 1: Prefix Extraction Consistency
    /// For any S3 key with a prefix, the extracted prefix SHALL end with a forward slash.
    /// **Validates: Requirements 1.1, 1.2, 1.3, 1.4**
    /// </summary>
    [Property(MaxTest = 100)]
    public Property ExtractPrefix_WithPrefix_EndsWithSlash()
    {
        return Prop.ForAll(
            Gen.OneOf(
                S3KeyGenerators.SingleLevelKeyGen(),
                S3KeyGenerators.MultiLevelKeyGen()
            ).ToArbitrary(),
            testCase =>
            {
                var (key, _, _) = testCase;

                // Act
                var prefix = _configLoader.ExtractPrefix(key);

                // Assert - non-empty prefix should end with /
                return (prefix.EndsWith("/"))
                    .Label($"Prefix '{prefix}' should end with '/' for key '{key}'");
            });
    }

    /// <summary>
    /// Feature: lambda-merge-tool, Property 1: Prefix Extraction Consistency
    /// For root-level keys (no directory), the extracted prefix SHALL be empty.
    /// **Validates: Requirements 1.1, 1.2, 1.3, 1.4**
    /// </summary>
    [Property(MaxTest = 100)]
    public Property ExtractPrefix_RootLevelKey_ReturnsEmptyString()
    {
        return Prop.ForAll(
            S3KeyGenerators.RootLevelKeyGen().ToArbitrary(),
            testCase =>
            {
                var (key, _, _) = testCase;

                // Act
                var prefix = _configLoader.ExtractPrefix(key);

                // Assert
                return (prefix == string.Empty)
                    .Label($"Root-level key '{key}' should have empty prefix, got '{prefix}'");
            });
    }
}
