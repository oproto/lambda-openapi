using System.Diagnostics;
using System.Reflection;
using System.Runtime.CompilerServices;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.OpenApi.Any;
using Microsoft.OpenApi.Models;
using Microsoft.OpenApi.Writers;
using NSubstitute;
using Oproto.OpenApi;

namespace Oproto.OpenApiGenerator.UnitTests;

// Test setup example:
/*
Required attribute definitions for testing:

var attributeSource = @"
    [System.AttributeUsage(System.AttributeTargets.Property | System.AttributeTargets.Parameter)]
    public class OpenApiSchemaAttribute : System.Attribute
    {
        public object Example { get; set; }
        public int MinLength { get; set; }
        public int MaxLength { get; set; }
        public string Pattern { get; set; }
        public double Minimum { get; set; }
        public double Maximum { get; set; }
    }

    [System.AttributeUsage(System.AttributeTargets.Property | System.AttributeTargets.Parameter)]
    public class OpenApiExampleAttribute : System.Attribute
    {
        public OpenApiExampleAttribute(object example)
        {
            Example = example;
        }

        public object Example { get; }
    }
";
*/

public class OpenApiGeneratorTests
{
    private readonly Compilation _compilation;

    private readonly OpenApiSpecGenerator _generator;

    public OpenApiGeneratorTests()
    {
        // Add all necessary references
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
            using Oproto.OpenApi;
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
        try
        {
            var syntaxTree = CSharpSyntaxTree.ParseText(source);
            var compilation = _compilation.AddSyntaxTrees(syntaxTree);

            // Add diagnostic output to help debug compilation issues
            var diagnostics = compilation.GetDiagnostics();
            if (diagnostics.Any())
                foreach (var diagnostic in diagnostics)
                    Debug.WriteLine($"Diagnostic: {diagnostic.Id} {diagnostic.GetMessage()}");

            var semanticModel = compilation.GetSemanticModel(syntaxTree);
            var typeDeclaration = syntaxTree.GetRoot()
                                      .DescendantNodes()
                                      .FirstOrDefault(n =>
                                          (n is ClassDeclarationSyntax cls && cls.Identifier.Text == typeName) ||
                                          (n is EnumDeclarationSyntax enm && enm.Identifier.Text == typeName))
                                  ?? throw new InvalidOperationException($"Could not find type {typeName}");

            var typeSymbol = semanticModel.GetDeclaredSymbol(typeDeclaration)
                             ?? throw new InvalidOperationException($"Could not get symbol for {typeName}");

            return _generator.CreateSchema(typeSymbol as ITypeSymbol);
        }
        catch (Exception ex)
        {
            throw new InvalidOperationException($"Error generating schema: {ex.Message}", ex);
        }
    }

    [Fact]
    public void CreateSchema_SimpleString_GeneratesCorrectSchema()
    {
        // Arrange
        var typeSymbol = CreateTypeSymbol(typeof(string));

        // Act
        var schema = _generator.CreateSchema(typeSymbol);

        // Debug output
        Console.WriteLine($"TypeSymbol Kind: {typeSymbol.TypeKind}");
        Console.WriteLine($"TypeSymbol SpecialType: {typeSymbol.SpecialType}");
        Console.WriteLine($"Generated Schema Type: {schema.Type}");

        // Assert
        Assert.Equal("string", schema.Type);
    }


    private ITypeSymbol CreateTypeSymbol(Type type)
    {
        var references = new[]
        {
            MetadataReference.CreateFromFile(typeof(object).Assembly.Location),
            MetadataReference.CreateFromFile(type.Assembly.Location)
        };

        var compilation = CSharpCompilation.Create(
            "TestAssembly",
            references: references,
            options: new CSharpCompilationOptions(OutputKind.DynamicallyLinkedLibrary));

        return compilation.GetSpecialType(SpecialType.System_String);
    }

