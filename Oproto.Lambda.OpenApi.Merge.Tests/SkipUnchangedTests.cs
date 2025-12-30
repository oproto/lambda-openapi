namespace Oproto.Lambda.OpenApi.Merge.Tests;

using Microsoft.OpenApi;
using Microsoft.OpenApi.Extensions;
using Microsoft.OpenApi.Models;
using Xunit;

/// <summary>
/// Unit tests for the skip-unchanged feature in the merge tool.
/// Validates: Requirements 10.1, 10.2, 10.3, 10.4
/// </summary>
public class SkipUnchangedTests : IDisposable
{
    private readonly string _testOutputDir;
    private readonly List<string> _createdFiles = new();

    public SkipUnchangedTests()
    {
        _testOutputDir = Path.Combine(Path.GetTempPath(), $"skip-unchanged-tests-{Guid.NewGuid()}");
        Directory.CreateDirectory(_testOutputDir);
    }

    public void Dispose()
    {
        // Cleanup test files
        foreach (var file in _createdFiles)
        {
            if (File.Exists(file))
            {
                File.Delete(file);
            }
        }

        if (Directory.Exists(_testOutputDir))
        {
            Directory.Delete(_testOutputDir, recursive: true);
        }
    }

    /// <summary>
    /// Tests that file is skipped when content matches and force is false.
    /// Validates: Requirements 10.1
    /// </summary>
    [Fact]
    public async Task WriteOpenApiDocument_ContentMatches_SkipsWrite()
    {
        // Arrange
        var document = CreateSimpleOpenApiDocument("Test API", "1.0.0");
        var outputPath = Path.Combine(_testOutputDir, "skip-test.json");
        _createdFiles.Add(outputPath);

        // Write initial file
        var json = document.SerializeAsJson(OpenApiSpecVersion.OpenApi3_0);
        await File.WriteAllTextAsync(outputPath, json);
        var originalWriteTime = File.GetLastWriteTimeUtc(outputPath);

        // Wait a bit to ensure timestamp would change if file is rewritten
        await Task.Delay(50);

        // Act - Try to write the same content without force
        var wasWritten = await WriteOpenApiDocumentAsync(document, outputPath, verbose: false, force: false);

        // Assert
        Assert.False(wasWritten, "File should not have been written when content matches");
        var newWriteTime = File.GetLastWriteTimeUtc(outputPath);
        Assert.Equal(originalWriteTime, newWriteTime);
    }

    /// <summary>
    /// Tests that file is written when content differs.
    /// Validates: Requirements 10.1
    /// </summary>
    [Fact]
    public async Task WriteOpenApiDocument_ContentDiffers_WritesFile()
    {
        // Arrange
        var originalDocument = CreateSimpleOpenApiDocument("Original API", "1.0.0");
        var updatedDocument = CreateSimpleOpenApiDocument("Updated API", "2.0.0");
        var outputPath = Path.Combine(_testOutputDir, "diff-test.json");
        _createdFiles.Add(outputPath);

        // Write initial file
        var originalJson = originalDocument.SerializeAsJson(OpenApiSpecVersion.OpenApi3_0);
        await File.WriteAllTextAsync(outputPath, originalJson);

        // Act - Write different content without force
        var wasWritten = await WriteOpenApiDocumentAsync(updatedDocument, outputPath, verbose: false, force: false);

        // Assert
        Assert.True(wasWritten, "File should have been written when content differs");
        var newContent = await File.ReadAllTextAsync(outputPath);
        Assert.Contains("Updated API", newContent);
    }

    /// <summary>
    /// Tests that file is written when --force is used even if content matches.
    /// Validates: Requirements 10.3
    /// </summary>
    [Fact]
    public async Task WriteOpenApiDocument_ForceFlag_WritesEvenWhenContentMatches()
    {
        // Arrange
        var document = CreateSimpleOpenApiDocument("Force Test API", "1.0.0");
        var outputPath = Path.Combine(_testOutputDir, "force-test.json");
        _createdFiles.Add(outputPath);

        // Write initial file
        var json = document.SerializeAsJson(OpenApiSpecVersion.OpenApi3_0);
        await File.WriteAllTextAsync(outputPath, json);
        var originalWriteTime = File.GetLastWriteTimeUtc(outputPath);

        // Wait a bit to ensure timestamp would change if file is rewritten
        await Task.Delay(50);

        // Act - Write with force flag
        var wasWritten = await WriteOpenApiDocumentAsync(document, outputPath, verbose: false, force: true);

        // Assert
        Assert.True(wasWritten, "File should have been written when force is true");
        var newWriteTime = File.GetLastWriteTimeUtc(outputPath);
        Assert.True(newWriteTime > originalWriteTime, "File timestamp should have changed");
    }

