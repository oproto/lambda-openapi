using FsCheck;
using FsCheck.Xunit;
using Microsoft.OpenApi.Models;
using Oproto.Lambda.OpenApi.Merge;

namespace Oproto.Lambda.OpenApi.Merge.Tests;

/// <summary>
/// Property-based tests for PathMerger.
/// </summary>
public class PathMergerPropertyTests
{
    /// <summary>
    /// Generators for path-related test data.
    /// </summary>
    private static class PathGenerators
    {
        public static Gen<string> PathGen()
        {
            return from segmentCount in Gen.Choose(1, 3)
                   from segments in Gen.ListOf(segmentCount, PathSegmentGen())
                   let path = "/" + string.Join("/", segments)
                   select path;
        }

        public static Gen<string> PathSegmentGen()
        {
            return Gen.OneOf(
                Gen.Elements("users", "products", "orders", "items", "api", "v1", "v2", "admin"),
                Gen.Elements("{id}", "{userId}", "{productId}", "{orderId}")
            );
        }

        public static Gen<string> PrefixGen()
        {
            return Gen.OneOf(
                Gen.Elements("/api", "/v1", "/v2", "/admin", "/public", "/internal"),
                Gen.Elements("api", "v1", "v2", "admin") // Without leading slash
            );
        }

        public static Gen<string> SourceNameGen()
        {
            return Gen.Elements("ServiceA", "ServiceB", "Users", "Products", "Orders", "Inventory");
        }

        public static Gen<string> OperationIdGen()
        {
            return Gen.Elements("getUsers", "createUser", "updateUser", "deleteUser", 
                               "getProducts", "createProduct", "listOrders", "getOrder");
        }

        public static Gen<OpenApiPaths> SimplePathsGen()
        {
            return from pathCount in Gen.Choose(1, 5)
                   from paths in Gen.ListOf(pathCount, PathGen())
                   let uniquePaths = paths.Distinct().ToList()
                   let openApiPaths = new OpenApiPaths()
                   select CreatePaths(uniquePaths);
        }

        private static OpenApiPaths CreatePaths(List<string> paths)
        {
            var openApiPaths = new OpenApiPaths();
            foreach (var path in paths)
            {
                openApiPaths[path] = new OpenApiPathItem
                {
                    Operations = new Dictionary<OperationType, OpenApiOperation>
                    {
                        [OperationType.Get] = new OpenApiOperation
                        {
                            OperationId = "get" + path.Replace("/", "_").Replace("{", "").Replace("}", ""),
                            Summary = $"Get {path}"
                        }
                    }
                };
            }
            return openApiPaths;
        }
    }

    /// <summary>
    /// Feature: openapi-merge-tool, Property 6: Path Prefix Application
    /// For any source configuration with a pathPrefix, all paths from that source 
    /// in the merged document SHALL start with that prefix.
    /// **Validates: Requirements 2.1, 2.2**
    /// </summary>
    [Property(MaxTest = 100)]
    public Property PathsWithPrefix_ShouldAllStartWithPrefix()
    {
        return Prop.ForAll(
            PathGenerators.SimplePathsGen().ToArbitrary(),
            PathGenerators.PrefixGen().ToArbitrary(),
            PathGenerators.SourceNameGen().ToArbitrary(),
            (sourcePaths, prefix, sourceName) =>
            {
                var sourceConfig = new SourceConfiguration
                {
                    Path = "test.json",
                    PathPrefix = prefix,
                    Name = sourceName
                };

                var pathMerger = new PathMerger();
                pathMerger.AddPaths(sourcePaths, sourceConfig, new Dictionary<string, string>());

                var mergedPaths = pathMerger.GetPaths();

                // Normalize prefix for comparison (should start with /)
                var normalizedPrefix = prefix.StartsWith("/") ? prefix : "/" + prefix;

                // Property: all merged paths should start with the normalized prefix
                return mergedPaths.Keys.All(path => path.StartsWith(normalizedPrefix))
                    .Label($"All paths should start with prefix '{normalizedPrefix}'")
                    .And(mergedPaths.Count == sourcePaths.Count)
                    .Label($"Path count should be preserved: expected {sourcePaths.Count}, got {mergedPaths.Count}");
            });
    }

