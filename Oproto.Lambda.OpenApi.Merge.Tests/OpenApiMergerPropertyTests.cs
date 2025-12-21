using FsCheck;
using FsCheck.Xunit;
using Microsoft.OpenApi.Models;
using Oproto.Lambda.OpenApi.Merge;

namespace Oproto.Lambda.OpenApi.Merge.Tests;

/// <summary>
/// Property-based tests for OpenApiMerger.
/// </summary>
public class OpenApiMergerPropertyTests
{
    /// <summary>
    /// Generators for OpenApiMerger test data.
    /// </summary>
    private static class MergerGenerators
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

        public static Gen<string> SourceNameGen()
        {
            return Gen.Elements("ServiceA", "ServiceB", "Users", "Products", "Orders", "Inventory");
        }

        public static Gen<string> SchemaNameGen()
        {
            return Gen.Elements("User", "Product", "Order", "Item", "Response", "Request", "Data", "Result");
        }

        public static Gen<OpenApiDocument> SimpleDocumentGen()
        {
            return from pathCount in Gen.Choose(1, 3)
                   from paths in Gen.ListOf(pathCount, PathGen())
                   let uniquePaths = paths.Distinct().ToList()
                   select CreateDocument(uniquePaths);
        }

        public static Gen<OpenApiDocument> DocumentWithSchemasGen()
        {
            return from pathCount in Gen.Choose(1, 2)
                   from paths in Gen.ListOf(pathCount, PathGen())
                   from schemaCount in Gen.Choose(1, 3)
                   from schemaNames in Gen.ListOf(schemaCount, SchemaNameGen())
                   let uniquePaths = paths.Distinct().ToList()
                   let uniqueSchemas = schemaNames.Distinct().ToList()
                   select CreateDocumentWithSchemas(uniquePaths, uniqueSchemas);
        }

        private static OpenApiDocument CreateDocument(List<string> paths)
        {
            var doc = new OpenApiDocument
            {
                Info = new OpenApiInfo { Title = "Test API", Version = "1.0.0" },
                Paths = new OpenApiPaths()
            };

            foreach (var path in paths)
            {
                doc.Paths[path] = new OpenApiPathItem
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

            return doc;
        }

        private static OpenApiDocument CreateDocumentWithSchemas(List<string> paths, List<string> schemaNames)
        {
            var doc = CreateDocument(paths);
            doc.Components = new OpenApiComponents
            {
                Schemas = new Dictionary<string, OpenApiSchema>()
            };

            foreach (var schemaName in schemaNames)
            {
                doc.Components.Schemas[schemaName] = new OpenApiSchema
                {
                    Type = "object",
                    Properties = new Dictionary<string, OpenApiSchema>
                    {
                        ["id"] = new OpenApiSchema { Type = "string" },
                        ["name"] = new OpenApiSchema { Type = "string" }
                    }
                };
            }

            return doc;
        }

        public static Gen<MergeConfiguration> SimpleMergeConfigGen()
        {
            return Gen.Constant(new MergeConfiguration
            {
                Info = new MergeInfoConfiguration
                {
                    Title = "Merged API",
                    Version = "1.0.0",
                    Description = "Merged API description"
                },
                SchemaConflict = SchemaConflictStrategy.Rename
            });
        }
    }

