using FsCheck;
using FsCheck.Xunit;
using Oproto.Lambda.OpenApi.Merge;

namespace Oproto.Lambda.OpenApi.Merge.Tests;

/// <summary>
/// Property-based tests for PathExpander utility class.
/// </summary>
public class PathExpanderPropertyTests
{
    /// <summary>
    /// Generators for path-related test data.
    /// </summary>
    private static class PathGenerators
    {
        /// <summary>
        /// Generates valid relative path segments.
        /// </summary>
        public static Gen<string> PathSegmentGen()
        {
            return Gen.Elements(
                "documents", "downloads", "projects", "code", "config",
                "api", "specs", "openapi", "merged", "output", "test"
            );
        }

        /// <summary>
        /// Generates a relative path with 1-4 segments.
        /// </summary>
        public static Gen<string> RelativePathGen()
        {
            return from segmentCount in Gen.Choose(1, 4)
                   from segments in Gen.ListOf(segmentCount, PathSegmentGen())
                   select string.Join("/", segments);
        }

        /// <summary>
        /// Generates file names with common extensions.
        /// </summary>
        public static Gen<string> FileNameGen()
        {
            return from name in Gen.Elements("openapi", "merged", "api", "spec", "config", "output")
                   from ext in Gen.Elements(".json", ".yaml", ".yml", ".txt")
                   select name + ext;
        }

        /// <summary>
        /// Generates a tilde path (~/relative/path/file.ext).
        /// </summary>
        public static Gen<string> TildePathGen()
        {
            return from relativePath in RelativePathGen()
                   from fileName in FileNameGen()
                   select $"~/{relativePath}/{fileName}";
        }

        /// <summary>
        /// Generates a non-tilde path (absolute or relative).
        /// </summary>
        public static Gen<string> NonTildePathGen()
        {
            return Gen.OneOf(
                // Absolute Unix-style paths
                from relativePath in RelativePathGen()
                from fileName in FileNameGen()
                select $"/home/user/{relativePath}/{fileName}",
                // Relative paths
                from relativePath in RelativePathGen()
                from fileName in FileNameGen()
                select $"{relativePath}/{fileName}"
            );
        }
    }

    /// <summary>
    /// Feature: generator-bug-fixes, Property 8: Tilde Path Expansion
    /// For any file path starting with `~/`, the merge tool SHALL expand it to the user's home directory 
    /// before attempting to access the file.
    /// **Validates: Requirements 5.1, 5.3**
    /// </summary>
    [Property(MaxTest = 100)]
    public Property TildePaths_ShouldExpandToHomeDirectory()
    {
        return Prop.ForAll(
            PathGenerators.TildePathGen().ToArbitrary(),
            (tildePath) =>
            {
                var result = PathExpander.ExpandPath(tildePath);
                var home = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);

                // Property: expanded path should start with home directory
                var startsWithHome = result.StartsWith(home);
                
                // Property: expanded path should be absolute
                var isAbsolute = Path.IsPathRooted(result);
                
                // Property: expanded path should not contain tilde
                var noTilde = !result.Contains("~");

                return startsWithHome
                    .Label($"Expanded path '{result}' should start with home directory '{home}'")
                    .And(isAbsolute)
                    .Label($"Expanded path '{result}' should be absolute")
                    .And(noTilde)
                    .Label($"Expanded path '{result}' should not contain tilde");
            });
    }

    /// <summary>
    /// Feature: generator-bug-fixes, Property 8: Tilde Path Expansion
    /// For any file path NOT starting with `~`, the path SHALL remain unchanged.
    /// **Validates: Requirements 5.1, 5.3**
    /// </summary>
    [Property(MaxTest = 100)]
    public Property NonTildePaths_ShouldRemainUnchanged()
    {
        return Prop.ForAll(
            PathGenerators.NonTildePathGen().ToArbitrary(),
            (path) =>
            {
                var result = PathExpander.ExpandPath(path);

                // Property: non-tilde paths should remain unchanged
                return (result == path)
                    .Label($"Non-tilde path '{path}' should remain unchanged, got '{result}'");
            });
    }

    /// <summary>
    /// Feature: generator-bug-fixes, Property 8: Tilde Path Expansion
    /// Tilde expansion should preserve the relative path portion after the tilde.
    /// **Validates: Requirements 5.1, 5.3**
    /// </summary>
    [Property(MaxTest = 100)]
    public Property TildeExpansion_ShouldPreserveRelativePath()
    {
        return Prop.ForAll(
            PathGenerators.RelativePathGen().ToArbitrary(),
            PathGenerators.FileNameGen().ToArbitrary(),
            (relativePath, fileName) =>
            {
                var tildePath = $"~/{relativePath}/{fileName}";
                var result = PathExpander.ExpandPath(tildePath);
                var home = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);

                // Property: the relative path portion should be preserved
                var expectedSuffix = Path.Combine(relativePath, fileName);
                var containsRelativePath = result.Contains(relativePath) && result.Contains(fileName);

                return containsRelativePath
                    .Label($"Expanded path '{result}' should contain relative path '{relativePath}' and file '{fileName}'");
            });
    }

    /// <summary>
    /// Feature: generator-bug-fixes, Property 8: Tilde Path Expansion
    /// Tilde-only path should expand to exactly the home directory.
    /// **Validates: Requirements 5.1**
    /// </summary>
    [Property(MaxTest = 100)]
    public Property TildeOnly_ShouldExpandToExactHomeDirectory()
    {
        // This is a simple property that doesn't need generation
        return Prop.ForAll(
            Gen.Constant("~").ToArbitrary(),
            (tilde) =>
            {
                var result = PathExpander.ExpandPath(tilde);
                var home = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);

                return (result == home)
                    .Label($"'~' should expand to '{home}', got '{result}'");
            });
    }

    /// <summary>
    /// Feature: generator-bug-fixes, Property 8: Tilde Path Expansion
    /// Empty and null paths should be returned as-is.
    /// **Validates: Requirements 5.1**
    /// </summary>
    [Property(MaxTest = 100)]
    public Property EmptyPaths_ShouldRemainUnchanged()
    {
        return Prop.ForAll(
            Gen.Elements("", null!).ToArbitrary(),
            (path) =>
            {
                var result = PathExpander.ExpandPath(path);

                return (result == path)
                    .Label($"Empty/null path should remain unchanged");
            });
    }
}
