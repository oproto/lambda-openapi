namespace Oproto.Lambda.OpenApi.Merge;

using System;
using System.Collections.Generic;
using System.Linq;
using Microsoft.OpenApi.Any;
using Microsoft.OpenApi.Models;

/// <summary>
/// Sorts OpenAPI document collections for deterministic output.
/// </summary>
public static class OpenApiDocumentSorter
{
    /// <summary>
    /// HTTP method ordering for operations within a path.
    /// </summary>
    private static readonly OperationType[] OperationOrder =
    {
        OperationType.Get,
        OperationType.Put,
        OperationType.Post,
        OperationType.Delete,
        OperationType.Options,
        OperationType.Head,
        OperationType.Patch,
        OperationType.Trace
    };

    /// <summary>
    /// Sorts all collections in the document for deterministic output.
    /// Modifies the document in place and returns it.
    /// </summary>
    /// <param name="document">The OpenAPI document to sort.</param>
    /// <returns>The sorted document (same instance, modified in place).</returns>
    public static OpenApiDocument Sort(OpenApiDocument document)
    {
        if (document == null)
            throw new ArgumentNullException(nameof(document));

        // Sort paths
        if (document.Paths != null && document.Paths.Count > 0)
        {
            document.Paths = SortPaths(document.Paths);
        }

        // Sort schemas
        if (document.Components?.Schemas != null && document.Components.Schemas.Count > 0)
        {
            document.Components.Schemas = SortSchemas(document.Components.Schemas);
        }

        // Sort tags
        if (document.Tags != null && document.Tags.Count > 0)
        {
            document.Tags = SortTags(document.Tags);
        }

        // Sort security schemes
        if (document.Components?.SecuritySchemes != null && document.Components.SecuritySchemes.Count > 0)
        {
            document.Components.SecuritySchemes = SortSecuritySchemes(document.Components.SecuritySchemes);
        }

        // Sort tag groups
        SortTagGroups(document);

        return document;
    }


    /// <summary>
    /// Sorts paths alphabetically by path string.
    /// </summary>
    /// <param name="paths">The paths to sort.</param>
    /// <returns>A new OpenApiPaths with sorted entries.</returns>
    internal static OpenApiPaths SortPaths(OpenApiPaths paths)
    {
        if (paths == null || paths.Count == 0)
            return paths ?? new OpenApiPaths();

        var sortedPaths = new OpenApiPaths();
        foreach (var path in paths.OrderBy(p => p.Key, StringComparer.Ordinal))
        {
            sortedPaths[path.Key] = SortPathItem(path.Value);
        }
        return sortedPaths;
    }

    /// <summary>
    /// Sorts operations within a path item and their responses/examples.
    /// </summary>
    private static OpenApiPathItem SortPathItem(OpenApiPathItem pathItem)
    {
        if (pathItem?.Operations == null || pathItem.Operations.Count == 0)
            return pathItem ?? new OpenApiPathItem();

        pathItem.Operations = SortOperations(pathItem.Operations);
        return pathItem;
    }

    /// <summary>
    /// Sorts schemas alphabetically by name and sorts properties within each schema.
    /// </summary>
    /// <param name="schemas">The schemas to sort.</param>
    /// <returns>A new dictionary with sorted entries.</returns>
    internal static IDictionary<string, OpenApiSchema> SortSchemas(IDictionary<string, OpenApiSchema> schemas)
    {
        if (schemas == null || schemas.Count == 0)
            return schemas ?? new Dictionary<string, OpenApiSchema>();

        var sortedSchemas = new Dictionary<string, OpenApiSchema>();
        foreach (var schema in schemas.OrderBy(s => s.Key, StringComparer.Ordinal))
        {
            sortedSchemas[schema.Key] = SortSchemaProperties(schema.Value);
        }
        return sortedSchemas;
    }

