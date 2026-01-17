namespace Oproto.Lambda.OpenApi.Merge.Lambda.Services;

/// <summary>
/// Interface for comparing OpenAPI document outputs.
/// </summary>
public interface IOutputComparer
{
    /// <summary>
    /// Normalizes JSON content for comparison by sorting keys and using consistent formatting.
    /// </summary>
    /// <param name="json">The JSON content to normalize.</param>
    /// <returns>Normalized JSON string.</returns>
    string NormalizeJson(string json);

    /// <summary>
    /// Compares two JSON strings for semantic equality, ignoring formatting differences.
    /// </summary>
    /// <param name="json1">First JSON string.</param>
    /// <param name="json2">Second JSON string.</param>
    /// <returns>True if the JSON documents are semantically equivalent, false otherwise.</returns>
    bool AreEquivalent(string json1, string json2);
}
