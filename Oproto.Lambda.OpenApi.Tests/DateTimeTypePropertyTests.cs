#nullable enable
using System.Text.Json;
using FsCheck;
using FsCheck.Xunit;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Oproto.Lambda.OpenApi.SourceGenerator;

namespace Oproto.Lambda.OpenApi.Tests;

/// <summary>
/// Property-based tests for DateOnly and TimeOnly type handling.
/// </summary>
public class DateTimeTypePropertyTests
{
    /// <summary>
    /// **Feature: generator-bug-fixes, Property 3: DateOnly/TimeOnly Schema Mapping**
    /// **Validates: Requirements 2.1, 2.2, 2.3, 2.4**
    /// 
    /// For any property or parameter of type DateOnly, TimeOnly, DateOnly?, or TimeOnly?,
    /// the generated schema SHALL have type: string with the appropriate format 
    /// (date for DateOnly, time for TimeOnly), and these types SHALL NOT appear 
    /// as separate schema definitions in components/schemas.
    /// </summary>
    [Property(MaxTest = 100)]
    public Property DateOnlyTimeOnly_GeneratesCorrectSchemaFormat()
    {
        // Generate combinations of type and nullability
        var typeGen = Gen.Elements("DateOnly", "TimeOnly");
        var nullableGen = Gen.Elements(true, false);
        var propertyNameGen = Gen.Elements("StartDate", "EndDate", "OpenTime", "CloseTime", "BirthDate", "ScheduledTime");

        return Prop.ForAll(
            typeGen.ToArbitrary(),
            nullableGen.ToArbitrary(),
            propertyNameGen.ToArbitrary(),
            (typeName, isNullable, propertyName) =>
            {
                var source = GenerateSourceWithDateTimeProperty(typeName, isNullable, propertyName);
                var schemaInfo = ExtractSchemaInfo(source, propertyName);

                if (schemaInfo == null)
                    return false.Label($"Could not extract schema for property '{propertyName}'");

                // Verify type is "string"
                var hasCorrectType = schemaInfo.Type == "string";

                // Verify format is correct
                var expectedFormat = typeName == "DateOnly" ? "date" : "time";
                var hasCorrectFormat = schemaInfo.Format == expectedFormat;

                // Verify nullable is set correctly
                var hasCorrectNullable = schemaInfo.Nullable == isNullable;

                return (hasCorrectType && hasCorrectFormat && hasCorrectNullable)
                    .Label($"Expected type='string', format='{expectedFormat}', nullable={isNullable} " +
                           $"but got type='{schemaInfo.Type}', format='{schemaInfo.Format}', nullable={schemaInfo.Nullable}");
            });
    }

    /// <summary>
    /// **Feature: generator-bug-fixes, Property 3: DateOnly/TimeOnly Schema Mapping (Components Exclusion)**
    /// **Validates: Requirements 2.4**
    /// 
    /// DateOnly and TimeOnly types SHALL NOT appear as separate schema definitions 
    /// in the components/schemas section.
    /// </summary>
    [Property(MaxTest = 100)]
    public Property DateOnlyTimeOnly_NotInComponentsSchemas()
    {
        var typeGen = Gen.Elements("DateOnly", "TimeOnly");
        var nullableGen = Gen.Elements(true, false);

        return Prop.ForAll(
            typeGen.ToArbitrary(),
            nullableGen.ToArbitrary(),
            (typeName, isNullable) =>
            {
                var source = GenerateSourceWithDateTimeProperty(typeName, isNullable, "TestProperty");
                var componentSchemas = ExtractComponentSchemas(source);

                // DateOnly and TimeOnly should NOT be in components/schemas
                var hasDateOnlySchema = componentSchemas.Contains("DateOnly");
                var hasTimeOnlySchema = componentSchemas.Contains("TimeOnly");

                return (!hasDateOnlySchema && !hasTimeOnlySchema)
                    .Label($"DateOnly or TimeOnly found in components/schemas: [{string.Join(", ", componentSchemas)}]");
            });
    }

