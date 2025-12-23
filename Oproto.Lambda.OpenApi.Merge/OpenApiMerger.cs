namespace Oproto.Lambda.OpenApi.Merge;

using System;
using System.Collections.Generic;
using System.Linq;
using Microsoft.OpenApi.Any;
using Microsoft.OpenApi.Models;

/// <summary>
/// Merges multiple OpenAPI documents into a single unified specification.
/// </summary>
public class OpenApiMerger
{
    /// <summary>
    /// Merge multiple OpenAPI documents based on configuration.
    /// </summary>
    /// <param name="config">Merge configuration</param>
    /// <param name="documents">Source documents with their configurations</param>
    /// <returns>Merge result containing the merged document and any warnings</returns>
    public MergeResult Merge(
        MergeConfiguration config,
        IEnumerable<(SourceConfiguration Source, OpenApiDocument Document)> documents)
    {
        if (config == null)
            throw new ArgumentNullException(nameof(config));
        if (documents == null)
            throw new ArgumentNullException(nameof(documents));

        var documentList = documents.ToList();
        var warnings = new List<MergeWarning>();

        // Initialize the merged document with info from configuration
        var mergedDocument = new OpenApiDocument
        {
            Info = CreateInfo(config.Info),
            Servers = CreateServers(config.Servers),
            Paths = new OpenApiPaths(),
            Components = new OpenApiComponents
            {
                Schemas = new Dictionary<string, OpenApiSchema>(),
                SecuritySchemes = new Dictionary<string, OpenApiSecurityScheme>()
            },
            Tags = new List<OpenApiTag>()
        };

        // Phase 1: Collect and deduplicate schemas from all sources
        var schemaDeduplicator = new SchemaDeduplicator(config.SchemaConflict);
        foreach (var (source, document) in documentList)
        {
            var sourceName = GetSourceName(source);
            if (document.Components?.Schemas != null)
            {
                foreach (var schema in document.Components.Schemas)
                {
                    var (_, warning) = schemaDeduplicator.AddSchema(schema.Key, schema.Value, sourceName);
                    if (warning != null)
                    {
                        warnings.Add(warning);
                    }
                }
            }
        }

        // Add deduplicated schemas to merged document
        foreach (var schema in schemaDeduplicator.GetSchemas())
        {
            mergedDocument.Components.Schemas[schema.Key] = schema.Value;
        }

        // Phase 2: Merge paths with reference rewriting
        var pathMerger = new PathMerger();
        foreach (var (source, document) in documentList)
        {
            var sourceName = GetSourceName(source);
            var schemaRenames = schemaDeduplicator.GetRenames(sourceName);

            if (document.Paths != null)
            {
                pathMerger.AddPaths(document.Paths, source, schemaRenames);
            }
        }

        mergedDocument.Paths = pathMerger.GetPaths();
        warnings.AddRange(pathMerger.GetWarnings());

        // Phase 3: Merge tags from all sources
        var tagSet = new HashSet<string>();
        foreach (var (_, document) in documentList)
        {
            if (document.Tags != null)
            {
                foreach (var tag in document.Tags)
                {
                    if (!tagSet.Contains(tag.Name))
                    {
                        tagSet.Add(tag.Name);
                        mergedDocument.Tags.Add(new OpenApiTag
                        {
                            Name = tag.Name,
                            Description = tag.Description,
                            ExternalDocs = tag.ExternalDocs
                        });
                    }
                }
            }
        }

        // Phase 4: Merge security schemes from all sources
        var securitySchemeWarnings = MergeSecuritySchemes(mergedDocument, documentList);
        warnings.AddRange(securitySchemeWarnings);

        // Phase 5: Merge tag groups from all sources
        MergeTagGroups(mergedDocument, documentList);

        return new MergeResult(mergedDocument, warnings, success: true);
    }

    private static OpenApiInfo CreateInfo(MergeInfoConfiguration infoConfig)
    {
        return new OpenApiInfo
        {
            Title = infoConfig.Title ?? "Merged API",
            Version = infoConfig.Version ?? "1.0.0",
            Description = infoConfig.Description
        };
    }

    private static IList<OpenApiServer> CreateServers(List<MergeServerConfiguration> serverConfigs)
    {
        if (serverConfigs == null || serverConfigs.Count == 0)
        {
            return new List<OpenApiServer>();
        }

        return serverConfigs.Select(s => new OpenApiServer
        {
            Url = s.Url,
            Description = s.Description
        }).ToList();
    }

