using System.Diagnostics;
using System.Text.Json;

namespace Oproto.OpenApiGenerator.UnitTests;

public static class JsonTestHelper
{
    public static string ExtractAndCleanJsonContent(string generatedContent)
    {
        var contentStart = generatedContent.IndexOf("Content = @\"") + 11;
        var contentEnd = generatedContent.IndexOf("\";");
        var rawJson = generatedContent.Substring(contentStart, contentEnd - contentStart);
        // Clean the JSON string
        return rawJson
            .Replace("\"\"", "\"")
            .Replace("\r\n", " ")
            .Replace("\n", " ")
            .Replace("\r", " ")
            .Replace("\\\"", "\"")
            .Trim()
            .TrimStart('"')
            .TrimEnd('"');
    }

    public static JsonElement ParseAndValidateOpenApiSchema(string jsonContent)
    {
        try
        {
            var options = new JsonSerializerOptions
            {
                AllowTrailingCommas = true,
                ReadCommentHandling = JsonCommentHandling.Skip
            };

            var element = JsonSerializer.Deserialize<JsonElement>(jsonContent, options);

            // Validate basic OpenAPI structure
            if (!element.TryGetProperty("paths", out _))
                throw new JsonException("Missing 'paths' property in OpenAPI schema");

            if (!element.TryGetProperty("components", out var components))
                throw new JsonException("Missing 'components' property in OpenAPI schema");

            if (!components.TryGetProperty("schemas", out _))
                throw new JsonException("Missing 'schemas' property in components");

            return element;
        }
        catch (JsonException ex)
        {
            Debug.WriteLine("\n=== JSON Parse Error ===");
            Debug.WriteLine($"Error: {ex.Message}");
            Debug.WriteLine("JSON Content:");
            Debug.WriteLine(jsonContent);
            throw;
        }
    }
}