using System;

namespace Oproto.Lambda.OpenApi.Attributes
{
    /// <summary>
    ///     Specifies a response header for an API operation in the OpenAPI specification.
    /// </summary>
    /// <remarks>
    ///     Use this attribute to document response headers for API operations.
    ///     Multiple headers can be defined by applying this attribute multiple times.
    /// </remarks>
    /// <example>
    ///     <code>
    ///     [OpenApiResponseHeader("X-Request-Id", Description = "Unique request identifier")]
    ///     [OpenApiResponseHeader("X-Rate-Limit-Remaining", Type = typeof(int), StatusCode = 200)]
    ///     public async Task&lt;IHttpResult&gt; GetProduct(string id)
    ///     </code>
    /// </example>
    [AttributeUsage(AttributeTargets.Method, AllowMultiple = true)]
    public class OpenApiResponseHeaderAttribute : Attribute
    {
        /// <summary>
        ///     Initializes a new instance of the <see cref="OpenApiResponseHeaderAttribute"/> class.
        /// </summary>
        /// <param name="name">The name of the response header.</param>
        public OpenApiResponseHeaderAttribute(string name)
        {
            Name = name ?? throw new ArgumentNullException(nameof(name));
        }

        /// <summary>
        ///     Gets the name of the response header.
        /// </summary>
        public string Name { get; }

        /// <summary>
        ///     Gets or sets the HTTP status code this header applies to.
        /// </summary>
        public int StatusCode { get; set; } = 200;

        /// <summary>
        ///     Gets or sets a description of the header.
        /// </summary>
        public string Description { get; set; }

        /// <summary>
        ///     Gets or sets the type of the header value.
        /// </summary>
        public Type Type { get; set; } = typeof(string);

        /// <summary>
        ///     Gets or sets whether the header is required.
        /// </summary>
        public bool Required { get; set; } = false;
    }
}