    private static string GetSourceName(SourceConfiguration source)
    {
        return source.Name ?? System.IO.Path.GetFileNameWithoutExtension(source.Path);
    }

    private static List<MergeWarning> MergeSecuritySchemes(
        OpenApiDocument mergedDocument,
        List<(SourceConfiguration Source, OpenApiDocument Document)> documents)
    {
        var warnings = new List<MergeWarning>();
        var existingSchemes = new Dictionary<string, (OpenApiSecurityScheme Scheme, string SourceName)>();

        foreach (var (source, document) in documents)
        {
            var sourceName = GetSourceName(source);
            if (document.Components?.SecuritySchemes == null)
                continue;

            foreach (var scheme in document.Components.SecuritySchemes)
            {
                if (existingSchemes.TryGetValue(scheme.Key, out var existing))
                {
                    // Check if schemes are equivalent
                    if (!AreSecuritySchemesEquivalent(existing.Scheme, scheme.Value))
                    {
                        warnings.Add(new MergeWarning(
                            MergeWarningType.SecuritySchemeConflict,
                            $"Security scheme '{scheme.Key}' from source '{sourceName}' conflicts with scheme from '{existing.SourceName}'. Using first scheme.",
                            sourceName));
                    }
                    // Skip duplicate - first wins
                }
                else
                {
                    existingSchemes[scheme.Key] = (scheme.Value, sourceName);
                    mergedDocument.Components.SecuritySchemes[scheme.Key] = CloneSecurityScheme(scheme.Value);
                }
            }
        }

        return warnings;
    }

    private static bool AreSecuritySchemesEquivalent(OpenApiSecurityScheme a, OpenApiSecurityScheme b)
    {
        if (a.Type != b.Type) return false;
        if (a.Name != b.Name) return false;
        if (a.In != b.In) return false;
        if (a.Scheme != b.Scheme) return false;
        if (a.BearerFormat != b.BearerFormat) return false;
        if (a.OpenIdConnectUrl != b.OpenIdConnectUrl) return false;

        // For OAuth2, compare flows
        if (a.Flows != null && b.Flows != null)
        {
            if (!AreOAuthFlowsEquivalent(a.Flows, b.Flows)) return false;
        }
        else if (a.Flows != null || b.Flows != null)
        {
            return false;
        }

        return true;
    }

    private static bool AreOAuthFlowsEquivalent(OpenApiOAuthFlows a, OpenApiOAuthFlows b)
    {
        return AreOAuthFlowEquivalent(a.Implicit, b.Implicit) &&
               AreOAuthFlowEquivalent(a.Password, b.Password) &&
               AreOAuthFlowEquivalent(a.ClientCredentials, b.ClientCredentials) &&
               AreOAuthFlowEquivalent(a.AuthorizationCode, b.AuthorizationCode);
    }

    private static bool AreOAuthFlowEquivalent(OpenApiOAuthFlow? a, OpenApiOAuthFlow? b)
    {
        if (a == null && b == null) return true;
        if (a == null || b == null) return false;

        if (a.AuthorizationUrl != b.AuthorizationUrl) return false;
        if (a.TokenUrl != b.TokenUrl) return false;
        if (a.RefreshUrl != b.RefreshUrl) return false;

        // Compare scopes
        if (a.Scopes == null && b.Scopes == null) return true;
        if (a.Scopes == null || b.Scopes == null) return false;
        if (a.Scopes.Count != b.Scopes.Count) return false;

        foreach (var scope in a.Scopes)
        {
            if (!b.Scopes.TryGetValue(scope.Key, out var bValue) || scope.Value != bValue)
                return false;
        }

        return true;
    }

    private static OpenApiSecurityScheme CloneSecurityScheme(OpenApiSecurityScheme scheme)
    {
        var cloned = new OpenApiSecurityScheme
        {
            Type = scheme.Type,
            Description = scheme.Description,
            Name = scheme.Name,
            In = scheme.In,
            Scheme = scheme.Scheme,
            BearerFormat = scheme.BearerFormat,
            OpenIdConnectUrl = scheme.OpenIdConnectUrl
        };

        if (scheme.Flows != null)
        {
            cloned.Flows = new OpenApiOAuthFlows
            {
                Implicit = CloneOAuthFlow(scheme.Flows.Implicit),
                Password = CloneOAuthFlow(scheme.Flows.Password),
                ClientCredentials = CloneOAuthFlow(scheme.Flows.ClientCredentials),
                AuthorizationCode = CloneOAuthFlow(scheme.Flows.AuthorizationCode)
            };
        }

        return cloned;
    }

