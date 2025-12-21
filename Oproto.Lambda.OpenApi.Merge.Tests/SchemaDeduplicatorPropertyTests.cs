using FsCheck;
using FsCheck.Xunit;
using Microsoft.OpenApi.Models;
using Oproto.Lambda.OpenApi.Merge;

namespace Oproto.Lambda.OpenApi.Merge.Tests;

/// <summary>
/// Property-based tests for SchemaDeduplicator.
/// </summary>
public class SchemaDeduplicatorPropertyTests
{
    /// <summary>
    /// Generates random OpenApiSchema instances for property testing.
    /// </summary>
    private static class SchemaGenerators
    {
        public static Arbitrary<OpenApiSchema> Schema()
        {
            return Gen.OneOf(
                SimpleSchemaGen(),
                ObjectSchemaGen(),
                ArraySchemaGen()
            ).ToArbitrary();
        }

        private static Gen<OpenApiSchema> SimpleSchemaGen()
        {
            return from type in Gen.Elements("string", "integer", "number", "boolean")
                   from format in Gen.Elements<string?>(null, "int32", "int64", "float", "double", "date", "date-time", "email", "uuid")
                   from description in Gen.Elements<string?>(null, "A description", "Another description")
                   from nullable in Arb.Generate<bool>()
                   select new OpenApiSchema
                   {
                       Type = type,
                       Format = type == "string" ? (format == "int32" || format == "int64" || format == "float" || format == "double" ? null : format) : 
                                type == "integer" ? Gen.Elements<string?>(null, "int32", "int64").Sample(1, 1).Head :
                                type == "number" ? Gen.Elements<string?>(null, "float", "double").Sample(1, 1).Head : null,
                       Description = description,
                       Nullable = nullable
                   };
        }

        private static Gen<OpenApiSchema> ObjectSchemaGen()
        {
            return from propCount in Gen.Choose(0, 3)
                   from propNames in Gen.ListOf(propCount, Gen.Elements("id", "name", "value", "count", "status", "type"))
                   from propTypes in Gen.ListOf(propCount, Gen.Elements("string", "integer", "boolean"))
                   let properties = propNames.Zip(propTypes, (name, type) => (name, type))
                                             .GroupBy(p => p.name)
                                             .Select(g => g.First())
                                             .ToDictionary(p => p.name, p => new OpenApiSchema { Type = p.type })
                   select new OpenApiSchema
                   {
                       Type = "object",
                       Properties = properties
                   };
        }

        private static Gen<OpenApiSchema> ArraySchemaGen()
        {
            return from itemType in Gen.Elements("string", "integer", "boolean")
                   select new OpenApiSchema
                   {
                       Type = "array",
                       Items = new OpenApiSchema { Type = itemType }
                   };
        }

        public static Gen<string> SchemaNameGen()
        {
            return Gen.Elements("User", "Product", "Order", "Item", "Response", "Request", "Data", "Result");
        }

        public static Gen<string> SourceNameGen()
        {
            return Gen.Elements("ServiceA", "ServiceB", "ServiceC", "Users", "Products", "Orders");
        }
    }

    /// <summary>
    /// Feature: openapi-merge-tool, Property 12: Schema Structural Deduplication
    /// For any two sources defining schemas with the same name and identical structure,
    /// the merged document SHALL contain exactly one copy of that schema.
    /// **Validates: Requirements 4.1**
    /// </summary>
    [Property(MaxTest = 100)]
    public Property IdenticalSchemas_ShouldBeDeduplicated()
    {
        return Prop.ForAll(
            SchemaGenerators.SchemaNameGen().ToArbitrary(),
            SchemaGenerators.SourceNameGen().ToArbitrary(),
            SchemaGenerators.SourceNameGen().Where(s => true).ToArbitrary(),
            (schemaName, source1, source2) =>
            {
                // Ensure different source names
                if (source1 == source2) source2 = source2 + "_2";

                // Create identical schemas
                var schema1 = new OpenApiSchema
                {
                    Type = "object",
                    Properties = new Dictionary<string, OpenApiSchema>
                    {
                        ["id"] = new OpenApiSchema { Type = "string" },
                        ["name"] = new OpenApiSchema { Type = "string" }
                    }
                };

                var schema2 = new OpenApiSchema
                {
                    Type = "object",
                    Properties = new Dictionary<string, OpenApiSchema>
                    {
                        ["id"] = new OpenApiSchema { Type = "string" },
                        ["name"] = new OpenApiSchema { Type = "string" }
                    }
                };

                var deduplicator = new SchemaDeduplicator(SchemaConflictStrategy.Rename);

                // Add first schema
                var (finalName1, warning1) = deduplicator.AddSchema(schemaName, schema1, source1);

                // Add identical schema from different source
                var (finalName2, warning2) = deduplicator.AddSchema(schemaName, schema2, source2);

                var schemas = deduplicator.GetSchemas();

                // Property: identical schemas should result in single schema
                return (finalName1 == finalName2)
                    .Label("Final names should be equal for identical schemas")
                    .And(warning2 == null)
                    .Label("No warning should be generated for identical schemas")
                    .And(schemas.Count == 1)
                    .Label("Only one schema should exist in output");
            });
    }

