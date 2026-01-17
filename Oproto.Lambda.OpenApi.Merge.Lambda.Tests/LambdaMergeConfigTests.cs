using System.Text.Json;
using Oproto.Lambda.OpenApi.Merge;
using Oproto.Lambda.OpenApi.Merge.Lambda.Models;

namespace Oproto.Lambda.OpenApi.Merge.Lambda.Tests;

/// <summary>
/// Unit tests for LambdaMergeConfig deserialization and default values.
/// </summary>
public class LambdaMergeConfigTests
{
    [Fact]
    public void Deserialize_MinimalConfig_SetsDefaults()
    {
        // Arrange
        var json = """
        {
            "info": {
                "title": "Test API",
                "version": "1.0.0"
            },
            "output": "merged.json"
        }
        """;

        // Act
        var config = JsonSerializer.Deserialize<LambdaMergeConfig>(json);

        // Assert
        Assert.NotNull(config);
        Assert.Equal("Test API", config.Info.Title);
        Assert.Equal("1.0.0", config.Info.Version);
        Assert.Equal("merged.json", config.Output);
        Assert.False(config.AutoDiscover);
        Assert.Empty(config.ExcludePatterns);
        Assert.Empty(config.Sources);
        Assert.Empty(config.Servers);
        Assert.Null(config.OutputBucket);
        Assert.Equal(SchemaConflictStrategy.Rename, config.SchemaConflict);
    }

    [Fact]
    public void Deserialize_FullConfig_SetsAllProperties()
    {
        // Arrange
        var json = """
        {
            "info": {
                "title": "Full API",
                "version": "2.0.0",
                "description": "A complete API"
            },
            "servers": [
                {
                    "url": "https://api.example.com",
                    "description": "Production"
                }
            ],
            "autoDiscover": true,
            "excludePatterns": ["*-draft.json", "*.backup.json"],
            "output": "api.json",
            "outputBucket": "output-bucket",
            "schemaConflict": "fail"
        }
        """;

        // Act
        var config = JsonSerializer.Deserialize<LambdaMergeConfig>(json);

        // Assert
        Assert.NotNull(config);
        Assert.Equal("Full API", config.Info.Title);
        Assert.Equal("2.0.0", config.Info.Version);
        Assert.Equal("A complete API", config.Info.Description);
        Assert.Single(config.Servers);
        Assert.Equal("https://api.example.com", config.Servers[0].Url);
        Assert.Equal("Production", config.Servers[0].Description);
        Assert.True(config.AutoDiscover);
        Assert.Equal(2, config.ExcludePatterns.Count);
        Assert.Contains("*-draft.json", config.ExcludePatterns);
        Assert.Contains("*.backup.json", config.ExcludePatterns);
        Assert.Equal("api.json", config.Output);
        Assert.Equal("output-bucket", config.OutputBucket);
        Assert.Equal(SchemaConflictStrategy.Fail, config.SchemaConflict);
    }

    [Fact]
    public void Deserialize_WithExplicitSources_LoadsSources()
    {
        // Arrange
        var json = """
        {
            "info": {
                "title": "API with Sources",
                "version": "1.0.0"
            },
            "autoDiscover": false,
            "sources": [
                {
                    "path": "users.json",
                    "name": "Users",
                    "pathPrefix": "/users"
                },
                {
                    "path": "orders.json",
                    "name": "Orders"
                }
            ],
            "output": "merged.json"
        }
        """;

        // Act
        var config = JsonSerializer.Deserialize<LambdaMergeConfig>(json);

        // Assert
        Assert.NotNull(config);
        Assert.False(config.AutoDiscover);
        Assert.Equal(2, config.Sources.Count);
        Assert.Equal("users.json", config.Sources[0].Path);
        Assert.Equal("Users", config.Sources[0].Name);
        Assert.Equal("/users", config.Sources[0].PathPrefix);
        Assert.Equal("orders.json", config.Sources[1].Path);
        Assert.Equal("Orders", config.Sources[1].Name);
    }

    [Fact]
    public void Deserialize_OutputBucketNull_WhenNotSpecified()
    {
        // Arrange
        var json = """
        {
            "info": {
                "title": "Test",
                "version": "1.0.0"
            }
        }
        """;

        // Act
        var config = JsonSerializer.Deserialize<LambdaMergeConfig>(json);

        // Assert
        Assert.NotNull(config);
        Assert.Null(config.OutputBucket);
    }

    [Fact]
    public void Serialize_RoundTrip_PreservesAllProperties()
    {
        // Arrange
        var original = new LambdaMergeConfig
        {
            Info = new MergeInfoConfiguration
            {
                Title = "Round Trip API",
                Version = "3.0.0",
                Description = "Testing round trip"
            },
            AutoDiscover = true,
            ExcludePatterns = new List<string> { "*.draft.json" },
            Output = "output.json",
            OutputBucket = "my-bucket",
            SchemaConflict = SchemaConflictStrategy.FirstWins
        };

        // Act
        var json = JsonSerializer.Serialize(original);
        var deserialized = JsonSerializer.Deserialize<LambdaMergeConfig>(json);

        // Assert
        Assert.NotNull(deserialized);
        Assert.Equal(original.Info.Title, deserialized.Info.Title);
        Assert.Equal(original.Info.Version, deserialized.Info.Version);
        Assert.Equal(original.Info.Description, deserialized.Info.Description);
        Assert.Equal(original.AutoDiscover, deserialized.AutoDiscover);
        Assert.Equal(original.ExcludePatterns, deserialized.ExcludePatterns);
        Assert.Equal(original.Output, deserialized.Output);
        Assert.Equal(original.OutputBucket, deserialized.OutputBucket);
        Assert.Equal(original.SchemaConflict, deserialized.SchemaConflict);
    }

    [Fact]
    public void LambdaMergeConfig_InheritsFromMergeConfiguration()
    {
        // Arrange & Act
        var config = new LambdaMergeConfig();

        // Assert - verify inheritance
        Assert.IsAssignableFrom<MergeConfiguration>(config);
    }
}