    [Fact]
    public void CreateSchema_SimpleTypes_GeneratesCorrectSchema()
    {
        var source = @"
            public class TestClass
            {
                public string StringProperty { get; set; }
                public int IntProperty { get; set; }
                public bool BoolProperty { get; set; }
                public DateTime DateProperty { get; set; }
            }";

        var schema = GenerateSchemaFromSource(source, "TestClass");

        Assert.Equal("object", schema.Type);
        Assert.Equal(4, schema.Properties.Count);

        Assert.Equal("string", schema.Properties["StringProperty"].Type);
        Assert.Equal("integer", schema.Properties["IntProperty"].Type);
        Assert.Equal("boolean", schema.Properties["BoolProperty"].Type);
        Assert.Equal("string", schema.Properties["DateProperty"].Type);
        Assert.Equal("date-time", schema.Properties["DateProperty"].Format);
    }

    [Fact]
    public void CreateSchema_WithAttributes_AppliesSchemaAttributes()
    {
        var source = @"
        using System;
        using Oproto.OpenApi;
        public class TestClass
        {
            [OpenApiSchema(MinLength = 3, MaxLength = 50)]
            public string Name { get; set; }

            [OpenApiSchema(Minimum = 0, Maximum = 100)]
            public int Age { get; set; }

            [OpenApiIgnore]
            public string Internal { get; set; }
        }";


        var schema = GenerateSchemaFromSource(source, "TestClass");

        Assert.Equal(2, schema.Properties.Count);
        Assert.Equal(3, schema.Properties["Name"].MinLength);
        Assert.Equal(50, schema.Properties["Name"].MaxLength);
        Assert.Equal(0, schema.Properties["Age"].Minimum);
        Assert.Equal(100, schema.Properties["Age"].Maximum);
        Assert.False(schema.Properties.ContainsKey("Internal"));
    }

    [Fact]
    public void CreateSchema_WithEnums_GeneratesEnumSchema()
    {
        var source = @"
            public enum TestEnum
            {
                Value1,
                Value2,
                Value3
            }";

        var schema = GenerateSchemaFromSource(source, "TestEnum");

        Assert.Equal("string", schema.Type);
        Assert.NotNull(schema.Enum);
        Assert.Equal(3, schema.Enum.Count);
        Assert.Contains(schema.Enum, v => v.GetType() == typeof(OpenApiString) && ((OpenApiString)v).Value == "Value1");
    }

    [Fact]
    public void CreateSchema_WithCollections_GeneratesArraySchema()
    {
        var source = @"
            public class TestClass
            {
                public List<string> StringList { get; set; }
                public IEnumerable<int> IntEnumerable { get; set; }
            }";

        var schema = GenerateSchemaFromSource(source, "TestClass");

        Assert.Equal(2, schema.Properties.Count);
        Assert.Equal("array", schema.Properties["StringList"].Type);
        Assert.Equal("string", schema.Properties["StringList"].Items.Type);
        Assert.Equal("array", schema.Properties["IntEnumerable"].Type);
        Assert.Equal("integer", schema.Properties["IntEnumerable"].Items.Type);
    }

    [Fact]
    public void CreateSchema_WithNullableTypes_HandlesNullableCorrectly()
    {
        var source = @"
        public class TestClass
        {
            public int? NullableInt { get; set; }
            public DateTime? NullableDate { get; set; }
        }";

        var schema = GenerateSchemaFromSource(source, "TestClass");

        Assert.Equal("integer", schema.Properties["NullableInt"].Type);
        Assert.Equal("string", schema.Properties["NullableDate"].Type);
        Assert.Equal("date-time", schema.Properties["NullableDate"].Format);
    }

    [Fact]
    public void CreateSchema_WithNestedObjects_GeneratesNestedSchema()
    {
        var source = @"
        public class Address
        {
            public string Street { get; set; }
            public string City { get; set; }
        }
        public class Person
        {
            public string Name { get; set; }
            public Address Address { get; set; }
        }";

        var schema = GenerateSchemaFromSource(source, "Person");

        Assert.Equal("object", schema.Properties["Address"].Type);
        Assert.NotNull(schema.Properties["Address"].Properties);
        Assert.Equal(2, schema.Properties["Address"].Properties.Count);
    }

