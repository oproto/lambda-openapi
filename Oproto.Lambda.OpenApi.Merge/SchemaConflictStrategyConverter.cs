namespace Oproto.Lambda.OpenApi.Merge;

using System;
using System.Text.Json;
using System.Text.Json.Serialization;

/// <summary>
/// JSON converter for SchemaConflictStrategy enum that handles kebab-case serialization.
/// </summary>
public class SchemaConflictStrategyConverter : JsonConverter<SchemaConflictStrategy>
{
    /// <inheritdoc />
    public override SchemaConflictStrategy Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
    {
        var value = reader.GetString();
        
        if (value == null)
        {
            return SchemaConflictStrategy.Rename;
        }

        return value.ToLowerInvariant() switch
        {
            "rename" => SchemaConflictStrategy.Rename,
            "first-wins" or "firstwins" => SchemaConflictStrategy.FirstWins,
            "fail" => SchemaConflictStrategy.Fail,
            "" => SchemaConflictStrategy.Rename,
            _ => throw new JsonException($"Unknown SchemaConflictStrategy value: {value}")
        };
    }

    /// <inheritdoc />
    public override void Write(Utf8JsonWriter writer, SchemaConflictStrategy value, JsonSerializerOptions options)
    {
        var stringValue = value switch
        {
            SchemaConflictStrategy.Rename => "rename",
            SchemaConflictStrategy.FirstWins => "first-wins",
            SchemaConflictStrategy.Fail => "fail",
            _ => throw new JsonException($"Unknown SchemaConflictStrategy value: {value}")
        };
        
        writer.WriteStringValue(stringValue);
    }
}