    /// <summary>
    /// Feature: openapi-merge-tool, Property 6: Path Prefix Application
    /// Prefix normalization: prefixes without leading slash should be normalized.
    /// **Validates: Requirements 2.2**
    /// </summary>
    [Property(MaxTest = 100)]
    public Property PrefixWithoutLeadingSlash_ShouldBeNormalized()
    {
        return Prop.ForAll(
            Gen.Elements("api", "v1", "admin", "public").ToArbitrary(),
            PathGenerators.PathGen().ToArbitrary(),
            (prefixWithoutSlash, path) =>
            {
                var result = PathMerger.ApplyPathPrefix(path, prefixWithoutSlash);

                // Property: result should start with / even if prefix didn't
                return result.StartsWith("/")
                    .Label($"Result '{result}' should start with /")
                    .And(result.StartsWith("/" + prefixWithoutSlash))
                    .Label($"Result '{result}' should start with '/{prefixWithoutSlash}'");
            });
    }

    /// <summary>
    /// Feature: openapi-merge-tool, Property 7: Path Identity Without Prefix
    /// For any source configuration without a pathPrefix, all paths from that source 
    /// SHALL appear unchanged in the merged document.
    /// **Validates: Requirements 2.3**
    /// </summary>
    [Property(MaxTest = 100)]
    public Property PathsWithoutPrefix_ShouldRemainUnchanged()
    {
        return Prop.ForAll(
            PathGenerators.SimplePathsGen().ToArbitrary(),
            PathGenerators.SourceNameGen().ToArbitrary(),
            (sourcePaths, sourceName) =>
            {
                var sourceConfig = new SourceConfiguration
                {
                    Path = "test.json",
                    PathPrefix = null, // No prefix
                    Name = sourceName
                };

                var pathMerger = new PathMerger();
                pathMerger.AddPaths(sourcePaths, sourceConfig, new Dictionary<string, string>());

                var mergedPaths = pathMerger.GetPaths();

                // Property: all original paths should exist unchanged
                var allPathsPreserved = sourcePaths.Keys.All(originalPath => mergedPaths.ContainsKey(originalPath));

                return allPathsPreserved
                    .Label("All original paths should be preserved unchanged")
                    .And(mergedPaths.Count == sourcePaths.Count)
                    .Label($"Path count should match: expected {sourcePaths.Count}, got {mergedPaths.Count}");
            });
    }

    /// <summary>
    /// Feature: openapi-merge-tool, Property 6: Path Prefix Application
    /// Empty prefix should behave like no prefix.
    /// **Validates: Requirements 2.3**
    /// </summary>
    [Property(MaxTest = 100)]
    public Property EmptyPrefix_ShouldBehaveLikeNoPrefix()
    {
        return Prop.ForAll(
            PathGenerators.PathGen().ToArbitrary(),
            (path) =>
            {
                var resultWithNull = PathMerger.ApplyPathPrefix(path, null);
                var resultWithEmpty = PathMerger.ApplyPathPrefix(path, "");

                // Property: empty and null prefix should return original path
                return (resultWithNull == path)
                    .Label($"Null prefix: expected '{path}', got '{resultWithNull}'")
                    .And(resultWithEmpty == path)
                    .Label($"Empty prefix: expected '{path}', got '{resultWithEmpty}'");
            });
    }

