using FsCheck;
using FsCheck.Xunit;
using Oproto.Lambda.OpenApi.Merge.Cdk;

namespace Oproto.Lambda.OpenApi.Merge.Lambda.Tests;

/// <summary>
/// Property-based tests for output path construction.
/// Feature: lambda-merge-tool, Property 7: Output Path Construction
/// **Validates: Requirements 6.3, 6.5**
/// </summary>
public class OutputPathConstructionPropertyTests
{
    /// <summary>
    /// Generators for output path test data.
    /// </summary>
    private static class OutputPathGenerators
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
        /// Generates valid simple output filenames (no path separators).
        /// </summary>
        public static Gen<string> SimpleFilenameGen()
        {
            return Gen.Elements(
                "merged.json", "openapi.json", "api.json", "output.json",
                "combined.json", "spec.json", "merged-openapi.json",
                "public-api.json", "internal-api.json");
        }

        /// <summary>
        /// Generates a single-level prefix (e.g., "publicapi/").
        /// </summary>
        public static Gen<string> SingleLevelPrefixGen()
        {
            return from segment in PrefixSegmentGen()
                   select $"{segment}/";
        }

        /// <summary>
        /// Generates a multi-level prefix (e.g., "internal/v2/").
        /// </summary>
        public static Gen<string> MultiLevelPrefixGen()
        {
            return from segmentCount in Gen.Choose(2, 4)
                   from segments in Gen.ListOf(segmentCount, PrefixSegmentGen())
                   select string.Join("/", segments) + "/";
        }

        /// <summary>
        /// Generates a prefix without trailing slash (to test normalization).
        /// </summary>
        public static Gen<string> PrefixWithoutTrailingSlashGen()
        {
            return from segment in PrefixSegmentGen()
                   select segment;
        }

        /// <summary>
        /// Generates any valid prefix.
        /// </summary>
        public static Gen<string> AnyPrefixGen()
        {
            return Gen.OneOf(
                SingleLevelPrefixGen(),
                MultiLevelPrefixGen(),
                PrefixWithoutTrailingSlashGen());
        }

        /// <summary>
        /// Generates a full path output (contains '/').
        /// </summary>
        public static Gen<string> FullPathOutputGen()
        {
            return from segment in PrefixSegmentGen()
                   from filename in SimpleFilenameGen()
                   select $"{segment}/{filename}";
        }

