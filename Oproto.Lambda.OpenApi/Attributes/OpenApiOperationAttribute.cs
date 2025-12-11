using System;

namespace Oproto.Lambda.OpenApi.Attributes
{
    /// <summary>
    ///     Provides additional OpenAPI operation information for methods.
    /// </summary>
    /// <remarks>
    ///     Use this attribute to add metadata to API operations such as summary, description,
    ///     deprecation status, and operation ID.
    /// </remarks>
    /// <example>
    ///     <code>
    ///     [OpenApiOperation(Summary = "Get a product", Description = "Retrieves a product by ID")]
    ///     public async Task&lt;IHttpResult&gt; GetProduct(string id)
    ///     
    ///     [OpenApiOperation(Summary = "Delete product", Deprecated = true, OperationId = "removeProduct")]
    ///     public async Task&lt;IHttpResult&gt; DeleteProduct(string id)
    ///     </code>
    /// </example>
    [AttributeUsage(AttributeTargets.Method)]
    public class OpenApiOperationAttribute : Attribute
    {
        /// <summary>
        ///     Gets or sets a short summary of what the operation does.
        /// </summary>
        public string Summary { get; set; }

        /// <summary>
        ///     Gets or sets a detailed explanation of the operation behavior.
        /// </summary>
        public string Description { get; set; }

        /// <summary>
        ///     Gets or sets whether the operation is deprecated.
        /// </summary>
        public bool Deprecated { get; set; }

        /// <summary>
        ///     Gets or sets a custom operation ID for code generation.
        /// </summary>
        /// <remarks>
        ///     If not specified, the operation ID is generated from the method name.
        ///     This can also be set using <see cref="OpenApiOperationIdAttribute"/> if you only need to set the ID.
        /// </remarks>
        public string OperationId { get; set; }
    }
}