    /// <summary>
    /// Feature: openapi-merge-tool, Property 8: Path Conflict Warning
    /// For any merge where two sources produce the same path (after prefix application), 
    /// the merge result SHALL contain a warning identifying the conflict.
    /// **Validates: Requirements 2.4, 8.1**
    /// </summary>
    [Property(MaxTest = 100)]
    public Property DuplicatePaths_ShouldGenerateWarning()
    {
        return Prop.ForAll(
            PathGenerators.PathGen().ToArbitrary(),
            PathGenerators.SourceNameGen().ToArbitrary(),
            PathGenerators.SourceNameGen().ToArbitrary(),
            (path, source1Name, source2Name) =>
            {
                // Ensure different source names
                if (source1Name == source2Name) source2Name = source2Name + "_2";

                var paths1 = new OpenApiPaths
                {
                    [path] = new OpenApiPathItem
                    {
                        Operations = new Dictionary<OperationType, OpenApiOperation>
                        {
                            [OperationType.Get] = new OpenApiOperation { OperationId = "op1" }
                        }
                    }
                };

                var paths2 = new OpenApiPaths
                {
                    [path] = new OpenApiPathItem
                    {
                        Operations = new Dictionary<OperationType, OpenApiOperation>
                        {
                            [OperationType.Get] = new OpenApiOperation { OperationId = "op2" }
                        }
                    }
                };

                var sourceConfig1 = new SourceConfiguration { Path = "test1.json", Name = source1Name };
                var sourceConfig2 = new SourceConfiguration { Path = "test2.json", Name = source2Name };

                var pathMerger = new PathMerger();
                pathMerger.AddPaths(paths1, sourceConfig1, new Dictionary<string, string>());
                pathMerger.AddPaths(paths2, sourceConfig2, new Dictionary<string, string>());

                var warnings = pathMerger.GetWarnings();
                var mergedPaths = pathMerger.GetPaths();

                // Property: duplicate path should generate warning and be skipped
                return (warnings.Count == 1)
                    .Label($"Expected 1 warning, got {warnings.Count}")
                    .And(warnings[0].Type == MergeWarningType.PathConflict)
                    .Label("Warning type should be PathConflict")
                    .And(mergedPaths.Count == 1)
                    .Label($"Only first path should be kept, got {mergedPaths.Count} paths");
            });
    }

    /// <summary>
    /// Feature: openapi-merge-tool, Property 9: OperationId Prefix Application
    /// For any source configuration with an operationIdPrefix, all operationIds from that source 
    /// in the merged document SHALL start with that prefix.
    /// **Validates: Requirements 3.1**
    /// </summary>
    [Property(MaxTest = 100)]
    public Property OperationIdsWithPrefix_ShouldAllStartWithPrefix()
    {
        return Prop.ForAll(
            PathGenerators.PathGen().ToArbitrary(),
            PathGenerators.OperationIdGen().ToArbitrary(),
            Gen.Elements("admin_", "user_", "api_", "v1_").ToArbitrary(),
            (path, operationId, prefix) =>
            {
                var sourcePaths = new OpenApiPaths
                {
                    [path] = new OpenApiPathItem
                    {
                        Operations = new Dictionary<OperationType, OpenApiOperation>
                        {
                            [OperationType.Get] = new OpenApiOperation { OperationId = operationId }
                        }
                    }
                };

                var sourceConfig = new SourceConfiguration
                {
                    Path = "test.json",
                    OperationIdPrefix = prefix,
                    Name = "TestSource"
                };

                var pathMerger = new PathMerger();
                pathMerger.AddPaths(sourcePaths, sourceConfig, new Dictionary<string, string>());

                var mergedPaths = pathMerger.GetPaths();
                var mergedOperation = mergedPaths[path].Operations[OperationType.Get];

                // Property: operationId should start with prefix
                return mergedOperation.OperationId.StartsWith(prefix)
                    .Label($"OperationId '{mergedOperation.OperationId}' should start with '{prefix}'")
                    .And(mergedOperation.OperationId == prefix + operationId)
                    .Label($"OperationId should be '{prefix + operationId}', got '{mergedOperation.OperationId}'");
            });
    }

