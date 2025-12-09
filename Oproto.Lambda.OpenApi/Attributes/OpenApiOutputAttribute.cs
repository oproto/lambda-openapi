using System;

namespace Oproto.Lambda.OpenApi.Attributes
{
    /// <summary>
    ///     Specifies the output configuration for the generated OpenAPI specification.
    /// </summary>
    /// <remarks>
    ///     Apply this attribute at the assembly level to configure where the OpenAPI specification should be written.
    /// </remarks>
    [AttributeUsage(AttributeTargets.Assembly)]
    public class OpenApiOutputAttribute : Attribute
    {
        /// <summary>
        ///     Initializes a new instance of the <see cref="OpenApiOutputAttribute" /> class.
        /// </summary>
        /// <param name="specification">The name or identifier of the specification.</param>
        /// <param name="outputPath">The file path where the OpenAPI specification should be written.</param>
        public OpenApiOutputAttribute(string specification, string outputPath)
        {
            Specification = specification;
            OutputPath = outputPath;
        }

        /// <summary>
        ///     Gets the name or identifier of the specification.
        /// </summary>
        public string Specification { get; }

        /// <summary>
        ///     Gets the file path where the OpenAPI specification should be written.
        /// </summary>
        public string OutputPath { get; }
    }
}
