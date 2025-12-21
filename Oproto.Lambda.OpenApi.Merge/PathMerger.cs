namespace Oproto.Lambda.OpenApi.Merge;

using System;
using System.Collections.Generic;
using System.Linq;
using Microsoft.OpenApi.Models;

/// <summary>
/// Handles path merging with prefix support and conflict detection.
/// </summary>
public class PathMerger
{
    private readonly OpenApiPaths _paths = new OpenApiPaths();
    private readonly List<MergeWarning> _warnings = new List<MergeWarning>();
    private readonly HashSet<string> _operationIds = new HashSet<string>();

    /// <summary>
    /// Add paths from a source document.
    /// </summary>
    /// <param name="sourcePaths">The paths from the source document.</param>
    /// <param name="sourceConfig">The source configuration.</param>
    /// <param name="schemaRenames">Dictionary mapping original schema names to final names for this source.</param>
    public void AddPaths(
        OpenApiPaths sourcePaths,
        SourceConfiguration sourceConfig,
        IReadOnlyDictionary<string, string> schemaRenames)
    {
        if (sourcePaths == null)
            throw new ArgumentNullException(nameof(sourcePaths));
        if (sourceConfig == null)
            throw new ArgumentNullException(nameof(sourceConfig));

        var sourceName = sourceConfig.Name ?? System.IO.Path.GetFileNameWithoutExtension(sourceConfig.Path);
        var renames = schemaRenames ?? new Dictionary<string, string>();

        foreach (var pathEntry in sourcePaths)
        {
            var originalPath = pathEntry.Key;
            var pathItem = pathEntry.Value;

            // Apply path prefix
            var finalPath = ApplyPathPrefix(originalPath, sourceConfig.PathPrefix);

            // Check for path conflict
            if (_paths.ContainsKey(finalPath))
            {
                _warnings.Add(new MergeWarning(
                    MergeWarningType.PathConflict,
                    $"Path '{finalPath}' already exists. Skipping duplicate from source '{sourceName}'.",
                    sourceName));
                continue;
            }

            // Clone the path item and apply transformations
            var clonedPathItem = ClonePathItem(pathItem, sourceConfig, sourceName, renames);

            _paths.Add(finalPath, clonedPathItem);
        }
    }

    /// <summary>
    /// Apply path prefix with leading slash normalization.
    /// </summary>
    /// <param name="path">The original path.</param>
    /// <param name="prefix">The prefix to apply (may be null).</param>
    /// <returns>The path with prefix applied.</returns>
    public static string ApplyPathPrefix(string path, string? prefix)
    {
        if (string.IsNullOrEmpty(prefix))
            return path;

        // Normalize prefix to start with /
        var normalizedPrefix = prefix!.StartsWith("/") ? prefix : "/" + prefix;

        // Remove trailing slash from prefix if present
        normalizedPrefix = normalizedPrefix.TrimEnd('/');

        // Ensure path starts with /
        var normalizedPath = path.StartsWith("/") ? path : "/" + path;

        return normalizedPrefix + normalizedPath;
    }

    /// <summary>
    /// Get the merged paths.
    /// </summary>
    /// <returns>The merged OpenApiPaths.</returns>
    public OpenApiPaths GetPaths()
    {
        return _paths;
    }

    /// <summary>
    /// Get warnings generated during path merging.
    /// </summary>
    /// <returns>List of warnings.</returns>
    public IReadOnlyList<MergeWarning> GetWarnings()
    {
        return _warnings;
    }

    private OpenApiPathItem ClonePathItem(
        OpenApiPathItem pathItem,
        SourceConfiguration sourceConfig,
        string sourceName,
        IReadOnlyDictionary<string, string> schemaRenames)
    {
        var cloned = new OpenApiPathItem
        {
            Summary = pathItem.Summary,
            Description = pathItem.Description
        };

        // Clone parameters with reference rewriting
        if (pathItem.Parameters != null && pathItem.Parameters.Count > 0)
        {
            cloned.Parameters = pathItem.Parameters
                .Select(p => CloneParameter(p, schemaRenames))
                .ToList();
        }

        // Clone operations
        foreach (var operation in pathItem.Operations)
        {
            var clonedOperation = CloneOperation(operation.Value, sourceConfig, sourceName, schemaRenames);
            cloned.Operations.Add(operation.Key, clonedOperation);
        }

        return cloned;
    }

