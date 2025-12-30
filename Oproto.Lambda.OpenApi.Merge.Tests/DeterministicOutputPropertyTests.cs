using FsCheck;
using FsCheck.Xunit;
using Microsoft.OpenApi;
using Microsoft.OpenApi.Any;
using Microsoft.OpenApi.Extensions;
using Microsoft.OpenApi.Models;
using Microsoft.OpenApi.Readers;
using Oproto.Lambda.OpenApi.Merge;

namespace Oproto.Lambda.OpenApi.Merge.Tests;

/// <summary>
/// Property-based tests for OpenApiDocumentSorter deterministic output.
/// </summary>
public class DeterministicOutputPropertyTests
{
    /// <summary>
    /// Generators for deterministic output test data.
    /// </summary>
    private static class SorterGenerators
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
                Gen.Elements("users", "products", "orders", "items", "api", "v1", "v2", "admin", "auth", "config"),
                Gen.Elements("{id}", "{userId}", "{productId}", "{orderId}")
            );
        }

        public static Gen<string> SchemaNameGen()
        {
            return Gen.Elements("User", "Product", "Order", "Item", "Response", "Request", "Data", "Result",
                               "Customer", "Invoice", "Payment", "Address", "Contact", "Category");
        }

        public static Gen<string> PropertyNameGen()
        {
            return Gen.Elements("id", "name", "value", "count", "status", "email", "phone", "address",
                               "createdAt", "updatedAt", "description", "price", "quantity", "total");
        }

        public static Gen<string> TagNameGen()
        {
            return Gen.Elements("Users", "Products", "Orders", "Items", "Admin", "Auth", "Config",
                               "Inventory", "Payments", "Reports", "Analytics", "Settings");
        }

        public static Gen<string> SecuritySchemeNameGen()
        {
            return Gen.Elements("apiKey", "bearer", "oauth2", "basic", "openIdConnect",
                               "customAuth", "jwt", "session", "token");
        }

        public static Gen<string> ExampleNameGen()
        {
            return Gen.Elements("success", "error", "notFound", "created", "updated", "deleted",
                               "validRequest", "invalidRequest", "emptyResponse", "fullResponse");
        }

        public static Gen<string> TagGroupNameGen()
        {
            return Gen.Elements("User Management", "Product Catalog", "Order Processing",
                               "Administration", "Authentication", "Configuration", "Analytics");
        }

        public static Gen<string> ServerUrlGen()
        {
            return Gen.Elements(
                "https://api.example.com/v1",
                "https://staging.example.com/v1",
                "https://dev.example.com/v1",
                "https://api.production.com",
                "https://api.test.com",
                "https://localhost:5000",
                "https://api.internal.com/v2",
                "https://gateway.example.com");
        }


        public static Gen<OpenApiDocument> DocumentWithPathsGen()
        {
            return from pathCount in Gen.Choose(2, 5)
                   from paths in Gen.ListOf(pathCount, PathGen())
                   let uniquePaths = paths.Distinct().ToList()
                   where uniquePaths.Count >= 2
                   select CreateDocumentWithPaths(uniquePaths);
        }

        public static Gen<OpenApiDocument> DocumentWithSchemasGen()
        {
            return from schemaCount in Gen.Choose(2, 5)
                   from schemaNames in Gen.ListOf(schemaCount, SchemaNameGen())
                   let uniqueSchemas = schemaNames.Distinct().ToList()
                   where uniqueSchemas.Count >= 2
                   select CreateDocumentWithSchemas(uniqueSchemas);
        }

        public static Gen<OpenApiDocument> DocumentWithTagsGen()
        {
            return from tagCount in Gen.Choose(2, 5)
                   from tagNames in Gen.ListOf(tagCount, TagNameGen())
                   let uniqueTags = tagNames.Distinct().ToList()
                   where uniqueTags.Count >= 2
                   select CreateDocumentWithTags(uniqueTags);
        }

        public static Gen<OpenApiDocument> DocumentWithSecuritySchemesGen()
        {
            return from schemeCount in Gen.Choose(2, 4)
                   from schemeNames in Gen.ListOf(schemeCount, SecuritySchemeNameGen())
                   let uniqueSchemes = schemeNames.Distinct().ToList()
                   where uniqueSchemes.Count >= 2
                   select CreateDocumentWithSecuritySchemes(uniqueSchemes);
        }

        public static Gen<OpenApiDocument> DocumentWithTagGroupsGen()
        {
            return from groupCount in Gen.Choose(2, 4)
                   from groupNames in Gen.ListOf(groupCount, TagGroupNameGen())
                   from tagCount in Gen.Choose(2, 4)
                   from tagNames in Gen.ListOf(tagCount, TagNameGen())
                   let uniqueGroups = groupNames.Distinct().ToList()
                   let uniqueTags = tagNames.Distinct().ToList()
                   where uniqueGroups.Count >= 2 && uniqueTags.Count >= 2
                   select CreateDocumentWithTagGroups(uniqueGroups, uniqueTags);
        }

        public static Gen<OpenApiDocument> DocumentWithOperationsGen()
        {
            return from opCount in Gen.Choose(2, 5)
                   from opTypes in Gen.ListOf(opCount, Gen.Elements(
                       OperationType.Get, OperationType.Put, OperationType.Post,
                       OperationType.Delete, OperationType.Patch, OperationType.Options))
                   let uniqueOps = opTypes.Distinct().ToList()
                   where uniqueOps.Count >= 2
                   select CreateDocumentWithOperations(uniqueOps);
        }

        public static Gen<OpenApiDocument> DocumentWithResponsesGen()
        {
            return from responseCount in Gen.Choose(2, 4)
                   from statusCodes in Gen.ListOf(responseCount, Gen.Elements("200", "201", "400", "404", "500", "default"))
                   let uniqueCodes = statusCodes.Distinct().ToList()
                   where uniqueCodes.Count >= 2
                   select CreateDocumentWithResponses(uniqueCodes);
        }

        public static Gen<OpenApiDocument> DocumentWithExamplesGen()
        {
            return from exampleCount in Gen.Choose(2, 4)
                   from exampleNames in Gen.ListOf(exampleCount, ExampleNameGen())
                   let uniqueExamples = exampleNames.Distinct().ToList()
                   where uniqueExamples.Count >= 2
                   select CreateDocumentWithExamples(uniqueExamples);
        }

        public static Gen<OpenApiDocument> DocumentWithServersGen()
        {
            return from serverCount in Gen.Choose(2, 4)
                   from serverUrls in Gen.ListOf(serverCount, ServerUrlGen())
                   let uniqueServers = serverUrls.Distinct().ToList()
                   where uniqueServers.Count >= 2
                   select CreateDocumentWithServers(uniqueServers);
        }

        public static Gen<OpenApiSchema> SchemaWithPropertiesGen()
        {
            return from propCount in Gen.Choose(2, 5)
                   from propNames in Gen.ListOf(propCount, PropertyNameGen())
                   let uniqueProps = propNames.Distinct().ToList()
                   where uniqueProps.Count >= 2
                   select CreateSchemaWithProperties(uniqueProps);
        }

        /// <summary>
        /// Generates a comprehensive OpenAPI document with multiple paths, schemas, tags, and operations
        /// for testing round-trip idempotence.
        /// </summary>
        public static Gen<OpenApiDocument> ComprehensiveDocumentGen()
        {
            return from pathCount in Gen.Choose(2, 4)
                   from paths in Gen.ListOf(pathCount, PathGen())
                   from schemaCount in Gen.Choose(2, 4)
                   from schemaNames in Gen.ListOf(schemaCount, SchemaNameGen())
                   from tagCount in Gen.Choose(2, 4)
                   from tagNames in Gen.ListOf(tagCount, TagNameGen())
                   from propCount in Gen.Choose(2, 4)
                   from propNames in Gen.ListOf(propCount, PropertyNameGen())
                   let uniquePaths = paths.Distinct().ToList()
                   let uniqueSchemas = schemaNames.Distinct().ToList()
                   let uniqueTags = tagNames.Distinct().ToList()
                   let uniqueProps = propNames.Distinct().ToList()
                   where uniquePaths.Count >= 2 && uniqueSchemas.Count >= 2 && uniqueTags.Count >= 2 && uniqueProps.Count >= 2
                   select CreateComprehensiveDocument(uniquePaths, uniqueSchemas, uniqueTags, uniqueProps);
        }


        private static OpenApiDocument CreateDocumentWithPaths(List<string> paths)
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

        private static OpenApiDocument CreateDocumentWithSchemas(List<string> schemaNames)
        {
            var doc = new OpenApiDocument
            {
                Info = new OpenApiInfo { Title = "Test API", Version = "1.0.0" },
                Paths = new OpenApiPaths(),
                Components = new OpenApiComponents
                {
                    Schemas = new Dictionary<string, OpenApiSchema>()
                }
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

        private static OpenApiDocument CreateDocumentWithTags(List<string> tagNames)
        {
            var doc = new OpenApiDocument
            {
                Info = new OpenApiInfo { Title = "Test API", Version = "1.0.0" },
                Paths = new OpenApiPaths(),
                Tags = new List<OpenApiTag>()
            };

            foreach (var tagName in tagNames)
            {
                doc.Tags.Add(new OpenApiTag { Name = tagName, Description = $"Description for {tagName}" });
            }

            return doc;
        }

        private static OpenApiDocument CreateDocumentWithSecuritySchemes(List<string> schemeNames)
        {
            var doc = new OpenApiDocument
            {
                Info = new OpenApiInfo { Title = "Test API", Version = "1.0.0" },
                Paths = new OpenApiPaths(),
                Components = new OpenApiComponents
                {
                    SecuritySchemes = new Dictionary<string, OpenApiSecurityScheme>()
                }
            };

            foreach (var schemeName in schemeNames)
            {
                doc.Components.SecuritySchemes[schemeName] = new OpenApiSecurityScheme
                {
                    Type = SecuritySchemeType.ApiKey,
                    Name = schemeName,
                    In = ParameterLocation.Header,
                    Description = $"Security scheme {schemeName}"
                };
            }

            return doc;
        }


        private static OpenApiDocument CreateDocumentWithTagGroups(List<string> groupNames, List<string> tagNames)
        {
            var doc = new OpenApiDocument
            {
                Info = new OpenApiInfo { Title = "Test API", Version = "1.0.0" },
                Paths = new OpenApiPaths(),
                Extensions = new Dictionary<string, Microsoft.OpenApi.Interfaces.IOpenApiExtension>()
            };

            var tagGroupsArray = new OpenApiArray();
            var tagIndex = 0;
            foreach (var groupName in groupNames)
            {
                var tagsArray = new OpenApiArray();
                // Assign some tags to each group
                var tagsForGroup = tagNames.Skip(tagIndex % tagNames.Count).Take(2).ToList();
                foreach (var tag in tagsForGroup)
                {
                    tagsArray.Add(new OpenApiString(tag));
                }
                tagIndex++;

                var groupObject = new OpenApiObject
                {
                    ["name"] = new OpenApiString(groupName),
                    ["tags"] = tagsArray
                };
                tagGroupsArray.Add(groupObject);
            }

            doc.Extensions["x-tagGroups"] = tagGroupsArray;
            return doc;
        }

        private static OpenApiDocument CreateDocumentWithOperations(List<OperationType> operationTypes)
        {
            var doc = new OpenApiDocument
            {
                Info = new OpenApiInfo { Title = "Test API", Version = "1.0.0" },
                Paths = new OpenApiPaths()
            };

            var operations = new Dictionary<OperationType, OpenApiOperation>();
            foreach (var opType in operationTypes)
            {
                operations[opType] = new OpenApiOperation
                {
                    OperationId = opType.ToString().ToLower() + "Resource",
                    Summary = $"{opType} resource"
                };
            }

            doc.Paths["/resource"] = new OpenApiPathItem { Operations = operations };
            return doc;
        }

        private static OpenApiDocument CreateDocumentWithResponses(List<string> statusCodes)
        {
            var doc = new OpenApiDocument
            {
                Info = new OpenApiInfo { Title = "Test API", Version = "1.0.0" },
                Paths = new OpenApiPaths()
            };

            var responses = new OpenApiResponses();
            foreach (var code in statusCodes)
            {
                responses[code] = new OpenApiResponse { Description = $"Response {code}" };
            }

            doc.Paths["/resource"] = new OpenApiPathItem
            {
                Operations = new Dictionary<OperationType, OpenApiOperation>
                {
                    [OperationType.Get] = new OpenApiOperation
                    {
                        OperationId = "getResource",
                        Responses = responses
                    }
                }
            };

            return doc;
        }

        private static OpenApiDocument CreateDocumentWithExamples(List<string> exampleNames)
        {
            var doc = new OpenApiDocument
            {
                Info = new OpenApiInfo { Title = "Test API", Version = "1.0.0" },
                Paths = new OpenApiPaths()
            };

            var examples = new Dictionary<string, OpenApiExample>();
            foreach (var name in exampleNames)
            {
                examples[name] = new OpenApiExample
                {
                    Summary = $"Example {name}",
                    Value = new OpenApiString($"value for {name}")
                };
            }

            doc.Paths["/resource"] = new OpenApiPathItem
            {
                Operations = new Dictionary<OperationType, OpenApiOperation>
                {
                    [OperationType.Get] = new OpenApiOperation
                    {
                        OperationId = "getResource",
                        Responses = new OpenApiResponses
                        {
                            ["200"] = new OpenApiResponse
                            {
                                Description = "Success",
                                Content = new Dictionary<string, OpenApiMediaType>
                                {
                                    ["application/json"] = new OpenApiMediaType
                                    {
                                        Examples = examples
                                    }
                                }
                            }
                        }
                    }
                }
            };

            return doc;
        }

        private static OpenApiDocument CreateDocumentWithServers(List<string> serverUrls)
        {
            var doc = new OpenApiDocument
            {
                Info = new OpenApiInfo { Title = "Test API", Version = "1.0.0" },
                Paths = new OpenApiPaths(),
                Servers = new List<OpenApiServer>()
            };

            foreach (var url in serverUrls)
            {
                doc.Servers.Add(new OpenApiServer
                {
                    Url = url,
                    Description = $"Server at {url}"
                });
            }

            return doc;
        }

        private static OpenApiSchema CreateSchemaWithProperties(List<string> propertyNames)
        {
            var schema = new OpenApiSchema
            {
                Type = "object",
                Properties = new Dictionary<string, OpenApiSchema>()
            };

            foreach (var propName in propertyNames)
            {
                schema.Properties[propName] = new OpenApiSchema { Type = "string" };
            }

            return schema;
        }

        private static OpenApiDocument CreateComprehensiveDocument(
            List<string> paths, 
            List<string> schemaNames, 
            List<string> tagNames,
            List<string> propertyNames)
        {
            var doc = new OpenApiDocument
            {
                Info = new OpenApiInfo { Title = "Comprehensive Test API", Version = "1.0.0" },
                Paths = new OpenApiPaths(),
                Tags = new List<OpenApiTag>(),
                Components = new OpenApiComponents
                {
                    Schemas = new Dictionary<string, OpenApiSchema>()
                }
            };

            // Add paths with multiple operations
            foreach (var path in paths)
            {
                doc.Paths[path] = new OpenApiPathItem
                {
                    Operations = new Dictionary<OperationType, OpenApiOperation>
                    {
                        [OperationType.Get] = new OpenApiOperation
                        {
                            OperationId = "get" + path.Replace("/", "_").Replace("{", "").Replace("}", ""),
                            Summary = $"Get {path}",
                            Tags = new List<OpenApiTag> { new OpenApiTag { Name = tagNames.First() } },
                            Responses = new OpenApiResponses
                            {
                                ["200"] = new OpenApiResponse { Description = "Success" },
                                ["404"] = new OpenApiResponse { Description = "Not found" }
                            }
                        },
                        [OperationType.Post] = new OpenApiOperation
                        {
                            OperationId = "post" + path.Replace("/", "_").Replace("{", "").Replace("}", ""),
                            Summary = $"Create {path}",
                            Tags = new List<OpenApiTag> { new OpenApiTag { Name = tagNames.Last() } },
                            Responses = new OpenApiResponses
                            {
                                ["201"] = new OpenApiResponse { Description = "Created" },
                                ["400"] = new OpenApiResponse { Description = "Bad request" }
                            }
                        }
                    }
                };
            }

            // Add schemas with properties
            foreach (var schemaName in schemaNames)
            {
                var schema = new OpenApiSchema
                {
                    Type = "object",
                    Properties = new Dictionary<string, OpenApiSchema>()
                };

                foreach (var propName in propertyNames)
                {
                    schema.Properties[propName] = new OpenApiSchema { Type = "string" };
                }

                doc.Components.Schemas[schemaName] = schema;
            }

            // Add tags
            foreach (var tagName in tagNames)
            {
                doc.Tags.Add(new OpenApiTag { Name = tagName, Description = $"Description for {tagName}" });
            }

            return doc;
        }
    }


    /// <summary>
    /// Feature: deterministic-output, Property 1: Path Ordering
    /// For any OpenAPI document with multiple paths, after sorting, the paths SHALL appear 
    /// in alphabetical order by path string using ordinal comparison.
    /// **Validates: Requirements 1.1, 1.2**
    /// </summary>
    [Property(MaxTest = 100)]
    public Property Paths_ShouldBeSorted_Alphabetically()
    {
        return Prop.ForAll(
            SorterGenerators.DocumentWithPathsGen().ToArbitrary(),
            doc =>
            {
                var sortedDoc = OpenApiDocumentSorter.Sort(doc);
                var pathKeys = sortedDoc.Paths.Keys.ToList();

                // Verify paths are in alphabetical order
                var isSorted = pathKeys.SequenceEqual(pathKeys.OrderBy(p => p, StringComparer.Ordinal));

                return isSorted.Label("Paths should be in alphabetical order");
            });
    }

    /// <summary>
    /// Feature: deterministic-output, Property 2: Schema Ordering
    /// For any OpenAPI document with multiple schemas, after sorting, the schemas SHALL appear 
    /// in alphabetical order by schema name using ordinal comparison.
    /// **Validates: Requirements 2.1, 2.2**
    /// </summary>
    [Property(MaxTest = 100)]
    public Property Schemas_ShouldBeSorted_Alphabetically()
    {
        return Prop.ForAll(
            SorterGenerators.DocumentWithSchemasGen().ToArbitrary(),
            doc =>
            {
                var sortedDoc = OpenApiDocumentSorter.Sort(doc);
                var schemaKeys = sortedDoc.Components?.Schemas?.Keys.ToList() ?? new List<string>();

                // Verify schemas are in alphabetical order
                var isSorted = schemaKeys.SequenceEqual(schemaKeys.OrderBy(s => s, StringComparer.Ordinal));

                return isSorted.Label("Schemas should be in alphabetical order");
            });
    }

    /// <summary>
    /// Feature: deterministic-output, Property 3: Property Ordering Within Schemas
    /// For any OpenAPI schema with multiple properties, after sorting, the properties SHALL appear 
    /// in alphabetical order by property name using ordinal comparison.
    /// **Validates: Requirements 3.1, 3.2**
    /// </summary>
    [Property(MaxTest = 100)]
    public Property SchemaProperties_ShouldBeSorted_Alphabetically()
    {
        return Prop.ForAll(
            SorterGenerators.SchemaWithPropertiesGen().ToArbitrary(),
            schema =>
            {
                // Create a document with the schema to test through the public Sort method
                var doc = new OpenApiDocument
                {
                    Info = new OpenApiInfo { Title = "Test", Version = "1.0.0" },
                    Paths = new OpenApiPaths(),
                    Components = new OpenApiComponents
                    {
                        Schemas = new Dictionary<string, OpenApiSchema>
                        {
                            ["TestSchema"] = schema
                        }
                    }
                };

                var sortedDoc = OpenApiDocumentSorter.Sort(doc);
                var sortedSchema = sortedDoc.Components!.Schemas!["TestSchema"];
                var propKeys = sortedSchema.Properties?.Keys.ToList() ?? new List<string>();

                // Verify properties are in alphabetical order
                var isSorted = propKeys.SequenceEqual(propKeys.OrderBy(p => p, StringComparer.Ordinal));

                return isSorted.Label("Schema properties should be in alphabetical order");
            });
    }

    /// <summary>
    /// Feature: deterministic-output, Property 4: Tag Ordering
    /// For any OpenAPI document with multiple tags, after sorting, the tags SHALL appear 
    /// in alphabetical order by tag name using ordinal comparison.
    /// **Validates: Requirements 4.1, 4.2**
    /// </summary>
    [Property(MaxTest = 100)]
    public Property Tags_ShouldBeSorted_Alphabetically()
    {
        return Prop.ForAll(
            SorterGenerators.DocumentWithTagsGen().ToArbitrary(),
            doc =>
            {
                var sortedDoc = OpenApiDocumentSorter.Sort(doc);
                var tagNames = sortedDoc.Tags?.Select(t => t.Name).ToList() ?? new List<string>();

                // Verify tags are in alphabetical order
                var isSorted = tagNames.SequenceEqual(tagNames.OrderBy(t => t, StringComparer.Ordinal));

                return isSorted.Label("Tags should be in alphabetical order");
            });
    }


    /// <summary>
    /// Feature: deterministic-output, Property 5: Tag Group Ordering
    /// For any OpenAPI document with tag groups, after sorting, the tag groups SHALL appear 
    /// in alphabetical order by group name, and tags within each group SHALL appear in alphabetical order.
    /// **Validates: Requirements 5.1, 5.2, 5.3**
    /// </summary>
    [Property(MaxTest = 100)]
    public Property TagGroups_ShouldBeSorted_Alphabetically()
    {
        return Prop.ForAll(
            SorterGenerators.DocumentWithTagGroupsGen().ToArbitrary(),
            doc =>
            {
                var sortedDoc = OpenApiDocumentSorter.Sort(doc);

                // Read tag groups from the sorted document
                var tagGroups = OpenApiMerger.ReadTagGroupsExtension(sortedDoc);
                var groupNames = tagGroups.Select(g => g.Name).ToList();

                // Verify tag groups are in alphabetical order
                var groupsSorted = groupNames.SequenceEqual(groupNames.OrderBy(g => g, StringComparer.Ordinal));

                // Verify tags within each group are in alphabetical order
                var tagsWithinGroupsSorted = tagGroups.All(g =>
                    g.Tags.SequenceEqual(g.Tags.OrderBy(t => t, StringComparer.Ordinal)));

                return groupsSorted.Label("Tag groups should be in alphabetical order")
                    .And(tagsWithinGroupsSorted).Label("Tags within groups should be in alphabetical order");
            });
    }

    /// <summary>
    /// Feature: deterministic-output, Property 6: Security Scheme Ordering
    /// For any OpenAPI document with multiple security schemes, after sorting, the security schemes 
    /// SHALL appear in alphabetical order by scheme name using ordinal comparison.
    /// **Validates: Requirements 7.1, 7.2**
    /// </summary>
    [Property(MaxTest = 100)]
    public Property SecuritySchemes_ShouldBeSorted_Alphabetically()
    {
        return Prop.ForAll(
            SorterGenerators.DocumentWithSecuritySchemesGen().ToArbitrary(),
            doc =>
            {
                var sortedDoc = OpenApiDocumentSorter.Sort(doc);
                var schemeKeys = sortedDoc.Components?.SecuritySchemes?.Keys.ToList() ?? new List<string>();

                // Verify security schemes are in alphabetical order
                var isSorted = schemeKeys.SequenceEqual(schemeKeys.OrderBy(s => s, StringComparer.Ordinal));

                return isSorted.Label("Security schemes should be in alphabetical order");
            });
    }

    /// <summary>
    /// Feature: deterministic-output, Property 7: Operation Ordering
    /// For any path with multiple operations, after sorting, the operations SHALL appear 
    /// in HTTP method order: GET, PUT, POST, DELETE, OPTIONS, HEAD, PATCH, TRACE.
    /// **Validates: Requirements 8.1, 8.2**
    /// </summary>
    [Property(MaxTest = 100)]
    public Property Operations_ShouldBeSorted_ByHttpMethodOrder()
    {
        return Prop.ForAll(
            SorterGenerators.DocumentWithOperationsGen().ToArbitrary(),
            doc =>
            {
                var sortedDoc = OpenApiDocumentSorter.Sort(doc);
                var operations = sortedDoc.Paths["/resource"].Operations.Keys.ToList();

                // Expected order
                var expectedOrder = new[]
                {
                    OperationType.Get, OperationType.Put, OperationType.Post,
                    OperationType.Delete, OperationType.Options, OperationType.Head,
                    OperationType.Patch, OperationType.Trace
                };

                // Filter expected order to only include operations that exist
                var expectedFiltered = expectedOrder.Where(op => operations.Contains(op)).ToList();

                // Verify operations are in the expected order
                var isSorted = operations.SequenceEqual(expectedFiltered);

                return isSorted.Label("Operations should be in HTTP method order (GET, PUT, POST, DELETE, OPTIONS, HEAD, PATCH, TRACE)");
            });
    }


    /// <summary>
    /// Feature: deterministic-output, Property 8: Response Ordering
    /// For any operation with multiple responses, after sorting, the responses SHALL appear 
    /// in ascending order by status code (treating status codes as integers, with "default" sorted last).
    /// **Validates: Requirements 9.1, 9.2**
    /// </summary>
    [Property(MaxTest = 100)]
    public Property Responses_ShouldBeSorted_ByStatusCode()
    {
        return Prop.ForAll(
            SorterGenerators.DocumentWithResponsesGen().ToArbitrary(),
            doc =>
            {
                var sortedDoc = OpenApiDocumentSorter.Sort(doc);
                var responses = sortedDoc.Paths["/resource"].Operations[OperationType.Get].Responses.Keys.ToList();

                // Verify responses are sorted by status code (numeric ascending, "default" last)
                var expectedOrder = responses.OrderBy(key =>
                {
                    if (key.Equals("default", StringComparison.OrdinalIgnoreCase))
                        return int.MaxValue;
                    if (int.TryParse(key, out var code))
                        return code;
                    return int.MaxValue - 1;
                }).ThenBy(key => key, StringComparer.Ordinal).ToList();

                var isSorted = responses.SequenceEqual(expectedOrder);

                return isSorted.Label("Responses should be sorted by status code (ascending, 'default' last)");
            });
    }

    /// <summary>
    /// Feature: deterministic-output, Property 9: Example Ordering
    /// For any media type with multiple examples, after sorting, the examples SHALL appear 
    /// in alphabetical order by example name using ordinal comparison.
    /// **Validates: Requirements 11.1, 11.2**
    /// </summary>
    [Property(MaxTest = 100)]
    public Property Examples_ShouldBeSorted_Alphabetically()
    {
        return Prop.ForAll(
            SorterGenerators.DocumentWithExamplesGen().ToArbitrary(),
            doc =>
            {
                var sortedDoc = OpenApiDocumentSorter.Sort(doc);
                var examples = sortedDoc.Paths["/resource"]
                    .Operations[OperationType.Get]
                    .Responses["200"]
                    .Content["application/json"]
                    .Examples.Keys.ToList();

                // Verify examples are in alphabetical order
                var isSorted = examples.SequenceEqual(examples.OrderBy(e => e, StringComparer.Ordinal));

                return isSorted.Label("Examples should be in alphabetical order");
            });
    }

    /// <summary>
    /// Feature: deterministic-output, Property 10: Output Idempotence (Round-Trip)
    /// For any valid OpenAPI document, serializing the document, then deserializing and 
    /// re-serializing SHALL produce identical JSON output.
    /// **Validates: Requirements 1.3, 2.3, 3.3, 4.3**
    /// </summary>
    [Property(MaxTest = 100)]
    public Property Output_ShouldBeIdempotent_AfterRoundTrip()
    {
        return Prop.ForAll(
            SorterGenerators.ComprehensiveDocumentGen().ToArbitrary(),
            doc =>
            {
                // First sort and serialize
                var sortedDoc = OpenApiDocumentSorter.Sort(doc);
                var firstJson = sortedDoc.SerializeAsJson(OpenApiSpecVersion.OpenApi3_0);

                // Deserialize and re-serialize
                var reader = new OpenApiStringReader();
                var parsedDoc = reader.Read(firstJson, out var diagnostic);
                
                // Sort again and serialize
                var reSortedDoc = OpenApiDocumentSorter.Sort(parsedDoc);
                var secondJson = reSortedDoc.SerializeAsJson(OpenApiSpecVersion.OpenApi3_0);

                // The two JSON outputs should be identical
                var isIdempotent = firstJson == secondJson;

                return isIdempotent.Label("Output should be identical after round-trip (serialize -> deserialize -> serialize)");
            });
    }

    /// <summary>
    /// Feature: deterministic-output, Property 11: Server Order Preservation
    /// For any OpenAPI document with servers, the servers SHALL appear in the same order 
    /// as they were declared in source code or configuration.
    /// **Validates: Requirements 6.1, 6.2**
    /// </summary>
    [Property(MaxTest = 100)]
    public Property Servers_ShouldPreserveDeclarationOrder()
    {
        return Prop.ForAll(
            SorterGenerators.DocumentWithServersGen().ToArbitrary(),
            doc =>
            {
                // Capture the original server order before sorting
                var originalServerUrls = doc.Servers?.Select(s => s.Url).ToList() ?? new List<string>();

                // Sort the document
                var sortedDoc = OpenApiDocumentSorter.Sort(doc);

                // Get the server order after sorting
                var sortedServerUrls = sortedDoc.Servers?.Select(s => s.Url).ToList() ?? new List<string>();

                // Verify servers maintain their original declaration order (not alphabetically sorted)
                var orderPreserved = originalServerUrls.SequenceEqual(sortedServerUrls);

                return orderPreserved.Label("Servers should preserve their original declaration order after sorting");
            });
    }
}
