namespace Oproto.Lambda.OpenApi.Merge.Tests;

using Oproto.Lambda.OpenApi.Merge.Tool.Commands;
using Xunit;

/// <summary>
/// Unit tests for CLI auto-discover functionality.
/// Validates: Requirements 3.2, 3.9
/// </summary>
public class MergeCommandAutoDiscoverTests
{
    #region MatchesGlobPattern Tests

    [Theory]
    [InlineData("test.json", "*.json", true)]
    [InlineData("test.json", "*.txt", false)]
    [InlineData("api-draft.json", "*-draft.json", true)]
    [InlineData("api-final.json", "*-draft.json", false)]
    [InlineData("backup.api.json", "*.backup.json", false)]
    [InlineData("api.backup.json", "*.backup.json", true)]
    [InlineData("test.json", "test.json", true)]
    [InlineData("other.json", "test.json", false)]
    [InlineData("a.json", "?.json", true)]
    [InlineData("ab.json", "?.json", false)]
    [InlineData("TEST.JSON", "*.json", true)] // Case insensitive
    [InlineData("Api-Draft.json", "*-draft.json", true)] // Case insensitive
    public void MatchesGlobPattern_VariousPatterns_ReturnsExpectedResult(
        string fileName, string pattern, bool expected)
    {
        // Act
        var result = MergeCommand.MatchesGlobPattern(fileName, pattern);

        // Assert
        Assert.Equal(expected, result);
    }

    [Fact]
    public void MatchesGlobPattern_EmptyPattern_MatchesEmptyFileName()
    {
        // Act & Assert
        Assert.True(MergeCommand.MatchesGlobPattern("", ""));
        Assert.False(MergeCommand.MatchesGlobPattern("test.json", ""));
    }

    [Fact]
    public void MatchesGlobPattern_WildcardOnly_MatchesAnyFileName()
    {
        // Act & Assert
        Assert.True(MergeCommand.MatchesGlobPattern("anything.json", "*"));
        Assert.True(MergeCommand.MatchesGlobPattern("", "*"));
        Assert.True(MergeCommand.MatchesGlobPattern("complex-file-name.backup.json", "*"));
    }

    #endregion

    #region MatchesExcludePattern Tests

    [Fact]
    public void MatchesExcludePattern_EmptyPatternList_ReturnsFalse()
    {
        // Arrange
        var patterns = new List<string>();

        // Act
        var result = MergeCommand.MatchesExcludePattern("test.json", patterns);

        // Assert
        Assert.False(result);
    }

    [Fact]
    public void MatchesExcludePattern_SingleMatchingPattern_ReturnsTrue()
    {
        // Arrange
        var patterns = new List<string> { "*-draft.json" };

        // Act
        var result = MergeCommand.MatchesExcludePattern("api-draft.json", patterns);

        // Assert
        Assert.True(result);
    }

    [Fact]
    public void MatchesExcludePattern_SingleNonMatchingPattern_ReturnsFalse()
    {
        // Arrange
        var patterns = new List<string> { "*-draft.json" };

        // Act
        var result = MergeCommand.MatchesExcludePattern("api-final.json", patterns);

        // Assert
        Assert.False(result);
    }

    [Fact]
    public void MatchesExcludePattern_MultiplePatterns_MatchesAny()
    {
        // Arrange
        var patterns = new List<string> { "*-draft.json", "*.backup.json", "temp-*" };

        // Act & Assert
        Assert.True(MergeCommand.MatchesExcludePattern("api-draft.json", patterns));
        Assert.True(MergeCommand.MatchesExcludePattern("old.backup.json", patterns));
        Assert.True(MergeCommand.MatchesExcludePattern("temp-file.json", patterns));
        Assert.False(MergeCommand.MatchesExcludePattern("api-final.json", patterns));
    }

    [Fact]
    public void MatchesExcludePattern_CaseInsensitive_MatchesRegardlessOfCase()
    {
        // Arrange
        var patterns = new List<string> { "*-DRAFT.json" };

        // Act
        var result = MergeCommand.MatchesExcludePattern("api-draft.json", patterns);

        // Assert
        Assert.True(result);
    }

    #endregion
}
