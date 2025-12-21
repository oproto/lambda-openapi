namespace Oproto.Lambda.OpenApi.Merge.Tests;

using System.Text.Json;
using Microsoft.OpenApi;
using Microsoft.OpenApi.Extensions;
using Microsoft.OpenApi.Models;
using Microsoft.OpenApi.Readers;
using Xunit;

/// <summary>
/// Integration tests for the OpenAPI merge functionality.
/// </summary>
public class IntegrationTests
{
    private static readonly string TestDataPath = Path.Combine(AppContext.BaseDirectory, "TestData");

    #region Config-Based Merge Tests

    /// <summary>
    /// Tests loading a basic config and merging sources.
    /// Validates: Requirements 6.1, 6.2
    /// </summary>
    [Fact]
    public async Task ConfigBasedMerge_BasicConfig_MergesSourcesSuccessfully()
    {
        // Arrange
        var configPath = Path.Combine(TestDataPath, "configs", "basic.json");
        var config = await LoadConfigurationAsync(configPath);

        // Load source documents
        var documents = new List<(SourceConfiguration Source, OpenApiDocument Document)>();
        foreach (var source in config.Sources)
        {
            var document = await LoadOpenApiDocumentAsync(source.Path);
            documents.Add((source, document));
        }

        // Act
        var merger = new OpenApiMerger();
        var result = merger.Merge(config, documents);

        // Assert
        Assert.True(result.Success);
        Assert.NotNull(result.Document);
        Assert.Equal("Merged API", result.Document.Info.Title);
        Assert.Equal("1.0.0", result.Document.Info.Version);
        
        // Verify paths from both sources are present
        Assert.Contains("/items", result.Document.Paths.Keys);
        Assert.Contains("/items/{id}", result.Document.Paths.Keys);
        Assert.Contains("/users", result.Document.Paths.Keys);
        Assert.Contains("/orders", result.Document.Paths.Keys);
        
        // Verify tags are merged
        Assert.Contains(result.Document.Tags, t => t.Name == "Users");
        Assert.Contains(result.Document.Tags, t => t.Name == "Orders");
    }

    /// <summary>
    /// Tests loading a config with path and operationId prefixes.
    /// Validates: Requirements 6.1, 6.2, 6.3
    /// </summary>
    [Fact]
    public async Task ConfigBasedMerge_WithPrefixes_AppliesPrefixesCorrectly()
    {
        // Arrange
        var configPath = Path.Combine(TestDataPath, "configs", "with-prefixes.json");
        var config = await LoadConfigurationAsync(configPath);

        var documents = new List<(SourceConfiguration Source, OpenApiDocument Document)>();
        foreach (var source in config.Sources)
        {
            var document = await LoadOpenApiDocumentAsync(source.Path);
            documents.Add((source, document));
        }

        // Act
        var merger = new OpenApiMerger();
        var result = merger.Merge(config, documents);

        // Assert
        Assert.True(result.Success);
        
        // Verify path prefixes are applied
        Assert.Contains("/items-service/items", result.Document.Paths.Keys);
        Assert.Contains("/items-service/items/{id}", result.Document.Paths.Keys);
        Assert.Contains("/products-service/products", result.Document.Paths.Keys);
        
        // Verify operationId prefixes are applied
        var itemsPath = result.Document.Paths["/items-service/items"];
        Assert.StartsWith("items_", itemsPath.Operations[OperationType.Get].OperationId);
        
        var productsPath = result.Document.Paths["/products-service/products"];
        Assert.StartsWith("products_", productsPath.Operations[OperationType.Get].OperationId);
    }

    /// <summary>
    /// Tests that servers from config are used in merged output.
    /// Validates: Requirements 6.2
    /// </summary>
    [Fact]
    public async Task ConfigBasedMerge_WithServers_UsesConfiguredServers()
    {
        // Arrange
        var configPath = Path.Combine(TestDataPath, "configs", "basic.json");
        var config = await LoadConfigurationAsync(configPath);

        var documents = new List<(SourceConfiguration Source, OpenApiDocument Document)>();
        foreach (var source in config.Sources)
        {
            var document = await LoadOpenApiDocumentAsync(source.Path);
            documents.Add((source, document));
        }

        // Act
        var merger = new OpenApiMerger();
        var result = merger.Merge(config, documents);

        // Assert
        Assert.Single(result.Document.Servers);
        Assert.Equal("https://api.example.com", result.Document.Servers[0].Url);
        Assert.Equal("Production", result.Document.Servers[0].Description);
    }

