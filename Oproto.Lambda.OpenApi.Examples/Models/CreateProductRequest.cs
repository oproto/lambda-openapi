using Oproto.Lambda.OpenApi.Attributes;

namespace Examples.Models;

/// <summary>
/// Request model for creating a new product.
/// </summary>
public class CreateProductRequest
{
    /// <summary>
    /// Gets or sets the product name.
    /// </summary>
    [OpenApiSchema(Description = "Product name", MinLength = 1, MaxLength = 200, Example = "Widget Pro")]
    public string Name { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets the product price in USD.
    /// </summary>
    [OpenApiSchema(Description = "Product price in USD", Minimum = 0.01, Example = "29.99")]
    public decimal Price { get; set; }

    /// <summary>
    /// Gets or sets the product category.
    /// </summary>
    [OpenApiSchema(Description = "Product category", Example = "Electronics")]
    public string Category { get; set; } = string.Empty;
}