    /// <summary>
    /// Feature: openapi-merge-tool, Property 10: OperationId Identity Without Prefix
    /// For any source configuration without an operationIdPrefix, all operationIds from that source 
    /// SHALL appear unchanged in the merged document.
    /// **Validates: Requirements 3.2**
    /// </summary>
    [Property(MaxTest = 100)]
    public Property OperationIdsWithoutPrefix_ShouldRemainUnchanged()
    {
        return Prop.ForAll(
            PathGenerators.PathGen().ToArbitrary(),
            PathGenerators.OperationIdGen().ToArbitrary(),
            PathGenerators.SourceNameGen().ToArbitrary(),
            (path, operationId, sourceName) =>
            {
                var sourcePaths = new OpenApiPaths
                {
                    [path] = new OpenApiPathItem
                    {
                        Operations = new Dictionary<OperationType, OpenApiOperation>
                        {
                            [OperationType.Get] = new OpenApiOperation { OperationId = operationId }
                        }
                    }
                };

                var sourceConfig = new SourceConfiguration
                {
                    Path = "test.json",
                    OperationIdPrefix = null, // No prefix
                    Name = sourceName
                };

                var pathMerger = new PathMerger();
                pathMerger.AddPaths(sourcePaths, sourceConfig, new Dictionary<string, string>());

                var mergedPaths = pathMerger.GetPaths();
                var mergedOperation = mergedPaths[path].Operations[OperationType.Get];

                // Property: operationId should remain unchanged
                return (mergedOperation.OperationId == operationId)
                    .Label($"OperationId should be '{operationId}', got '{mergedOperation.OperationId}'");
            });
    }

    /// <summary>
    /// Feature: openapi-merge-tool, Property 11: OperationId Conflict Warning
    /// For any merge where duplicate operationIds exist after merging, 
    /// the merge result SHALL contain a warning identifying the conflict.
    /// **Validates: Requirements 3.3, 8.3**
    /// </summary>
    [Property(MaxTest = 100)]
    public Property DuplicateOperationIds_ShouldGenerateWarning()
    {
        return Prop.ForAll(
            PathGenerators.OperationIdGen().ToArbitrary(),
            PathGenerators.SourceNameGen().ToArbitrary(),
            PathGenerators.SourceNameGen().ToArbitrary(),
            (operationId, source1Name, source2Name) =>
            {
                // Ensure different source names
                if (source1Name == source2Name) source2Name = source2Name + "_2";

                var paths1 = new OpenApiPaths
                {
                    ["/path1"] = new OpenApiPathItem
                    {
                        Operations = new Dictionary<OperationType, OpenApiOperation>
                        {
                            [OperationType.Get] = new OpenApiOperation { OperationId = operationId }
                        }
                    }
                };

                var paths2 = new OpenApiPaths
                {
                    ["/path2"] = new OpenApiPathItem
                    {
                        Operations = new Dictionary<OperationType, OpenApiOperation>
                        {
                            [OperationType.Get] = new OpenApiOperation { OperationId = operationId }
                        }
                    }
                };

                var sourceConfig1 = new SourceConfiguration { Path = "test1.json", Name = source1Name };
                var sourceConfig2 = new SourceConfiguration { Path = "test2.json", Name = source2Name };

                var pathMerger = new PathMerger();
                pathMerger.AddPaths(paths1, sourceConfig1, new Dictionary<string, string>());
                pathMerger.AddPaths(paths2, sourceConfig2, new Dictionary<string, string>());

                var warnings = pathMerger.GetWarnings();

                // Property: duplicate operationId should generate warning
                return (warnings.Count == 1)
                    .Label($"Expected 1 warning, got {warnings.Count}")
                    .And(warnings[0].Type == MergeWarningType.OperationIdConflict)
                    .Label($"Warning type should be OperationIdConflict, got {warnings[0].Type}");
            });
    }
}


/// <summary>
/// Property-based tests for reference rewriting (Property 16).
/// </summary>
public class ReferenceRewritingPropertyTests
{
    /// <summary>
    /// Generators for reference rewriting test data.
    /// </summary>
    private static class RefGenerators
    {
        public static Gen<string> SchemaNameGen()
        {
            return Gen.Elements("User", "Product", "Order", "Item", "Response", "Request", "Data", "Result");
        }