    /// <summary>
    /// Tests merging sources with schemas.
    /// Validates: Requirements 6.2
    /// </summary>
    [Fact]
    public async Task ConfigBasedMerge_WithSchemas_MergesSchemasCorrectly()
    {
        // Arrange
        var config = new MergeConfiguration
        {
            Info = new MergeInfoConfiguration
            {
                Title = "Schema Test API",
                Version = "1.0.0"
            },
            Sources = new List<SourceConfiguration>
            {
                new SourceConfiguration
                {
                    Path = Path.Combine(TestDataPath, "valid", "with-schemas.json"),
                    Name = "Products"
                }
            },
            SchemaConflict = SchemaConflictStrategy.Rename
        };

        var documents = new List<(SourceConfiguration Source, OpenApiDocument Document)>();
        foreach (var source in config.Sources)
        {
            var document = await LoadOpenApiDocumentAsync(source.Path);
            documents.Add((source, document));
        }

        // Act
        var merger = new OpenApiMerger();
        var result = merger.Merge(config, documents);

        // Assert
        Assert.True(result.Success);
        Assert.Contains("Product", result.Document.Components.Schemas.Keys);
        Assert.Contains("CreateProductRequest", result.Document.Components.Schemas.Keys);
    }

    /// <summary>
    /// Tests merging sources with security schemes.
    /// Validates: Requirements 6.2
    /// </summary>
    [Fact]
    public async Task ConfigBasedMerge_WithSecurity_MergesSecuritySchemesCorrectly()
    {
        // Arrange
        var config = new MergeConfiguration
        {
            Info = new MergeInfoConfiguration
            {
                Title = "Security Test API",
                Version = "1.0.0"
            },
            Sources = new List<SourceConfiguration>
            {
                new SourceConfiguration
                {
                    Path = Path.Combine(TestDataPath, "valid", "with-security.json"),
                    Name = "Security"
                }
            }
        };

        var documents = new List<(SourceConfiguration Source, OpenApiDocument Document)>();
        foreach (var source in config.Sources)
        {
            var document = await LoadOpenApiDocumentAsync(source.Path);
            documents.Add((source, document));
        }

        // Act
        var merger = new OpenApiMerger();
        var result = merger.Merge(config, documents);

        // Assert
        Assert.True(result.Success);
        Assert.Contains("bearerAuth", result.Document.Components.SecuritySchemes.Keys);
        Assert.Contains("apiKey", result.Document.Components.SecuritySchemes.Keys);
    }

    /// <summary>
    /// Tests that output file can be written successfully.
    /// Validates: Requirements 6.3
    /// </summary>
    [Fact]
    public async Task ConfigBasedMerge_OutputFile_WritesSuccessfully()
    {
        // Arrange
        var config = new MergeConfiguration
        {
            Info = new MergeInfoConfiguration
            {
                Title = "Output Test API",
                Version = "1.0.0"
            },
            Sources = new List<SourceConfiguration>
            {
                new SourceConfiguration
                {
                    Path = Path.Combine(TestDataPath, "valid", "simple-api.json"),
                    Name = "Simple"
                }
            },
            Output = Path.Combine(Path.GetTempPath(), $"test-output-{Guid.NewGuid()}.json")
        };

        var documents = new List<(SourceConfiguration Source, OpenApiDocument Document)>();
        foreach (var source in config.Sources)
        {
            var document = await LoadOpenApiDocumentAsync(source.Path);
            documents.Add((source, document));
        }

        try
        {
            // Act
            var merger = new OpenApiMerger();
            var result = merger.Merge(config, documents);

            // Write output
            var json = result.Document.SerializeAsJson(Microsoft.OpenApi.OpenApiSpecVersion.OpenApi3_0);
            await File.WriteAllTextAsync(config.Output, json);

            // Assert
            Assert.True(File.Exists(config.Output));
            
            // Verify the output is valid JSON and can be parsed back
            var outputContent = await File.ReadAllTextAsync(config.Output);
            var parsedDoc = JsonDocument.Parse(outputContent);
            Assert.Equal("Output Test API", parsedDoc.RootElement.GetProperty("info").GetProperty("title").GetString());
        }
        finally
        {
            // Cleanup
            if (File.Exists(config.Output))
            {
                File.Delete(config.Output);
            }
        }
    }

    #endregion

    #region Helper Methods