        /// <summary>
        /// Generates an absolute path output (starts with '/').
        /// </summary>
        public static Gen<string> AbsolutePathOutputGen()
        {
            return from segment in PrefixSegmentGen()
                   from filename in SimpleFilenameGen()
                   select $"/{segment}/{filename}";
        }
    }

    /// <summary>
    /// Feature: lambda-merge-tool, Property 7: Output Path Construction
    /// For any prefix and simple filename (no '/'), the output path SHALL be {prefix}/{filename}.
    /// **Validates: Requirements 6.3, 6.5**
    /// </summary>
    [Property(MaxTest = 100)]
    public Property SimpleFilename_IsRelativeToPrefix()
    {
        return Prop.ForAll(
            OutputPathGenerators.AnyPrefixGen().ToArbitrary(),
            OutputPathGenerators.SimpleFilenameGen().ToArbitrary(),
            (prefix, filename) =>
            {
                // Act
                var outputPath = OutputPathHelper.ConstructOutputPath(prefix, filename);

                // Assert - path should be normalized prefix + filename
                var normalizedPrefix = prefix.TrimEnd('/') + "/";
                var expectedPath = normalizedPrefix + filename;

                return (outputPath == expectedPath)
                    .Label($"Prefix: '{prefix}', Filename: '{filename}' -> Expected: '{expectedPath}', Actual: '{outputPath}'");
            });
    }

    /// <summary>
    /// Feature: lambda-merge-tool, Property 7: Output Path Construction
    /// For any full path output (contains '/'), the output path SHALL be the full path as-is.
    /// **Validates: Requirements 6.3, 6.5**
    /// </summary>
    [Property(MaxTest = 100)]
    public Property FullPathOutput_IsUsedAsIs()
    {
        return Prop.ForAll(
            OutputPathGenerators.AnyPrefixGen().ToArbitrary(),
            OutputPathGenerators.FullPathOutputGen().ToArbitrary(),
            (prefix, fullPath) =>
            {
                // Act
                var outputPath = OutputPathHelper.ConstructOutputPath(prefix, fullPath);

                // Assert - full path should be used as-is (not prefixed)
                return (outputPath == fullPath)
                    .Label($"Full path '{fullPath}' should be used as-is, not prefixed. Got: '{outputPath}'");
            });
    }

    /// <summary>
    /// Feature: lambda-merge-tool, Property 7: Output Path Construction
    /// For any absolute path output (starts with '/'), the output path SHALL be the path without leading slash.
    /// **Validates: Requirements 6.3, 6.5**
    /// </summary>
    [Property(MaxTest = 100)]
    public Property AbsolutePathOutput_HasLeadingSlashRemoved()
    {
        return Prop.ForAll(
            OutputPathGenerators.AnyPrefixGen().ToArbitrary(),
            OutputPathGenerators.AbsolutePathOutputGen().ToArbitrary(),
            (prefix, absolutePath) =>
            {
                // Act
                var outputPath = OutputPathHelper.ConstructOutputPath(prefix, absolutePath);

                // Assert - leading slash should be removed
                var expectedPath = absolutePath.TrimStart('/');
                return (outputPath == expectedPath)
                    .Label($"Absolute path '{absolutePath}' should have leading slash removed. Expected: '{expectedPath}', Got: '{outputPath}'");
            });
    }

    /// <summary>
    /// Feature: lambda-merge-tool, Property 7: Output Path Construction
    /// Output path construction SHALL be deterministic (same inputs always produce same output).
    /// **Validates: Requirements 6.3, 6.5**
    /// </summary>
    [Property(MaxTest = 100)]
    public Property ConstructOutputPath_IsDeterministic()
    {
        return Prop.ForAll(
            OutputPathGenerators.AnyPrefixGen().ToArbitrary(),
            OutputPathGenerators.SimpleFilenameGen().ToArbitrary(),
            (prefix, filename) =>
            {
                // Act - construct path multiple times
                var result1 = OutputPathHelper.ConstructOutputPath(prefix, filename);
                var result2 = OutputPathHelper.ConstructOutputPath(prefix, filename);
                var result3 = OutputPathHelper.ConstructOutputPath(prefix, filename);

                // Assert - all results should be identical
                return (result1 == result2 && result2 == result3)
                    .Label($"Construction should be deterministic for prefix '{prefix}' and filename '{filename}'");
            });
    }

    /// <summary>
    /// Feature: lambda-merge-tool, Property 7: Output Path Construction
    /// For simple filenames, extracting the prefix from constructed path SHALL return the normalized prefix.
    /// **Validates: Requirements 6.3, 6.5**
    /// </summary>
    [Property(MaxTest = 100)]
    public Property ExtractPrefix_FromSimpleFilename_ReturnsNormalizedPrefix()
    {
        return Prop.ForAll(
            OutputPathGenerators.AnyPrefixGen().ToArbitrary(),
            OutputPathGenerators.SimpleFilenameGen().ToArbitrary(),
            (prefix, filename) =>
            {
                // Act
                var outputPath = OutputPathHelper.ConstructOutputPath(prefix, filename);
                var extractedPrefix = OutputPathHelper.ExtractPrefix(outputPath);

                // Assert
                var normalizedPrefix = prefix.TrimEnd('/') + "/";
                return (extractedPrefix == normalizedPrefix)
                    .Label($"Extracted prefix '{extractedPrefix}' should equal normalized prefix '{normalizedPrefix}' for path '{outputPath}'");
            });
    }

    /// <summary>
    /// Feature: lambda-merge-tool, Property 7: Output Path Construction
    /// Empty prefix with simple filename SHALL return just the filename.
    /// **Validates: Requirements 6.3, 6.5**
    /// </summary>
    [Property(MaxTest = 100)]
    public Property EmptyPrefix_ReturnsJustFilename()
    {
        return Prop.ForAll(
            OutputPathGenerators.SimpleFilenameGen().ToArbitrary(),
            filename =>
            {
                // Act
                var outputPath = OutputPathHelper.ConstructOutputPath("", filename);

                // Assert
                return (outputPath == filename)
                    .Label($"Empty prefix with filename '{filename}' should return just the filename. Got: '{outputPath}'");
            });
    }
}