        public static Gen<string> SourceNameGen()
        {
            return Gen.Elements("ServiceA", "ServiceB", "Users", "Products", "Orders");
        }

        public static Gen<string> PathGen()
        {
            return Gen.Elements("/users", "/products", "/orders", "/items", "/api/users", "/api/products");
        }
    }

    /// <summary>
    /// Feature: openapi-merge-tool, Property 16: Reference Rewriting
    /// For any schema that is renamed during merge, all $ref references to that schema 
    /// throughout the merged document SHALL be updated to the new name.
    /// **Validates: Requirements 4.5**
    /// </summary>
    [Property(MaxTest = 100)]
    public Property SchemaReferences_ShouldBeRewritten_WhenSchemaRenamed()
    {
        return Prop.ForAll(
            RefGenerators.SchemaNameGen().ToArbitrary(),
            RefGenerators.SourceNameGen().ToArbitrary(),
            RefGenerators.PathGen().ToArbitrary(),
            (schemaName, sourceName, path) =>
            {
                var originalRef = schemaName;
                var renamedRef = $"{sourceName}_{schemaName}";

                // Create schema renames dictionary (simulating a rename)
                var schemaRenames = new Dictionary<string, string>
                {
                    [originalRef] = renamedRef
                };

                // Create a path with a reference to the schema
                var sourcePaths = new OpenApiPaths
                {
                    [path] = new OpenApiPathItem
                    {
                        Operations = new Dictionary<OperationType, OpenApiOperation>
                        {
                            [OperationType.Get] = new OpenApiOperation
                            {
                                OperationId = "getItem",
                                Responses = new OpenApiResponses
                                {
                                    ["200"] = new OpenApiResponse
                                    {
                                        Description = "Success",
                                        Content = new Dictionary<string, OpenApiMediaType>
                                        {
                                            ["application/json"] = new OpenApiMediaType
                                            {
                                                Schema = new OpenApiSchema
                                                {
                                                    Reference = new OpenApiReference
                                                    {
                                                        Type = ReferenceType.Schema,
                                                        Id = originalRef
                                                    }
                                                }
                                            }
                                        }
                                    }
                                }
                            }
                        }
                    }
                };

                var sourceConfig = new SourceConfiguration
                {
                    Path = "test.json",
                    Name = sourceName
                };

                var pathMerger = new PathMerger();
                pathMerger.AddPaths(sourcePaths, sourceConfig, schemaRenames);

                var mergedPaths = pathMerger.GetPaths();
                var mergedResponse = mergedPaths[path].Operations[OperationType.Get].Responses["200"];
                var mergedSchema = mergedResponse.Content["application/json"].Schema;

                // Property: reference should be rewritten to the new name
                var refIsNotNull = mergedSchema.Reference != null;
                var refIdMatches = mergedSchema.Reference?.Id == renamedRef;
                
                return refIsNotNull
                    .Label("Schema reference should not be null")
                    .And(refIdMatches)
                    .Label($"Reference should be '{renamedRef}', got '{mergedSchema.Reference?.Id}'");
            });
    }