    private static async Task<MergeConfiguration> LoadConfigurationAsync(string configPath)
    {
        if (!File.Exists(configPath))
        {
            throw new FileNotFoundException($"Configuration file not found: {configPath}");
        }

        var json = await File.ReadAllTextAsync(configPath);
        var config = JsonSerializer.Deserialize<MergeConfiguration>(json);
        
        if (config == null)
        {
            throw new ConfigurationException("Failed to deserialize configuration file.");
        }

        // Resolve relative paths based on config file location
        var configDir = Path.GetDirectoryName(configPath) ?? ".";
        foreach (var source in config.Sources)
        {
            if (!Path.IsPathRooted(source.Path))
            {
                source.Path = Path.GetFullPath(Path.Combine(configDir, source.Path));
            }
        }

        if (!Path.IsPathRooted(config.Output))
        {
            config.Output = Path.GetFullPath(Path.Combine(configDir, config.Output));
        }

        return config;
    }

    private static async Task<OpenApiDocument> LoadOpenApiDocumentAsync(string path)
    {
        if (!File.Exists(path))
        {
            throw new FileNotFoundException($"Source file not found: {path}");
        }

        using var stream = File.OpenRead(path);
        var reader = new OpenApiStreamReader();
        var result = await reader.ReadAsync(stream);

        if (result.OpenApiDiagnostic.Errors.Count > 0)
        {
            var errors = string.Join("; ", result.OpenApiDiagnostic.Errors.Select(e => e.Message));
            throw new OpenApiValidationException($"Invalid OpenAPI specification in {path}: {errors}");
        }

        return result.OpenApiDocument;
    }

    #endregion
}


/// <summary>
/// Integration tests for error scenarios.
/// </summary>
public class ErrorScenarioTests
{
    private static readonly string TestDataPath = Path.Combine(AppContext.BaseDirectory, "TestData");

    #region Missing Source File Tests

    /// <summary>
    /// Tests that missing source file throws FileNotFoundException.
    /// Validates: Requirements 9.1
    /// </summary>
    [Fact]
    public async Task MissingSourceFile_ThrowsFileNotFoundException()
    {
        // Arrange
        var config = new MergeConfiguration
        {
            Info = new MergeInfoConfiguration
            {
                Title = "Test API",
                Version = "1.0.0"
            },
            Sources = new List<SourceConfiguration>
            {
                new SourceConfiguration
                {
                    Path = Path.Combine(TestDataPath, "nonexistent", "missing-file.json"),
                    Name = "Missing"
                }
            }
        };

        // Act & Assert
        var exception = await Assert.ThrowsAsync<FileNotFoundException>(async () =>
        {
            await LoadOpenApiDocumentAsync(config.Sources[0].Path);
        });

        Assert.Contains("missing-file.json", exception.Message);
    }

    #endregion

    #region Invalid JSON Tests

    /// <summary>
    /// Tests that invalid JSON file throws appropriate exception.
    /// Validates: Requirements 9.2
    /// </summary>
    [Fact]
    public async Task InvalidJson_ThrowsException()
    {
        // Arrange
        var invalidJsonPath = Path.Combine(TestDataPath, "invalid", "not-json.txt");

        // Act & Assert
        await Assert.ThrowsAnyAsync<Exception>(async () =>
        {
            await LoadOpenApiDocumentAsync(invalidJsonPath);
        });
    }

    #endregion

    #region Invalid OpenAPI Tests

    /// <summary>
    /// Tests that non-OpenAPI JSON file throws an exception.
    /// Validates: Requirements 9.3
    /// </summary>
    [Fact]
    public async Task InvalidOpenApi_ThrowsException()
    {
        // Arrange
        var notOpenApiPath = Path.Combine(TestDataPath, "invalid", "not-openapi.json");

        // Act & Assert
        // The OpenAPI reader may throw different exception types for invalid specs
        // (OpenApiValidationException, OpenApiUnsupportedSpecVersionException, etc.)
        await Assert.ThrowsAnyAsync<Exception>(async () =>
        {
            await LoadOpenApiDocumentAsync(notOpenApiPath);
        });
    }

    #endregion

    #region Missing Config Fields Tests

