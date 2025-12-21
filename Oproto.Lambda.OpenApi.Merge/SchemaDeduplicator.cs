namespace Oproto.Lambda.OpenApi.Merge;

using System;
using System.Collections.Generic;
using System.Linq;
using Microsoft.OpenApi.Models;

/// <summary>
/// Exception thrown when a schema conflict occurs with the Fail strategy.
/// </summary>
public class SchemaMergeException : Exception
{
    /// <summary>
    /// The name of the conflicting schema.
    /// </summary>
    public string SchemaName { get; }

    /// <summary>
    /// The name of the source that caused the conflict.
    /// </summary>
    public string SourceName { get; }

    /// <summary>
    /// Creates a new schema merge exception.
    /// </summary>
    public SchemaMergeException(string schemaName, string sourceName)
        : base($"Schema conflict: '{schemaName}' from source '{sourceName}' conflicts with existing schema and strategy is 'Fail'")
    {
        SchemaName = schemaName;
        SourceName = sourceName;
    }
}

/// <summary>
/// Handles schema deduplication and conflict resolution during merge.
/// </summary>
public class SchemaDeduplicator
{
    private readonly SchemaConflictStrategy _strategy;
    private readonly Dictionary<string, (OpenApiSchema Schema, string SourceName)> _schemas = new();
    private readonly Dictionary<string, Dictionary<string, string>> _renames = new();

    /// <summary>
    /// Creates a new schema deduplicator with the specified conflict strategy.
    /// </summary>
    /// <param name="strategy">The strategy to use when schema conflicts occur.</param>
    public SchemaDeduplicator(SchemaConflictStrategy strategy)
    {
        _strategy = strategy;
    }

    /// <summary>
    /// Add a schema, handling conflicts according to the configured strategy.
    /// </summary>
    /// <param name="name">The schema name.</param>
    /// <param name="schema">The schema to add.</param>
    /// <param name="sourceName">The name of the source providing this schema.</param>
    /// <returns>The final schema name and any warning generated.</returns>
    public (string FinalName, MergeWarning? Warning) AddSchema(
        string name,
        OpenApiSchema schema,
        string sourceName)
    {
        if (string.IsNullOrEmpty(name))
            throw new ArgumentException("Schema name cannot be null or empty", nameof(name));
        if (schema == null)
            throw new ArgumentNullException(nameof(schema));
        if (string.IsNullOrEmpty(sourceName))
            throw new ArgumentException("Source name cannot be null or empty", nameof(sourceName));

        // Initialize renames dictionary for this source if needed
        if (!_renames.ContainsKey(sourceName))
        {
            _renames[sourceName] = new Dictionary<string, string>();
        }

        // Check if schema with this name already exists
        if (_schemas.TryGetValue(name, out var existing))
        {
            // Check if schemas are structurally equal
            if (AreStructurallyEqual(existing.Schema, schema))
            {
                // Identical schemas - no conflict, use existing
                _renames[sourceName][name] = name;
                return (name, null);
            }

            // Schemas conflict - handle according to strategy
            return HandleConflict(name, schema, sourceName, existing.SourceName);
        }

        // No conflict - add the schema
        _schemas[name] = (schema, sourceName);
        _renames[sourceName][name] = name;
        return (name, null);
    }

    private (string FinalName, MergeWarning? Warning) HandleConflict(
        string name,
        OpenApiSchema schema,
        string sourceName,
        string existingSourceName)
    {
        switch (_strategy)
        {
            case SchemaConflictStrategy.Rename:
                return RenameSchema(name, schema, sourceName);

            case SchemaConflictStrategy.FirstWins:
                var warning = new MergeWarning(
                    MergeWarningType.SchemaConflict,
                    $"Schema '{name}' from source '{sourceName}' conflicts with schema from '{existingSourceName}'. Using first schema (first-wins strategy).",
                    sourceName);
                _renames[sourceName][name] = name;
                return (name, warning);

            case SchemaConflictStrategy.Fail:
                throw new SchemaMergeException(name, sourceName);

            default:
                throw new InvalidOperationException($"Unknown schema conflict strategy: {_strategy}");
        }
    }

    private (string FinalName, MergeWarning? Warning) RenameSchema(
        string originalName,
        OpenApiSchema schema,
        string sourceName)
    {
        // Generate new name with source prefix
        var newName = $"{sourceName}_{originalName}";

        // Ensure uniqueness by adding numeric suffix if needed
        var finalName = newName;
        var counter = 1;
        while (_schemas.ContainsKey(finalName))
        {
            finalName = $"{newName}_{counter}";
            counter++;
        }

        // Add the renamed schema
        _schemas[finalName] = (schema, sourceName);
        _renames[sourceName][originalName] = finalName;

        var warning = new MergeWarning(
            MergeWarningType.SchemaRenamed,
            $"Schema '{originalName}' from source '{sourceName}' renamed to '{finalName}' due to conflict.",
            sourceName);

        return (finalName, warning);
    }

    /// <summary>
    /// Get all schema renames for a source (for updating $ref references).
    /// </summary>
    /// <param name="sourceName">The source name.</param>
    /// <returns>A dictionary mapping original names to final names.</returns>
    public IReadOnlyDictionary<string, string> GetRenames(string sourceName)
    {
        if (_renames.TryGetValue(sourceName, out var renames))
        {
            return renames;
        }
        return new Dictionary<string, string>();
    }

    /// <summary>
    /// Get all deduplicated schemas.
    /// </summary>
    /// <returns>A dictionary of schema names to schemas.</returns>
    public IReadOnlyDictionary<string, OpenApiSchema> GetSchemas()
    {
        return _schemas.ToDictionary(kvp => kvp.Key, kvp => kvp.Value.Schema);
    }

