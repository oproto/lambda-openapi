namespace Oproto.Lambda.OpenApi.Merge.Tool.Commands;

using System.CommandLine;
using System.Text.Json;
using System.Text.RegularExpressions;
using Microsoft.OpenApi;
using Microsoft.OpenApi.Extensions;
using Microsoft.OpenApi.Models;
using Microsoft.OpenApi.Readers;
using Oproto.Lambda.OpenApi.Merge;

// PathExpander is used for tilde path expansion

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

        var forceOption = new Option<bool>(
            new[] { "-f", "--force" },
            "Force write output even if unchanged");

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
        AddOption(forceOption);
        AddArgument(filesArgument);

        this.SetHandler(ExecuteAsync, configOption, outputOption, titleOption,
            versionOption, schemaConflictOption, verboseOption, forceOption, filesArgument);
    }

    private async Task<int> ExecuteAsync(
        FileInfo? config,
        FileInfo output,
        string? title,
        string? version,
        SchemaConflictStrategy schemaConflict,
        bool verbose,
        bool force,
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

            var wasWritten = await WriteOpenApiDocumentAsync(result.Document, outputPath, verbose, force);

            if (verbose)
            {
                Console.WriteLine($"Merge completed successfully with {result.Warnings.Count} warning(s).");
            }
            else
            {
                if (wasWritten)
                {
                    Console.WriteLine($"Merged {documents.Count} specifications into {outputPath}");
                }
                else
                {
                    Console.WriteLine($"Output unchanged, skipped writing: {outputPath}");
                }
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

        // Resolve relative paths based on config file location
        var configDir = configFile.DirectoryName ?? ".";

        // Handle auto-discover mode
        if (config.AutoDiscover)
        {
            if (verbose)
            {
                Console.WriteLine("  Auto-discover mode enabled, scanning for JSON files...");
            }
            
            var discoveredSources = DiscoverSourceFiles(configDir, config, verbose);
            config.Sources = discoveredSources;
            
            if (config.Sources.Count == 0)
            {
                throw new ConfigurationException("No source files found in auto-discover mode.");
            }
        }
        else
        {
            // Validate required fields for explicit sources mode
            ValidateConfiguration(config);

            // Resolve relative paths for explicit sources
            foreach (var source in config.Sources)
            {
                // First expand tilde paths
                source.Path = PathExpander.ExpandPath(source.Path);
                
                // Then resolve relative paths
                if (!Path.IsPathRooted(source.Path))
                {
                    source.Path = Path.GetFullPath(Path.Combine(configDir, source.Path));
                }
            }
        }

        // Expand tilde in output path
        config.Output = PathExpander.ExpandPath(config.Output);
        
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
            Console.WriteLine($"  Auto-Discover: {config.AutoDiscover}");
            if (config.ExcludePatterns.Count > 0)
            {
                Console.WriteLine($"  Exclude Patterns: {string.Join(", ", config.ExcludePatterns)}");
            }
        }

        return config;
    }

    /// <summary>
    /// Discovers source files in the specified directory based on configuration.
    /// </summary>
    private static List<SourceConfiguration> DiscoverSourceFiles(string directory, MergeConfiguration config, bool verbose)
    {
        var sources = new List<SourceConfiguration>();
        var outputFileName = Path.GetFileName(config.Output);
        
        // Get all JSON files in the directory
        var jsonFiles = Directory.GetFiles(directory, "*.json", SearchOption.TopDirectoryOnly);
        
        foreach (var filePath in jsonFiles)
        {
            var fileName = Path.GetFileName(filePath);
            
            // Skip config.json
            if (fileName.Equals("config.json", StringComparison.OrdinalIgnoreCase))
            {
                if (verbose)
                {
                    Console.WriteLine($"    Skipping config file: {fileName}");
                }
                continue;
            }
            
            // Skip output file
            if (fileName.Equals(outputFileName, StringComparison.OrdinalIgnoreCase))
            {
                if (verbose)
                {
                    Console.WriteLine($"    Skipping output file: {fileName}");
                }
                continue;
            }
            
            // Check exclude patterns
            if (MatchesExcludePattern(fileName, config.ExcludePatterns))
            {
                if (verbose)
                {
                    Console.WriteLine($"    Excluding (pattern match): {fileName}");
                }
                continue;
            }
            
            if (verbose)
            {
                Console.WriteLine($"    Discovered: {fileName}");
            }
            
            sources.Add(new SourceConfiguration
            {
                Path = filePath,
                Name = Path.GetFileNameWithoutExtension(fileName)
            });
        }
        
        // Sort sources by name for deterministic ordering
        sources.Sort((a, b) => string.Compare(a.Name, b.Name, StringComparison.Ordinal));
        
        return sources;
    }

    /// <summary>
    /// Checks if a filename matches any of the exclude patterns.
    /// Supports simple glob patterns: * (any characters), ? (single character)
    /// </summary>
    internal static bool MatchesExcludePattern(string fileName, List<string> excludePatterns)
    {
        foreach (var pattern in excludePatterns)
        {
            if (MatchesGlobPattern(fileName, pattern))
            {
                return true;
            }
        }
        return false;
    }

    /// <summary>
    /// Matches a filename against a simple glob pattern.
    /// Supports * (any characters) and ? (single character).
    /// </summary>
    internal static bool MatchesGlobPattern(string fileName, string pattern)
    {
        // Convert glob pattern to regex
        var regexPattern = "^" + Regex.Escape(pattern)
            .Replace("\\*", ".*")
            .Replace("\\?", ".") + "$";
        
        return Regex.IsMatch(fileName, regexPattern, RegexOptions.IgnoreCase);
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
        // Expand tilde in path
        var expandedPath = PathExpander.ExpandPath(path);
        
        if (!File.Exists(expandedPath))
        {
            var errorMessage = $"Source file not found: {path}";
            if (expandedPath != path)
                errorMessage += $" (expanded to: {expandedPath})";
            throw new FileNotFoundException(errorMessage);
        }

        if (verbose)
        {
            if (expandedPath != path)
                Console.WriteLine($"  Loading: {path} (expanded to: {expandedPath})");
            else
                Console.WriteLine($"  Loading: {path}");
        }

        using var stream = File.OpenRead(expandedPath);
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

    private static async Task<bool> WriteOpenApiDocumentAsync(OpenApiDocument document, string outputPath, bool verbose, bool force)
    {
        // Expand tilde in output path
        var expandedPath = PathExpander.ExpandPath(outputPath);
        
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
        var outputDir = Path.GetDirectoryName(expandedPath);
        if (!string.IsNullOrEmpty(outputDir) && !Directory.Exists(outputDir))
        {
            Directory.CreateDirectory(outputDir);
        }

        var json = document.SerializeAsJson(OpenApiSpecVersion.OpenApi3_0);
        
        // Check if file exists and content matches (skip-unchanged feature)
        if (!force && File.Exists(expandedPath))
        {
            try
            {
                var existingContent = await File.ReadAllTextAsync(expandedPath);
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

        await File.WriteAllTextAsync(expandedPath, json);
        return true; // Indicates file was written
    }
}
