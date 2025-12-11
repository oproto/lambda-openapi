using System;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text.RegularExpressions;
using Microsoft.Build.Framework;
using Microsoft.Build.Utilities;

namespace Oproto.Lambda.OpenApi.Build;

public class ExtractOpenApiSpecTask : Task
{
    [Required] public string AssemblyPath { get; set; }

    [Required] public string OutputPath { get; set; }

    /// <summary>
    /// Optional path to the intermediate output directory where generated files are stored.
    /// Used for AOT-compatible extraction from source files.
    /// </summary>
    public string IntermediateOutputPath { get; set; }

    public override bool Execute()
    {
        try
        {
            Log.LogMessage(MessageImportance.High, $"Loading assembly from: {AssemblyPath}");

            // Validate assembly path exists
            if (!File.Exists(AssemblyPath))
            {
                Log.LogError($"Assembly not found: {AssemblyPath}");
                return false;
            }

            // Validate OutputPath
            if (string.IsNullOrEmpty(Path.GetExtension(OutputPath)))
                OutputPath = Path.Combine(OutputPath, "openapi.json");

            // Strategy 1: Try parsing generated source file (AOT-compatible)
            var json = TryExtractFromSourceFile();

            // Strategy 2: Fall back to reflection (non-AOT)
            if (json == null)
            {
                json = TryExtractViaReflection();
            }

            if (json == null)
            {
                Log.LogWarning("No OpenAPI specification found. " +
                    "For AOT builds, ensure EmitCompilerGeneratedFiles=true is set in your project.");
                return true;
            }

            // Ensure directory exists
            var directory = Path.GetDirectoryName(OutputPath);
            if (!string.IsNullOrEmpty(directory)) Directory.CreateDirectory(directory);

            // Write the spec to file
            File.WriteAllText(OutputPath, json);

            Log.LogMessage(MessageImportance.Normal,
                $"Successfully wrote OpenAPI specification to: {OutputPath}");

            return true;
        }
        catch (Exception ex)
        {
            Log.LogError($"Failed to extract or write OpenAPI specification: {ex.Message}");
            Log.LogMessage(MessageImportance.High, $"Stack trace: {ex.StackTrace}");
            return false;
        }
    }

    /// <summary>
    /// Attempts to extract the OpenAPI JSON from the generated source file.
    /// This method is AOT-compatible as it doesn't require loading the assembly.
    /// </summary>
    private string TryExtractFromSourceFile()
    {
        var sourceFilePath = FindGeneratedSourceFile();
        if (sourceFilePath == null || !File.Exists(sourceFilePath))
        {
            Log.LogMessage(MessageImportance.Low,
                "Generated source file not found. Will try reflection fallback.");
            return null;
        }

        Log.LogMessage(MessageImportance.Normal,
            $"Found generated source file: {sourceFilePath}");

        return ExtractJsonFromSourceFile(sourceFilePath);
    }

    /// <summary>
    /// Finds the generated OpenApiOutput.g.cs file in the intermediate output directory.
    /// </summary>
    private string FindGeneratedSourceFile()
    {
        // Build possible paths to the generated file
        var possiblePaths = new[]
        {
            // Standard path when EmitCompilerGeneratedFiles=true
            GetGeneratedFilePath("Oproto.Lambda.OpenApi.SourceGenerator",
                "Oproto.Lambda.OpenApi.SourceGenerator.OpenApiSpecGenerator",
                "OpenApiOutput.g.cs"),
            // Alternative path structure
            GetGeneratedFilePath("Oproto.Lambda.OpenApi.SourceGenerator",
                "OpenApiSpecGenerator",
                "OpenApiOutput.g.cs"),
        };

        foreach (var path in possiblePaths)
        {
            if (path != null && File.Exists(path))
            {
                return path;
            }
        }

        return null;
    }