    /// <summary>
    /// Feature: openapi-merge-tool, Property 12: Schema Structural Deduplication (reflexivity)
    /// For any schema, adding it twice from the same source should result in one schema.
    /// **Validates: Requirements 4.1**
    /// </summary>
    [Property(MaxTest = 100)]
    public Property SameSchema_AddedTwice_ShouldBeDeduplicated()
    {
        return Prop.ForAll(
            SchemaGenerators.SchemaNameGen().ToArbitrary(),
            SchemaGenerators.SourceNameGen().ToArbitrary(),
            Gen.Elements("string", "integer", "boolean", "number").ToArbitrary(),
            (schemaName, sourceName, schemaType) =>
            {
                var schema = new OpenApiSchema { Type = schemaType };
                var deduplicator = new SchemaDeduplicator(SchemaConflictStrategy.Rename);

                // Add schema first time
                var (finalName1, warning1) = deduplicator.AddSchema(schemaName, schema, sourceName);

                // Create identical schema and add again
                var identicalSchema = new OpenApiSchema { Type = schemaType };
                var (finalName2, warning2) = deduplicator.AddSchema(schemaName, identicalSchema, sourceName);

                var schemas = deduplicator.GetSchemas();

                return (finalName1 == finalName2)
                    .Label("Final names should be equal")
                    .And(schemas.Count == 1)
                    .Label("Only one schema should exist");
            });
    }

    /// <summary>
    /// Feature: openapi-merge-tool, Property 12: Schema Structural Deduplication (complex objects)
    /// For any two identical object schemas with properties, deduplication should work.
    /// **Validates: Requirements 4.1**
    /// </summary>
    [Property(MaxTest = 100)]
    public Property IdenticalObjectSchemas_ShouldBeDeduplicated()
    {
        return Prop.ForAll(
            SchemaGenerators.SchemaNameGen().ToArbitrary(),
            Gen.Choose(1, 5).ToArbitrary(),
            (schemaName, propCount) =>
            {
                var propNames = Enumerable.Range(1, propCount).Select(i => $"prop{i}").ToList();

                // Create two identical object schemas
                var schema1 = new OpenApiSchema
                {
                    Type = "object",
                    Properties = propNames.ToDictionary(p => p, p => new OpenApiSchema { Type = "string" })
                };

                var schema2 = new OpenApiSchema
                {
                    Type = "object",
                    Properties = propNames.ToDictionary(p => p, p => new OpenApiSchema { Type = "string" })
                };

                var deduplicator = new SchemaDeduplicator(SchemaConflictStrategy.Rename);

                deduplicator.AddSchema(schemaName, schema1, "Source1");
                var (finalName, warning) = deduplicator.AddSchema(schemaName, schema2, "Source2");

                var schemas = deduplicator.GetSchemas();

                return (schemas.Count == 1)
                    .Label($"Expected 1 schema but got {schemas.Count}")
                    .And(warning == null)
                    .Label("No warning expected for identical schemas");
            });
    }
}


