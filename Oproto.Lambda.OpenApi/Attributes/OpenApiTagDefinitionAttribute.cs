using System;

namespace Oproto.Lambda.OpenApi.Attributes
{
    /// <summary>
    ///     Defines a tag with metadata for the OpenAPI specification.
    /// </summary>
    /// <remarks>
    ///     Apply this attribute at the assembly level to define tags with descriptions
    ///     and external documentation links. These definitions appear in the specification's
    ///     tags array and provide additional context for API documentation viewers.
    /// </remarks>
    /// <example>
    ///     <code>
    ///     [assembly: OpenApiTagDefinition("Products", Description = "Operations related to products")]
    ///     [assembly: OpenApiTagDefinition("Orders", 
    ///         Description = "Order management operations",
    ///         ExternalDocsUrl = "https://docs.example.com/orders",
    ///         ExternalDocsDescription = "Order API documentation")]
    ///     </code>
    /// </example>
    [AttributeUsage(AttributeTargets.Assembly, AllowMultiple = true)]
    public class OpenApiTagDefinitionAttribute : Attribute
    {
        /// <summary>
        ///     Initializes a new instance of the <see cref="OpenApiTagDefinitionAttribute"/> class.
        /// </summary>
        /// <param name="name">The name of the tag.</param>
        public OpenApiTagDefinitionAttribute(string name)
        {
            Name = name ?? throw new ArgumentNullException(nameof(name));
        }

        /// <summary>
        ///     Gets the name of the tag.
        /// </summary>
        public string Name { get; }

        /// <summary>
        ///     Gets or sets a description of the tag.
        /// </summary>
        public string Description { get; set; }

        /// <summary>
        ///     Gets or sets the URL for external documentation about this tag.
        /// </summary>
        public string ExternalDocsUrl { get; set; }

        /// <summary>
        ///     Gets or sets a description for the external documentation.
        /// </summary>
        public string ExternalDocsDescription { get; set; }
    }
}