    /// <summary>
    /// Feature: openapi-merge-tool, Property 16: Reference Rewriting
    /// References that are not in the renames dictionary should remain unchanged.
    /// **Validates: Requirements 4.5**
    /// </summary>
    [Property(MaxTest = 100)]
    public Property SchemaReferences_ShouldRemainUnchanged_WhenNotRenamed()
    {
        return Prop.ForAll(
            RefGenerators.SchemaNameGen().ToArbitrary(),
            RefGenerators.SourceNameGen().ToArbitrary(),
            RefGenerators.PathGen().ToArbitrary(),
            (schemaName, sourceName, path) =>
            {
                // Empty renames dictionary - no schemas were renamed
                var schemaRenames = new Dictionary<string, string>();

                // Create a path with a reference to the schema
                var sourcePaths = new OpenApiPaths
                {
                    [path] = new OpenApiPathItem
                    {
                        Operations = new Dictionary<OperationType, OpenApiOperation>
                        {
                            [OperationType.Get] = new OpenApiOperation
                            {
                                OperationId = "getItem",
                                Responses = new OpenApiResponses
                                {
                                    ["200"] = new OpenApiResponse
                                    {
                                        Description = "Success",
                                        Content = new Dictionary<string, OpenApiMediaType>
                                        {
                                            ["application/json"] = new OpenApiMediaType
                                            {
                                                Schema = new OpenApiSchema
                                                {
                                                    Reference = new OpenApiReference
                                                    {
                                                        Type = ReferenceType.Schema,
                                                        Id = schemaName
                                                    }
                                                }
                                            }
                                        }
                                    }
                                }
                            }
                        }
                    }
                };

                var sourceConfig = new SourceConfiguration
                {
                    Path = "test.json",
                    Name = sourceName
                };

                var pathMerger = new PathMerger();
                pathMerger.AddPaths(sourcePaths, sourceConfig, schemaRenames);

                var mergedPaths = pathMerger.GetPaths();
                var mergedResponse = mergedPaths[path].Operations[OperationType.Get].Responses["200"];
                var mergedSchema = mergedResponse.Content["application/json"].Schema;

                // Property: reference should remain unchanged
                var refIsNotNull = mergedSchema.Reference != null;
                var refIdMatches = mergedSchema.Reference?.Id == schemaName;
                
                return refIsNotNull
                    .Label("Schema reference should not be null")
                    .And(refIdMatches)
                    .Label($"Reference should remain '{schemaName}', got '{mergedSchema.Reference?.Id}'");
            });
    }

    /// <summary>
    /// Feature: openapi-merge-tool, Property 16: Reference Rewriting
    /// References in request body schemas should be rewritten.
    /// **Validates: Requirements 4.5**
    /// </summary>
    [Property(MaxTest = 100)]
    public Property RequestBodyReferences_ShouldBeRewritten()
    {
        return Prop.ForAll(
            RefGenerators.SchemaNameGen().ToArbitrary(),
            RefGenerators.SourceNameGen().ToArbitrary(),
            RefGenerators.PathGen().ToArbitrary(),
            (schemaName, sourceName, path) =>
            {
                var originalRef = schemaName;
                var renamedRef = $"{sourceName}_{schemaName}";

                var schemaRenames = new Dictionary<string, string>
                {
                    [originalRef] = renamedRef
                };

                var sourcePaths = new OpenApiPaths
                {
                    [path] = new OpenApiPathItem
                    {
                        Operations = new Dictionary<OperationType, OpenApiOperation>
                        {
                            [OperationType.Post] = new OpenApiOperation
                            {
                                OperationId = "createItem",
                                RequestBody = new OpenApiRequestBody
                                {
                                    Description = "Request body",
                                    Content = new Dictionary<string, OpenApiMediaType>
                                    {
                                        ["application/json"] = new OpenApiMediaType
                                        {
                                            Schema = new OpenApiSchema
                                            {
                                                Reference = new OpenApiReference
                                                {
                                                    Type = ReferenceType.Schema,
                                                    Id = originalRef
                                                }
                                            }
                                        }
                                    }
                                }
                            }
                        }
                    }
                };

                var sourceConfig = new SourceConfiguration
                {
                    Path = "test.json",
                    Name = sourceName
                };

                var pathMerger = new PathMerger();
                pathMerger.AddPaths(sourcePaths, sourceConfig, schemaRenames);

                var mergedPaths = pathMerger.GetPaths();
                var mergedRequestBody = mergedPaths[path].Operations[OperationType.Post].RequestBody;
                var mergedSchema = mergedRequestBody.Content["application/json"].Schema;

                // Property: request body reference should be rewritten
                var refIsNotNull = mergedSchema.Reference != null;
                var refIdMatches = mergedSchema.Reference?.Id == renamedRef;
                
                return refIsNotNull
                    .Label("Schema reference should not be null")
                    .And(refIdMatches)
                    .Label($"Reference should be '{renamedRef}', got '{mergedSchema.Reference?.Id}'");
            });
    }

