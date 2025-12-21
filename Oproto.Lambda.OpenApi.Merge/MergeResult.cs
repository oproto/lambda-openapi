namespace Oproto.Lambda.OpenApi.Merge;

using System;
using System.Collections.Generic;
using Microsoft.OpenApi.Models;

/// <summary>
/// Result of a merge operation.
/// </summary>
public class MergeResult
{
    /// <summary>
    /// The merged OpenAPI document.
    /// </summary>
    public OpenApiDocument Document { get; }

    /// <summary>
    /// Warnings generated during the merge process.
    /// </summary>
    public IReadOnlyList<MergeWarning> Warnings { get; }

    /// <summary>
    /// Whether the merge completed successfully (may still have warnings).
    /// </summary>
    public bool Success { get; }

    /// <summary>
    /// Creates a new merge result.
    /// </summary>
    /// <param name="document">The merged OpenAPI document.</param>
    /// <param name="warnings">Warnings generated during the merge process.</param>
    /// <param name="success">Whether the merge completed successfully.</param>
    public MergeResult(OpenApiDocument document, IReadOnlyList<MergeWarning>? warnings = null, bool success = true)
    {
        Document = document ?? throw new ArgumentNullException(nameof(document));
        Warnings = warnings ?? Array.Empty<MergeWarning>();
        Success = success;
    }
}

/// <summary>
/// A warning generated during the merge process.
/// </summary>
public class MergeWarning
{
    /// <summary>
    /// The type of warning.
    /// </summary>
    public MergeWarningType Type { get; }

    /// <summary>
    /// A descriptive message about the warning.
    /// </summary>
    public string Message { get; }

    /// <summary>
    /// The name of the source that generated the warning, if applicable.
    /// </summary>
    public string? SourceName { get; }

    /// <summary>
    /// Creates a new merge warning.
    /// </summary>
    /// <param name="type">The type of warning.</param>
    /// <param name="message">A descriptive message about the warning.</param>
    /// <param name="sourceName">The name of the source that generated the warning.</param>
    public MergeWarning(MergeWarningType type, string message, string? sourceName = null)
    {
        Type = type;
        Message = message ?? throw new ArgumentNullException(nameof(message));
        SourceName = sourceName;
    }

    /// <summary>
    /// Returns a string representation of the warning.
    /// </summary>
    public override string ToString()
    {
        return SourceName != null
            ? $"[{Type}] {SourceName}: {Message}"
            : $"[{Type}] {Message}";
    }
}

/// <summary>
/// Types of warnings that can occur during merge operations.
/// </summary>
public enum MergeWarningType
{
    /// <summary>
    /// A path conflict occurred (duplicate paths after prefix application).
    /// </summary>
    PathConflict,

    /// <summary>
    /// A schema conflict occurred (same name, different structure).
    /// </summary>
    SchemaConflict,

    /// <summary>
    /// A schema was renamed to resolve a conflict.
    /// </summary>
    SchemaRenamed,

    /// <summary>
    /// An operationId conflict occurred (duplicate operationIds).
    /// </summary>
    OperationIdConflict,

    /// <summary>
    /// A security scheme conflict occurred.
    /// </summary>
    SecuritySchemeConflict
}