/// <summary>
/// Property-based tests for schema rename on conflict (Property 13).
/// </summary>
public class SchemaRenamePropertyTests
{
    /// <summary>
    /// Feature: openapi-merge-tool, Property 13: Schema Rename on Conflict
    /// For any two sources defining schemas with the same name but different structures (with rename strategy),
    /// the merged document SHALL contain both schemas with the conflicting one renamed using source name as prefix.
    /// **Validates: Requirements 4.2**
    /// </summary>
    [Property(MaxTest = 100)]
    public Property ConflictingSchemas_ShouldBeRenamed_WithSourcePrefix()
    {
        return Prop.ForAll(
            Gen.Elements("User", "Product", "Order", "Item", "Response").ToArbitrary(),
            Gen.Elements("ServiceA", "ServiceB", "Users", "Products").ToArbitrary(),
            Gen.Elements("ServiceC", "ServiceD", "Orders", "Items").ToArbitrary(),
            (schemaName, source1, source2) =>
            {
                // Ensure different source names
                if (source1 == source2) source2 = source2 + "_2";

                // Create different schemas with same name
                var schema1 = new OpenApiSchema
                {
                    Type = "object",
                    Properties = new Dictionary<string, OpenApiSchema>
                    {
                        ["id"] = new OpenApiSchema { Type = "string" }
                    }
                };

                var schema2 = new OpenApiSchema
                {
                    Type = "object",
                    Properties = new Dictionary<string, OpenApiSchema>
                    {
                        ["id"] = new OpenApiSchema { Type = "integer" } // Different type!
                    }
                };

                var deduplicator = new SchemaDeduplicator(SchemaConflictStrategy.Rename);

                // Add first schema
                var (finalName1, warning1) = deduplicator.AddSchema(schemaName, schema1, source1);

                // Add conflicting schema from different source
                var (finalName2, warning2) = deduplicator.AddSchema(schemaName, schema2, source2);

                var schemas = deduplicator.GetSchemas();

                // Property: conflicting schemas should result in two schemas
                return (schemas.Count == 2)
                    .Label($"Expected 2 schemas but got {schemas.Count}")
                    .And(finalName1 == schemaName)
                    .Label("First schema should keep original name")
                    .And(finalName2.StartsWith(source2 + "_"))
                    .Label($"Second schema name '{finalName2}' should start with source prefix '{source2}_'")
                    .And(finalName2.Contains(schemaName))
                    .Label($"Second schema name '{finalName2}' should contain original name '{schemaName}'")
                    .And(warning2 != null)
                    .Label("Warning should be generated for renamed schema")
                    .And(warning2?.Type == MergeWarningType.SchemaRenamed)
                    .Label("Warning type should be SchemaRenamed");
            });
    }

    /// <summary>
    /// Feature: openapi-merge-tool, Property 13: Schema Rename on Conflict
    /// Renamed schemas should be tracked in the renames dictionary for reference rewriting.
    /// **Validates: Requirements 4.2**
    /// </summary>
    [Property(MaxTest = 100)]
    public Property RenamedSchemas_ShouldBeTrackedInRenames()
    {
        return Prop.ForAll(
            Gen.Elements("Schema1", "Schema2", "Schema3").ToArbitrary(),
            Gen.Elements("SourceA", "SourceB").ToArbitrary(),
            Gen.Elements("SourceC", "SourceD").ToArbitrary(),
            (schemaName, source1, source2) =>
            {
                if (source1 == source2) source2 = source2 + "_2";

                // Create different schemas
                var schema1 = new OpenApiSchema { Type = "string" };
                var schema2 = new OpenApiSchema { Type = "integer" }; // Different!

                var deduplicator = new SchemaDeduplicator(SchemaConflictStrategy.Rename);

                deduplicator.AddSchema(schemaName, schema1, source1);
                var (finalName2, _) = deduplicator.AddSchema(schemaName, schema2, source2);

                var renames1 = deduplicator.GetRenames(source1);
                var renames2 = deduplicator.GetRenames(source2);

                // Property: renames should track the mapping
                return renames1.ContainsKey(schemaName)
                    .Label("Source1 renames should contain schema name")
                    .And(renames1[schemaName] == schemaName)
                    .Label("Source1 schema should map to original name")
                    .And(renames2.ContainsKey(schemaName))
                    .Label("Source2 renames should contain schema name")
                    .And(renames2[schemaName] == finalName2)
                    .Label($"Source2 schema should map to renamed name '{finalName2}'");
            });
    }