    /// <summary>
    /// Feature: openapi-merge-tool, Property 1: Path Preservation
    /// For any set of source OpenAPI documents, the merged document SHALL contain 
    /// all paths from all sources (with any configured prefixes applied).
    /// **Validates: Requirements 1.1, 1.2**
    /// </summary>
    [Property(MaxTest = 100)]
    public Property AllPaths_ShouldBePreserved_InMergedDocument()
    {
        // Generate a tuple of two documents and two source names
        var twoDocsGen = from doc1 in MergerGenerators.SimpleDocumentGen()
                         from doc2 in MergerGenerators.SimpleDocumentGen()
                         from source1Name in MergerGenerators.SourceNameGen()
                         from source2Name in MergerGenerators.SourceNameGen()
                         select (doc1, doc2, source1Name, source2Name);

        return Prop.ForAll(
            twoDocsGen.ToArbitrary(),
            tuple =>
            {
                var (doc1, doc2, source1Name, source2Name) = tuple;
                
                // Ensure different source names
                if (source1Name == source2Name) source2Name = source2Name + "_2";

                // Use different prefixes to avoid path conflicts
                var source1 = new SourceConfiguration
                {
                    Path = "source1.json",
                    Name = source1Name,
                    PathPrefix = "/service1"
                };

                var source2 = new SourceConfiguration
                {
                    Path = "source2.json",
                    Name = source2Name,
                    PathPrefix = "/service2"
                };

                var config = new MergeConfiguration
                {
                    Info = new MergeInfoConfiguration
                    {
                        Title = "Merged API",
                        Version = "1.0.0"
                    },
                    SchemaConflict = SchemaConflictStrategy.Rename
                };

                var merger = new OpenApiMerger();
                var documents = new List<(SourceConfiguration, OpenApiDocument)>
                {
                    (source1, doc1),
                    (source2, doc2)
                };

                var result = merger.Merge(config, documents);

                // Calculate expected path count (all paths from both sources with prefixes)
                var expectedPathCount = doc1.Paths.Count + doc2.Paths.Count;
                var actualPathCount = result.Document.Paths.Count;

                // Verify all paths from doc1 are present with prefix
                var doc1PathsPresent = doc1.Paths.Keys.All(originalPath =>
                    result.Document.Paths.ContainsKey("/service1" + originalPath));

                // Verify all paths from doc2 are present with prefix
                var doc2PathsPresent = doc2.Paths.Keys.All(originalPath =>
                    result.Document.Paths.ContainsKey("/service2" + originalPath));

                return (actualPathCount == expectedPathCount)
                    .Label($"Expected {expectedPathCount} paths, got {actualPathCount}")
                    .And(doc1PathsPresent)
                    .Label("All paths from doc1 should be present with /service1 prefix")
                    .And(doc2PathsPresent)
                    .Label("All paths from doc2 should be present with /service2 prefix")
                    .And(result.Success)
                    .Label("Merge should succeed");
            });
    }

    /// <summary>
    /// Feature: openapi-merge-tool, Property 1: Path Preservation
    /// For any single source document, all paths should be preserved unchanged when no prefix is applied.
    /// **Validates: Requirements 1.1, 1.2**
    /// </summary>
    [Property(MaxTest = 100)]
    public Property SingleDocument_AllPaths_ShouldBePreserved()
    {
        return Prop.ForAll(
            MergerGenerators.SimpleDocumentGen().ToArbitrary(),
            MergerGenerators.SourceNameGen().ToArbitrary(),
            (doc, sourceName) =>
            {
                var source = new SourceConfiguration
                {
                    Path = "source.json",
                    Name = sourceName,
                    PathPrefix = null // No prefix
                };

                var config = new MergeConfiguration
                {
                    Info = new MergeInfoConfiguration
                    {
                        Title = "Merged API",
                        Version = "1.0.0"
                    }
                };

                var merger = new OpenApiMerger();
                var documents = new List<(SourceConfiguration, OpenApiDocument)>
                {
                    (source, doc)
                };

                var result = merger.Merge(config, documents);

                // Verify all original paths are present
                var allPathsPresent = doc.Paths.Keys.All(path =>
                    result.Document.Paths.ContainsKey(path));

                return (result.Document.Paths.Count == doc.Paths.Count)
                    .Label($"Expected {doc.Paths.Count} paths, got {result.Document.Paths.Count}")
                    .And(allPathsPresent)
                    .Label("All original paths should be present")
                    .And(result.Success)
                    .Label("Merge should succeed");
            });
    }

