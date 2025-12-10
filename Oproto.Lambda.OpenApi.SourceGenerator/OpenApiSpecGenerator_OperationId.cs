using Microsoft.CodeAnalysis;

namespace Oproto.Lambda.OpenApi.SourceGenerator;

/// <summary>
/// Partial class containing operationId generation logic.
/// </summary>
public partial class OpenApiSpecGenerator
{
    /// <summary>
    /// Tracks used operation IDs to ensure uniqueness across the generated specification.
    /// </summary>
    private readonly HashSet<string> _usedOperationIds = new(StringComparer.OrdinalIgnoreCase);

    /// <summary>
    /// Gets the operationId for an endpoint, either from the [OpenApiOperationId] attribute
    /// or generated from the method name. Ensures uniqueness by appending numeric suffixes if needed.
    /// </summary>
    /// <param name="methodSymbol">The method symbol to get the operationId for.</param>
    /// <param name="methodName">The method name to use as fallback for generation.</param>
    /// <returns>A unique operationId string.</returns>
    private string GetOperationId(IMethodSymbol? methodSymbol, string methodName)
    {
        // First, check for [OpenApiOperationId] attribute
        var customOperationId = GetOperationIdFromAttribute(methodSymbol);
        var baseOperationId = customOperationId ?? methodName;

        // Ensure uniqueness
        return EnsureUniqueOperationId(baseOperationId);
    }

    /// <summary>
    /// Reads the [OpenApiOperationId] attribute from a method symbol.
    /// </summary>
    /// <param name="methodSymbol">The method symbol to check for the attribute.</param>
    /// <returns>The custom operationId if the attribute is present, null otherwise.</returns>
    private string? GetOperationIdFromAttribute(IMethodSymbol? methodSymbol)
    {
        if (methodSymbol == null)
            return null;

        foreach (var attr in methodSymbol.GetAttributes())
        {
            if (attr.AttributeClass?.Name != "OpenApiOperationIdAttribute")
                continue;

            // Constructor arg: (string operationId)
            if (attr.ConstructorArguments.Length > 0)
            {
                return attr.ConstructorArguments[0].Value as string;
            }
        }

        return null;
    }

    /// <summary>
    /// Ensures the operationId is unique by appending a numeric suffix if necessary.
    /// </summary>
    /// <param name="baseOperationId">The base operationId to make unique.</param>
    /// <returns>A unique operationId.</returns>
    private string EnsureUniqueOperationId(string baseOperationId)
    {
        if (string.IsNullOrEmpty(baseOperationId))
            baseOperationId = "operation";

        var operationId = baseOperationId;
        var suffix = 2;

        while (_usedOperationIds.Contains(operationId))
        {
            operationId = $"{baseOperationId}_{suffix}";
            suffix++;
        }

        _usedOperationIds.Add(operationId);
        return operationId;
    }

    /// <summary>
    /// Clears the used operation IDs. Should be called when starting a new document generation.
    /// </summary>
    private void ResetOperationIds()
    {
        _usedOperationIds.Clear();
    }
}
