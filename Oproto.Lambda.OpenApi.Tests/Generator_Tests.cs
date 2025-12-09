using System.Diagnostics;
using System.Text.Json;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Oproto.Lambda.OpenApi.SourceGenerator;

namespace Oproto.Lambda.OpenApi.Tests;

public class GeneratorTests
{
    private string ExtractOpenApiJson(Compilation outputCompilation)
    {
        // Get the generated file containing the assembly attribute
        var generatedFile = outputCompilation.SyntaxTrees
            .First(x => x.FilePath.EndsWith("OpenApiOutput.g.cs"));

        var generatedContent = generatedFile.GetRoot().GetText().ToString();

        // Extract the JSON content from the assembly attribute
        var attributeStart = generatedContent.IndexOf("[assembly: OpenApiOutput(@\"") + 26;
        var attributeEnd = generatedContent.LastIndexOf("\", \"openapi.json\")]");

        var rawJson = generatedContent[attributeStart..attributeEnd];

        // Clean the JSON string using the proven cleaning approach
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


    [Fact]
    public void Generator_ProcessesValidLambdaFunction_GeneratesOpenApiSpec()
    {
        // Arrange
        var source = @"
        using Amazon.Lambda.Annotations;
        using Amazon.Lambda.Annotations.APIGateway;
        using System.Net.Http;
        
        public class TestFunctions 
        {
            [LambdaFunction]
            [HttpApi(LambdaHttpMethod.Get, ""/items/{id}"")]
            public string GetItem(string id) => """";
        }";

        // Create compilation with Lambda references
        var compilation = CompilerHelper.CreateCompilation(source);

        // Verify compilation is valid
        var compilationDiagnostics = compilation.GetDiagnostics();
        Assert.Empty(compilationDiagnostics.Where(d => d.Severity == DiagnosticSeverity.Error));

        var generator = new OpenApiSpecGenerator();

        // Act
        var driver = CSharpGeneratorDriver.Create(generator);
        driver.RunGeneratorsAndUpdateCompilation(compilation,
            out var outputCompilation,
            out var diagnostics);

        // Assert
        Assert.Empty(diagnostics.Where(d => d.Severity == DiagnosticSeverity.Error));

        // Extract the JSON content using the helper method
        var jsonContent = ExtractOpenApiJson(outputCompilation);

        Assert.Contains("\"/items/{id}\"", jsonContent);
    }

    [Fact]
    public void Generator_ProcessesRestApiFunction_GeneratesCorrectSchema()
    {
        // Arrange
        var source = @"
        using Amazon.Lambda.Annotations;
        using Amazon.Lambda.Annotations.APIGateway;
        
        public class TestFunctions 
        {
            [LambdaFunction]
            [RestApi(LambdaHttpMethod.Post, ""/ledgers"")]
            public Item CreateLedger(Item item) => null;
        }

        public class Item 
        {
            public string Id { get; set; }
            public string Name { get; set; }
        }";

        var compilation = CompilerHelper.CreateCompilation(source);

        // Verify compilation is valid
        var compilationDiagnostics = compilation.GetDiagnostics();
        Assert.Empty(compilationDiagnostics.Where(d => d.Severity == DiagnosticSeverity.Error));

        var generator = new OpenApiSpecGenerator();

        // Act
        var driver = CSharpGeneratorDriver.Create(generator);
        driver.RunGeneratorsAndUpdateCompilation(compilation,
            out var outputCompilation,
            out var diagnostics);

        // Assert with more detail
        Assert.Empty(diagnostics.Where(d => d.Severity == DiagnosticSeverity.Error));

        // Extract the JSON content using the helper method
        var jsonContent = ExtractOpenApiJson(outputCompilation);

        // Verify the OpenAPI spec content
        Assert.Contains("\"/ledgers\"", jsonContent);
        Assert.Contains("\"Item\"", jsonContent);
        Assert.Contains("\"post\"", jsonContent.ToLower());
    }

    [Fact]
    public void Generator_ProcessesMultipleEndpointTypes_GeneratesCorrectPaths()
    {
        // Arrange
        var source = @"
            using Amazon.Lambda.Annotations;
            using Amazon.Lambda.Annotations.APIGateway;
            
            public class TestFunctions 
            {
                [LambdaFunction]
                [HttpApi(LambdaHttpMethod.Get, ""/items/{id}"")]
                public string GetItemHttp(string id) => """";

                [LambdaFunction]
                [RestApi(LambdaHttpMethod.Get, ""/api/items/{id}"")]
                public string GetItemRest(string id) => """";
            }";

        var compilation = CompilerHelper.CreateCompilation(source);
        // Verify compilation
        var compilationDiagnostics = compilation.GetDiagnostics();
        Assert.Empty(compilationDiagnostics.Where(d => d.Severity == DiagnosticSeverity.Error));

        var generator = new OpenApiSpecGenerator();

        // Act
        var driver = CSharpGeneratorDriver.Create(generator);
        driver.RunGeneratorsAndUpdateCompilation(compilation,
            out var outputCompilation,
            out var diagnostics);

        // Debug - List all generated files
        var allFiles = outputCompilation.SyntaxTrees
            .Select(x => x.FilePath)
            .ToList();

        foreach (var file in allFiles) Debug.WriteLine($"Generated file: {file}");

        // Look for any .g.cs files
        var generatedCSharpFiles = outputCompilation.SyntaxTrees
            .Where(x => x.FilePath.EndsWith(".g.cs"))
            .ToList();

        Assert.NotEmpty(generatedCSharpFiles);

        // Extract the JSON content using the helper method
        var jsonContent = ExtractOpenApiJson(outputCompilation);

        // Verify the JSON content contains our paths
        Assert.Contains("\"/items/{id}\"", jsonContent);
        Assert.Contains("\"/api/items/{id}\"", jsonContent);
    }

    [Fact]
    public void Generator_IgnoresNonHttpLambdaFunctions()
    {
        // Arrange
        var source = @"
        using Amazon.Lambda.Annotations;
        using Amazon.Lambda.Annotations.APIGateway;
        
        public class TestFunctions 
        {
            [LambdaFunction]
            public string ProcessQueueMessage(string message) => """";

            [LambdaFunction]
            [HttpApi(LambdaHttpMethod.Get, ""/items/{id}"")]
            public string GetItem(string id) => """";
        }";

        var compilation = CompilerHelper.CreateCompilation(source);
        var generator = new OpenApiSpecGenerator();

        // Act
        var driver = CSharpGeneratorDriver.Create(generator);
        var result = driver.RunGeneratorsAndUpdateCompilation(compilation,
            out var outputCompilation,
            out var diagnostics);

        // Debug - check what files were generated
        var runResult = result.GetRunResult();
        foreach (var generatedSource in runResult.GeneratedTrees)
        {
            Debug.WriteLine($"Generated file: {generatedSource.FilePath}");
            Debug.WriteLine(generatedSource.GetText().ToString());
        }

        // Verify no errors occurred
        Assert.Empty(diagnostics.Where(d => d.Severity == DiagnosticSeverity.Error));

        // Verify at least one source file was generated
        Assert.NotEmpty(runResult.GeneratedTrees);

        // Now try to find our specific file
        var openApiFile = runResult.GeneratedTrees
            .FirstOrDefault(x => x.FilePath.EndsWith("OpenApiOutput.g.cs"));

        Assert.NotNull(openApiFile); // Verify the file exists

        var fileContent = openApiFile.GetText().ToString();
        Assert.Contains("[assembly: OpenApiOutput(", fileContent);
    }


    [Fact]
    public void Generator_ProcessesValidClass_GeneratesFiles()
    {
        // Arrange
        var source = @"
    using Amazon.Lambda.Annotations;
    using Amazon.Lambda.Annotations.APIGateway;
    using Amazon.Lambda.Core;
    using System.Threading.Tasks;
    
    namespace Test 
    {
        public class TestFunctions 
        {
            [LambdaFunction]
            [HttpApi(LambdaHttpMethod.Get, ""/items/{id}"")]
            public async Task<ItemResponse> GetItemHttp(
                [FromRoute] string id,
                [FromServices] ILambdaContext context,
                [FromQuery] string filter = null)
            {
                return new ItemResponse { Id = id };
            }
        }

        public class ItemResponse
        {
            public string Id { get; set; }
        }
    }";

        var compilation = CompilerHelper.CreateCompilation(source);
        var generator = new OpenApiSpecGenerator();

        // Act
        var driver = CSharpGeneratorDriver.Create(generator);
        driver.RunGeneratorsAndUpdateCompilation(compilation,
            out var outputCompilation,
            out var diagnostics);

        // Assert no generator diagnostics
        Assert.Empty(diagnostics.Where(d => d.Severity == DiagnosticSeverity.Error));

        // Extract and validate the OpenAPI JSON
        var jsonContent = ExtractOpenApiJson(outputCompilation);
        using var doc = JsonDocument.Parse(jsonContent);
        var root = doc.RootElement;

        // Verify basic OpenAPI structure
        Assert.Equal("3.0.1", root.GetProperty("openapi").GetString());

        // Verify paths
        var paths = root.GetProperty("paths");
        Assert.True(paths.TryGetProperty("/items/{id}", out var pathItem));

        // Verify the GET operation
        Assert.True(pathItem.TryGetProperty("get", out var operation));

        // Verify parameters
        var parameters = operation.GetProperty("parameters");
        var parameterArray = parameters.EnumerateArray();

        // Helper function to find parameter by name
        bool HasParameter(JsonElement.ArrayEnumerator array, string name, string location)
        {
            return array.Any(p =>
                p.GetProperty("name").GetString() == name &&
                p.GetProperty("in").GetString() == location);
        }

        // Verify id parameter - should be "path" not "header"
        Assert.True(HasParameter(parameterArray, "id", "path"));

        // Verify filter parameter is in query
        Assert.True(HasParameter(parameterArray, "filter", "query"));

        // Context parameter should be excluded since it's [FromServices]
        Assert.False(HasParameter(parameterArray, "context", "header"));
    }


    [Fact]
    public void Generator_ProcessesNestedObjects_GeneratesCorrectSchema()
    {
        // Arrange
        var source = @"
    using System;
    using System.Collections.Generic;
    using Amazon.Lambda.Annotations;
    using Amazon.Lambda.Annotations.APIGateway;
    
    public class TestFunctions 
    {
        [LambdaFunction]
        [RestApi(LambdaHttpMethod.Post, ""/orders"")]
        public Order CreateOrder(Order order) => null;
    }

    public class Order 
    {
        public string Id { get; set; }
        public Customer Customer { get; set; }
        public List<OrderItem> Items { get; set; }
    }

    public class Customer
    {
        public string Id { get; set; }
        public string Name { get; set; }
        public Address Address { get; set; }
    }

    public class Address
    {
        public string Street { get; set; }
        public string City { get; set; }
        public string Country { get; set; }
    }

    public class OrderItem
    {
        public string ProductId { get; set; }
        public int Quantity { get; set; }
        public decimal Price { get; set; }
    }";


        var compilation = CompilerHelper.CreateCompilation(source);

        // Verify compilation is valid
        var compilationDiagnostics = compilation.GetDiagnostics();
        Assert.Empty(compilationDiagnostics.Where(d => d.Severity == DiagnosticSeverity.Error));

        var generator = new OpenApiSpecGenerator();

        // Act
        var driver = CSharpGeneratorDriver.Create(generator);
        driver.RunGeneratorsAndUpdateCompilation(compilation,
            out var outputCompilation,
            out var diagnostics);

        // Assert no generator diagnostics
        Assert.Empty(diagnostics.Where(d => d.Severity == DiagnosticSeverity.Error));

        // Extract the JSON content using the helper method
        var jsonContent = ExtractOpenApiJson(outputCompilation);

        // Assert
        Assert.Contains("\"Order\"", jsonContent);
        Assert.Contains("\"Customer\"", jsonContent);
        Assert.Contains("\"Address\"", jsonContent);
        Assert.Contains("\"OrderItem\"", jsonContent);
        Assert.Contains("\"array\"", jsonContent);
        Assert.Contains("\"$ref\"", jsonContent);
    }

    [Fact]
    public void Generator_ProcessesEnumTypes_GeneratesCorrectSchema()
    {
        // Arrange
        var source = @"
    using Amazon.Lambda.Annotations;
    using Amazon.Lambda.Annotations.APIGateway;
    
    public class TestFunctions 
    {
        [LambdaFunction]
        [RestApi(LambdaHttpMethod.Post, ""/orders"")]
        public OrderStatus UpdateOrderStatus(OrderUpdate update) => OrderStatus.Completed;
    }

    public class OrderUpdate
    {
        public string OrderId { get; set; }
        public OrderStatus NewStatus { get; set; }
    }

    public enum OrderStatus
    {
        Pending,
        Processing,
        Completed,
        Cancelled
    }";

        var compilation = CompilerHelper.CreateCompilation(source);

        // Verify compilation is valid
        var compilationDiagnostics = compilation.GetDiagnostics();
        Assert.Empty(compilationDiagnostics.Where(d => d.Severity == DiagnosticSeverity.Error));

        var generator = new OpenApiSpecGenerator();

        // Act
        var driver = CSharpGeneratorDriver.Create(generator);
        driver.RunGeneratorsAndUpdateCompilation(compilation,
            out var outputCompilation,
            out var diagnostics);

        // Assert no generator diagnostics
        Assert.Empty(diagnostics.Where(d => d.Severity == DiagnosticSeverity.Error));

        // Extract the JSON content using the helper method
        var jsonContent = ExtractOpenApiJson(outputCompilation);

        // Assert
        Assert.Contains("\"enum\"", jsonContent);
        Assert.Contains("\"Pending\"", jsonContent);
        Assert.Contains("\"Processing\"", jsonContent);
        Assert.Contains("\"Completed\"", jsonContent);
        Assert.Contains("\"Cancelled\"", jsonContent);
    }

    [Fact]
    public void Generator_ProcessesCollectionTypes_GeneratesCorrectSchema()
    {
        // Arrange
        var source = @"
    using Amazon.Lambda.Annotations;
    using Amazon.Lambda.Annotations.APIGateway;
    using System.Collections.Generic;
    
    public class TestFunctions 
    {
        [LambdaFunction]
        [RestApi(LambdaHttpMethod.Get, ""/products"")]
        public List<Product> GetProducts() => null;

        [LambdaFunction]
        [RestApi(LambdaHttpMethod.Post, ""/products/batch"")]
        public IEnumerable<Product> CreateProducts(ProductBatchInput input) => null;
    }

    public class Product
    {
        public string Id { get; set; }
        public string Name { get; set; }
        public string[] Tags { get; set; }
    }

    public class ProductBatchInput
    {
        public List<ProductInput> Products { get; set; }
    }

    public class ProductInput
    {
        public string Name { get; set; }
        public string[] Tags { get; set; }
    }";

        var compilation = CompilerHelper.CreateCompilation(source);

        // Verify compilation is valid
        var compilationDiagnostics = compilation.GetDiagnostics();
        Assert.Empty(compilationDiagnostics.Where(d => d.Severity == DiagnosticSeverity.Error));

        var generator = new OpenApiSpecGenerator();

        // Act
        var driver = CSharpGeneratorDriver.Create(generator);
        driver.RunGeneratorsAndUpdateCompilation(compilation,
            out var outputCompilation,
            out var diagnostics);

        // Assert no generator diagnostics
        Assert.Empty(diagnostics.Where(d => d.Severity == DiagnosticSeverity.Error));

        // Extract the JSON content using the helper method
        var jsonContent = ExtractOpenApiJson(outputCompilation);

        // Assert
        Assert.Contains("\"array\"", jsonContent);
        Assert.Contains("\"Product\"", jsonContent);
        Assert.Contains("\"ProductInput\"", jsonContent);
        Assert.Contains("\"ProductBatchInput\"", jsonContent);
        Assert.Contains("\"Tags\"", jsonContent);
    }


    /// <summary>
    ///     Tests the schema generation for nullable reference types.
    /// </summary>
    /// <remarks>
    ///     For nullable reference types, the OpenAPI schema uses the following structure:
    ///     {
    ///     "allOf": [
    ///     {
    ///     "$ref": "#/components/schemas/ReferencedType"
    ///     }
    ///     ],
    ///     "nullable": true
    ///     }
    ///     This approach was chosen over alternatives (like oneOf with null) because:
    ///     1. It maintains better compatibility with OpenAPI 3.0
    ///     2. It clearly separates the reference from its nullability
    ///     3. Tools and code generators can more easily understand this pattern
    ///     The test validates both the reference structure using allOf and
    ///     ensures the nullable property is correctly set at the root level.
    /// </remarks>
    [Fact]
    public void Generator_ProcessesNullableTypes_GeneratesCorrectSchema()
    {
        // Arrange
        var source = @"
    using System;
    using System.Collections.Generic;
    using Amazon.Lambda.Annotations;
    using Amazon.Lambda.Annotations.APIGateway;
    
    public class TestFunctions 
    {
        [LambdaFunction]
        [RestApi(LambdaHttpMethod.Post, ""/users"")]
        public User CreateUser(User user) => null;
    }

    public class User
    {
        public string Id { get; set; }
        public string Name { get; set; }
        public int? Age { get; set; }
        public DateTime? LastLoginDate { get; set; }
        public UserPreferences? Preferences { get; set; }
    }

    public class UserPreferences
    {
        public bool ReceiveEmails { get; set; }
        public string? Theme { get; set; }
    }";


        var compilation = CompilerHelper.CreateCompilation(source);

        // Verify compilation is valid
        var compilationDiagnostics = compilation.GetDiagnostics();
        Assert.Empty(compilationDiagnostics.Where(d => d.Severity == DiagnosticSeverity.Error));

        var generator = new OpenApiSpecGenerator();

        // Act
        var driver = CSharpGeneratorDriver.Create(generator);
        driver.RunGeneratorsAndUpdateCompilation(compilation,
            out var outputCompilation,
            out var diagnostics);

        // Assert no generator diagnostics
        Assert.Empty(diagnostics.Where(d => d.Severity == DiagnosticSeverity.Error));

        // Extract the JSON content using the helper method
        var jsonContent = ExtractOpenApiJson(outputCompilation);
        var root = JsonTestHelper.ParseAndValidateOpenApiSchema(jsonContent);

        try
        {
            // Basic structure checks
            Assert.True(root.TryGetProperty("openapi", out _));
            Assert.True(root.TryGetProperty("paths", out _));
            Assert.True(root.TryGetProperty("components", out var components));
            Assert.True(components.TryGetProperty("schemas", out var schemas));
            Assert.True(schemas.TryGetProperty("User", out var userSchema));

            // Debug the User schema
            Debug.WriteLine("\nUser schema:");
            Debug.WriteLine(userSchema.GetRawText());

            var properties = userSchema.GetProperty("properties");
            Assert.True(properties.TryGetProperty("Age", out var ageProperty));
            Assert.True(ageProperty.GetProperty("nullable").GetBoolean());
            Assert.True(properties.TryGetProperty("LastLoginDate", out var lastLoginDateProperty));
            Assert.True(lastLoginDateProperty.GetProperty("nullable").GetBoolean());
            Assert.True(properties.TryGetProperty("Preferences", out var preferencesProperty));

            // For referenced types, nullable should be at the same level as $ref
            Assert.True(preferencesProperty.TryGetProperty("allOf", out var allOf));
            var allOfArray = allOf.EnumerateArray().ToArray();
            Assert.Single(allOfArray);

            // First element should be the reference
            Assert.True(allOfArray[0].TryGetProperty("$ref", out var reference));
            Assert.Equal("#/components/schemas/UserPreferences", reference.GetString());

            // Check nullable property
            Assert.True(preferencesProperty.TryGetProperty("nullable", out var nullable));
            Assert.True(nullable.GetBoolean());
        }
        catch (JsonException ex)
        {
            Debug.WriteLine($"JSON Parse Error: {ex.Message}");
            Debug.WriteLine(
                $"Error Position: Line {ex.LineNumber}, Position {ex.BytePositionInLine}");
            Debug.WriteLine("JSON Content around error:");
            var errorPosition = (int)ex.BytePositionInLine;
            var start = Math.Max(0, errorPosition - 20);
            var length = Math.Min(40, jsonContent.Length - start);
            Debug.WriteLine(jsonContent.Substring(start, length));
            throw;
        }
    }

    [Fact]
    public void Generator_ProcessesSchemaAttributes_GeneratesCorrectSchema()
    {
        // Arrange
        var source = @"
    using Amazon.Lambda.Annotations;
    using Amazon.Lambda.Annotations.APIGateway;
    using System.ComponentModel.DataAnnotations;
    
    public class TestFunctions 
    {
        [LambdaFunction]
        [RestApi(LambdaHttpMethod.Post, ""/products"")]
        public Product CreateProduct(ProductCreateRequest request) => null;
    }

    public class ProductCreateRequest
    {
        [Required]
        [StringLength(100, MinimumLength = 3)]
        public string Name { get; set; }

        [Range(0.01, 10000.00)]
        public decimal Price { get; set; }

        [Required]
        [MinLength(1)]
        [MaxLength(10)]
        public string[] Categories { get; set; }

        [RegularExpression(@""^[A-Z]{2}\d{6}$"")]
        public string SKU { get; set; }

        [Required]
        public ProductType Type { get; set; }
    }

    public class Product
    {
        public string Id { get; set; }
        public string Name { get; set; }
        public decimal Price { get; set; }
        public string[] Categories { get; set; }
        public string SKU { get; set; }
        public ProductType Type { get; set; }
    }

    public enum ProductType
    {
        Physical,
        Digital,
        Service
    }";

        var compilation = CompilerHelper.CreateCompilation(source);
        Assert.Empty(compilation.GetDiagnostics().Where(d => d.Severity == DiagnosticSeverity.Error));

        // Act
        var generator = new OpenApiSpecGenerator();
        var driver = CSharpGeneratorDriver.Create(generator);
        driver.RunGeneratorsAndUpdateCompilation(compilation, out var outputCompilation, out var diagnostics);

        // Assert
        Assert.Empty(diagnostics.Where(d => d.Severity == DiagnosticSeverity.Error));

        // Extract the JSON content using the helper method
        var jsonContent = ExtractOpenApiJson(outputCompilation);
        var root = JsonTestHelper.ParseAndValidateOpenApiSchema(jsonContent);

        // Verify ProductCreateRequest schema
        Assert.True(root.GetProperty("components")
            .GetProperty("schemas")
            .TryGetProperty("ProductCreateRequest", out var productCreateRequest));

        // Debug output
        Debug.WriteLine("\nProductCreateRequest schema:");
        Debug.WriteLine(JsonSerializer.Serialize(productCreateRequest,
            new JsonSerializerOptions { WriteIndented = true }));
    }

    [Fact]
    public void Generator_MergesMultipleClassSpecs()
    {
        // Arrange
        var source = @"
        using Amazon.Lambda.Annotations;
        using Amazon.Lambda.Annotations.APIGateway;
        using Amazon.Lambda.Core;
        using System.Threading.Tasks;
        
        namespace Test 
        {
            public class OrderFunctions 
            {
                [LambdaFunction]
                [HttpApi(LambdaHttpMethod.Get, ""/orders/{id}"")]
                public async Task<string> GetOrder(
                    [FromRoute] string id,
                    [FromServices] ILambdaContext context)
                {
                    return id;
                }

                [LambdaFunction]
                [HttpApi(LambdaHttpMethod.Post, ""/orders"")]
                public async Task<string> CreateOrder(
                    [FromBody] string order,
                    [FromServices] ILambdaContext context)
                {
                    return order;
                }
            }

            public class ProductFunctions 
            {
                [LambdaFunction]
                [HttpApi(LambdaHttpMethod.Get, ""/products/{id}"")]
                public async Task<string> GetProduct(
                    [FromRoute] string id,
                    [FromServices] ILambdaContext context)
                {
                    return id;
                }

                [LambdaFunction]
                public string ProcessProductQueue(string message) => """";
            }
        }";

        var compilation = CompilerHelper.CreateCompilation(source);
        var generator = new OpenApiSpecGenerator();

        // Act
        var driver = CSharpGeneratorDriver.Create(generator);
        var result = driver.RunGeneratorsAndUpdateCompilation(compilation,
            out var outputCompilation,
            out var diagnostics);

        // Assert
        Assert.Empty(diagnostics.Where(d => d.Severity == DiagnosticSeverity.Error));

        // Extract and validate the OpenAPI JSON
        var jsonContent = ExtractOpenApiJson(outputCompilation);
        using var doc = JsonDocument.Parse(jsonContent);
        var root = doc.RootElement;

        // Verify basic OpenAPI structure
        Assert.Equal("3.0.1", root.GetProperty("openapi").GetString());

        // Verify paths - should include endpoints from both classes
        var paths = root.GetProperty("paths");

        // Should have 3 paths total (2 from OrderFunctions, 1 from ProductFunctions)
        Assert.Equal(3, paths.EnumerateObject().Count());

        // Verify specific paths exist
        Assert.True(paths.TryGetProperty("/orders/{id}", out var orderPath));
        Assert.True(orderPath.TryGetProperty("get", out _));

        Assert.True(paths.TryGetProperty("/orders", out var ordersPath));
        Assert.True(ordersPath.TryGetProperty("post", out _));

        Assert.True(paths.TryGetProperty("/products/{id}", out var productPath));
        Assert.True(productPath.TryGetProperty("get", out _));

        // Verify non-HTTP endpoint is not included
        var allPaths = paths.EnumerateObject().Select(p => p.Name).ToList();
        Assert.DoesNotContain(allPaths, p => p.Contains("ProcessProductQueue"));
    }