    [Fact]
    public void CreateSchema_WithExamples_GeneratesExamples()
    {
        var attributeSource = @"
        using System;
        using Oproto.OpenApi;
";

        var source = @"
        public class TestClass
        {
            [OpenApiSchema(Example = ""John Doe"")]
            public string Name { get; set; }
            
            [OpenApiSchema(Example = ""42"")]
            public int Age { get; set; }
        }";

        var fullSource = attributeSource + source;
        var schema = GenerateSchemaFromSource(fullSource, "TestClass");

        Assert.NotNull(schema.Properties["Name"].Example);
        Assert.Equal("John Doe", ((OpenApiString)schema.Properties["Name"].Example).Value);
        Assert.NotNull(schema.Properties["Age"].Example);
    }

    [Fact]
    public void GenerateOpenApiDocument_WithValidLambdaClass_GeneratesValidSpec()
    {
        // Arrange
        var mockTypeSymbol = Substitute.For<ITypeSymbol>();
        mockTypeSymbol.Name.Returns("string");
        mockTypeSymbol.ToDisplayString().Returns("System.String");

        var classInfo = new LambdaClassInfo
        {
            ServiceName = "TestService",
            Endpoints = new List<EndpointInfo>
            {
                new()
                {
                    HttpMethod = "GET",
                    Route = "/test",
                    MethodName = "TestMethod",
                    Parameters = new List<ParameterInfo>
                    {
                        new()
                        {
                            Name = "id",
                            TypeSymbol = mockTypeSymbol,
                            Source = ParameterSource.Path,
                            IsRequired = true
                        }
                    },
                    SecuritySchemes = new List<string>(), // Add empty security schemes
                    RequiresAuthorization = false // Explicitly set to false
                }
            }
        };

        var generator = new OpenApiSpecGenerator();

        // Act
        var document = generator.GenerateOpenApiDocument(classInfo);
        using var writer = new StringWriter();
        document.SerializeAsV3(new OpenApiJsonWriter(writer));
        var json = writer.ToString();

        // Assert
        Assert.NotNull(document);
        Assert.Equal("TestService", document.Info.Title);
        Assert.Equal("1.0", document.Info.Version);
        Assert.Contains("/test", document.Paths.Keys);

        var path = document.Paths["/test"];
        Assert.NotNull(path.Operations[OperationType.Get]);
    }

    private ITypeSymbol CreateMockTypeSymbol(string name, string fullName)
    {
        var mockType = Substitute.For<ITypeSymbol>();
        mockType.Name.Returns(name);
        mockType.ToDisplayString().Returns(fullName);
        mockType.SpecialType.Returns(name.ToLower() == "string"
            ? SpecialType.System_String
            : SpecialType.None);
        mockType.TypeKind.Returns(TypeKind.Class);

        return mockType;
    }

    [Fact]
    public void GenerateOpenApiDocument_WithMultipleHttpMethods_GeneratesCorrectPaths()
    {
        var mockStringType = CreateMockTypeSymbol("string", "System.String");

        var classInfo = new LambdaClassInfo
        {
            ServiceName = "TestService",
            Endpoints = new List<EndpointInfo>
            {
                new()
                {
                    HttpMethod = "GET",
                    Route = "/items/{id}",
                    MethodName = "GetItem",
                    Parameters = new List<ParameterInfo>
                    {
                        new()
                        {
                            Name = "id", TypeSymbol = mockStringType, Source = ParameterSource.Path, IsRequired = true
                        }
                    },
                    RequiresApiKey = false
                },
                new()
                {
                    HttpMethod = "POST",
                    Route = "/items",
                    MethodName = "CreateItem",
                    Parameters = new List<ParameterInfo>(),
                    RequiresApiKey = false
                }
            }
        };

        var generator = new OpenApiSpecGenerator();
        var document = generator.GenerateOpenApiDocument(classInfo);

        Assert.Contains("/items/{id}", document.Paths.Keys);
        Assert.Contains("/items", document.Paths.Keys);
        Assert.NotNull(document.Paths["/items/{id}"].Operations[OperationType.Get]);
        Assert.NotNull(document.Paths["/items"].Operations[OperationType.Post]);
    }

