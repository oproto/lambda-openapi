using System;

namespace Oproto.Lambda.OpenApi.Attributes
{
    /// <summary>
    ///     Specifies an example for a request or response in the OpenAPI specification.
    /// </summary>
    /// <remarks>
    ///     Use this attribute to provide JSON examples for API operations.
    ///     Multiple examples can be provided by applying this attribute multiple times.
    /// </remarks>
    /// <example>
    ///     <code>
    ///     [OpenApiExample("Basic Product", "{\"name\": \"Widget\", \"price\": 9.99}", IsRequestExample = true)]
    ///     [OpenApiExample("Success Response", "{\"id\": 1, \"name\": \"Widget\"}", StatusCode = 200)]
    ///     public async Task&lt;IHttpResult&gt; CreateProduct(CreateProductRequest request)
    ///     </code>
    /// </example>
    [AttributeUsage(AttributeTargets.Method, AllowMultiple = true)]
    public class OpenApiExampleAttribute : Attribute
    {
        /// <summary>
        ///     Initializes a new instance of the <see cref="OpenApiExampleAttribute"/> class.
        /// </summary>
        /// <param name="name">The name of the example.</param>
        /// <param name="value">The JSON string value of the example.</param>
        public OpenApiExampleAttribute(string name, string value)
        {
            Name = name ?? throw new ArgumentNullException(nameof(name));
            Value = value ?? throw new ArgumentNullException(nameof(value));
        }

        /// <summary>
        ///     Gets the name of the example.
        /// </summary>
        public string Name { get; }

        /// <summary>
        ///     Gets the JSON string value of the example.
        /// </summary>
        public string Value { get; }

        /// <summary>
        ///     Gets or sets the HTTP status code this example applies to (for response examples).
        /// </summary>
        public int StatusCode { get; set; } = 200;

        /// <summary>
        ///     Gets or sets whether this is a request body example.
        /// </summary>
        public bool IsRequestExample { get; set; } = false;
    }
}
