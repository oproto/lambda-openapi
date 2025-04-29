using System;

namespace Oproto.OpenApi
{
    /// <summary>
    ///     Indicates that a property or parameter should be excluded from OpenAPI documentation.
    /// </summary>
    /// <remarks>
    ///     Use this attribute to hide internal or implementation-specific members from the API documentation.
    /// </remarks>
    [AttributeUsage(AttributeTargets.Parameter | AttributeTargets.Property)]
    public class OpenApiIgnoreAttribute : Attribute
    {
    }
}
