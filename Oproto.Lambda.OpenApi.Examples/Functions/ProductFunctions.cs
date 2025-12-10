using Amazon.Lambda.Annotations;
using Amazon.Lambda.Annotations.APIGateway;
using Amazon.Lambda.Core;
using Amazon.Lambda.Serialization.SystemTextJson;
using Oproto.Lambda.OpenApi.Attributes;
using Examples.Models;

[assembly: LambdaSerializer(typeof(DefaultLambdaJsonSerializer))]
[assembly: OpenApiInfo("Products API", "1.0", Description = "API for managing products in the catalog")]

// Server definitions - Requirements 4.1, 4.2
[assembly: OpenApiServer("https://api.example.com/v1", Description = "Production server")]
[assembly: OpenApiServer("https://staging-api.example.com/v1", Description = "Staging server")]

// Tag definitions - Requirements 5.2
[assembly: OpenApiTagDefinition("Products", Description = "Operations for managing products in the catalog")]
[assembly: OpenApiTagDefinition("Admin", Description = "Administrative operations", ExternalDocsUrl = "https://docs.example.com/admin", ExternalDocsDescription = "Admin API documentation")]

// External documentation - Requirements 6.1
[assembly: OpenApiExternalDocs("https://docs.example.com/products-api", Description = "Full Products API documentation")]

namespace Examples.Functions;

/// <summary>
/// Lambda functions for managing products in the catalog.
/// Demonstrates CRUD operations with OpenAPI documentation attributes.
/// </summary>
public class ProductFunctions
{
    /// <summary>
    /// Retrieves a list of products with optional filtering.
    /// </summary>
    /// <param name="limit">Maximum number of products to return.</param>
    /// <param name="category">Filter products by category.</param>
    /// <returns>A collection of products matching the criteria.</returns>
    [LambdaFunction]
    [HttpApi(LambdaHttpMethod.Get, "/products")]
    [OpenApiOperation(Summary = "List all products", Description = "Retrieves a paginated list of products with optional filtering by category.")]
    [OpenApiTag("Products")]
    [OpenApiOperationId("listProducts")]
    [OpenApiResponseHeader("X-Total-Count", Description = "Total number of products available", Type = typeof(int))]
    [OpenApiResponseHeader("X-Page-Size", Description = "Number of products returned in this response", Type = typeof(int))]
    [OpenApiExample("Product List", "[{\"id\": \"1\", \"name\": \"Widget Pro\", \"price\": 29.99, \"category\": \"Electronics\"}]", StatusCode = 200)]
    public Task<IEnumerable<Product>> GetProducts(
        [FromQuery] int limit = 100,
        [FromQuery] string category = "")
    {
        // Example implementation - in a real application, this would query a database
        var products = new List<Product>
        {
            new Product { Id = "1", Name = "Widget Pro", Price = 29.99m, Category = "Electronics" },
            new Product { Id = "2", Name = "Gadget Plus", Price = 49.99m, Category = "Electronics" }
        };

        IEnumerable<Product> result = products;

        if (!string.IsNullOrEmpty(category))
        {
            result = result.Where(p => p.Category.Equals(category, StringComparison.OrdinalIgnoreCase));
        }

        if (limit > 0)
        {
            result = result.Take(limit);
        }

        return Task.FromResult(result);
    }


    /// <summary>
    /// Retrieves a specific product by its unique identifier.
    /// </summary>
    /// <param name="id">The unique identifier of the product.</param>
    /// <returns>The product with the specified ID.</returns>
    [LambdaFunction]
    [HttpApi(LambdaHttpMethod.Get, "/products/{id}")]
    [OpenApiOperation(Summary = "Get product by ID", Description = "Retrieves a single product by its unique identifier.")]
    [OpenApiTag("Products")]
    [OpenApiOperationId("getProductById")]
    [OpenApiResponseHeader("X-Request-Id", Description = "Unique request identifier for tracing")]
    [OpenApiExample("Single Product", "{\"id\": \"123\", \"name\": \"Widget Pro\", \"price\": 29.99, \"category\": \"Electronics\"}", StatusCode = 200)]
    [OpenApiExternalDocs("https://docs.example.com/products/get-by-id", Description = "Detailed guide for retrieving products")]
    public Task<Product> GetProduct(string id)
    {
        // Example implementation - in a real application, this would query a database
        var product = new Product
        {
            Id = id,
            Name = "Widget Pro",
            Price = 29.99m,
            Category = "Electronics",
            InternalCreatedAt = DateTime.UtcNow
        };

        return Task.FromResult(product);
    }