    /// <summary>
    /// Tests that config missing info.title throws ConfigurationException.
    /// Validates: Requirements 6.4
    /// </summary>
    [Fact]
    public void MissingConfigTitle_ThrowsConfigurationException()
    {
        // Arrange
        var config = new MergeConfiguration
        {
            Info = new MergeInfoConfiguration
            {
                Version = "1.0.0"
                // Title is missing
            },
            Sources = new List<SourceConfiguration>
            {
                new SourceConfiguration
                {
                    Path = Path.Combine(TestDataPath, "valid", "simple-api.json"),
                    Name = "Simple"
                }
            }
        };

        // Act & Assert
        var exception = Assert.Throws<ConfigurationException>(() =>
        {
            ValidateConfiguration(config);
        });

        Assert.Contains("info.title", exception.Message);
    }

    /// <summary>
    /// Tests that config missing info.version throws ConfigurationException.
    /// Validates: Requirements 6.4
    /// </summary>
    [Fact]
    public void MissingConfigVersion_ThrowsConfigurationException()
    {
        // Arrange
        var config = new MergeConfiguration
        {
            Info = new MergeInfoConfiguration
            {
                Title = "Test API"
                // Version is missing
            },
            Sources = new List<SourceConfiguration>
            {
                new SourceConfiguration
                {
                    Path = Path.Combine(TestDataPath, "valid", "simple-api.json"),
                    Name = "Simple"
                }
            }
        };

        // Act & Assert
        var exception = Assert.Throws<ConfigurationException>(() =>
        {
            ValidateConfiguration(config);
        });

        Assert.Contains("info.version", exception.Message);
    }

    /// <summary>
    /// Tests that config with empty sources throws ConfigurationException.
    /// Validates: Requirements 6.4
    /// </summary>
    [Fact]
    public void EmptySources_ThrowsConfigurationException()
    {
        // Arrange
        var config = new MergeConfiguration
        {
            Info = new MergeInfoConfiguration
            {
                Title = "Test API",
                Version = "1.0.0"
            },
            Sources = new List<SourceConfiguration>()
        };

        // Act & Assert
        var exception = Assert.Throws<ConfigurationException>(() =>
        {
            ValidateConfiguration(config);
        });

        Assert.Contains("sources", exception.Message);
    }

    /// <summary>
    /// Tests that config with source missing path throws ConfigurationException.
    /// Validates: Requirements 6.4
    /// </summary>
    [Fact]
    public void SourceMissingPath_ThrowsConfigurationException()
    {
        // Arrange
        var config = new MergeConfiguration
        {
            Info = new MergeInfoConfiguration
            {
                Title = "Test API",
                Version = "1.0.0"
            },
            Sources = new List<SourceConfiguration>
            {
                new SourceConfiguration
                {
                    Name = "NoPath"
                    // Path is missing
                }
            }
        };

        // Act & Assert
        var exception = Assert.Throws<ConfigurationException>(() =>
        {
            ValidateConfiguration(config);
        });

        Assert.Contains("sources[0].path", exception.Message);
    }

    /// <summary>
    /// Tests loading config file with missing info section.
    /// Validates: Requirements 6.4
    /// </summary>
    [Fact]
    public async Task LoadConfigWithMissingInfo_ThrowsConfigurationException()
    {
        // Arrange
        var configPath = Path.Combine(TestDataPath, "configs", "invalid-missing-info.json");

        // Act & Assert
        var exception = await Assert.ThrowsAsync<ConfigurationException>(async () =>
        {
            await LoadConfigurationAsync(configPath);
        });

        Assert.Contains("info.title", exception.Message);
    }

    #endregion

    #region Schema Conflict Tests

    /// <summary>
    /// Tests that schema conflict with Fail strategy throws SchemaMergeException.
    /// Validates: Requirements 9.3
    /// </summary>
    [Fact]
    public async Task SchemaConflictWithFailStrategy_ThrowsSchemaMergeException()
    {
        // Arrange
        var config = new MergeConfiguration
        {
            Info = new MergeInfoConfiguration
            {
                Title = "Conflict Test API",
                Version = "1.0.0"
            },
            Sources = new List<SourceConfiguration>
            {
                new SourceConfiguration
                {
                    Path = Path.Combine(TestDataPath, "conflicts", "different-schemas", "api1.json"),
                    Name = "API1"
                },
                new SourceConfiguration
                {
                    Path = Path.Combine(TestDataPath, "conflicts", "different-schemas", "api2.json"),
                    Name = "API2"
                }
            },
            SchemaConflict = SchemaConflictStrategy.Fail
        };

        var documents = new List<(SourceConfiguration Source, OpenApiDocument Document)>();
        foreach (var source in config.Sources)
        {
            var document = await LoadOpenApiDocumentAsync(source.Path);
            documents.Add((source, document));
        }

        // Act & Assert
        var merger = new OpenApiMerger();
        Assert.Throws<SchemaMergeException>(() =>
        {
            merger.Merge(config, documents);
        });
    }

