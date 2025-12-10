using System;

namespace Oproto.Lambda.OpenApi.Attributes
{
    /// <summary>
    ///     Specifies the response type for a specific HTTP status code in the OpenAPI specification.
    /// </summary>
    /// <remarks>
    ///     Use this attribute to document the response types for API operations, especially when
    ///     the method return type (e.g., IHttpResult) doesn't reflect the actual response body.
    ///     Multiple attributes can be applied to document different status codes.
    /// </remarks>
    /// <example>
    ///     <code>
    ///     [OpenApiResponseType(typeof(Product), 200, "Returns the created product")]
    ///     [OpenApiResponseType(typeof(ValidationError), 400, "Validation failed")]
    ///     public async Task&lt;IHttpResult&gt; CreateProduct(CreateProductRequest request)
    ///     </code>
    /// </example>
    [AttributeUsage(AttributeTargets.Method, AllowMultiple = true)]
    public class OpenApiResponseTypeAttribute : Attribute
    {
        /// <summary>
        ///     Initializes a new instance of the <see cref="OpenApiResponseTypeAttribute"/> class.
        /// </summary>
        /// <param name="responseType">The type of the response body.</param>
        /// <param name="statusCode">The HTTP status code (default: 200).</param>
        public OpenApiResponseTypeAttribute(Type responseType, int statusCode = 200)
        {
            ResponseType = responseType;
            StatusCode = statusCode;
        }

        /// <summary>
        ///     Gets the type of the response body.
        /// </summary>
        public Type ResponseType { get; }

        /// <summary>
        ///     Gets the HTTP status code this response applies to.
        /// </summary>
        public int StatusCode { get; }

        /// <summary>
        ///     Gets or sets a description of the response.
        /// </summary>
        public string Description { get; set; }
    }
}