    [Fact]
    public void GenerateOpenApiDocument_WithDifferentParameterSources_GeneratesCorrectParameters()
    {
        var mockStringType = CreateMockTypeSymbol("string", "System.String");
        var mockIntType = CreateMockTypeSymbol("int", "System.Int32");

        var classInfo = new LambdaClassInfo
        {
            ServiceName = "TestService",
            Endpoints = new List<EndpointInfo>
            {
                new()
                {
                    HttpMethod = "GET",
                    Route = "/search/{category}",
                    MethodName = "SearchItems",
                    Parameters = new List<ParameterInfo>
                    {
                        new()
                        {
                            Name = "category", TypeSymbol = mockStringType, Source = ParameterSource.Path,
                            IsRequired = true
                        },
                        new()
                        {
                            Name = "query", TypeSymbol = mockStringType, Source = ParameterSource.Query,
                            IsRequired = false
                        },
                        new()
                        {
                            Name = "page", TypeSymbol = mockIntType, Source = ParameterSource.Query, IsRequired = false
                        }
                    },
                    RequiresApiKey = false
                }
            }
        };

        var generator = new OpenApiSpecGenerator();
        var document = generator.GenerateOpenApiDocument(classInfo);

        var operation = document.Paths["/search/{category}"].Operations[OperationType.Get];
        Assert.Equal(3, operation.Parameters.Count);

        var routeParam = operation.Parameters.First(p => p.Name == "category");
        Assert.Equal(ParameterLocation.Path, routeParam.In);
        Assert.True(routeParam.Required);

        var queryParam = operation.Parameters.First(p => p.Name == "query");
        Assert.Equal(ParameterLocation.Query, queryParam.In);
        Assert.False(queryParam.Required);
    }

    [Fact]
    public void GenerateOpenApiDocument_WithSecurity_GeneratesSecuritySchemes()
    {
        var endpoint = new EndpointInfo
        {
            HttpMethod = "GET",
            Route = "/secure",
            MethodName = "SecureEndpoint",
            Parameters = new List<ParameterInfo>(),
            RequiresApiKey = true
        };

        var classInfo = new LambdaClassInfo
        {
            ServiceName = "TestService",
            Endpoints = new List<EndpointInfo> { endpoint }
        };

        var generator = new OpenApiSpecGenerator();
        var document = generator.GenerateOpenApiDocument(classInfo);

        // Get the operation
        var operation = document.Paths["/secure"].Operations[OperationType.Get];

        // Debug the security requirements in detail
        Console.WriteLine("\nDetailed Security Requirements:");
        foreach (var securityRequirement in operation.Security)
        foreach (var (scheme, scopes) in securityRequirement)
        {
            Console.WriteLine($"Scheme Reference: {scheme.Reference?.Id ?? "null"}");
            if (scheme.Reference != null)
            {
                var referencedScheme2 = document.Components.SecuritySchemes[scheme.Reference.Id];
                Console.WriteLine($"Referenced Scheme Type: {referencedScheme2.Type}");
                Console.WriteLine($"Referenced Scheme In: {referencedScheme2.In}");
                Console.WriteLine($"Referenced Scheme Name: {referencedScheme2.Name}");
            }
        }

        // Verify the security scheme exists
        Assert.NotNull(document.Components.SecuritySchemes);
        Assert.True(document.Components.SecuritySchemes.ContainsKey("apiKey"));

        // Get the first security requirement
        Assert.NotNull(operation.Security);
        Assert.NotEmpty(operation.Security);

        var firstSecurityRequirement = operation.Security.First();
        Assert.NotEmpty(firstSecurityRequirement);

        // Get the first scheme from the requirement
        var securityScheme = firstSecurityRequirement.Keys.First();
        Assert.NotNull(securityScheme.Reference);
        Assert.Equal("apiKey", securityScheme.Reference.Id);

        // Verify the referenced scheme is correct
        var referencedScheme = document.Components.SecuritySchemes["apiKey"];
        Assert.Equal(SecuritySchemeType.ApiKey, referencedScheme.Type);
        Assert.Equal(ParameterLocation.Header, referencedScheme.In);
        Assert.Equal("x-api-key", referencedScheme.Name);
    }


