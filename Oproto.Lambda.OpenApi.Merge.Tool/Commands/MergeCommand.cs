namespace Oproto.Lambda.OpenApi.Merge.Tool.Commands;

using System.CommandLine;
using System.Text.Json;
using Microsoft.OpenApi;
using Microsoft.OpenApi.Extensions;
using Microsoft.OpenApi.Models;
using Microsoft.OpenApi.Readers;
using Oproto.Lambda.OpenApi.Merge;

/// <summary>
/// Command for merging multiple OpenAPI specifications.
/// </summary>
public class MergeCommand : Command
{
    public MergeCommand() : base("merge", "Merge multiple OpenAPI specifications")
    {
        // Config-based invocation
        var configOption = new Option<FileInfo?>(
            "--config",
            "Path to merge configuration JSON file");

        // Direct invocation options
        var outputOption = new Option<FileInfo>(
            new[] { "-o", "--output" },
            () => new FileInfo("merged-openapi.json"),
            "Output file path");

        var titleOption = new Option<string?>(
            "--title",
            "API title for merged specification");

        var versionOption = new Option<string?>(
            "--version",
            "API version for merged specification");

        var schemaConflictOption = new Option<SchemaConflictStrategy>(
            "--schema-conflict",
            () => SchemaConflictStrategy.Rename,
            "Strategy for handling schema conflicts");

        var verboseOption = new Option<bool>(
            new[] { "-v", "--verbose" },
            "Show detailed progress and warnings");

        // Positional argument for direct file list
        var filesArgument = new Argument<FileInfo[]>(
            "files",
            "OpenAPI specification files to merge")
        {
            Arity = ArgumentArity.ZeroOrMore
        };

        AddOption(configOption);
        AddOption(outputOption);
        AddOption(titleOption);
        AddOption(versionOption);
        AddOption(schemaConflictOption);
        AddOption(verboseOption);
        AddArgument(filesArgument);

        this.SetHandler(ExecuteAsync, configOption, outputOption, titleOption,
            versionOption, schemaConflictOption, verboseOption, filesArgument);
    }

    private async Task<int> ExecuteAsync(
        FileInfo? config,
        FileInfo output,
        string? title,
        string? version,
        SchemaConflictStrategy schemaConflict,
        bool verbose,
        FileInfo[] files)
    {
        try
        {
            MergeConfiguration mergeConfig;

            if (config != null)
            {
                // Config-based invocation
                if (verbose)
                {
                    Console.WriteLine($"Loading configuration from: {config.FullName}");
                }

                mergeConfig = await LoadConfigurationAsync(config, verbose);
            }
            else if (files.Length > 0)
            {
                // Direct invocation mode
                if (string.IsNullOrWhiteSpace(title) || string.IsNullOrWhiteSpace(version))
                {
                    Console.Error.WriteLine("Error: --title and --version are required when not using a config file.");
                    Console.Error.WriteLine("Usage: openapi-merge merge --title \"API Title\" --version \"1.0.0\" file1.json file2.json");
                    return 1;
                }

                mergeConfig = BuildConfigurationFromArgs(title, version, schemaConflict, output, files);
            }
            else
            {
                Console.Error.WriteLine("Error: Either --config or input files must be specified.");
                Console.Error.WriteLine("Usage: openapi-merge merge --config merge.config.json");
                Console.Error.WriteLine("   or: openapi-merge merge --title \"API Title\" --version \"1.0.0\" file1.json file2.json");
                return 1;
            }

            // Override output if specified via CLI
            if (config != null && output.Name != "merged-openapi.json")
            {
                mergeConfig.Output = output.FullName;
            }

            // Load source documents
            if (verbose)
            {
                Console.WriteLine($"Loading {mergeConfig.Sources.Count} source file(s)...");
            }

            var documents = new List<(SourceConfiguration Source, OpenApiDocument Document)>();
            foreach (var source in mergeConfig.Sources)
            {
                var document = await LoadOpenApiDocumentAsync(source.Path, verbose);
                documents.Add((source, document));
            }

            // Perform merge
            if (verbose)
            {
                Console.WriteLine("Merging specifications...");
            }

            var merger = new OpenApiMerger();
            var result = merger.Merge(mergeConfig, documents);

            // Output warnings to stderr
            foreach (var warning in result.Warnings)
            {
                Console.Error.WriteLine($"Warning: {warning}");
            }

            // Write output file
            var outputPath = mergeConfig.Output;
            if (verbose)
            {
                Console.WriteLine($"Writing merged specification to: {outputPath}");
            }

            await WriteOpenApiDocumentAsync(result.Document, outputPath, verbose);

            if (verbose)
            {
                Console.WriteLine($"Merge completed successfully with {result.Warnings.Count} warning(s).");
            }
            else
            {
                Console.WriteLine($"Merged {documents.Count} specifications into {outputPath}");
            }

            return 0;
        }
        catch (FileNotFoundException ex)
        {
            Console.Error.WriteLine($"Error: File not found - {ex.Message}");
            return 1;
        }
        catch (JsonException ex)
        {
            Console.Error.WriteLine($"Error: Invalid JSON - {ex.Message}");
            return 1;
        }
        catch (ConfigurationException ex)
        {
            Console.Error.WriteLine($"Error: Configuration error - {ex.Message}");
            return 1;
        }
        catch (OpenApiValidationException ex)
        {
            Console.Error.WriteLine($"Error: Invalid OpenAPI specification - {ex.Message}");
            return 3;
        }
        catch (SchemaMergeException ex)
        {
            Console.Error.WriteLine($"Error: Schema merge conflict - {ex.Message}");
            return 2;
        }
        catch (Exception ex)
        {
            Console.Error.WriteLine($"Error: {ex.Message}");
            return 1;
        }
    }