    /// <summary>
    /// Feature: openapi-merge-tool, Property 1: Path Preservation
    /// For multiple documents with unique paths (no conflicts), all paths should be preserved.
    /// **Validates: Requirements 1.1, 1.2**
    /// </summary>
    [Property(MaxTest = 100)]
    public Property MultipleDocuments_WithUniquePaths_AllShouldBePreserved()
    {
        return Prop.ForAll(
            Gen.Choose(2, 4).ToArbitrary(),
            MergerGenerators.SourceNameGen().ToArbitrary(),
            (docCount, baseSourceName) =>
            {
                var documents = new List<(SourceConfiguration, OpenApiDocument)>();
                var totalExpectedPaths = 0;

                for (int i = 0; i < docCount; i++)
                {
                    // Create document with unique paths using index-based prefix
                    var doc = new OpenApiDocument
                    {
                        Info = new OpenApiInfo { Title = $"API {i}", Version = "1.0.0" },
                        Paths = new OpenApiPaths
                        {
                            [$"/unique{i}/resource"] = new OpenApiPathItem
                            {
                                Operations = new Dictionary<OperationType, OpenApiOperation>
                                {
                                    [OperationType.Get] = new OpenApiOperation
                                    {
                                        OperationId = $"getResource{i}",
                                        Summary = $"Get resource {i}"
                                    }
                                }
                            }
                        }
                    };

                    var source = new SourceConfiguration
                    {
                        Path = $"source{i}.json",
                        Name = $"{baseSourceName}_{i}"
                    };

                    documents.Add((source, doc));
                    totalExpectedPaths += doc.Paths.Count;
                }

                var config = new MergeConfiguration
                {
                    Info = new MergeInfoConfiguration
                    {
                        Title = "Merged API",
                        Version = "1.0.0"
                    }
                };

                var merger = new OpenApiMerger();
                var result = merger.Merge(config, documents);

                return (result.Document.Paths.Count == totalExpectedPaths)
                    .Label($"Expected {totalExpectedPaths} paths, got {result.Document.Paths.Count}")
                    .And(result.Success)
                    .Label("Merge should succeed");
            });
    }
}


/// <summary>
/// Property-based tests for schema preservation in OpenApiMerger.
/// </summary>
public class SchemaPreservationPropertyTests
{
    /// <summary>
    /// Generators for schema preservation test data.
    /// </summary>
    private static class SchemaGenerators
    {
        public static Gen<string> SchemaNameGen()
        {
            return Gen.Elements("User", "Product", "Order", "Item", "Response", "Request", "Data", "Result", 
                               "Customer", "Invoice", "Payment", "Address", "Contact", "Category");
        }

        public static Gen<string> SourceNameGen()
        {
            return Gen.Elements("ServiceA", "ServiceB", "Users", "Products", "Orders", "Inventory");
        }

        public static Gen<OpenApiSchema> SimpleSchemaGen()
        {
            return from type in Gen.Elements("string", "integer", "number", "boolean")
                   from description in Gen.Elements<string?>(null, "A description", "Another description")
                   select new OpenApiSchema
                   {
                       Type = type,
                       Description = description
                   };
        }

        public static Gen<OpenApiSchema> ObjectSchemaGen()
        {
            return from propCount in Gen.Choose(1, 3)
                   from propNames in Gen.ListOf(propCount, Gen.Elements("id", "name", "value", "count", "status"))
                   let uniqueProps = propNames.Distinct().ToList()
                   select new OpenApiSchema
                   {
                       Type = "object",
                       Properties = uniqueProps.ToDictionary(p => p, p => new OpenApiSchema { Type = "string" })
                   };
        }

        public static Gen<OpenApiDocument> DocumentWithUniqueSchemasGen(int schemaCount, string schemaPrefix)
        {
            return from schemas in Gen.ListOf(schemaCount, ObjectSchemaGen())
                   select CreateDocumentWithSchemas(schemas.ToList(), schemaPrefix);
        }

