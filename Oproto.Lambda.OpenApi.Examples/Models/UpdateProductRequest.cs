using Oproto.Lambda.OpenApi.Attributes;

namespace Examples.Models;

/// <summary>
/// Request model for updating an existing product.
/// All properties are optional to support partial updates.
/// </summary>
public class UpdateProductRequest
{
    /// <summary>
    /// Gets or sets the product name.
    /// </summary>
    [OpenApiSchema(Description = "Product name", MinLength = 1, MaxLength = 200)]
    public string? Name { get; set; }

    /// <summary>
    /// Gets or sets the product price in USD.
    /// </summary>
    [OpenApiSchema(Description = "Product price in USD", Minimum = 0.01)]
    public decimal? Price { get; set; }

    /// <summary>
    /// Gets or sets the product category.
    /// </summary>
    [OpenApiSchema(Description = "Product category")]
    public string? Category { get; set; }
}