    /// <summary>
    /// Feature: openapi-merge-tool, Property 16: Reference Rewriting
    /// References in nested schemas (array items, properties) should be rewritten.
    /// **Validates: Requirements 4.5**
    /// </summary>
    [Property(MaxTest = 100)]
    public Property NestedSchemaReferences_ShouldBeRewritten()
    {
        return Prop.ForAll(
            RefGenerators.SchemaNameGen().ToArbitrary(),
            RefGenerators.SourceNameGen().ToArbitrary(),
            RefGenerators.PathGen().ToArbitrary(),
            (schemaName, sourceName, path) =>
            {
                var originalRef = schemaName;
                var renamedRef = $"{sourceName}_{schemaName}";

                var schemaRenames = new Dictionary<string, string>
                {
                    [originalRef] = renamedRef
                };

                // Create a path with an array response containing references
                var sourcePaths = new OpenApiPaths
                {
                    [path] = new OpenApiPathItem
                    {
                        Operations = new Dictionary<OperationType, OpenApiOperation>
                        {
                            [OperationType.Get] = new OpenApiOperation
                            {
                                OperationId = "listItems",
                                Responses = new OpenApiResponses
                                {
                                    ["200"] = new OpenApiResponse
                                    {
                                        Description = "Success",
                                        Content = new Dictionary<string, OpenApiMediaType>
                                        {
                                            ["application/json"] = new OpenApiMediaType
                                            {
                                                Schema = new OpenApiSchema
                                                {
                                                    Type = "array",
                                                    Items = new OpenApiSchema
                                                    {
                                                        Reference = new OpenApiReference
                                                        {
                                                            Type = ReferenceType.Schema,
                                                            Id = originalRef
                                                        }
                                                    }
                                                }
                                            }
                                        }
                                    }
                                }
                            }
                        }
                    }
                };

                var sourceConfig = new SourceConfiguration
                {
                    Path = "test.json",
                    Name = sourceName
                };

                var pathMerger = new PathMerger();
                pathMerger.AddPaths(sourcePaths, sourceConfig, schemaRenames);

                var mergedPaths = pathMerger.GetPaths();
                var mergedResponse = mergedPaths[path].Operations[OperationType.Get].Responses["200"];
                var mergedSchema = mergedResponse.Content["application/json"].Schema;

                // Property: nested reference in array items should be rewritten
                var itemsNotNull = mergedSchema.Items != null;
                var itemsRefNotNull = mergedSchema.Items?.Reference != null;
                var itemsRefIdMatches = mergedSchema.Items?.Reference?.Id == renamedRef;
                
                return itemsNotNull
                    .Label("Array items should not be null")
                    .And(itemsRefNotNull)
                    .Label("Items reference should not be null")
                    .And(itemsRefIdMatches)
                    .Label($"Items reference should be '{renamedRef}', got '{mergedSchema.Items?.Reference?.Id}'");
            });
    }

