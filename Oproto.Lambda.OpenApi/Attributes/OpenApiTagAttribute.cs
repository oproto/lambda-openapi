using System;

namespace Oproto.Lambda.OpenApi.Attributes
{
    /// <summary>
    ///     Specifies OpenAPI tag information for a class or method.
    /// </summary>
    /// <remarks>
    ///     Tags can be used to group operations by resources or any other qualifier.
    ///     Multiple tags can be assigned to a single operation by applying this attribute multiple times.
    /// </remarks>
    [AttributeUsage(AttributeTargets.Class | AttributeTargets.Method | AttributeTargets.Parameter |
                    AttributeTargets.Property, AllowMultiple = true)]
    public class OpenApiTagAttribute : Attribute
    {
        /// <summary>
        ///     Initializes a new instance of the <see cref="OpenApiTagAttribute" /> class.
        /// </summary>
        /// <param name="tag">The tag name used for grouping operations.</param>
        /// <param name="description">Optional description of the tag.</param>
        public OpenApiTagAttribute(string tag, string description = null)
        {
            Tag = tag;
            Description = description;
        }

        /// <summary>
        ///     Gets the tag name used for grouping operations.
        /// </summary>
        public string Tag { get; }

        /// <summary>
        ///     Gets the description of the tag.
        /// </summary>
        public string Description { get; }
    }
}