    [Fact]
    public void Generator_HandlesParameterSources_GeneratesCorrectOpenApi()
    {
        // Arrange
        var source = @"
        using Amazon.Lambda.Annotations;
        using Amazon.Lambda.Annotations.APIGateway;
        using Amazon.Lambda.Core;
        using System.Threading.Tasks;
        
        namespace Test 
        {
            public class TestFunctions 
            {
                [LambdaFunction]
                [HttpApi(LambdaHttpMethod.Get, ""/items/{id}/details"")]
                public async Task<string> GetItemDetails(
                    [FromRoute] string id,
                    [FromQuery] string filter,
                    [FromHeader(Name = ""X-Api-Key"")] string apiKey,
                    [FromServices] ILambdaContext context)
                {
                    return id;
                }

                [LambdaFunction]
                [HttpApi(LambdaHttpMethod.Post, ""/items"")]
                public async Task<Item> CreateItem(
                    [FromBody] Item item,
                    [FromHeader(Name = ""Correlation-Id"")] string correlationId)
                {
                    return item;
                }
            }

            public class Item 
            {
                public string Id { get; set; }
                public string Name { get; set; }
            }
        }";

        var compilation = CompilerHelper.CreateCompilation(source);
        var generator = new OpenApiSpecGenerator();

        // Act
        var driver = CSharpGeneratorDriver.Create(generator);
        var result = driver.RunGeneratorsAndUpdateCompilation(compilation,
            out var outputCompilation,
            out var diagnostics);

        // Assert
        Assert.Empty(diagnostics.Where(d => d.Severity == DiagnosticSeverity.Error));

        var jsonContent = ExtractOpenApiJson(outputCompilation);
        using var doc = JsonDocument.Parse(jsonContent);
        var root = doc.RootElement;

        // Verify GET endpoint parameters
        var getOperation = root.GetProperty("paths")
            .GetProperty("/items/{id}/details")
            .GetProperty("get");

        var parameters = getOperation.GetProperty("parameters");

        // Verify route parameter
        var idParam = parameters.EnumerateArray()
            .First(p => p.GetProperty("name").GetString() == "id");
        Assert.Equal("path", idParam.GetProperty("in").GetString());
        Assert.True(idParam.GetProperty("required").GetBoolean());

        // Verify query parameter
        var filterParam = parameters.EnumerateArray()
            .First(p => p.GetProperty("name").GetString() == "filter");
        Assert.Equal("query", filterParam.GetProperty("in").GetString());

        // Verify header parameter
        var apiKeyParam = parameters.EnumerateArray()
            .First(p => p.GetProperty("name").GetString() == "X-Api-Key");
        Assert.Equal("header", apiKeyParam.GetProperty("in").GetString());

        // Verify POST endpoint
        var postOperation = root.GetProperty("paths")
            .GetProperty("/items")
            .GetProperty("post");

        // Verify request body
        Assert.True(postOperation.TryGetProperty("requestBody", out var requestBody));
        Assert.Contains("application/json", requestBody.GetProperty("content").EnumerateObject().Select(x => x.Name));

        // Verify header parameter in POST
        var postParams = postOperation.GetProperty("parameters");
        var correlationIdParam = postParams.EnumerateArray()
            .First(p => p.GetProperty("name").GetString() == "Correlation-Id");
        Assert.Equal("header", correlationIdParam.GetProperty("in").GetString());

        // Verify schema references are created
        Assert.True(root.GetProperty("components").GetProperty("schemas").TryGetProperty("Item", out _));
    }
}