        private static OpenApiDocument CreateDocumentWithSchemas(List<OpenApiSchema> schemas, string schemaPrefix)
        {
            var doc = new OpenApiDocument
            {
                Info = new OpenApiInfo { Title = "Test API", Version = "1.0.0" },
                Paths = new OpenApiPaths
                {
                    ["/test"] = new OpenApiPathItem
                    {
                        Operations = new Dictionary<OperationType, OpenApiOperation>
                        {
                            [OperationType.Get] = new OpenApiOperation
                            {
                                OperationId = "getTest",
                                Summary = "Test endpoint"
                            }
                        }
                    }
                },
                Components = new OpenApiComponents
                {
                    Schemas = new Dictionary<string, OpenApiSchema>()
                }
            };

            for (int i = 0; i < schemas.Count; i++)
            {
                doc.Components.Schemas[$"{schemaPrefix}Schema{i}"] = schemas[i];
            }

            return doc;
        }
    }

    /// <summary>
    /// Feature: openapi-merge-tool, Property 2: Schema Preservation
    /// For any set of source OpenAPI documents with unique schema names, the merged document 
    /// SHALL contain all schemas from all sources in the components/schemas section.
    /// **Validates: Requirements 1.3**
    /// </summary>
    [Property(MaxTest = 100)]
    public Property AllUniqueSchemas_ShouldBePreserved_InMergedDocument()
    {
        return Prop.ForAll(
            Gen.Choose(1, 3).ToArbitrary(),
            Gen.Choose(1, 3).ToArbitrary(),
            SchemaGenerators.SourceNameGen().ToArbitrary(),
            (schemaCount1, schemaCount2, sourceName) =>
            {
                // Create documents with unique schema names (using different prefixes)
                var doc1Gen = SchemaGenerators.DocumentWithUniqueSchemasGen(schemaCount1, "Doc1_");
                var doc2Gen = SchemaGenerators.DocumentWithUniqueSchemasGen(schemaCount2, "Doc2_");

                var doc1 = doc1Gen.Sample(1, 1).Head;
                var doc2 = doc2Gen.Sample(1, 1).Head;

                var source1 = new SourceConfiguration
                {
                    Path = "source1.json",
                    Name = sourceName + "_1",
                    PathPrefix = "/service1"
                };

                var source2 = new SourceConfiguration
                {
                    Path = "source2.json",
                    Name = sourceName + "_2",
                    PathPrefix = "/service2"
                };

                var config = new MergeConfiguration
                {
                    Info = new MergeInfoConfiguration
                    {
                        Title = "Merged API",
                        Version = "1.0.0"
                    },
                    SchemaConflict = SchemaConflictStrategy.Rename
                };

                var merger = new OpenApiMerger();
                var documents = new List<(SourceConfiguration, OpenApiDocument)>
                {
                    (source1, doc1),
                    (source2, doc2)
                };

                var result = merger.Merge(config, documents);

                // Calculate expected schema count
                var expectedSchemaCount = schemaCount1 + schemaCount2;
                var actualSchemaCount = result.Document.Components?.Schemas?.Count ?? 0;

                // Verify all schemas from doc1 are present
                var doc1SchemasPresent = doc1.Components?.Schemas?.Keys.All(schemaName =>
                    result.Document.Components?.Schemas?.ContainsKey(schemaName) == true) ?? true;

                // Verify all schemas from doc2 are present
                var doc2SchemasPresent = doc2.Components?.Schemas?.Keys.All(schemaName =>
                    result.Document.Components?.Schemas?.ContainsKey(schemaName) == true) ?? true;

                return (actualSchemaCount == expectedSchemaCount)
                    .Label($"Expected {expectedSchemaCount} schemas, got {actualSchemaCount}")
                    .And(doc1SchemasPresent)
                    .Label("All schemas from doc1 should be present")
                    .And(doc2SchemasPresent)
                    .Label("All schemas from doc2 should be present")
                    .And(result.Success)
                    .Label("Merge should succeed");
            });
    }