    [Theory]
    [InlineData("/items/{id}", "id")] // Route parameter matches parameter name
    [InlineData("/items/{productId}", "productId")] // Route parameter name is what matters
    [InlineData("/{category}/{id}", "category,id")] // Multiple parameters
    public void GenerateOpenApiDocument_WithRouteParameters_GeneratesCorrectParameterDefinitions(string route,
        string expectedParamNames)
    {
        var mockStringType = CreateMockTypeSymbol("string", "System.String");
        var parameters = expectedParamNames.Split(',')
            .Select(name => new ParameterInfo
            {
                Name = name,
                TypeSymbol = mockStringType,
                Source = ParameterSource.Path,
                IsRequired = true
            })
            .ToList();

        var classInfo = new LambdaClassInfo
        {
            ServiceName = "TestService",
            Endpoints = new List<EndpointInfo>
            {
                new()
                {
                    HttpMethod = "GET",
                    Route = route,
                    MethodName = "TestMethod",
                    Parameters = parameters,
                    RequiresApiKey = false
                }
            }
        };

        var generator = new OpenApiSpecGenerator();
        var document = generator.GenerateOpenApiDocument(classInfo);

        // Verify the path exists
        Assert.True(document.Paths.ContainsKey(route));
        var pathItem = document.Paths[route];

        // Verify the operation exists
        Assert.NotNull(pathItem.Operations[OperationType.Get]);
        var operation = pathItem.Operations[OperationType.Get];

        // Verify parameters
        Assert.NotNull(operation.Parameters);
        var expectedParamList = expectedParamNames.Split(',');
        Assert.Equal(expectedParamList.Length, operation.Parameters.Count);

        foreach (var expectedParam in expectedParamList)
        {
            var param = operation.Parameters.FirstOrDefault(p => p.Name == expectedParam);
            Assert.NotNull(param);
            Assert.Equal(ParameterLocation.Path, param.In);
            Assert.True(param.Required);
            Assert.Equal("string", param.Schema.Type);
        }
    }

    [Fact]
    public void IsCollectionType_HandlesVariousCollectionTypes()
    {
        var source = @"
    public class CollectionTestClass
    {
        public List<string> StringList { get; set; }
        public string[] StringArray { get; set; }
        public IEnumerable<int> NumberEnumerable { get; set; }
        public IReadOnlyList<bool> ReadOnlyBoolList { get; set; }
        public IList<DateTime> DateList { get; set; }
    }";

        var schema = GenerateSchemaFromSource(source, "CollectionTestClass");

        Assert.Equal("array", schema.Properties["StringList"].Type);
        Assert.Equal("string", schema.Properties["StringList"].Items.Type);

        Assert.Equal("array", schema.Properties["StringArray"].Type);
        Assert.Equal("string", schema.Properties["StringArray"].Items.Type);

        Assert.Equal("array", schema.Properties["NumberEnumerable"].Type);
        Assert.Equal("integer", schema.Properties["NumberEnumerable"].Items.Type);

        Assert.Equal("array", schema.Properties["ReadOnlyBoolList"].Type);
        Assert.Equal("boolean", schema.Properties["ReadOnlyBoolList"].Items.Type);

        Assert.Equal("array", schema.Properties["DateList"].Type);
        Assert.Equal("string", schema.Properties["DateList"].Items.Type);
        Assert.Equal("date-time", schema.Properties["DateList"].Items.Format);
    }

    [Fact]
    public void GenerateSchema_WithNestedCollections_GeneratesCorrectSchema()
    {
        var source = @"
    public class NestedCollectionClass
    {
        public List<List<string>> NestedLists { get; set; }
        public Dictionary<string, List<int>> DictionaryOfLists { get; set; }
    }";

        var schema = GenerateSchemaFromSource(source, "NestedCollectionClass");

        Assert.Equal("array", schema.Properties["NestedLists"].Type);
        Assert.Equal("array", schema.Properties["NestedLists"].Items.Type);
        Assert.Equal("string", schema.Properties["NestedLists"].Items.Items.Type);
    }