    /// <summary>
    /// Feature: openapi-merge-tool, Property 13: Schema Rename on Conflict
    /// Multiple conflicting schemas should all get unique names.
    /// **Validates: Requirements 4.2**
    /// </summary>
    [Property(MaxTest = 100)]
    public Property MultipleConflicts_ShouldAllGetUniqueNames()
    {
        return Prop.ForAll(
            Gen.Elements("CommonSchema", "SharedType", "BaseModel").ToArbitrary(),
            Gen.Choose(2, 5).ToArbitrary(),
            (schemaName, sourceCount) =>
            {
                var deduplicator = new SchemaDeduplicator(SchemaConflictStrategy.Rename);
                var finalNames = new List<string>();

                for (int i = 0; i < sourceCount; i++)
                {
                    // Each schema has a different type to ensure conflict
                    var schema = new OpenApiSchema
                    {
                        Type = "object",
                        Description = $"Schema from source {i}" // Different description ensures conflict
                    };

                    var (finalName, _) = deduplicator.AddSchema(schemaName, schema, $"Source{i}");
                    finalNames.Add(finalName);
                }

                var schemas = deduplicator.GetSchemas();
                var uniqueNames = finalNames.Distinct().ToList();

                // Property: all final names should be unique
                return (uniqueNames.Count == sourceCount)
                    .Label($"Expected {sourceCount} unique names but got {uniqueNames.Count}")
                    .And(schemas.Count == sourceCount)
                    .Label($"Expected {sourceCount} schemas but got {schemas.Count}");
            });
    }

    /// <summary>
    /// Feature: openapi-merge-tool, Property 13: Schema Rename on Conflict (FirstWins strategy)
    /// With FirstWins strategy, conflicting schemas should keep first and generate warning.
    /// **Validates: Requirements 4.3**
    /// </summary>
    [Property(MaxTest = 100)]
    public Property FirstWinsStrategy_ShouldKeepFirstSchema()
    {
        return Prop.ForAll(
            Gen.Elements("User", "Product", "Order").ToArbitrary(),
            Gen.Elements("ServiceA", "ServiceB").ToArbitrary(),
            Gen.Elements("ServiceC", "ServiceD").ToArbitrary(),
            (schemaName, source1, source2) =>
            {
                if (source1 == source2) source2 = source2 + "_2";

                var schema1 = new OpenApiSchema { Type = "string" };
                var schema2 = new OpenApiSchema { Type = "integer" }; // Different!

                var deduplicator = new SchemaDeduplicator(SchemaConflictStrategy.FirstWins);

                var (finalName1, warning1) = deduplicator.AddSchema(schemaName, schema1, source1);
                var (finalName2, warning2) = deduplicator.AddSchema(schemaName, schema2, source2);

                var schemas = deduplicator.GetSchemas();

                // Property: first-wins should keep only first schema
                return (schemas.Count == 1)
                    .Label($"Expected 1 schema but got {schemas.Count}")
                    .And(finalName1 == schemaName)
                    .Label("First schema should keep original name")
                    .And(finalName2 == schemaName)
                    .Label("Second schema should also map to original name (first wins)")
                    .And(warning2 != null)
                    .Label("Warning should be generated")
                    .And(warning2?.Type == MergeWarningType.SchemaConflict)
                    .Label("Warning type should be SchemaConflict");
            });
    }

    /// <summary>
    /// Feature: openapi-merge-tool, Property 13: Schema Rename on Conflict (Fail strategy)
    /// With Fail strategy, conflicting schemas should throw exception.
    /// **Validates: Requirements 4.4**
    /// </summary>
    [Property(MaxTest = 100)]
    public Property FailStrategy_ShouldThrowException()
    {
        return Prop.ForAll(
            Gen.Elements("User", "Product", "Order").ToArbitrary(),
            Gen.Elements("ServiceA", "ServiceB").ToArbitrary(),
            Gen.Elements("ServiceC", "ServiceD").ToArbitrary(),
            (schemaName, source1, source2) =>
            {
                if (source1 == source2) source2 = source2 + "_2";

                var schema1 = new OpenApiSchema { Type = "string" };
                var schema2 = new OpenApiSchema { Type = "integer" }; // Different!

                var deduplicator = new SchemaDeduplicator(SchemaConflictStrategy.Fail);

                deduplicator.AddSchema(schemaName, schema1, source1);

                // Property: adding conflicting schema should throw
                try
                {
                    deduplicator.AddSchema(schemaName, schema2, source2);
                    return false.Label("Expected SchemaMergeException to be thrown");
                }
                catch (SchemaMergeException ex)
                {
                    return (ex.SchemaName == schemaName)
                        .Label($"Exception schema name should be '{schemaName}'")
                        .And(ex.SourceName == source2)
                        .Label($"Exception source name should be '{source2}'");
                }
            });
    }
}