    /// <summary>
    /// Feature: openapi-merge-tool, Property 2: Schema Preservation
    /// For a single source document, all schemas should be preserved unchanged.
    /// **Validates: Requirements 1.3**
    /// </summary>
    [Property(MaxTest = 100)]
    public Property SingleDocument_AllSchemas_ShouldBePreserved()
    {
        return Prop.ForAll(
            Gen.Choose(1, 5).ToArbitrary(),
            SchemaGenerators.SourceNameGen().ToArbitrary(),
            (schemaCount, sourceName) =>
            {
                var docGen = SchemaGenerators.DocumentWithUniqueSchemasGen(schemaCount, "Test_");
                var doc = docGen.Sample(1, 1).Head;

                var source = new SourceConfiguration
                {
                    Path = "source.json",
                    Name = sourceName
                };

                var config = new MergeConfiguration
                {
                    Info = new MergeInfoConfiguration
                    {
                        Title = "Merged API",
                        Version = "1.0.0"
                    }
                };

                var merger = new OpenApiMerger();
                var documents = new List<(SourceConfiguration, OpenApiDocument)>
                {
                    (source, doc)
                };

                var result = merger.Merge(config, documents);

                var actualSchemaCount = result.Document.Components?.Schemas?.Count ?? 0;

                // Verify all original schemas are present
                var allSchemasPresent = doc.Components?.Schemas?.Keys.All(schemaName =>
                    result.Document.Components?.Schemas?.ContainsKey(schemaName) == true) ?? true;

                return (actualSchemaCount == schemaCount)
                    .Label($"Expected {schemaCount} schemas, got {actualSchemaCount}")
                    .And(allSchemasPresent)
                    .Label("All original schemas should be present")
                    .And(result.Success)
                    .Label("Merge should succeed");
            });
    }

    /// <summary>
    /// Feature: openapi-merge-tool, Property 2: Schema Preservation
    /// For multiple documents with unique schemas, all schemas should be preserved.
    /// **Validates: Requirements 1.3**
    /// </summary>
    [Property(MaxTest = 100)]
    public Property MultipleDocuments_WithUniqueSchemas_AllShouldBePreserved()
    {
        return Prop.ForAll(
            Gen.Choose(2, 4).ToArbitrary(),
            SchemaGenerators.SourceNameGen().ToArbitrary(),
            (docCount, baseSourceName) =>
            {
                var documents = new List<(SourceConfiguration, OpenApiDocument)>();
                var totalExpectedSchemas = 0;

                for (int i = 0; i < docCount; i++)
                {
                    // Create document with unique schemas using index-based prefix
                    var doc = new OpenApiDocument
                    {
                        Info = new OpenApiInfo { Title = $"API {i}", Version = "1.0.0" },
                        Paths = new OpenApiPaths
                        {
                            [$"/unique{i}/resource"] = new OpenApiPathItem
                            {
                                Operations = new Dictionary<OperationType, OpenApiOperation>
                                {
                                    [OperationType.Get] = new OpenApiOperation
                                    {
                                        OperationId = $"getResource{i}",
                                        Summary = $"Get resource {i}"
                                    }
                                }
                            }
                        },
                        Components = new OpenApiComponents
                        {
                            Schemas = new Dictionary<string, OpenApiSchema>
                            {
                                [$"UniqueSchema{i}"] = new OpenApiSchema
                                {
                                    Type = "object",
                                    Properties = new Dictionary<string, OpenApiSchema>
                                    {
                                        ["id"] = new OpenApiSchema { Type = "string" }
                                    }
                                }
                            }
                        }
                    };

                    var source = new SourceConfiguration
                    {
                        Path = $"source{i}.json",
                        Name = $"{baseSourceName}_{i}"
                    };

                    documents.Add((source, doc));
                    totalExpectedSchemas += doc.Components.Schemas.Count;
                }

                var config = new MergeConfiguration
                {
                    Info = new MergeInfoConfiguration
                    {
                        Title = "Merged API",
                        Version = "1.0.0"
                    }
                };

                var merger = new OpenApiMerger();
                var result = merger.Merge(config, documents);

                var actualSchemaCount = result.Document.Components?.Schemas?.Count ?? 0;

                return (actualSchemaCount == totalExpectedSchemas)
                    .Label($"Expected {totalExpectedSchemas} schemas, got {actualSchemaCount}")
                    .And(result.Success)
                    .Label("Merge should succeed");
            });
    }
}
