using FsCheck;
using FsCheck.Xunit;
using Oproto.Lambda.OpenApi.Merge;
using System.Text.Json;

namespace Oproto.Lambda.OpenApi.Merge.Tests;

/// <summary>
/// Property-based tests for MergeConfiguration compatibility and round-trip serialization.
/// </summary>
public class ConfigCompatibilityPropertyTests
{
    /// <summary>
    /// Generators for MergeConfiguration test data.
    /// </summary>
    private static class ConfigGenerators
    {
        public static Gen<string> TitleGen()
        {
            return Gen.Elements("My API", "Test API", "Public API", "Internal API", "Service API", "Gateway API");
        }

        public static Gen<string> VersionGen()
        {
            return Gen.Elements("1.0.0", "2.0.0", "1.0.0-beta", "3.1.0", "0.1.0", "1.2.3");
        }

        public static Gen<string?> DescriptionGen()
        {
            return Gen.OneOf(
                Gen.Constant<string?>(null),
                Gen.Elements<string?>("API description", "Test description", "Merged API specification", "Service endpoints")
            );
        }

        public static Gen<string> ServerUrlGen()
        {
            return Gen.Elements(
                "https://api.example.com/v1",
                "https://staging.example.com/v1",
                "https://dev.example.com/v1",
                "https://api.production.com",
                "https://localhost:5000");
        }

        public static Gen<string?> ServerDescriptionGen()
        {
            return Gen.OneOf(
                Gen.Constant<string?>(null),
                Gen.Elements<string?>("Production", "Staging", "Development", "Local")
            );
        }

        public static Gen<string> SourcePathGen()
        {
            return Gen.Elements(
                "./api1.json", "./api2.json", "./users-service.json", 
                "./orders-service.json", "./products.json", "service.json");
        }

        public static Gen<string?> PathPrefixGen()
        {
            return Gen.OneOf(
                Gen.Constant<string?>(null),
                Gen.Elements<string?>("/v1", "/v2", "/api", "/users", "/orders", "/products")
            );
        }

        public static Gen<string?> OperationIdPrefixGen()
        {
            return Gen.OneOf(
                Gen.Constant<string?>(null),
                Gen.Elements<string?>("api1_", "api2_", "users_", "orders_", "products_")
            );
        }

        public static Gen<string?> SourceNameGen()
        {
            return Gen.OneOf(
                Gen.Constant<string?>(null),
                Gen.Elements<string?>("Users", "Orders", "Products", "API1", "API2", "Service")
            );
        }

        public static Gen<string> OutputGen()
        {
            return Gen.Elements(
                "merged-openapi.json", "output.json", "api.json", 
                "merged.json", "combined-api.json", "openapi.json");
        }

        public static Gen<string> ExcludePatternGen()
        {
            return Gen.Elements(
                "*-draft.json", "*.backup.json", "temp-*.json", 
                "*-old.json", "*.bak", "draft/*.json");
        }

        public static Gen<MergeServerConfiguration> ServerConfigGen()
        {
            return from url in ServerUrlGen()
                   from description in ServerDescriptionGen()
                   select new MergeServerConfiguration
                   {
                       Url = url,
                       Description = description
                   };
        }

        public static Gen<SourceConfiguration> SourceConfigGen()
        {
            return from path in SourcePathGen()
                   from pathPrefix in PathPrefixGen()
                   from operationIdPrefix in OperationIdPrefixGen()
                   from name in SourceNameGen()
                   select new SourceConfiguration
                   {
                       Path = path,
                       PathPrefix = pathPrefix,
                       OperationIdPrefix = operationIdPrefix,
                       Name = name
                   };
        }

        public static Gen<MergeInfoConfiguration> InfoConfigGen()
        {
            return from title in TitleGen()
                   from version in VersionGen()
                   from description in DescriptionGen()
                   select new MergeInfoConfiguration
                   {
                       Title = title,
                       Version = version,
                       Description = description
                   };
        }