    #endregion

    #region Path Conflict Tests

    /// <summary>
    /// Tests that duplicate paths generate warnings.
    /// Validates: Requirements 8.1
    /// </summary>
    [Fact]
    public async Task DuplicatePaths_GeneratesWarning()
    {
        // Arrange
        var config = new MergeConfiguration
        {
            Info = new MergeInfoConfiguration
            {
                Title = "Path Conflict Test API",
                Version = "1.0.0"
            },
            Sources = new List<SourceConfiguration>
            {
                new SourceConfiguration
                {
                    Path = Path.Combine(TestDataPath, "conflicts", "duplicate-paths", "api1.json"),
                    Name = "API1"
                },
                new SourceConfiguration
                {
                    Path = Path.Combine(TestDataPath, "conflicts", "duplicate-paths", "api2.json"),
                    Name = "API2"
                }
            }
        };

        var documents = new List<(SourceConfiguration Source, OpenApiDocument Document)>();
        foreach (var source in config.Sources)
        {
            var document = await LoadOpenApiDocumentAsync(source.Path);
            documents.Add((source, document));
        }

        // Act
        var merger = new OpenApiMerger();
        var result = merger.Merge(config, documents);

        // Assert
        Assert.True(result.Success);
        Assert.Contains(result.Warnings, w => w.Type == MergeWarningType.PathConflict);
        Assert.Contains(result.Warnings, w => w.Message.Contains("/shared/resource"));
    }

    #endregion

    #region Helper Methods

    private static async Task<MergeConfiguration> LoadConfigurationAsync(string configPath)
    {
        if (!File.Exists(configPath))
        {
            throw new FileNotFoundException($"Configuration file not found: {configPath}");
        }

        var json = await File.ReadAllTextAsync(configPath);
        var config = System.Text.Json.JsonSerializer.Deserialize<MergeConfiguration>(json);
        
        if (config == null)
        {
            throw new ConfigurationException("Failed to deserialize configuration file.");
        }

        // Validate required fields
        ValidateConfiguration(config);

        // Resolve relative paths based on config file location
        var configDir = Path.GetDirectoryName(configPath) ?? ".";
        foreach (var source in config.Sources)
        {
            if (!Path.IsPathRooted(source.Path))
            {
                source.Path = Path.GetFullPath(Path.Combine(configDir, source.Path));
            }
        }

        if (!Path.IsPathRooted(config.Output))
        {
            config.Output = Path.GetFullPath(Path.Combine(configDir, config.Output));
        }

        return config;
    }

    private static void ValidateConfiguration(MergeConfiguration config)
    {
        var missingFields = new List<string>();

        if (config.Info == null)
        {
            missingFields.Add("info");
        }
        else
        {
            if (string.IsNullOrWhiteSpace(config.Info.Title))
            {
                missingFields.Add("info.title");
            }
            if (string.IsNullOrWhiteSpace(config.Info.Version))
            {
                missingFields.Add("info.version");
            }
        }

        if (config.Sources == null || config.Sources.Count == 0)
        {
            missingFields.Add("sources");
        }
        else
        {
            for (int i = 0; i < config.Sources.Count; i++)
            {
                if (string.IsNullOrWhiteSpace(config.Sources[i].Path))
                {
                    missingFields.Add($"sources[{i}].path");
                }
            }
        }

        if (missingFields.Count > 0)
        {
            throw new ConfigurationException($"Missing required fields: {string.Join(", ", missingFields)}");
        }
    }

    private static async Task<OpenApiDocument> LoadOpenApiDocumentAsync(string path)
    {
        if (!File.Exists(path))
        {
            throw new FileNotFoundException($"Source file not found: {path}");
        }

        using var stream = File.OpenRead(path);
        var reader = new OpenApiStreamReader();
        var result = await reader.ReadAsync(stream);

        if (result.OpenApiDiagnostic.Errors.Count > 0)
        {
            var errors = string.Join("; ", result.OpenApiDiagnostic.Errors.Select(e => e.Message));
            throw new OpenApiValidationException($"Invalid OpenAPI specification in {path}: {errors}");
        }

        return result.OpenApiDocument;
    }

    #endregion
}
