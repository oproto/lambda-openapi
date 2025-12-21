using Oproto.Lambda.OpenApi.Merge;
using System.Text.Json;

namespace Oproto.Lambda.OpenApi.Merge.Tests;

/// <summary>
/// Unit tests for MergeConfiguration.
/// </summary>
public class MergeConfigurationTests
{
    [Fact]
    public void MergeConfiguration_DefaultValues_AreCorrect()
    {
        var config = new MergeConfiguration();

        Assert.NotNull(config.Info);
        Assert.NotNull(config.Servers);
        Assert.Empty(config.Servers);
        Assert.NotNull(config.Sources);
        Assert.Empty(config.Sources);
        Assert.Equal("merged-openapi.json", config.Output);
        Assert.Equal(SchemaConflictStrategy.Rename, config.SchemaConflict);
    }

    [Fact]
    public void SourceConfiguration_DefaultValues_AreCorrect()
    {
        var source = new SourceConfiguration();

        Assert.Equal(string.Empty, source.Path);
        Assert.Null(source.PathPrefix);
        Assert.Null(source.OperationIdPrefix);
        Assert.Null(source.Name);
    }

    [Fact]
    public void MergeConfiguration_Deserialize_FullConfig()
    {
        var json = """
        {
            "info": {
                "title": "Test API",
                "version": "1.0.0",
                "description": "Test description"
            },
            "servers": [
                { "url": "https://api.example.com", "description": "Production" }
            ],
            "sources": [
                { "path": "./api1.json", "pathPrefix": "/v1", "operationIdPrefix": "api1_", "name": "API1" }
            ],
            "output": "output.json",
            "schemaConflict": "rename"
        }
        """;

        var config = JsonSerializer.Deserialize<MergeConfiguration>(json);

        Assert.NotNull(config);
        Assert.Equal("Test API", config.Info.Title);
        Assert.Equal("1.0.0", config.Info.Version);
        Assert.Equal("Test description", config.Info.Description);
        Assert.Single(config.Servers);
        Assert.Equal("https://api.example.com", config.Servers[0].Url);
        Assert.Equal("Production", config.Servers[0].Description);
        Assert.Single(config.Sources);
        Assert.Equal("./api1.json", config.Sources[0].Path);
        Assert.Equal("/v1", config.Sources[0].PathPrefix);
        Assert.Equal("api1_", config.Sources[0].OperationIdPrefix);
        Assert.Equal("API1", config.Sources[0].Name);
        Assert.Equal("output.json", config.Output);
        Assert.Equal(SchemaConflictStrategy.Rename, config.SchemaConflict);
    }

    [Fact]
    public void MergeConfiguration_Deserialize_MinimalConfig()
    {
        var json = """
        {
            "info": {
                "title": "Minimal API",
                "version": "1.0"
            },
            "sources": [
                { "path": "./api.json" }
            ]
        }
        """;

        var config = JsonSerializer.Deserialize<MergeConfiguration>(json);

        Assert.NotNull(config);
        Assert.Equal("Minimal API", config.Info.Title);
        Assert.Equal("1.0", config.Info.Version);
        Assert.Null(config.Info.Description);
        Assert.Empty(config.Servers);
        Assert.Single(config.Sources);
        Assert.Equal("./api.json", config.Sources[0].Path);
        Assert.Null(config.Sources[0].PathPrefix);
        Assert.Equal("merged-openapi.json", config.Output);
        Assert.Equal(SchemaConflictStrategy.Rename, config.SchemaConflict);
    }

    [Theory]
    [InlineData("rename", SchemaConflictStrategy.Rename)]
    [InlineData("first-wins", SchemaConflictStrategy.FirstWins)]
    [InlineData("fail", SchemaConflictStrategy.Fail)]
    [InlineData("RENAME", SchemaConflictStrategy.Rename)]
    [InlineData("FIRST-WINS", SchemaConflictStrategy.FirstWins)]
    [InlineData("FAIL", SchemaConflictStrategy.Fail)]
    public void MergeConfiguration_Deserialize_SchemaConflictStrategy(string strategyValue, SchemaConflictStrategy expected)
    {
        var json = $$"""
        {
            "schemaConflict": "{{strategyValue}}"
        }
        """;

        var config = JsonSerializer.Deserialize<MergeConfiguration>(json);

        Assert.NotNull(config);
        Assert.Equal(expected, config.SchemaConflict);
    }

    [Fact]
    public void MergeConfiguration_Serialize_RoundTrip()
    {
        var config = new MergeConfiguration
        {
            Info = new MergeInfoConfiguration
            {
                Title = "Round Trip API",
                Version = "2.0.0",
                Description = "Testing serialization"
            },
            Servers = new List<MergeServerConfiguration>
            {
                new() { Url = "https://api.test.com", Description = "Test Server" }
            },
            Sources = new List<SourceConfiguration>
            {
                new() { Path = "./test.json", PathPrefix = "/api", OperationIdPrefix = "test_", Name = "Test" }
            },
            Output = "merged.json",
            SchemaConflict = SchemaConflictStrategy.FirstWins
        };

        var json = JsonSerializer.Serialize(config);
        var deserialized = JsonSerializer.Deserialize<MergeConfiguration>(json);

        Assert.NotNull(deserialized);
        Assert.Equal(config.Info.Title, deserialized.Info.Title);
        Assert.Equal(config.Info.Version, deserialized.Info.Version);
        Assert.Equal(config.Info.Description, deserialized.Info.Description);
        Assert.Equal(config.Servers.Count, deserialized.Servers.Count);
        Assert.Equal(config.Servers[0].Url, deserialized.Servers[0].Url);
        Assert.Equal(config.Sources.Count, deserialized.Sources.Count);
        Assert.Equal(config.Sources[0].Path, deserialized.Sources[0].Path);
        Assert.Equal(config.Output, deserialized.Output);
        Assert.Equal(config.SchemaConflict, deserialized.SchemaConflict);
    }

    [Fact]
    public void SchemaConflictStrategy_Serialize_UsesKebabCase()
    {
        var config = new MergeConfiguration { SchemaConflict = SchemaConflictStrategy.FirstWins };

        var json = JsonSerializer.Serialize(config);

        Assert.Contains("\"first-wins\"", json);
    }

    [Fact]
    public void MergeConfiguration_Deserialize_InvalidSchemaConflict_Throws()
    {
        var json = """
        {
            "schemaConflict": "invalid-strategy"
        }
        """;

        Assert.Throws<JsonException>(() => JsonSerializer.Deserialize<MergeConfiguration>(json));
    }

    [Fact]
    public void MergeInfoConfiguration_DefaultValues_AreNull()
    {
        var info = new MergeInfoConfiguration();

        Assert.Null(info.Title);
        Assert.Null(info.Version);
        Assert.Null(info.Description);
    }

    [Fact]
    public void MergeServerConfiguration_DefaultValues_AreCorrect()
    {
        var server = new MergeServerConfiguration();

        Assert.Equal(string.Empty, server.Url);
        Assert.Null(server.Description);
    }
}
