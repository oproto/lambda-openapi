#nullable enable
using System.Reflection;
using System.Runtime.CompilerServices;
using FsCheck;
using FsCheck.Xunit;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.OpenApi.Models;
using Oproto.Lambda.OpenApi.Attributes;
using Oproto.Lambda.OpenApi.SourceGenerator;

namespace Oproto.Lambda.OpenApi.Tests;

/// <summary>
/// Property-based tests for dictionary type handling in OpenAPI schema generation.
/// Feature: dictionary-schema-support
/// </summary>
public class DictionarySchemaPropertyTests
{
    private readonly Compilation _compilation;
    private readonly OpenApiSpecGenerator _generator;

    public DictionarySchemaPropertyTests()
    {
        var references = new[]
        {
            MetadataReference.CreateFromFile(typeof(object).Assembly.Location),
            MetadataReference.CreateFromFile(typeof(CompilerGeneratedAttribute).Assembly.Location),
            MetadataReference.CreateFromFile(typeof(List<>).Assembly.Location),
            MetadataReference.CreateFromFile(typeof(Enumerable).Assembly.Location),
            MetadataReference.CreateFromFile(typeof(OpenApiSchema).Assembly.Location),
            MetadataReference.CreateFromFile(typeof(DateTime).Assembly.Location),
            MetadataReference.CreateFromFile(Assembly.Load("netstandard").Location),
            MetadataReference.CreateFromFile(Assembly.Load("System.Runtime").Location),
            MetadataReference.CreateFromFile(typeof(Attribute).Assembly.Location),
            MetadataReference.CreateFromFile(typeof(OpenApiSchemaAttribute).Assembly.Location)
        };

        var syntaxTrees = new[]
        {
            CSharpSyntaxTree.ParseText(@"
            using System;
            using System.Collections.Generic;
            using Microsoft.OpenApi.Models;
            using Microsoft.OpenApi.Any;
            using Oproto.Lambda.OpenApi.Attributes;
            using System.ComponentModel;
        ")
        };

        _compilation = CSharpCompilation.Create(
            "TestAssembly",
            syntaxTrees,
            references,
            new CSharpCompilationOptions(
                OutputKind.DynamicallyLinkedLibrary,
                nullableContextOptions: NullableContextOptions.Enable)
        );

        _generator = new OpenApiSpecGenerator();
    }

    private OpenApiSchema GenerateSchemaFromSource(string source, string typeName)
    {
        var syntaxTree = CSharpSyntaxTree.ParseText(source);
        var compilation = _compilation.AddSyntaxTrees(syntaxTree);

        var semanticModel = compilation.GetSemanticModel(syntaxTree);
        var typeDeclaration = syntaxTree.GetRoot()
                                  .DescendantNodes()
                                  .FirstOrDefault(n =>
                                      (n is ClassDeclarationSyntax cls && cls.Identifier.Text == typeName) ||
                                      (n is EnumDeclarationSyntax enm && enm.Identifier.Text == typeName))
                              ?? throw new InvalidOperationException($"Could not find type {typeName}");

        var typeSymbol = semanticModel.GetDeclaredSymbol(typeDeclaration) as ITypeSymbol
                         ?? throw new InvalidOperationException($"Could not get symbol for {typeName}");

        return _generator.CreateSchema(typeSymbol);
    }

    /// <summary>
    /// **Feature: dictionary-schema-support, Property 1: Dictionary Type Detection**
    /// **Validates: Requirements 1.1, 1.2, 1.3, 1.4, 1.5**
    /// 
    /// For any type symbol that is Dictionary{K,V}, IDictionary{K,V}, IReadOnlyDictionary{K,V},
    /// or implements IDictionary{K,V}, the generated schema SHALL have type: "object" and
    /// a non-null additionalProperties schema.
    /// </summary>
    [Property(MaxTest = 100)]
    public Property DictionaryTypes_AreDetectedAndGenerateObjectSchema()
    {
        // Generate combinations of dictionary types and value types
        var dictionaryTypeGen = Gen.Elements(
            "Dictionary<string, {0}>",
            "IDictionary<string, {0}>",
            "IReadOnlyDictionary<string, {0}>"
        );
        
        var valueTypeGen = Gen.Elements("string", "int", "bool", "decimal");
        var propertyNameGen = Gen.Elements("Items", "MyData", "Mapping", "Values", "Entries");

        return Prop.ForAll(
            dictionaryTypeGen.ToArbitrary(),
            valueTypeGen.ToArbitrary(),
            propertyNameGen.ToArbitrary(),
            (dictionaryTypeTemplate, valueType, propertyName) =>
            {
                var dictionaryType = string.Format(dictionaryTypeTemplate, valueType);
                var source = GenerateClassSource(dictionaryType, propertyName);
                
                try
                {
                    var schema = GenerateSchemaFromSource(source, "TestClass");
                    
                    // Debug: list all properties
                    var allProps = string.Join(", ", schema.Properties.Keys);
                    
                    if (!schema.Properties.ContainsKey(propertyName))
                        return false.Label($"Property '{propertyName}' not found in schema. Available: [{allProps}]");

                    var propSchema = schema.Properties[propertyName];
                    
                    // Verify type is "object"
                    var hasCorrectType = propSchema.Type == "object";

                    // Verify additionalProperties is present
                    var hasAdditionalProperties = propSchema.AdditionalProperties != null;

                    return (hasCorrectType && hasAdditionalProperties)
                        .Label($"Expected type='object' with additionalProperties " +
                               $"but got type='{propSchema.Type}', hasAdditionalProperties={hasAdditionalProperties} " +
                               $"for dictionary type '{dictionaryType}'");
                }
                catch (Exception ex)
                {
                    return false.Label($"Exception: {ex.Message}");
                }
            });
    }


    /// <summary>
    /// **Feature: dictionary-schema-support, Property 2: Dictionary Schema Structure**
    /// **Validates: Requirements 5.2**
    /// 
    /// For any dictionary type, the generated OpenAPI schema SHALL have type: "object"
    /// and a non-null additionalProperties schema (not empty properties).
    /// </summary>
    [Property(MaxTest = 100)]
    public Property DictionarySchema_HasObjectTypeAndAdditionalProperties()
    {
        var dictionaryTypeGen = Gen.Elements(
            "Dictionary<string, string>",
            "Dictionary<string, int>",
            "Dictionary<string, bool>",
            "IDictionary<string, decimal>",
            "IReadOnlyDictionary<string, DateTime>"
        );
        
        var propertyNameGen = Gen.Elements("Map", "Dict", "Lookup", "Index", "Cache");

        return Prop.ForAll(
            dictionaryTypeGen.ToArbitrary(),
            propertyNameGen.ToArbitrary(),
            (dictionaryType, propertyName) =>
            {
                var source = GenerateClassSource(dictionaryType, propertyName);
                
                try
                {
                    var schema = GenerateSchemaFromSource(source, "TestClass");
                    
                    if (!schema.Properties.ContainsKey(propertyName))
                        return false.Label($"Property '{propertyName}' not found in schema");

                    var propSchema = schema.Properties[propertyName];

                    // Verify type is "object" (not array or other type)
                    var hasObjectType = propSchema.Type == "object";

                    // Verify additionalProperties is present (dictionary pattern)
                    var hasAdditionalProperties = propSchema.AdditionalProperties != null;

                    // Verify it does NOT have empty properties (which would indicate complex type handling)
                    var doesNotHaveEmptyProperties = propSchema.Properties == null || propSchema.Properties.Count == 0;

                    return (hasObjectType && hasAdditionalProperties && doesNotHaveEmptyProperties)
                        .Label($"Expected type='object' with additionalProperties and no empty properties " +
                               $"but got type='{propSchema.Type}', hasAdditionalProperties={hasAdditionalProperties}, " +
                               $"hasProperties={propSchema.Properties?.Count ?? 0}");
                }
                catch (Exception ex)
                {
                    return false.Label($"Exception: {ex.Message}");
                }
            });
    }

    /// <summary>
    /// **Feature: dictionary-schema-support, Property 3: Simple Value Type Schema**
    /// **Validates: Requirements 2.1, 2.2, 2.3, 2.4, 2.5**
    /// 
    /// For any dictionary with a simple value type (string, int, bool, decimal, DateTime, etc.),
    /// the additionalProperties schema SHALL have the correct OpenAPI type and format
    /// matching the value type.
    /// </summary>
    [Property(MaxTest = 100)]
    public Property SimpleValueType_MapsToCorrectOpenApiType()
    {
        // Map C# types to expected OpenAPI types and formats
        // Note: decimal maps to number with format "decimal" in the implementation
        var typeMapping = new[]
        {
            ("string", "string", (string?)null),
            ("int", "integer", (string?)null),
            ("bool", "boolean", (string?)null),
            ("decimal", "number", "decimal"),
            ("DateTime", "string", "date-time")
        };

        var typeMappingGen = Gen.Elements(typeMapping);
        var propertyNameGen = Gen.Elements("Values", "MyData", "Items", "Entries", "Records");

        return Prop.ForAll(
            typeMappingGen.ToArbitrary(),
            propertyNameGen.ToArbitrary(),
            (typeInfo, propertyName) =>
            {
                var (csharpType, expectedOpenApiType, expectedFormat) = typeInfo;
                var dictionaryType = $"Dictionary<string, {csharpType}>";
                var source = GenerateClassSource(dictionaryType, propertyName);
                
                try
                {
                    var schema = GenerateSchemaFromSource(source, "TestClass");
                    
                    if (!schema.Properties.ContainsKey(propertyName))
                        return false.Label($"Property '{propertyName}' not found in schema");

                    var propSchema = schema.Properties[propertyName];
                    var additionalProps = propSchema.AdditionalProperties;

                    if (additionalProps == null)
                        return false.Label($"additionalProperties is null for Dictionary<string, {csharpType}>");

                    // Verify additionalProperties type matches expected OpenAPI type
                    var hasCorrectType = additionalProps.Type == expectedOpenApiType;

                    // Verify format matches (if expected)
                    var hasCorrectFormat = expectedFormat == null 
                        ? string.IsNullOrEmpty(additionalProps.Format)
                        : additionalProps.Format == expectedFormat;

                    return (hasCorrectType && hasCorrectFormat)
                        .Label($"For Dictionary<string, {csharpType}>, expected additionalProperties type='{expectedOpenApiType}', format='{expectedFormat ?? "null"}' " +
                               $"but got type='{additionalProps.Type}', format='{additionalProps.Format ?? "null"}'");
                }
                catch (Exception ex)
                {
                    return false.Label($"Exception: {ex.Message}");
                }
            });
    }


    /// <summary>
    /// **Feature: dictionary-schema-support, Property 4: Complex Value Type Reference**
    /// **Validates: Requirements 3.1**
    /// 
    /// For any dictionary with a complex (non-simple) value type, the additionalProperties
    /// schema SHALL contain a reference ($ref) or inline object schema for the value type.
    /// </summary>
    [Property(MaxTest = 100)]
    public Property ComplexValueType_GeneratesObjectOrRefSchema()
    {
        var complexTypeNameGen = Gen.Elements("Address", "Person", "Order", "Product", "Customer");
        var propertyNameGen = Gen.Elements("Items", "Records", "Entities", "Objects", "Data");

        return Prop.ForAll(
            complexTypeNameGen.ToArbitrary(),
            propertyNameGen.ToArbitrary(),
            (complexTypeName, propertyName) =>
            {
                var source = GenerateComplexDictionarySource(complexTypeName, propertyName);
                
                try
                {
                    var schema = GenerateSchemaFromSource(source, "TestClass");
                    
                    if (!schema.Properties.ContainsKey(propertyName))
                        return false.Label($"Property '{propertyName}' not found in schema");

                    var propSchema = schema.Properties[propertyName];

                    // Verify dictionary has type "object"
                    var hasObjectType = propSchema.Type == "object";

                    // Verify additionalProperties is present
                    var hasAdditionalProperties = propSchema.AdditionalProperties != null;

                    // Verify additionalProperties is either an object type or has a $ref
                    var additionalProps = propSchema.AdditionalProperties;
                    var hasComplexValueSchema = additionalProps != null && 
                        (additionalProps.Type == "object" || additionalProps.Reference != null);

                    return (hasObjectType && hasAdditionalProperties && hasComplexValueSchema)
                        .Label($"Expected dictionary with complex value type to have additionalProperties with object type or $ref " +
                               $"but got type='{propSchema.Type}', hasAdditionalProperties={hasAdditionalProperties}, " +
                               $"additionalPropertiesType='{additionalProps?.Type}', hasRef={additionalProps?.Reference != null}");
                }
                catch (Exception ex)
                {
                    return false.Label($"Exception: {ex.Message}");
                }
            });
    }

    /// <summary>
    /// **Feature: dictionary-schema-support, Property 5: Nullable Dictionary Handling**
    /// **Validates: Requirements 4.1, 4.2**
    /// 
    /// For any nullable dictionary type (either Nullable{Dictionary{K,V}} or dictionary property
    /// with nullable annotation), the generated schema SHALL have nullable: true.
    /// </summary>
    [Property(MaxTest = 100)]
    public Property NullableDictionary_HasNullableTrue()
    {
        var valueTypeGen = Gen.Elements("string", "int", "bool", "decimal");
        var propertyNameGen = Gen.Elements("OptionalMap", "NullableDict", "MaybeData", "OptionalItems");

        return Prop.ForAll(
            valueTypeGen.ToArbitrary(),
            propertyNameGen.ToArbitrary(),
            (valueType, propertyName) =>
            {
                var source = GenerateNullableDictionarySource(valueType, propertyName);
                
                try
                {
                    var schema = GenerateSchemaFromSource(source, "TestClass");
                    
                    if (!schema.Properties.ContainsKey(propertyName))
                        return false.Label($"Property '{propertyName}' not found in schema");

                    var propSchema = schema.Properties[propertyName];

                    // Verify type is "object"
                    var hasObjectType = propSchema.Type == "object";

                    // Verify nullable is true
                    var isNullable = propSchema.Nullable;

                    // Verify additionalProperties is present
                    var hasAdditionalProperties = propSchema.AdditionalProperties != null;

                    return (hasObjectType && isNullable && hasAdditionalProperties)
                        .Label($"Expected nullable dictionary to have type='object', nullable=true, and additionalProperties " +
                               $"but got type='{propSchema.Type}', nullable={propSchema.Nullable}, hasAdditionalProperties={hasAdditionalProperties}");
                }
                catch (Exception ex)
                {
                    return false.Label($"Exception: {ex.Message}");
                }
            });
    }


    #region Helper Methods

    private string GenerateClassSource(string dictionaryType, string propertyName)
    {
        return $@"
using System;
using System.Collections.Generic;

public class TestClass
{{
    public {dictionaryType} {propertyName} {{ get; set; }}
}}";
    }

    private string GenerateComplexDictionarySource(string complexTypeName, string propertyName)
    {
        return $@"
using System;
using System.Collections.Generic;

public class {complexTypeName}
{{
    public string Id {{ get; set; }}
    public string Name {{ get; set; }}
}}

public class TestClass
{{
    public Dictionary<string, {complexTypeName}> {propertyName} {{ get; set; }}
}}";
    }

    private string GenerateNullableDictionarySource(string valueType, string propertyName)
    {
        return $@"
#nullable enable
using System;
using System.Collections.Generic;

public class TestClass
{{
    public Dictionary<string, {valueType}>? {propertyName} {{ get; set; }}
}}";
    }

    #endregion
}