    private OpenApiOperation CloneOperation(
        OpenApiOperation operation,
        SourceConfiguration sourceConfig,
        string sourceName,
        IReadOnlyDictionary<string, string> schemaRenames)
    {
        var cloned = new OpenApiOperation
        {
            Summary = operation.Summary,
            Description = operation.Description,
            Deprecated = operation.Deprecated,
            ExternalDocs = operation.ExternalDocs
        };

        // Apply operationId prefix
        if (!string.IsNullOrEmpty(operation.OperationId))
        {
            var finalOperationId = ApplyOperationIdPrefix(operation.OperationId, sourceConfig.OperationIdPrefix);
            
            // Check for operationId conflict
            if (_operationIds.Contains(finalOperationId))
            {
                _warnings.Add(new MergeWarning(
                    MergeWarningType.OperationIdConflict,
                    $"OperationId '{finalOperationId}' already exists. Duplicate from source '{sourceName}'.",
                    sourceName));
            }
            else
            {
                _operationIds.Add(finalOperationId);
            }
            
            cloned.OperationId = finalOperationId;
        }

        // Clone tags
        if (operation.Tags != null && operation.Tags.Count > 0)
        {
            cloned.Tags = operation.Tags.Select(t => new OpenApiTag { Name = t.Name, Description = t.Description }).ToList();
        }

        // Clone parameters with reference rewriting
        if (operation.Parameters != null && operation.Parameters.Count > 0)
        {
            cloned.Parameters = operation.Parameters
                .Select(p => CloneParameter(p, schemaRenames))
                .ToList();
        }

        // Clone request body with reference rewriting
        if (operation.RequestBody != null)
        {
            cloned.RequestBody = CloneRequestBody(operation.RequestBody, schemaRenames);
        }

        // Clone responses with reference rewriting
        if (operation.Responses != null && operation.Responses.Count > 0)
        {
            cloned.Responses = new OpenApiResponses();
            foreach (var response in operation.Responses)
            {
                cloned.Responses.Add(response.Key, CloneResponse(response.Value, schemaRenames));
            }
        }

        // Clone security requirements
        if (operation.Security != null && operation.Security.Count > 0)
        {
            cloned.Security = operation.Security.Select(CloneSecurityRequirement).ToList();
        }

        // Clone callbacks with reference rewriting
        if (operation.Callbacks != null && operation.Callbacks.Count > 0)
        {
            cloned.Callbacks = new Dictionary<string, OpenApiCallback>();
            foreach (var callback in operation.Callbacks)
            {
                cloned.Callbacks.Add(callback.Key, CloneCallback(callback.Value, sourceConfig, sourceName, schemaRenames));
            }
        }

        return cloned;
    }

    private static string ApplyOperationIdPrefix(string operationId, string? prefix)
    {
        if (string.IsNullOrEmpty(prefix))
            return operationId;

        return prefix + operationId;
    }

    private OpenApiParameter CloneParameter(
        OpenApiParameter parameter,
        IReadOnlyDictionary<string, string> schemaRenames)
    {
        var cloned = new OpenApiParameter
        {
            Name = parameter.Name,
            In = parameter.In,
            Description = parameter.Description,
            Required = parameter.Required,
            Deprecated = parameter.Deprecated,
            AllowEmptyValue = parameter.AllowEmptyValue,
            Style = parameter.Style,
            Explode = parameter.Explode,
            AllowReserved = parameter.AllowReserved,
            Example = parameter.Example
        };

        if (parameter.Schema != null)
        {
            cloned.Schema = CloneSchemaWithRewrites(parameter.Schema, schemaRenames);
        }

        if (parameter.Reference != null)
        {
            cloned.Reference = RewriteReference(parameter.Reference, schemaRenames);
        }

        return cloned;
    }