    /// <summary>
    /// Tests that DateOnly properties in request bodies have correct schema.
    /// </summary>
    [Property(MaxTest = 100)]
    public Property DateOnlyInRequestBody_HasCorrectSchema()
    {
        var nullableGen = Gen.Elements(true, false);

        return Prop.ForAll(
            nullableGen.ToArbitrary(),
            isNullable =>
            {
                var source = GenerateSourceWithRequestBody("DateOnly", isNullable);
                var requestBodySchema = ExtractRequestBodyPropertySchema(source, "Date");

                if (requestBodySchema == null)
                    return false.Label("Could not extract request body schema");

                var hasCorrectType = requestBodySchema.Type == "string";
                var hasCorrectFormat = requestBodySchema.Format == "date";
                var hasCorrectNullable = requestBodySchema.Nullable == isNullable;

                return (hasCorrectType && hasCorrectFormat && hasCorrectNullable)
                    .Label($"Expected type='string', format='date', nullable={isNullable} " +
                           $"but got type='{requestBodySchema.Type}', format='{requestBodySchema.Format}', nullable={requestBodySchema.Nullable}");
            });
    }

    /// <summary>
    /// Tests that TimeOnly properties in request bodies have correct schema.
    /// </summary>
    [Property(MaxTest = 100)]
    public Property TimeOnlyInRequestBody_HasCorrectSchema()
    {
        var nullableGen = Gen.Elements(true, false);

        return Prop.ForAll(
            nullableGen.ToArbitrary(),
            isNullable =>
            {
                var source = GenerateSourceWithRequestBody("TimeOnly", isNullable);
                var requestBodySchema = ExtractRequestBodyPropertySchema(source, "Time");

                if (requestBodySchema == null)
                    return false.Label("Could not extract request body schema");

                var hasCorrectType = requestBodySchema.Type == "string";
                var hasCorrectFormat = requestBodySchema.Format == "time";
                var hasCorrectNullable = requestBodySchema.Nullable == isNullable;

                return (hasCorrectType && hasCorrectFormat && hasCorrectNullable)
                    .Label($"Expected type='string', format='time', nullable={isNullable} " +
                           $"but got type='{requestBodySchema.Type}', format='{requestBodySchema.Format}', nullable={requestBodySchema.Nullable}");
            });
    }

    private string GenerateSourceWithDateTimeProperty(string typeName, bool isNullable, string propertyName)
    {
        var nullableSuffix = isNullable ? "?" : "";
        var fullTypeName = $"{typeName}{nullableSuffix}";

        return $@"
using Amazon.Lambda.Annotations;
using Amazon.Lambda.Annotations.APIGateway;
using System;
using System.Threading.Tasks;

public class TestModel
{{
    public {fullTypeName} {propertyName} {{ get; set; }}
}}

public class TestFunctions 
{{
    [LambdaFunction]
    [HttpApi(LambdaHttpMethod.Get, ""/items"")]
    public TestModel GetItem() => new TestModel();
}}";
    }

    private string GenerateSourceWithRequestBody(string typeName, bool isNullable)
    {
        var nullableSuffix = isNullable ? "?" : "";
        var fullTypeName = $"{typeName}{nullableSuffix}";
        var propertyName = typeName == "DateOnly" ? "Date" : "Time";

        return $@"
using Amazon.Lambda.Annotations;
using Amazon.Lambda.Annotations.APIGateway;
using System;
using System.Threading.Tasks;

public class CreateRequest
{{
    public {fullTypeName} {propertyName} {{ get; set; }}
}}

public class TestFunctions 
{{
    [LambdaFunction]
    [HttpApi(LambdaHttpMethod.Post, ""/items"")]
    public string CreateItem([FromBody] CreateRequest request) => ""created"";
}}";
    }