        public static Gen<MergeConfiguration> MergeConfigGen()
        {
            return from info in InfoConfigGen()
                   from serverCount in Gen.Choose(0, 3)
                   from servers in Gen.ListOf(serverCount, ServerConfigGen())
                   from sourceCount in Gen.Choose(1, 4)
                   from sources in Gen.ListOf(sourceCount, SourceConfigGen())
                   from output in OutputGen()
                   from schemaConflict in Gen.Elements(
                       SchemaConflictStrategy.Rename, 
                       SchemaConflictStrategy.FirstWins, 
                       SchemaConflictStrategy.Fail)
                   from autoDiscover in Gen.Elements(true, false)
                   from excludePatternCount in Gen.Choose(0, 3)
                   from excludePatterns in Gen.ListOf(excludePatternCount, ExcludePatternGen())
                   select new MergeConfiguration
                   {
                       Info = info,
                       Servers = servers.ToList(),
                       Sources = sources.ToList(),
                       Output = output,
                       SchemaConflict = schemaConflict,
                       AutoDiscover = autoDiscover,
                       ExcludePatterns = excludePatterns.Distinct().ToList()
                   };
        }

        /// <summary>
        /// Generates a MergeConfiguration without the new AutoDiscover and ExcludePatterns properties
        /// to test backwards compatibility.
        /// </summary>
        public static Gen<MergeConfiguration> LegacyMergeConfigGen()
        {
            return from info in InfoConfigGen()
                   from serverCount in Gen.Choose(0, 3)
                   from servers in Gen.ListOf(serverCount, ServerConfigGen())
                   from sourceCount in Gen.Choose(1, 4)
                   from sources in Gen.ListOf(sourceCount, SourceConfigGen())
                   from output in OutputGen()
                   from schemaConflict in Gen.Elements(
                       SchemaConflictStrategy.Rename, 
                       SchemaConflictStrategy.FirstWins, 
                       SchemaConflictStrategy.Fail)
                   select new MergeConfiguration
                   {
                       Info = info,
                       Servers = servers.ToList(),
                       Sources = sources.ToList(),
                       Output = output,
                       SchemaConflict = schemaConflict
                       // AutoDiscover and ExcludePatterns use defaults
                   };
        }
    }

    /// <summary>
    /// Feature: lambda-merge-tool, Property 2: Config Compatibility Round-Trip
    /// For any valid MergeConfiguration object, serializing it to JSON and deserializing it 
    /// SHALL preserve all original property values.
    /// **Validates: Requirements 2.3, 3.1**
    /// </summary>
    [Property(MaxTest = 100)]
    public Property MergeConfiguration_RoundTrip_PreservesAllProperties()
    {
        return Prop.ForAll(
            ConfigGenerators.MergeConfigGen().ToArbitrary(),
            config =>
            {
                // Serialize to JSON
                var json = JsonSerializer.Serialize(config);
                
                // Deserialize back
                var deserialized = JsonSerializer.Deserialize<MergeConfiguration>(json);

                // Verify all properties are preserved
                var infoPreserved = deserialized!.Info.Title == config.Info.Title
                    && deserialized.Info.Version == config.Info.Version
                    && deserialized.Info.Description == config.Info.Description;

                var serversPreserved = deserialized.Servers.Count == config.Servers.Count
                    && deserialized.Servers.Zip(config.Servers, (d, c) => 
                        d.Url == c.Url && d.Description == c.Description).All(x => x);

                var sourcesPreserved = deserialized.Sources.Count == config.Sources.Count
                    && deserialized.Sources.Zip(config.Sources, (d, c) =>
                        d.Path == c.Path 
                        && d.PathPrefix == c.PathPrefix 
                        && d.OperationIdPrefix == c.OperationIdPrefix 
                        && d.Name == c.Name).All(x => x);

                var outputPreserved = deserialized.Output == config.Output;
                var schemaConflictPreserved = deserialized.SchemaConflict == config.SchemaConflict;
                var autoDiscoverPreserved = deserialized.AutoDiscover == config.AutoDiscover;
                var excludePatternsPreserved = deserialized.ExcludePatterns.Count == config.ExcludePatterns.Count
                    && deserialized.ExcludePatterns.SequenceEqual(config.ExcludePatterns);

                return infoPreserved.Label("Info should be preserved")
                    .And(serversPreserved).Label("Servers should be preserved")
                    .And(sourcesPreserved).Label("Sources should be preserved")
                    .And(outputPreserved).Label("Output should be preserved")
                    .And(schemaConflictPreserved).Label("SchemaConflict should be preserved")
                    .And(autoDiscoverPreserved).Label("AutoDiscover should be preserved")
                    .And(excludePatternsPreserved).Label("ExcludePatterns should be preserved");
            });
    }