    private string GetGeneratedFilePath(string generatorName, string generatorTypeName, string fileName)
    {
        string objDir;

        if (!string.IsNullOrEmpty(IntermediateOutputPath))
        {
            // IntermediateOutputPath is typically obj/Debug/net8.0/ - go up to obj/
            var intermediateDir = IntermediateOutputPath.TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);
            objDir = Path.GetDirectoryName(Path.GetDirectoryName(intermediateDir));
            if (objDir == null)
            {
                // Fallback: just use obj relative to IntermediateOutputPath parent
                objDir = Path.Combine(Path.GetDirectoryName(Path.GetDirectoryName(Path.GetDirectoryName(intermediateDir))) ?? "", "obj");
            }
        }
        else
        {
            // Try to infer from AssemblyPath (bin/Debug/net8.0/assembly.dll)
            var assemblyDir = Path.GetDirectoryName(AssemblyPath);
            if (assemblyDir == null) return null;

            // Go up from bin/Debug/net8.0 to project root, then into obj
            var projectDir = Path.GetDirectoryName(Path.GetDirectoryName(Path.GetDirectoryName(assemblyDir)));
            if (projectDir == null) return null;

            objDir = Path.Combine(projectDir, "obj");
        }

        // Generated files are in obj/GeneratedFiles/
        var path = Path.Combine(objDir, "GeneratedFiles", generatorName, generatorTypeName, fileName);
        Log.LogMessage(MessageImportance.Low, $"Looking for generated file at: {path}");
        return path;
    }

    /// <summary>
    /// Extracts the JSON string from the generated source file content.
    /// The file contains: [assembly: OpenApiOutput(@"...", "openapi.json")]
    /// </summary>
    private string ExtractJsonFromSourceFile(string filePath)
    {
        try
        {
            var content = File.ReadAllText(filePath);

            // Match the OpenApiOutput attribute with verbatim string
            // In verbatim strings, quotes are escaped as "" so we need to match that pattern
            // Pattern: [assembly: OpenApiOutput(@"...", "...")]
            // The verbatim string content can contain "" (escaped quotes) so we match either
            // non-quote characters or pairs of quotes
            var match = Regex.Match(content,
                @"\[assembly:\s*OpenApiOutput\s*\(\s*@""((?:[^""]|"""")*)""\s*,",
                RegexOptions.Singleline);

            if (match.Success)
            {
                // Unescape the verbatim string (double quotes become single quotes)
                var json = match.Groups[1].Value.Replace("\"\"", "\"");
                Log.LogMessage(MessageImportance.Low,
                    "Successfully extracted JSON from generated source file.");
                return json;
            }

            Log.LogMessage(MessageImportance.Low,
                "Could not parse OpenApiOutput attribute from generated source file.");
            return null;
        }
        catch (Exception ex)
        {
            Log.LogMessage(MessageImportance.Low,
                $"Error reading generated source file: {ex.Message}");
            return null;
        }
    }

    /// <summary>
    /// Attempts to extract the OpenAPI JSON via reflection.
    /// This is the fallback method for non-AOT scenarios.
    /// </summary>
    private string TryExtractViaReflection()
    {
        try
        {
            // Read assembly bytes into memory to avoid file locking
            // This allows the file to be deleted after the task completes
            var assemblyBytes = File.ReadAllBytes(AssemblyPath);
            var assembly = Assembly.Load(assemblyBytes);
            var attributes = assembly.GetCustomAttributes();

            // Find the attribute by name without using type comparison
            var attribute = attributes
                .FirstOrDefault(a => a.GetType().FullName == "Oproto.Lambda.OpenApi.Attributes.OpenApiOutputAttribute");

            if (attribute == null)
            {
                Log.LogMessage(MessageImportance.Low,
                    $"No OpenApiOutput attribute found in assembly via reflection.");
                return null;
            }

            var json = attribute.GetType().GetProperty("Specification")?.GetValue(attribute) as string;
            Log.LogMessage(MessageImportance.Low,
                "Successfully extracted JSON via reflection.");
            return json;
        }
        catch (Exception ex)
        {
            Log.LogMessage(MessageImportance.Low,
                $"Reflection extraction failed: {ex.Message}");
            return null;
        }
    }
}