    /// <summary>
    /// Feature: openapi-merge-tool, Property 16: Reference Rewriting
    /// References in allOf/oneOf/anyOf compositions should be rewritten.
    /// **Validates: Requirements 4.5**
    /// </summary>
    [Property(MaxTest = 100)]
    public Property CompositionReferences_ShouldBeRewritten()
    {
        return Prop.ForAll(
            RefGenerators.SchemaNameGen().ToArbitrary(),
            RefGenerators.SourceNameGen().ToArbitrary(),
            RefGenerators.PathGen().ToArbitrary(),
            (schemaName, sourceName, path) =>
            {
                var originalRef = schemaName;
                var renamedRef = $"{sourceName}_{schemaName}";

                var schemaRenames = new Dictionary<string, string>
                {
                    [originalRef] = renamedRef
                };

                // Create a path with allOf composition containing references
                var sourcePaths = new OpenApiPaths
                {
                    [path] = new OpenApiPathItem
                    {
                        Operations = new Dictionary<OperationType, OpenApiOperation>
                        {
                            [OperationType.Get] = new OpenApiOperation
                            {
                                OperationId = "getItem",
                                Responses = new OpenApiResponses
                                {
                                    ["200"] = new OpenApiResponse
                                    {
                                        Description = "Success",
                                        Content = new Dictionary<string, OpenApiMediaType>
                                        {
                                            ["application/json"] = new OpenApiMediaType
                                            {
                                                Schema = new OpenApiSchema
                                                {
                                                    AllOf = new List<OpenApiSchema>
                                                    {
                                                        new OpenApiSchema
                                                        {
                                                            Reference = new OpenApiReference
                                                            {
                                                                Type = ReferenceType.Schema,
                                                                Id = originalRef
                                                            }
                                                        },
                                                        new OpenApiSchema
                                                        {
                                                            Type = "object",
                                                            Properties = new Dictionary<string, OpenApiSchema>
                                                            {
                                                                ["extra"] = new OpenApiSchema { Type = "string" }
                                                            }
                                                        }
                                                    }
                                                }
                                            }
                                        }
                                    }
                                }
                            }
                        }
                    }
                };

                var sourceConfig = new SourceConfiguration
                {
                    Path = "test.json",
                    Name = sourceName
                };

                var pathMerger = new PathMerger();
                pathMerger.AddPaths(sourcePaths, sourceConfig, schemaRenames);

                var mergedPaths = pathMerger.GetPaths();
                var mergedResponse = mergedPaths[path].Operations[OperationType.Get].Responses["200"];
                var mergedSchema = mergedResponse.Content["application/json"].Schema;

                // Property: reference in allOf should be rewritten
                var allOfNotEmpty = mergedSchema.AllOf != null && mergedSchema.AllOf.Count > 0;
                var allOfRefNotNull = mergedSchema.AllOf?[0]?.Reference != null;
                var allOfRefIdMatches = mergedSchema.AllOf?[0]?.Reference?.Id == renamedRef;
                
                return allOfNotEmpty
                    .Label("AllOf should not be null or empty")
                    .And(allOfRefNotNull)
                    .Label("First allOf item reference should not be null")
                    .And(allOfRefIdMatches)
                    .Label($"AllOf reference should be '{renamedRef}', got '{mergedSchema.AllOf?[0]?.Reference?.Id}'");
            });
    }

    /// <summary>
    /// Feature: openapi-merge-tool, Property 16: Reference Rewriting
    /// Non-schema references should not be affected by schema renames.
    /// **Validates: Requirements 4.5**
    /// </summary>
    [Property(MaxTest = 100)]
    public Property NonSchemaReferences_ShouldNotBeAffected()
    {
        return Prop.ForAll(
            RefGenerators.SchemaNameGen().ToArbitrary(),
            RefGenerators.SourceNameGen().ToArbitrary(),
            (refName, sourceName) =>
            {
                var schemaRenames = new Dictionary<string, string>
                {
                    [refName] = $"{sourceName}_{refName}"
                };

                // Create a non-schema reference (e.g., parameter reference)
                var parameterRef = new OpenApiReference
                {
                    Type = ReferenceType.Parameter,
                    Id = refName
                };

                var rewrittenRef = PathMerger.RewriteReference(parameterRef, schemaRenames);

                // Property: non-schema reference should not be rewritten
                return (rewrittenRef.Id == refName)
                    .Label($"Non-schema reference should remain '{refName}', got '{rewrittenRef.Id}'")
                    .And(rewrittenRef.Type == ReferenceType.Parameter)
                    .Label("Reference type should remain Parameter");
            });
    }
}
