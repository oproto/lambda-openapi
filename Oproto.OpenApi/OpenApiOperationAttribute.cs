using System;

namespace Oproto.OpenApi
{
    /// <summary>
    ///     Provides additional OpenAPI operation information for methods.
    /// </summary>
    /// <remarks>
    ///     Use this attribute to add metadata to API operations such as summary, description, and deprecation status.
    /// </remarks>
    [AttributeUsage(AttributeTargets.Method)]
    public class OpenApiOperationAttribute : Attribute
    {
        /// <summary>
        ///     Gets or sets a short summary of what the operation does.
        /// </summary>
        public string Summary { get; set; }

        /// <summary>
        ///     Gets or sets a detailed explanation of the operation behavior.
        /// </summary>
        public string Description { get; set; }

        /// <summary>
        ///     Gets or sets whether the operation is deprecated.
        /// </summary>
        public bool Deprecated { get; set; }
    }
}