    private OpenApiRequestBody CloneRequestBody(
        OpenApiRequestBody requestBody,
        IReadOnlyDictionary<string, string> schemaRenames)
    {
        var cloned = new OpenApiRequestBody
        {
            Description = requestBody.Description,
            Required = requestBody.Required
        };

        if (requestBody.Reference != null)
        {
            cloned.Reference = RewriteReference(requestBody.Reference, schemaRenames);
        }

        if (requestBody.Content != null && requestBody.Content.Count > 0)
        {
            cloned.Content = new Dictionary<string, OpenApiMediaType>();
            foreach (var content in requestBody.Content)
            {
                cloned.Content.Add(content.Key, CloneMediaType(content.Value, schemaRenames));
            }
        }

        return cloned;
    }

    private OpenApiResponse CloneResponse(
        OpenApiResponse response,
        IReadOnlyDictionary<string, string> schemaRenames)
    {
        var cloned = new OpenApiResponse
        {
            Description = response.Description
        };

        if (response.Reference != null)
        {
            cloned.Reference = RewriteReference(response.Reference, schemaRenames);
        }

        if (response.Headers != null && response.Headers.Count > 0)
        {
            cloned.Headers = new Dictionary<string, OpenApiHeader>();
            foreach (var header in response.Headers)
            {
                cloned.Headers.Add(header.Key, CloneHeader(header.Value, schemaRenames));
            }
        }

        if (response.Content != null && response.Content.Count > 0)
        {
            cloned.Content = new Dictionary<string, OpenApiMediaType>();
            foreach (var content in response.Content)
            {
                cloned.Content.Add(content.Key, CloneMediaType(content.Value, schemaRenames));
            }
        }

        return cloned;
    }

    private OpenApiHeader CloneHeader(
        OpenApiHeader header,
        IReadOnlyDictionary<string, string> schemaRenames)
    {
        var cloned = new OpenApiHeader
        {
            Description = header.Description,
            Required = header.Required,
            Deprecated = header.Deprecated,
            Style = header.Style,
            Explode = header.Explode,
            Example = header.Example
        };

        if (header.Schema != null)
        {
            cloned.Schema = CloneSchemaWithRewrites(header.Schema, schemaRenames);
        }

        return cloned;
    }

    private OpenApiMediaType CloneMediaType(
        OpenApiMediaType mediaType,
        IReadOnlyDictionary<string, string> schemaRenames)
    {
        var cloned = new OpenApiMediaType
        {
            Example = mediaType.Example
        };

        if (mediaType.Schema != null)
        {
            cloned.Schema = CloneSchemaWithRewrites(mediaType.Schema, schemaRenames);
        }

        if (mediaType.Examples != null && mediaType.Examples.Count > 0)
        {
            cloned.Examples = new Dictionary<string, OpenApiExample>();
            foreach (var example in mediaType.Examples)
            {
                cloned.Examples.Add(example.Key, example.Value);
            }
        }

        if (mediaType.Encoding != null && mediaType.Encoding.Count > 0)
        {
            cloned.Encoding = new Dictionary<string, OpenApiEncoding>();
            foreach (var encoding in mediaType.Encoding)
            {
                cloned.Encoding.Add(encoding.Key, encoding.Value);
            }
        }

        return cloned;
    }

    private static OpenApiSecurityRequirement CloneSecurityRequirement(OpenApiSecurityRequirement requirement)
    {
        var cloned = new OpenApiSecurityRequirement();
        foreach (var scheme in requirement)
        {
            cloned.Add(scheme.Key, scheme.Value.ToList());
        }
        return cloned;
    }

    private OpenApiCallback CloneCallback(
        OpenApiCallback callback,
        SourceConfiguration sourceConfig,
        string sourceName,
        IReadOnlyDictionary<string, string> schemaRenames)
    {
        var cloned = new OpenApiCallback();
        foreach (var pathItem in callback.PathItems)
        {
            cloned.PathItems.Add(pathItem.Key, ClonePathItem(pathItem.Value, sourceConfig, sourceName, schemaRenames));
        }
        return cloned;
    }

