namespace Oproto.Lambda.OpenApi.Merge.Lambda.Services;

using System.Text.Json;
using System.Text.Json.Nodes;
using Microsoft.Extensions.Logging;

/// <summary>
/// Service for comparing OpenAPI document outputs with JSON normalization.
/// Implements semantic comparison that ignores formatting differences.
/// </summary>
public class OutputComparer : IOutputComparer
{
    private readonly ILogger<OutputComparer> _logger;

    private static readonly JsonSerializerOptions NormalizedJsonOptions = new()
    {
        WriteIndented = true,
        PropertyNamingPolicy = null, // Preserve original property names
        Encoder = System.Text.Encodings.Web.JavaScriptEncoder.UnsafeRelaxedJsonEscaping
    };

    /// <summary>
    /// Initializes a new instance of the OutputComparer.
    /// </summary>
    /// <param name="logger">The logger.</param>
    public OutputComparer(ILogger<OutputComparer> logger)
    {
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    /// <inheritdoc />
    public string NormalizeJson(string json)
    {
        if (string.IsNullOrWhiteSpace(json))
        {
            return string.Empty;
        }

        try
        {
            var node = JsonNode.Parse(json);
            if (node == null)
            {
                return string.Empty;
            }

            var sortedNode = SortJsonNode(node);
            return sortedNode?.ToJsonString(NormalizedJsonOptions) ?? string.Empty;
        }
        catch (JsonException ex)
        {
            _logger.LogWarning(ex, "Failed to parse JSON for normalization, returning original");
            return json;
        }
    }

    /// <inheritdoc />
    public bool AreEquivalent(string json1, string json2)
    {
        // Handle null/empty cases
        if (string.IsNullOrWhiteSpace(json1) && string.IsNullOrWhiteSpace(json2))
        {
            return true;
        }

        if (string.IsNullOrWhiteSpace(json1) || string.IsNullOrWhiteSpace(json2))
        {
            return false;
        }

        try
        {
            var normalized1 = NormalizeJson(json1);
            var normalized2 = NormalizeJson(json2);

            var areEqual = string.Equals(normalized1, normalized2, StringComparison.Ordinal);

            if (!areEqual)
            {
                _logger.LogDebug("JSON documents differ after normalization");
            }

            return areEqual;
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Error comparing JSON documents, treating as different");
            return false;
        }
    }

    /// <summary>
    /// Recursively sorts a JSON node's properties alphabetically.
    /// </summary>
    /// <param name="node">The JSON node to sort.</param>
    /// <returns>A new sorted JSON node.</returns>
    private static JsonNode? SortJsonNode(JsonNode? node)
    {
        if (node == null)
        {
            return null;
        }

        switch (node)
        {
            case JsonObject obj:
                return SortJsonObject(obj);

            case JsonArray arr:
                return SortJsonArray(arr);

            default:
                // Value nodes (string, number, bool, null) - return a copy
                return JsonNode.Parse(node.ToJsonString());
        }
    }

    /// <summary>
    /// Sorts a JSON object's properties alphabetically.
    /// </summary>
    /// <param name="obj">The JSON object to sort.</param>
    /// <returns>A new sorted JSON object.</returns>
    private static JsonObject SortJsonObject(JsonObject obj)
    {
        var sortedObj = new JsonObject();

        // Get all properties sorted alphabetically
        var sortedProperties = obj
            .OrderBy(kvp => kvp.Key, StringComparer.Ordinal)
            .ToList();

        foreach (var kvp in sortedProperties)
        {
            sortedObj[kvp.Key] = SortJsonNode(kvp.Value);
        }

        return sortedObj;
    }

    /// <summary>
    /// Processes a JSON array, sorting any nested objects.
    /// Note: Array element order is preserved as it may be semantically significant.
    /// </summary>
    /// <param name="arr">The JSON array to process.</param>
    /// <returns>A new JSON array with sorted nested objects.</returns>
    private static JsonArray SortJsonArray(JsonArray arr)
    {
        var sortedArr = new JsonArray();

        foreach (var item in arr)
        {
            sortedArr.Add(SortJsonNode(item));
        }

        return sortedArr;
    }
}