    /// <summary>
    /// Sorts properties within a schema alphabetically.
    /// </summary>
    /// <param name="schema">The schema to sort.</param>
    /// <returns>The schema with sorted properties.</returns>
    internal static OpenApiSchema SortSchemaProperties(OpenApiSchema schema)
    {
        if (schema == null)
            return new OpenApiSchema();

        if (schema.Properties != null && schema.Properties.Count > 0)
        {
            var sortedProperties = new Dictionary<string, OpenApiSchema>();
            foreach (var prop in schema.Properties.OrderBy(p => p.Key, StringComparer.Ordinal))
            {
                // Recursively sort nested schema properties
                sortedProperties[prop.Key] = SortSchemaProperties(prop.Value);
            }
            schema.Properties = sortedProperties;
        }

        // Sort items schema if present (for arrays)
        if (schema.Items != null)
        {
            schema.Items = SortSchemaProperties(schema.Items);
        }

        // Sort additionalProperties schema if present
        if (schema.AdditionalProperties != null)
        {
            schema.AdditionalProperties = SortSchemaProperties(schema.AdditionalProperties);
        }

        // Sort allOf, oneOf, anyOf schemas
        if (schema.AllOf != null && schema.AllOf.Count > 0)
        {
            schema.AllOf = schema.AllOf.Select(SortSchemaProperties).ToList();
        }
        if (schema.OneOf != null && schema.OneOf.Count > 0)
        {
            schema.OneOf = schema.OneOf.Select(SortSchemaProperties).ToList();
        }
        if (schema.AnyOf != null && schema.AnyOf.Count > 0)
        {
            schema.AnyOf = schema.AnyOf.Select(SortSchemaProperties).ToList();
        }

        return schema;
    }


    /// <summary>
    /// Sorts tags alphabetically by name.
    /// </summary>
    /// <param name="tags">The tags to sort.</param>
    /// <returns>A new list with sorted tags.</returns>
    internal static IList<OpenApiTag> SortTags(IList<OpenApiTag> tags)
    {
        if (tags == null || tags.Count == 0)
            return tags ?? new List<OpenApiTag>();

        return tags.OrderBy(t => t.Name, StringComparer.Ordinal).ToList();
    }

    /// <summary>
    /// Sorts security schemes alphabetically by name.
    /// </summary>
    /// <param name="schemes">The security schemes to sort.</param>
    /// <returns>A new dictionary with sorted entries.</returns>
    internal static IDictionary<string, OpenApiSecurityScheme> SortSecuritySchemes(
        IDictionary<string, OpenApiSecurityScheme> schemes)
    {
        if (schemes == null || schemes.Count == 0)
            return schemes ?? new Dictionary<string, OpenApiSecurityScheme>();

        var sortedSchemes = new Dictionary<string, OpenApiSecurityScheme>();
        foreach (var scheme in schemes.OrderBy(s => s.Key, StringComparer.Ordinal))
        {
            sortedSchemes[scheme.Key] = scheme.Value;
        }
        return sortedSchemes;
    }

    /// <summary>
    /// Sorts operations within a path by HTTP method order: GET, PUT, POST, DELETE, OPTIONS, HEAD, PATCH, TRACE.
    /// </summary>
    /// <param name="operations">The operations to sort.</param>
    /// <returns>A new dictionary with sorted entries.</returns>
    internal static IDictionary<OperationType, OpenApiOperation> SortOperations(
        IDictionary<OperationType, OpenApiOperation> operations)
    {
        if (operations == null || operations.Count == 0)
            return operations ?? new Dictionary<OperationType, OpenApiOperation>();

        var sortedOperations = new Dictionary<OperationType, OpenApiOperation>();
        foreach (var opType in OperationOrder)
        {
            if (operations.TryGetValue(opType, out var operation))
            {
                // Sort responses and examples within the operation
                sortedOperations[opType] = SortOperation(operation);
            }
        }
        return sortedOperations;
    }

    /// <summary>
    /// Sorts responses and examples within an operation.
    /// </summary>
    private static OpenApiOperation SortOperation(OpenApiOperation operation)
    {
        if (operation == null)
            return new OpenApiOperation();

        // Sort responses
        if (operation.Responses != null && operation.Responses.Count > 0)
        {
            operation.Responses = SortResponses(operation.Responses);
        }

        // Sort request body examples if present
        if (operation.RequestBody?.Content != null)
        {
            foreach (var content in operation.RequestBody.Content.Values)
            {
                if (content.Examples != null && content.Examples.Count > 0)
                {
                    content.Examples = SortExamples(content.Examples);
                }
            }
        }

        return operation;
    }