    /// <summary>
    /// Clone a schema and rewrite any $ref references according to the renames dictionary.
    /// </summary>
    public static OpenApiSchema CloneSchemaWithRewrites(
        OpenApiSchema schema,
        IReadOnlyDictionary<string, string> schemaRenames)
    {
        if (schema == null)
            return null!;

        // If this schema is a reference, just rewrite the reference and return
        // Don't recurse into the resolved schema content to avoid circular references
        if (schema.Reference != null)
        {
            return new OpenApiSchema
            {
                Reference = RewriteReference(schema.Reference, schemaRenames),
                // Preserve nullable flag which can be set alongside $ref
                Nullable = schema.Nullable
            };
        }

        var cloned = new OpenApiSchema
        {
            Type = schema.Type,
            Format = schema.Format,
            Title = schema.Title,
            Description = schema.Description,
            Nullable = schema.Nullable,
            ReadOnly = schema.ReadOnly,
            WriteOnly = schema.WriteOnly,
            Deprecated = schema.Deprecated,
            Minimum = schema.Minimum,
            Maximum = schema.Maximum,
            ExclusiveMinimum = schema.ExclusiveMinimum,
            ExclusiveMaximum = schema.ExclusiveMaximum,
            MultipleOf = schema.MultipleOf,
            MinLength = schema.MinLength,
            MaxLength = schema.MaxLength,
            Pattern = schema.Pattern,
            MinItems = schema.MinItems,
            MaxItems = schema.MaxItems,
            UniqueItems = schema.UniqueItems,
            MinProperties = schema.MinProperties,
            MaxProperties = schema.MaxProperties,
            Default = schema.Default,
            Example = schema.Example
        };

        // Clone enum values
        if (schema.Enum != null && schema.Enum.Count > 0)
        {
            cloned.Enum = schema.Enum.ToList();
        }

        // Clone required properties
        if (schema.Required != null && schema.Required.Count > 0)
        {
            cloned.Required = new HashSet<string>(schema.Required);
        }

        // Clone items (for arrays)
        if (schema.Items != null)
        {
            cloned.Items = CloneSchemaWithRewrites(schema.Items, schemaRenames);
        }

        // Clone properties
        if (schema.Properties != null && schema.Properties.Count > 0)
        {
            cloned.Properties = new Dictionary<string, OpenApiSchema>();
            foreach (var prop in schema.Properties)
            {
                cloned.Properties.Add(prop.Key, CloneSchemaWithRewrites(prop.Value, schemaRenames));
            }
        }

        // Clone additionalProperties
        if (schema.AdditionalProperties != null)
        {
            cloned.AdditionalProperties = CloneSchemaWithRewrites(schema.AdditionalProperties, schemaRenames);
        }

        // Clone allOf, oneOf, anyOf
        if (schema.AllOf != null && schema.AllOf.Count > 0)
        {
            cloned.AllOf = schema.AllOf.Select(s => CloneSchemaWithRewrites(s, schemaRenames)).ToList();
        }

        if (schema.OneOf != null && schema.OneOf.Count > 0)
        {
            cloned.OneOf = schema.OneOf.Select(s => CloneSchemaWithRewrites(s, schemaRenames)).ToList();
        }

        if (schema.AnyOf != null && schema.AnyOf.Count > 0)
        {
            cloned.AnyOf = schema.AnyOf.Select(s => CloneSchemaWithRewrites(s, schemaRenames)).ToList();
        }

        // Clone not
        if (schema.Not != null)
        {
            cloned.Not = CloneSchemaWithRewrites(schema.Not, schemaRenames);
        }

        return cloned;
    }

    /// <summary>
    /// Rewrite a reference according to the schema renames dictionary.
    /// </summary>
    public static OpenApiReference RewriteReference(
        OpenApiReference reference,
        IReadOnlyDictionary<string, string> schemaRenames)
    {
        if (reference == null)
            return null!;

        // Only rewrite schema references
        if (reference.Type == ReferenceType.Schema && schemaRenames.TryGetValue(reference.Id, out var newName))
        {
            return new OpenApiReference
            {
                Type = reference.Type,
                Id = newName,
                ExternalResource = reference.ExternalResource
            };
        }

        // Return a clone of the original reference
        return new OpenApiReference
        {
            Type = reference.Type,
            Id = reference.Id,
            ExternalResource = reference.ExternalResource
        };
    }
}