    private static async Task<MergeConfiguration> LoadConfigurationAsync(FileInfo configFile, bool verbose)
    {
        if (!configFile.Exists)
        {
            throw new FileNotFoundException($"Configuration file not found: {configFile.FullName}");
        }

        var json = await File.ReadAllTextAsync(configFile.FullName);
        
        var config = JsonSerializer.Deserialize<MergeConfiguration>(json);
        if (config == null)
        {
            throw new ConfigurationException("Failed to deserialize configuration file.");
        }

        // Validate required fields
        ValidateConfiguration(config);

        // Resolve relative paths based on config file location
        var configDir = configFile.DirectoryName ?? ".";
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

        if (verbose)
        {
            Console.WriteLine($"  Title: {config.Info.Title}");
            Console.WriteLine($"  Version: {config.Info.Version}");
            Console.WriteLine($"  Sources: {config.Sources.Count}");
            Console.WriteLine($"  Output: {config.Output}");
            Console.WriteLine($"  Schema Conflict Strategy: {config.SchemaConflict}");
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

    private static MergeConfiguration BuildConfigurationFromArgs(
        string title,
        string version,
        SchemaConflictStrategy schemaConflict,
        FileInfo output,
        FileInfo[] files)
    {
        var config = new MergeConfiguration
        {
            Info = new MergeInfoConfiguration
            {
                Title = title,
                Version = version
            },
            SchemaConflict = schemaConflict,
            Output = output.FullName
        };

        foreach (var file in files)
        {
            config.Sources.Add(new SourceConfiguration
            {
                Path = file.FullName,
                Name = Path.GetFileNameWithoutExtension(file.Name)
            });
        }

        return config;
    }

    private static async Task<OpenApiDocument> LoadOpenApiDocumentAsync(string path, bool verbose)
    {
        if (!File.Exists(path))
        {
            throw new FileNotFoundException($"Source file not found: {path}");
        }

        if (verbose)
        {
            Console.WriteLine($"  Loading: {path}");
        }

        using var stream = File.OpenRead(path);
        var reader = new OpenApiStreamReader();
        var result = await reader.ReadAsync(stream);

        if (result.OpenApiDiagnostic.Errors.Count > 0)
        {
            var errorMessages = result.OpenApiDiagnostic.Errors.Select(e => $"  - {e.Message}");
            var errors = string.Join(Environment.NewLine, errorMessages);
            throw new OpenApiValidationException($"Invalid OpenAPI specification in {path}:{Environment.NewLine}{errors}");
        }

        return result.OpenApiDocument;
    }

    private static async Task WriteOpenApiDocumentAsync(OpenApiDocument document, string outputPath, bool verbose)
    {
        // Validate the merged document before writing
        var errors = document.Validate(Microsoft.OpenApi.Validations.ValidationRuleSet.GetDefaultRuleSet());
        var errorList = errors.ToList();
        
        if (errorList.Count > 0)
        {
            var errorMessages = errorList.Select(e => $"  - {e.Message} (at {e.Pointer})");
            var errorText = string.Join(Environment.NewLine, errorMessages);
            
            if (verbose)
            {
                Console.Error.WriteLine($"Validation issues found:{Environment.NewLine}{errorText}");
            }
            
            throw new OpenApiValidationException($"Merged specification validation failed:{Environment.NewLine}{errorText}");
        }

        // Ensure output directory exists
        var outputDir = Path.GetDirectoryName(outputPath);
        if (!string.IsNullOrEmpty(outputDir) && !Directory.Exists(outputDir))
        {
            Directory.CreateDirectory(outputDir);
        }

        var json = document.SerializeAsJson(OpenApiSpecVersion.OpenApi3_0);
        await File.WriteAllTextAsync(outputPath, json);
    }
}
