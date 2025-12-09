using System;

namespace Oproto.Lambda.OpenApi.Attributes
{
    /// <summary>
    ///     Marks a class for OpenAPI specification generation.
    /// </summary>
    /// <remarks>
    ///     Apply this attribute to Lambda function classes to include them in the generated OpenAPI specification.
    /// </remarks>
    [AttributeUsage(AttributeTargets.Class)]
    public class GenerateOpenApiSpecAttribute : Attribute
    {
        /// <summary>
        ///     Initializes a new instance of the <see cref="GenerateOpenApiSpecAttribute" /> class.
        /// </summary>
        /// <param name="serviceName">The name of the service for the OpenAPI specification.</param>
        /// <param name="version">The version of the API specification. Defaults to "1.0".</param>
        public GenerateOpenApiSpecAttribute(string serviceName, string version = "1.0")
        {
            ServiceName = serviceName;
            Version = version;
        }

        /// <summary>
        ///     Gets the name of the service for the OpenAPI specification.
        /// </summary>
        public string ServiceName { get; }

        /// <summary>
        ///     Gets the version of the API specification.
        /// </summary>
        public string Version { get; }
    }
}
