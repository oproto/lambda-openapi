using System;
using System.IO;
using System.Linq;
using System.Reflection;
using Microsoft.Build.Framework;
using Microsoft.Build.Utilities;

namespace Oproto.OpenApiGenerator.Tasks;

public class ExtractOpenApiSpecTask : Task
{
    [Required] public string AssemblyPath { get; set; }

    [Required] public string OutputPath { get; set; }

    public override bool Execute()
    {
        try
        {
            Log.LogMessage(MessageImportance.High, $"Loading assembly from: {AssemblyPath}");

            // Validate OutputPath
            if (string.IsNullOrEmpty(Path.GetExtension(OutputPath)))
                OutputPath = Path.Combine(OutputPath, "openapi.json");

            var assembly = Assembly.LoadFrom(AssemblyPath);
            var attributes = assembly.GetCustomAttributes();

            // Find the attribute by name without using type comparison
            var attribute = attributes
                .FirstOrDefault(a => a.GetType().FullName == "Oproto.OpenApi.OpenApiOutputAttribute");

            if (attribute == null)
            {
                Log.LogWarning($"No OpenApiOutput attribute found in assembly: {AssemblyPath}");
                return true;
            }

            // Ensure directory exists
            var directory = Path.GetDirectoryName(OutputPath);
            if (!string.IsNullOrEmpty(directory)) Directory.CreateDirectory(directory);

            // Write the spec to file
            var json = attribute.GetType().GetProperty("Specification")?.GetValue(attribute) as string;
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
}