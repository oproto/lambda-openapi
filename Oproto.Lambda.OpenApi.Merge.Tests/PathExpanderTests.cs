namespace Oproto.Lambda.OpenApi.Merge.Tests;

using Xunit;

/// <summary>
/// Unit tests for PathExpander utility class.
/// </summary>
public class PathExpanderTests
{
    [Fact]
    public void ExpandPath_NullPath_ReturnsNull()
    {
        var result = PathExpander.ExpandPath(null!);
        Assert.Null(result);
    }

    [Fact]
    public void ExpandPath_EmptyPath_ReturnsEmpty()
    {
        var result = PathExpander.ExpandPath(string.Empty);
        Assert.Equal(string.Empty, result);
    }

    [Fact]
    public void ExpandPath_NonTildePath_ReturnsUnchanged()
    {
        var path = "/some/absolute/path";
        var result = PathExpander.ExpandPath(path);
        Assert.Equal(path, result);
    }

    [Fact]
    public void ExpandPath_RelativePath_ReturnsUnchanged()
    {
        var path = "relative/path/file.json";
        var result = PathExpander.ExpandPath(path);
        Assert.Equal(path, result);
    }

    [Fact]
    public void ExpandPath_TildeOnly_ReturnsHomeDirectory()
    {
        var result = PathExpander.ExpandPath("~");
        var expected = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);
        Assert.Equal(expected, result);
    }

    [Fact]
    public void ExpandPath_TildeSlash_ExpandsToHomeDirectory()
    {
        var result = PathExpander.ExpandPath("~/documents/file.json");
        var home = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);
        var expected = Path.Combine(home, "documents/file.json");
        Assert.Equal(expected, result);
    }

    [Fact]
    public void ExpandPath_TildeBackslash_ExpandsToHomeDirectory()
    {
        var result = PathExpander.ExpandPath("~\\documents\\file.json");
        var home = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);
        var expected = Path.Combine(home, "documents\\file.json");
        Assert.Equal(expected, result);
    }

    [Fact]
    public void ExpandPath_TildeUsername_OnWindows_ThrowsArgumentException()
    {
        if (Environment.OSVersion.Platform != PlatformID.Win32NT)
        {
            // Skip this test on non-Windows platforms
            return;
        }

        var ex = Assert.Throws<ArgumentException>(() => PathExpander.ExpandPath("~someuser/path"));
        Assert.Contains("~username syntax is not supported on Windows", ex.Message);
    }

    [Fact]
    public void ExpandPath_TildeUsername_OnUnix_WithInvalidUser_ThrowsArgumentException()
    {
        if (Environment.OSVersion.Platform == PlatformID.Win32NT)
        {
            // Skip this test on Windows
            return;
        }

        var ex = Assert.Throws<ArgumentException>(() => PathExpander.ExpandPath("~nonexistentuser12345/path"));
        Assert.Contains("User 'nonexistentuser12345' not found", ex.Message);
    }

    [Fact]
    public void ExpandPath_TildeWithSubdirectory_ExpandsCorrectly()
    {
        var result = PathExpander.ExpandPath("~/subdir/nested/file.txt");
        var home = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);
        
        Assert.StartsWith(home, result);
        Assert.Contains("subdir", result);
        Assert.Contains("file.txt", result);
    }

    [Fact]
    public void ExpandPath_ResultIsAbsolutePath()
    {
        var result = PathExpander.ExpandPath("~/test");
        Assert.True(Path.IsPathRooted(result), "Expanded path should be absolute");
    }
}
