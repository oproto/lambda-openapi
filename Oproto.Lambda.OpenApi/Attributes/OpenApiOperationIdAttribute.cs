using System;

namespace Oproto.Lambda.OpenApi.Attributes
{
    /// <summary>
    ///     Specifies a custom operation ID for an API operation in the OpenAPI specification.
    /// </summary>
    /// <remarks>
    ///     Use this attribute to override the auto-generated operation ID with a custom value.
    ///     Operation IDs are used by code generators to create meaningful method names.
    /// </remarks>
    /// <example>
    ///     <code>
    ///     [OpenApiOperationId("listAllProducts")]
    ///     public async Task&lt;IHttpResult&gt; GetProducts()
    ///     
    ///     [OpenApiOperationId("getProductById")]
    ///     public async Task&lt;IHttpResult&gt; GetProduct(string id)
    ///     </code>
    /// </example>
    [AttributeUsage(AttributeTargets.Method)]
    public class OpenApiOperationIdAttribute : Attribute
    {
        /// <summary>
        ///     Initializes a new instance of the <see cref="OpenApiOperationIdAttribute"/> class.
        /// </summary>
        /// <param name="operationId">The custom operation ID.</param>
        public OpenApiOperationIdAttribute(string operationId)
        {
            OperationId = operationId ?? throw new ArgumentNullException(nameof(operationId));
        }

        /// <summary>
        ///     Gets the custom operation ID.
        /// </summary>
        public string OperationId { get; }
    }
}
