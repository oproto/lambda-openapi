using System;

namespace Oproto.Lambda.OpenApi.Attributes
{
    /// <summary>
    ///     Defines a tag group for organizing related tags in the OpenAPI specification.
    /// </summary>
    /// <remarks>
    ///     Apply this attribute at the assembly level to group related tags together.
    ///     Tag groups are rendered using the x-tagGroups extension, which is supported
    ///     by documentation tools like Redoc for hierarchical navigation.
    /// </remarks>
    /// <example>
    ///     <code>
    ///     [assembly: OpenApiTagGroup("User Management", "Users", "Authentication", "Roles")]
    ///     [assembly: OpenApiTagGroup("Product Catalog", "Products", "Categories", "Inventory")]
    ///     </code>
    /// </example>
    [AttributeUsage(AttributeTargets.Assembly, AllowMultiple = true)]
    public class OpenApiTagGroupAttribute : Attribute
    {
        /// <summary>
        ///     Initializes a new instance of the <see cref="OpenApiTagGroupAttribute"/> class.
        /// </summary>
        /// <param name="name">The name of the tag group.</param>
        /// <param name="tags">The tags that belong to this group.</param>
        public OpenApiTagGroupAttribute(string name, params string[] tags)
        {
            Name = name ?? throw new ArgumentNullException(nameof(name));
            Tags = tags ?? Array.Empty<string>();
        }

        /// <summary>
        ///     Gets the name of the tag group.
        /// </summary>
        public string Name { get; }

        /// <summary>
        ///     Gets the tags that belong to this group.
        /// </summary>
        public string[] Tags { get; }
    }
}
