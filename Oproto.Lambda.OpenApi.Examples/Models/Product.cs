using Oproto.Lambda.OpenApi.Attributes;

namespace Examples.Models;

/// <summary>
/// Represents a product in the catalog.
/// </summary>
public class Product
{
    /// <summary>
    /// Gets or sets the unique product identifier.
    /// </summary>
    [OpenApiSchema(Description = "Unique product identifier", Format = "uuid")]
    public string Id { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets the product name.
    /// </summary>
    [OpenApiSchema(Description = "Product name", MinLength = 1, MaxLength = 200)]
    public string Name { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets the product price in USD.
    /// </summary>
    [OpenApiSchema(Description = "Product price in USD", Minimum = 0)]
    public decimal Price { get; set; }

    /// <summary>
    /// Gets or sets the product category.
    /// </summary>
    [OpenApiSchema(Description = "Product category")]
    public string Category { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets the internal creation timestamp.
    /// This property is excluded from OpenAPI documentation.
    /// </summary>
    [OpenApiIgnore]
    public DateTime InternalCreatedAt { get; set; }
}
