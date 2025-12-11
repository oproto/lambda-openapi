using System;

namespace Oproto.Lambda.OpenApi.Attributes
{
    /// <summary>
    ///     Specifies a server URL for the OpenAPI specification.
    /// </summary>
    /// <remarks>
    ///     Apply this attribute at the assembly level to define server URLs for the API.
    ///     Multiple servers can be defined by applying this attribute multiple times.
    /// </remarks>
    /// <example>
    ///     <code>
    ///     [assembly: OpenApiServer("https://api.example.com", Description = "Production server")]
    ///     [assembly: OpenApiServer("https://staging-api.example.com", Description = "Staging server")]
    ///     </code>
    /// </example>
    [AttributeUsage(AttributeTargets.Assembly, AllowMultiple = true)]
    public class OpenApiServerAttribute : Attribute
    {
        /// <summary>
        ///     Initializes a new instance of the <see cref="OpenApiServerAttribute"/> class.
        /// </summary>
        /// <param name="url">The URL of the server.</param>
        public OpenApiServerAttribute(string url)
        {
            Url = url ?? throw new ArgumentNullException(nameof(url));
        }

        /// <summary>
        ///     Gets the URL of the server.
        /// </summary>
        public string Url { get; }

        /// <summary>
        ///     Gets or sets a description of the server.
        /// </summary>
        public string Description { get; set; }
    }
}