    /// <summary>
    /// Check if two schemas are structurally equivalent.
    /// </summary>
    /// <param name="a">The first schema.</param>
    /// <param name="b">The second schema.</param>
    /// <returns>True if the schemas are structurally equivalent.</returns>
    public static bool AreStructurallyEqual(OpenApiSchema? a, OpenApiSchema? b)
    {
        if (ReferenceEquals(a, b)) return true;
        if (a == null || b == null) return false;

        // Compare basic properties
        if (a.Type != b.Type) return false;
        if (a.Format != b.Format) return false;
        if (a.Title != b.Title) return false;
        if (a.Description != b.Description) return false;
        if (a.Nullable != b.Nullable) return false;
        if (a.ReadOnly != b.ReadOnly) return false;
        if (a.WriteOnly != b.WriteOnly) return false;
        if (a.Deprecated != b.Deprecated) return false;

        // Compare numeric constraints
        if (a.Minimum != b.Minimum) return false;
        if (a.Maximum != b.Maximum) return false;
        if (a.ExclusiveMinimum != b.ExclusiveMinimum) return false;
        if (a.ExclusiveMaximum != b.ExclusiveMaximum) return false;
        if (a.MultipleOf != b.MultipleOf) return false;

        // Compare string constraints
        if (a.MinLength != b.MinLength) return false;
        if (a.MaxLength != b.MaxLength) return false;
        if (a.Pattern != b.Pattern) return false;

        // Compare array constraints
        if (a.MinItems != b.MinItems) return false;
        if (a.MaxItems != b.MaxItems) return false;
        if (a.UniqueItems != b.UniqueItems) return false;

        // Compare object constraints
        if (a.MinProperties != b.MinProperties) return false;
        if (a.MaxProperties != b.MaxProperties) return false;

        // Compare default value
        if (!AreOpenApiAnyEqual(a.Default, b.Default)) return false;

        // Compare enum values
        if (!AreEnumsEqual(a.Enum, b.Enum)) return false;

        // Compare required properties
        if (!AreStringCollectionsEqual(a.Required, b.Required)) return false;

        // Compare reference
        if (!AreReferencesEqual(a.Reference, b.Reference)) return false;

        // Compare items (for arrays)
        if (!AreStructurallyEqual(a.Items, b.Items)) return false;

        // Compare properties
        if (!ArePropertiesEqual(a.Properties, b.Properties)) return false;

        // Compare additionalProperties
        if (!AreStructurallyEqual(a.AdditionalProperties, b.AdditionalProperties)) return false;

        // Compare allOf, oneOf, anyOf
        if (!AreSchemaListsEqual(a.AllOf, b.AllOf)) return false;
        if (!AreSchemaListsEqual(a.OneOf, b.OneOf)) return false;
        if (!AreSchemaListsEqual(a.AnyOf, b.AnyOf)) return false;

        // Compare not
        if (!AreStructurallyEqual(a.Not, b.Not)) return false;

        return true;
    }

    private static bool AreOpenApiAnyEqual(Microsoft.OpenApi.Any.IOpenApiAny? a, Microsoft.OpenApi.Any.IOpenApiAny? b)
    {
        if (ReferenceEquals(a, b)) return true;
        if (a == null || b == null) return false;

        // Simple comparison - convert to string representation
        return a.ToString() == b.ToString();
    }

    private static bool AreEnumsEqual(IList<Microsoft.OpenApi.Any.IOpenApiAny>? a, IList<Microsoft.OpenApi.Any.IOpenApiAny>? b)
    {
        if (ReferenceEquals(a, b)) return true;
        if (a == null || b == null) return a == null && b == null;
        if (a.Count != b.Count) return false;

        for (int i = 0; i < a.Count; i++)
        {
            if (!AreOpenApiAnyEqual(a[i], b[i])) return false;
        }
        return true;
    }

    private static bool AreStringCollectionsEqual(ISet<string>? a, ISet<string>? b)
    {
        if (ReferenceEquals(a, b)) return true;
        if (a == null || b == null) return (a == null || a.Count == 0) && (b == null || b.Count == 0);
        return a.SetEquals(b);
    }

    private static bool AreReferencesEqual(OpenApiReference? a, OpenApiReference? b)
    {
        if (ReferenceEquals(a, b)) return true;
        if (a == null || b == null) return false;
        return a.Id == b.Id && a.Type == b.Type;
    }

    private static bool ArePropertiesEqual(
        IDictionary<string, OpenApiSchema>? a,
        IDictionary<string, OpenApiSchema>? b)
    {
        if (ReferenceEquals(a, b)) return true;
        if (a == null || b == null) return (a == null || a.Count == 0) && (b == null || b.Count == 0);
        if (a.Count != b.Count) return false;

        foreach (var kvp in a)
        {
            if (!b.TryGetValue(kvp.Key, out var bValue)) return false;
            if (!AreStructurallyEqual(kvp.Value, bValue)) return false;
        }
        return true;
    }

    private static bool AreSchemaListsEqual(IList<OpenApiSchema>? a, IList<OpenApiSchema>? b)
    {
        if (ReferenceEquals(a, b)) return true;
        if (a == null || b == null) return (a == null || a.Count == 0) && (b == null || b.Count == 0);
        if (a.Count != b.Count) return false;

        for (int i = 0; i < a.Count; i++)
        {
            if (!AreStructurallyEqual(a[i], b[i])) return false;
        }
        return true;
    }
}