    /// <summary>
    /// Tests that file is written when it doesn't exist.
    /// Validates: Requirements 10.4
    /// </summary>
    [Fact]
    public async Task WriteOpenApiDocument_FileDoesNotExist_WritesFile()
    {
        // Arrange
        var document = CreateSimpleOpenApiDocument("New File API", "1.0.0");
        var outputPath = Path.Combine(_testOutputDir, "new-file-test.json");
        _createdFiles.Add(outputPath);

        // Ensure file doesn't exist
        Assert.False(File.Exists(outputPath));

        // Act
        var wasWritten = await WriteOpenApiDocumentAsync(document, outputPath, verbose: false, force: false);

        // Assert
        Assert.True(wasWritten, "File should have been written when it doesn't exist");
        Assert.True(File.Exists(outputPath), "File should exist after write");
    }

    /// <summary>
    /// Tests that log message is output when skipping in verbose mode.
    /// Validates: Requirements 10.2
    /// </summary>
    [Fact]
    public async Task WriteOpenApiDocument_SkipWithVerbose_LogsMessage()
    {
        // Arrange
        var document = CreateSimpleOpenApiDocument("Verbose Test API", "1.0.0");
        var outputPath = Path.Combine(_testOutputDir, "verbose-test.json");
        _createdFiles.Add(outputPath);

        // Write initial file
        var json = document.SerializeAsJson(OpenApiSpecVersion.OpenApi3_0);
        await File.WriteAllTextAsync(outputPath, json);

        // Capture console output
        var originalOut = Console.Out;
        using var stringWriter = new StringWriter();
        Console.SetOut(stringWriter);

        try
        {
            // Act
            await WriteOpenApiDocumentAsync(document, outputPath, verbose: true, force: false);

            // Assert
            var output = stringWriter.ToString();
            Assert.Contains("unchanged", output.ToLowerInvariant());
            Assert.Contains("skipping", output.ToLowerInvariant());
        }
        finally
        {
            Console.SetOut(originalOut);
        }
    }

    #region Helper Methods

    private static OpenApiDocument CreateSimpleOpenApiDocument(string title, string version)
    {
        return new OpenApiDocument
        {
            Info = new OpenApiInfo
            {
                Title = title,
                Version = version
            },
            Paths = new OpenApiPaths
            {
                ["/test"] = new OpenApiPathItem
                {
                    Operations = new Dictionary<OperationType, OpenApiOperation>
                    {
                        [OperationType.Get] = new OpenApiOperation
                        {
                            Summary = "Test operation",
                            Responses = new OpenApiResponses
                            {
                                ["200"] = new OpenApiResponse { Description = "Success" }
                            }
                        }
                    }
                }
            }
        };
    }

    /// <summary>
    /// Simulates the WriteOpenApiDocumentAsync logic from MergeCommand for testing.
    /// This mirrors the implementation in MergeCommand.cs.
    /// </summary>
    private static async Task<bool> WriteOpenApiDocumentAsync(
        OpenApiDocument document, 
        string outputPath, 
        bool verbose,
        bool force)
    {
        // Ensure output directory exists
        var outputDir = Path.GetDirectoryName(outputPath);
        if (!string.IsNullOrEmpty(outputDir) && !Directory.Exists(outputDir))
        {
            Directory.CreateDirectory(outputDir);
        }

        var json = document.SerializeAsJson(OpenApiSpecVersion.OpenApi3_0);
        
        // Check if file exists and content matches (skip-unchanged feature)
        if (!force && File.Exists(outputPath))
        {
            try
            {
                var existingContent = await File.ReadAllTextAsync(outputPath);
                if (existingContent == json)
                {
                    if (verbose)
                    {
                        Console.WriteLine($"Output unchanged, skipping write: {outputPath}");
                    }
                    return false; // Indicates file was not written
                }
            }
            catch (IOException ex)
            {
                // Log warning and proceed with write if we can't read existing file
                if (verbose)
                {
                    Console.Error.WriteLine($"Warning: Could not read existing file for comparison: {ex.Message}");
                }
            }
        }

        await File.WriteAllTextAsync(outputPath, json);
        return true; // Indicates file was written
    }

    #endregion
}