    private SchemaInfo? ExtractSchemaInfo(string source, string propertyName)
    {
        try
        {
            var jsonContent = GenerateOpenApiJson(source);
            if (string.IsNullOrEmpty(jsonContent))
                return null;

            using var doc = JsonDocument.Parse(jsonContent);

            // Look for the property in components/schemas/TestModel
            if (doc.RootElement.TryGetProperty("components", out var components) &&
                components.TryGetProperty("schemas", out var schemas) &&
                schemas.TryGetProperty("TestModel", out var testModel) &&
                testModel.TryGetProperty("properties", out var properties) &&
                properties.TryGetProperty(propertyName, out var property))
            {
                return ExtractSchemaInfoFromElement(property);
            }
        }
        catch
        {
            // Return null on error
        }

        return null;
    }

    private SchemaInfo? ExtractRequestBodyPropertySchema(string source, string propertyName)
    {
        try
        {
            var jsonContent = GenerateOpenApiJson(source);
            if (string.IsNullOrEmpty(jsonContent))
                return null;

            using var doc = JsonDocument.Parse(jsonContent);

            // Look for the property in components/schemas/CreateRequest
            if (doc.RootElement.TryGetProperty("components", out var components) &&
                components.TryGetProperty("schemas", out var schemas) &&
                schemas.TryGetProperty("CreateRequest", out var createRequest) &&
                createRequest.TryGetProperty("properties", out var properties) &&
                properties.TryGetProperty(propertyName, out var property))
            {
                return ExtractSchemaInfoFromElement(property);
            }
        }
        catch
        {
            // Return null on error
        }

        return null;
    }

    private SchemaInfo ExtractSchemaInfoFromElement(JsonElement property)
    {
        var type = property.TryGetProperty("type", out var typeProp) ? typeProp.GetString() : null;
        var format = property.TryGetProperty("format", out var formatProp) ? formatProp.GetString() : null;
        var nullable = property.TryGetProperty("nullable", out var nullableProp) && nullableProp.GetBoolean();

        return new SchemaInfo
        {
            Type = type ?? "",
            Format = format ?? "",
            Nullable = nullable
        };
    }

    private List<string> ExtractComponentSchemas(string source)
    {
        var schemas = new List<string>();

        try
        {
            var jsonContent = GenerateOpenApiJson(source);
            if (string.IsNullOrEmpty(jsonContent))
                return schemas;

            using var doc = JsonDocument.Parse(jsonContent);

            if (doc.RootElement.TryGetProperty("components", out var components) &&
                components.TryGetProperty("schemas", out var schemasElement))
            {
                foreach (var schema in schemasElement.EnumerateObject())
                {
                    schemas.Add(schema.Name);
                }
            }
        }
        catch
        {
            // Return empty on error
        }

        return schemas;
    }

    private string GenerateOpenApiJson(string source)
    {
        var compilation = CompilerHelper.CreateCompilation(source);
        var generator = new OpenApiSpecGenerator();

        var driver = CSharpGeneratorDriver.Create(generator);
        driver.RunGeneratorsAndUpdateCompilation(compilation,
            out var outputCompilation,
            out var diagnostics);

        if (diagnostics.Any(d => d.Severity == DiagnosticSeverity.Error))
            return string.Empty;

        var generatedFile = outputCompilation.SyntaxTrees
            .FirstOrDefault(x => x.FilePath.EndsWith("OpenApiOutput.g.cs"));

        if (generatedFile == null)
            return string.Empty;

        var generatedContent = generatedFile.GetRoot().GetText().ToString();

        var attributeStart = generatedContent.IndexOf("[assembly: OpenApiOutput(@\"") + 26;
        var attributeEnd = generatedContent.LastIndexOf("\", \"openapi.json\")]");

        if (attributeStart < 26 || attributeEnd < 0)
            return string.Empty;

        var rawJson = generatedContent[attributeStart..attributeEnd];

        return rawJson
            .Replace("\"\"", "\"")
            .Replace("\r\n", " ")
            .Replace("\n", " ")
            .Replace("\r", " ")
            .Replace("\\\"", "\"")
            .Trim()
            .TrimStart('"')
            .TrimEnd('"');
    }

    private class SchemaInfo
    {
        public string Type { get; set; } = "";
        public string Format { get; set; } = "";
        public bool Nullable { get; set; }
    }
}