    [Fact]
    public void GenerateSchema_WithCustomCollectionTypes_GeneratesCorrectSchema()
    {
        var source = @"
    using System.Collections;
    using System.Collections.Generic;

    public class CustomCollection<T> : IEnumerable<T>
    {
        public IEnumerator<T> GetEnumerator()
        {
            yield break;  // Minimal implementation
        }

        IEnumerator IEnumerable.GetEnumerator()
        {
            return GetEnumerator();
        }
    }
    
    public class CustomCollectionTest
    {
        public CustomCollection<string> CustomStrings { get; set; }
    }";

        var schema = GenerateSchemaFromSource(source, "CustomCollectionTest");

        Assert.Equal("array", schema.Properties["CustomStrings"].Type);
        Assert.Equal("string", schema.Properties["CustomStrings"].Items.Type);
    }

    [Fact]
    public void GenerateSchema_WithExamples_GeneratesCorrectSchema()
    {
        var source = @"
    using System;
    using Oproto.OpenApi;

    public class ExampleClass
    {
        [OpenApiSchema(Example = ""42"")]
        public int Number { get; set; }

        [OpenApiSchema(Example = ""[1,2,3]"")]
        public List<int> Numbers { get; set; }

        [OpenApiSchema(Example = ""{\""key\"":\""value\""}"")]
        public Dictionary<string, string> Dictionary { get; set; }

        [OpenApiSchema(Example = ""true"")]
        public bool Flag { get; set; }

        [OpenApiSchema(Example = ""simple string"")]
        public string Text { get; set; }
    }";

        var schema = GenerateSchemaFromSource(source, "ExampleClass");

        Assert.NotNull(schema);

        // Check number example
        Assert.NotNull(schema.Properties["Number"].Example);
        Assert.IsType<OpenApiInteger>(schema.Properties["Number"].Example);

        // Check array example
        Assert.NotNull(schema.Properties["Numbers"].Example);
        Assert.IsType<OpenApiArray>(schema.Properties["Numbers"].Example);

        // Check dictionary example
        Assert.NotNull(schema.Properties["Dictionary"].Example);
        Assert.IsType<OpenApiObject>(schema.Properties["Dictionary"].Example);

        // Check boolean example
        Assert.NotNull(schema.Properties["Flag"].Example);
        Assert.IsType<OpenApiBoolean>(schema.Properties["Flag"].Example);

        // Check string example
        Assert.NotNull(schema.Properties["Text"].Example);
        Assert.IsType<OpenApiString>(schema.Properties["Text"].Example);
    }


    [Fact]
    public void GenerateSchema_WithInheritedAttributes_GeneratesCorrectSchema()
    {
        var source = @"
    using System;
    using Oproto.OpenApi;

    [GenerateOpenApiSpec(""BaseService"", ""1.0"")]
    public class BaseClass
    {
        [OpenApiSchema(Description = ""Base Property"")]
        public virtual string BaseProp { get; set; }
    }

    [GenerateOpenApiSpec(""DerivedService"", ""2.0"")]
    public class DerivedClass : BaseClass
    {
        [OpenApiSchema(Description = ""Derived Property"")]
        public string DerivedProp { get; set; }

        public override string BaseProp { get; set; }
    }";

        var baseSchema = GenerateSchemaFromSource(source, "BaseClass");
        var derivedSchema = GenerateSchemaFromSource(source, "DerivedClass");

        Assert.Equal("Base Property", baseSchema.Properties["BaseProp"].Description);
        Assert.Equal("Base Property", derivedSchema.Properties["BaseProp"].Description);
        Assert.Equal("Derived Property", derivedSchema.Properties["DerivedProp"].Description);
    }


    [Fact]
    public void GenerateSchema_WithInvalidExamples_HandlesErrorsGracefully()
    {
        var source = @"
    public class InvalidExampleClass
    {
        [OpenApiSchema(Example = ""invalid json"")]
        public Dictionary<string, string> InvalidDictionary { get; set; }

        [OpenApiSchema(Example = ""not a date"")]
        public DateTime InvalidDate { get; set; }
    }";

        var schema = GenerateSchemaFromSource(source, "InvalidExampleClass");

        Assert.NotNull(schema);
        // Verify that invalid examples don't crash the generator
        Assert.Null(schema.Properties["InvalidDictionary"].Example);
        Assert.Null(schema.Properties["InvalidDate"].Example);
    }
}