    /// <summary>
    /// Feature: lambda-merge-tool, Property 2: Config Compatibility Round-Trip
    /// For any legacy MergeConfiguration (without AutoDiscover/ExcludePatterns), deserializing 
    /// SHALL have sensible defaults for the new properties (AutoDiscover = false, ExcludePatterns = empty).
    /// **Validates: Requirements 2.3, 3.1**
    /// </summary>
    [Property(MaxTest = 100)]
    public Property LegacyConfig_Deserialized_HasSensibleDefaults()
    {
        return Prop.ForAll(
            ConfigGenerators.LegacyMergeConfigGen().ToArbitrary(),
            config =>
            {
                // Serialize to JSON (will include autoDiscover: false and excludePatterns: [])
                var json = JsonSerializer.Serialize(config);
                
                // Simulate legacy JSON by removing the new properties
                var jsonDoc = System.Text.Json.JsonDocument.Parse(json);
                var legacyJson = CreateLegacyJson(jsonDoc);
                
                // Deserialize the legacy JSON
                var deserialized = JsonSerializer.Deserialize<MergeConfiguration>(legacyJson);

                // Verify defaults are applied
                var autoDiscoverDefault = deserialized!.AutoDiscover == false;
                var excludePatternsDefault = deserialized.ExcludePatterns != null 
                    && deserialized.ExcludePatterns.Count == 0;

                // Verify original properties are preserved
                var infoPreserved = deserialized.Info.Title == config.Info.Title
                    && deserialized.Info.Version == config.Info.Version;
                var outputPreserved = deserialized.Output == config.Output;
                var schemaConflictPreserved = deserialized.SchemaConflict == config.SchemaConflict;

                return autoDiscoverDefault.Label("AutoDiscover should default to false")
                    .And(excludePatternsDefault).Label("ExcludePatterns should default to empty list")
                    .And(infoPreserved).Label("Info should be preserved")
                    .And(outputPreserved).Label("Output should be preserved")
                    .And(schemaConflictPreserved).Label("SchemaConflict should be preserved");
            });
    }

    /// <summary>
    /// Feature: lambda-merge-tool, Property 2: Config Compatibility Round-Trip
    /// For any MergeConfiguration, serialization SHALL be deterministic (same input produces same output).
    /// **Validates: Requirements 2.3, 3.1**
    /// </summary>
    [Property(MaxTest = 100)]
    public Property MergeConfiguration_Serialization_IsDeterministic()
    {
        return Prop.ForAll(
            ConfigGenerators.MergeConfigGen().ToArbitrary(),
            config =>
            {
                // Serialize twice
                var json1 = JsonSerializer.Serialize(config);
                var json2 = JsonSerializer.Serialize(config);

                // Both serializations should produce identical output
                return (json1 == json2).Label("Serialization should be deterministic");
            });
    }

    /// <summary>
    /// Creates a legacy JSON string by removing autoDiscover and excludePatterns properties.
    /// </summary>
    private static string CreateLegacyJson(System.Text.Json.JsonDocument jsonDoc)
    {
        var options = new JsonWriterOptions { Indented = false };
        using var stream = new System.IO.MemoryStream();
        using (var writer = new Utf8JsonWriter(stream, options))
        {
            writer.WriteStartObject();
            foreach (var property in jsonDoc.RootElement.EnumerateObject())
            {
                // Skip the new properties to simulate legacy JSON
                if (property.Name == "autoDiscover" || property.Name == "excludePatterns")
                    continue;
                
                property.WriteTo(writer);
            }
            writer.WriteEndObject();
        }
        return System.Text.Encoding.UTF8.GetString(stream.ToArray());
    }
}