    /// <summary>
    /// Sorts responses by status code (ascending), with "default" sorted last.
    /// </summary>
    /// <param name="responses">The responses to sort.</param>
    /// <returns>A new OpenApiResponses with sorted entries.</returns>
    internal static OpenApiResponses SortResponses(OpenApiResponses responses)
    {
        if (responses == null || responses.Count == 0)
            return responses ?? new OpenApiResponses();

        var sortedResponses = new OpenApiResponses();
        
        // Sort by status code: numeric codes first (ascending), then "default" last
        var sortedKeys = responses.Keys.OrderBy(key =>
        {
            if (key.Equals("default", StringComparison.OrdinalIgnoreCase))
                return int.MaxValue;
            if (int.TryParse(key, out var code))
                return code;
            return int.MaxValue - 1; // Unknown non-numeric codes before "default"
        }).ThenBy(key => key, StringComparer.Ordinal);

        foreach (var key in sortedKeys)
        {
            var response = responses[key];
            // Sort examples within response content
            if (response.Content != null)
            {
                foreach (var content in response.Content.Values)
                {
                    if (content.Examples != null && content.Examples.Count > 0)
                    {
                        content.Examples = SortExamples(content.Examples);
                    }
                }
            }
            sortedResponses[key] = response;
        }
        return sortedResponses;
    }

    /// <summary>
    /// Sorts examples alphabetically by name.
    /// </summary>
    /// <param name="examples">The examples to sort.</param>
    /// <returns>A new dictionary with sorted entries.</returns>
    internal static IDictionary<string, OpenApiExample> SortExamples(IDictionary<string, OpenApiExample> examples)
    {
        if (examples == null || examples.Count == 0)
            return examples ?? new Dictionary<string, OpenApiExample>();

        var sortedExamples = new Dictionary<string, OpenApiExample>();
        foreach (var example in examples.OrderBy(e => e.Key, StringComparer.Ordinal))
        {
            sortedExamples[example.Key] = example.Value;
        }
        return sortedExamples;
    }

    /// <summary>
    /// Sorts tag groups alphabetically by name, and tags within groups alphabetically.
    /// </summary>
    /// <param name="document">The document containing tag groups to sort.</param>
    internal static void SortTagGroups(OpenApiDocument document)
    {
        if (document?.Extensions == null)
            return;

        if (!document.Extensions.TryGetValue("x-tagGroups", out var extension))
            return;

        if (extension is not OpenApiArray groupsArray || groupsArray.Count == 0)
            return;

        // Parse tag groups
        var tagGroups = new List<(string Name, List<string> Tags)>();
        foreach (var item in groupsArray)
        {
            if (item is not OpenApiObject groupObj)
                continue;

            string? name = null;
            if (groupObj.TryGetValue("name", out var nameValue) && nameValue is OpenApiString nameString)
            {
                name = nameString.Value;
            }

            if (string.IsNullOrEmpty(name))
                continue;

            var tags = new List<string>();
            if (groupObj.TryGetValue("tags", out var tagsValue) && tagsValue is OpenApiArray tagsArray)
            {
                foreach (var tagItem in tagsArray.OfType<OpenApiString>())
                {
                    if (!string.IsNullOrEmpty(tagItem.Value))
                    {
                        tags.Add(tagItem.Value);
                    }
                }
            }

            tagGroups.Add((name!, tags));
        }

        if (tagGroups.Count == 0)
            return;

        // Sort tag groups by name, and tags within each group
        var sortedGroups = tagGroups
            .OrderBy(g => g.Name, StringComparer.Ordinal)
            .Select(g => (g.Name, Tags: g.Tags.OrderBy(t => t, StringComparer.Ordinal).ToList()))
            .ToList();

        // Rebuild the extension
        var sortedArray = new OpenApiArray();
        foreach (var group in sortedGroups)
        {
            var tagsArray = new OpenApiArray();
            foreach (var tag in group.Tags)
            {
                tagsArray.Add(new OpenApiString(tag));
            }

            var groupObject = new OpenApiObject
            {
                ["name"] = new OpenApiString(group.Name),
                ["tags"] = tagsArray
            };
            sortedArray.Add(groupObject);
        }

        document.Extensions["x-tagGroups"] = sortedArray;
    }
}