    /// <summary>
    /// Creates a new product in the catalog.
    /// </summary>
    /// <param name="request">The product creation request containing product details.</param>
    /// <returns>The newly created product with its assigned ID.</returns>
    [LambdaFunction]
    [HttpApi(LambdaHttpMethod.Post, "/products")]
    [OpenApiOperation(Summary = "Create a product", Description = "Creates a new product in the catalog and returns the created product with its assigned ID.")]
    [OpenApiTag("Products")]
    [OpenApiOperationId("createProduct")]
    [OpenApiExample("Create Request", "{\"name\": \"New Widget\", \"price\": 19.99, \"category\": \"Electronics\"}", IsRequestExample = true)]
    [OpenApiExample("Created Product", "{\"id\": \"456\", \"name\": \"New Widget\", \"price\": 19.99, \"category\": \"Electronics\"}", StatusCode = 200)]
    [OpenApiResponseHeader("Location", Description = "URL of the newly created product")]
    public Task<Product> CreateProduct([FromBody] CreateProductRequest request)
    {
        // Example implementation - in a real application, this would persist to a database
        var product = new Product
        {
            Id = Guid.NewGuid().ToString(),
            Name = request.Name,
            Price = request.Price,
            Category = request.Category,
            InternalCreatedAt = DateTime.UtcNow
        };

        return Task.FromResult(product);
    }

    /// <summary>
    /// Updates an existing product in the catalog.
    /// </summary>
    /// <param name="id">The unique identifier of the product to update.</param>
    /// <param name="request">The product update request containing fields to update.</param>
    /// <returns>The updated product.</returns>
    [LambdaFunction]
    [HttpApi(LambdaHttpMethod.Put, "/products/{id}")]
    [OpenApiOperation(Summary = "Update a product", Description = "Updates an existing product in the catalog. Only provided fields will be updated.")]
    [OpenApiTag("Products")]
    [OpenApiOperationId("updateProduct")]
    [OpenApiExample("Update Request", "{\"name\": \"Updated Widget\", \"price\": 24.99}", IsRequestExample = true)]
    [OpenApiExample("Updated Product", "{\"id\": \"123\", \"name\": \"Updated Widget\", \"price\": 24.99, \"category\": \"Electronics\"}", StatusCode = 200)]
    public Task<Product> UpdateProduct(string id, [FromBody] UpdateProductRequest request)
    {
        // Example implementation - in a real application, this would update a database record
        var product = new Product
        {
            Id = id,
            Name = request.Name ?? "Existing Product",
            Price = request.Price ?? 0m,
            Category = request.Category ?? "Uncategorized",
            InternalCreatedAt = DateTime.UtcNow
        };

        return Task.FromResult(product);
    }

    /// <summary>
    /// Deletes a product from the catalog.
    /// </summary>
    /// <param name="id">The unique identifier of the product to delete.</param>
    [LambdaFunction]
    [HttpApi(LambdaHttpMethod.Delete, "/products/{id}")]
    [OpenApiOperation(Summary = "Delete a product", Description = "Permanently removes a product from the catalog.")]
    [OpenApiTag("Products")]
    [OpenApiOperationId("deleteProduct")]
    [Obsolete("Use the archive endpoint instead. This endpoint will be removed in v2.0.")]
    public Task DeleteProduct(string id)
    {
        // Example implementation - in a real application, this would delete from a database
        return Task.CompletedTask;
    }
}