    private static OpenApiOAuthFlow? CloneOAuthFlow(OpenApiOAuthFlow? flow)
    {
        if (flow == null) return null;

        var cloned = new OpenApiOAuthFlow
        {
            AuthorizationUrl = flow.AuthorizationUrl,
            TokenUrl = flow.TokenUrl,
            RefreshUrl = flow.RefreshUrl,
            Scopes = new Dictionary<string, string>()
        };

        if (flow.Scopes != null)
        {
            foreach (var scope in flow.Scopes)
            {
                cloned.Scopes[scope.Key] = scope.Value;
            }
        }

        return cloned;
    }

    /// <summary>
    /// Reads the x-tagGroups extension from an OpenAPI document.
    /// </summary>
    /// <param name="document">The OpenAPI document to read from.</param>
    /// <returns>A list of TagGroupInfo objects parsed from the extension.</returns>
    public static List<TagGroupInfo> ReadTagGroupsExtension(OpenApiDocument document)
    {
        var tagGroups = new List<TagGroupInfo>();

        if (document?.Extensions == null)
            return tagGroups;

        if (!document.Extensions.TryGetValue("x-tagGroups", out var extension))
            return tagGroups;

        if (extension is not OpenApiArray groupsArray)
            return tagGroups;

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

            tagGroups.Add(new TagGroupInfo { Name = name!, Tags = tags });
        }

        return tagGroups;
    }

    /// <summary>
    /// Writes tag groups to the x-tagGroups extension of an OpenAPI document.
    /// </summary>
    /// <param name="document">The OpenAPI document to write to.</param>
    /// <param name="tagGroups">The list of tag groups to write.</param>
    public static void WriteTagGroupsExtension(OpenApiDocument document, List<TagGroupInfo> tagGroups)
    {
        if (document == null)
            throw new ArgumentNullException(nameof(document));

        if (tagGroups == null || tagGroups.Count == 0)
            return;

        var tagGroupsArray = new OpenApiArray();

        foreach (var group in tagGroups)
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
            tagGroupsArray.Add(groupObject);
        }

        document.Extensions["x-tagGroups"] = tagGroupsArray;
    }

    /// <summary>
    /// Merges tag groups from multiple OpenAPI documents.
    /// Combines tag groups from all source documents, merging same-named groups and deduplicating tags.
    /// Preserves order (first occurrence wins for group position).
    /// </summary>
    /// <param name="mergedDocument">The merged document to write tag groups to.</param>
    /// <param name="documents">The source documents with their configurations.</param>
    internal static void MergeTagGroups(
        OpenApiDocument mergedDocument,
        List<(SourceConfiguration Source, OpenApiDocument Document)> documents)
    {
        if (mergedDocument == null)
            throw new ArgumentNullException(nameof(mergedDocument));

        if (documents == null || documents.Count == 0)
            return;

        var mergedGroups = new Dictionary<string, List<string>>();
        var groupOrder = new List<string>();

        foreach (var (_, document) in documents)
        {
            var tagGroups = ReadTagGroupsExtension(document);

            foreach (var group in tagGroups)
            {
                if (!mergedGroups.ContainsKey(group.Name))
                {
                    // First occurrence of this group - add to order list
                    mergedGroups[group.Name] = new List<string>();
                    groupOrder.Add(group.Name);
                }

                // Merge tags, deduplicating
                foreach (var tag in group.Tags)
                {
                    if (!mergedGroups[group.Name].Contains(tag))
                    {
                        mergedGroups[group.Name].Add(tag);
                    }
                }
            }
        }

        if (mergedGroups.Count > 0)
        {
            // Convert to list of TagGroupInfo preserving order
            var tagGroupsList = groupOrder
                .Select(name => new TagGroupInfo { Name = name, Tags = mergedGroups[name] })
                .ToList();

            WriteTagGroupsExtension(mergedDocument, tagGroupsList);
        }
    }
}
