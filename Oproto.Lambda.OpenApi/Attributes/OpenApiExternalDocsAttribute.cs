using System;

namespace Oproto.Lambda.OpenApi.Attributes
{
    /// <summary>
    ///     Specifies external documentation for the API or an operation in the OpenAPI specification.
    /// </summary>
    /// <remarks>
    ///     Apply this attribute at the assembly level to add external documentation to the entire API,
    ///     or at the method level to add external documentation to a specific operation.
    /// </remarks>
    /// <example>
    ///     <code>
    ///     // Assembly-level external documentation
    ///     [assembly: OpenApiExternalDocs("https://docs.example.com", Description = "Full API documentation")]
    ///     
    ///     // Method-level external documentation
    ///     [OpenApiExternalDocs("https://docs.example.com/products", Description = "Product API guide")]
    ///     public async Task&lt;IHttpResult&gt; GetProducts()
    ///     </code>
    /// </example>
    [AttributeUsage(AttributeTargets.Assembly | AttributeTargets.Method)]
    public class OpenApiExternalDocsAttribute : Attribute
    {
        /// <summary>
        ///     Initializes a new instance of the <see cref="OpenApiExternalDocsAttribute"/> class.
        /// </summary>
        /// <param name="url">The URL for the external documentation.</param>
        public OpenApiExternalDocsAttribute(string url)
        {
            Url = url ?? throw new ArgumentNullException(nameof(url));
        }

        /// <summary>
        ///     Gets the URL for the external documentation.
        /// </summary>
        public string Url { get; }

        /// <summary>
        ///     Gets or sets a description of the external documentation.
        /// </summary>
        public string Description { get; set; }
    }
}
