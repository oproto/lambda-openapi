using System;

namespace Oproto.Lambda.OpenApi.Attributes
{
    /// <summary>
    ///     Specifies global OpenAPI document information at the assembly level.
    /// </summary>
    /// <remarks>
    ///     Use this attribute to set the API title, version, and description that appear
    ///     in the generated OpenAPI specification's info section. This attribute should
    ///     be applied at the assembly level, typically in AssemblyInfo.cs or any .cs file.
    /// </remarks>
    /// <example>
    ///     <code>
    ///     [assembly: OpenApiInfo("My API", "1.0.0", Description = "API for managing resources")]
    ///     </code>
    /// </example>
    [AttributeUsage(AttributeTargets.Assembly)]
    public class OpenApiInfoAttribute : Attribute
    {
        /// <summary>
        ///     Initializes a new instance of the <see cref="OpenApiInfoAttribute"/> class.
        /// </summary>
        /// <param name="title">The title of the API.</param>
        /// <param name="version">The version of the API (default: "1.0.0").</param>
        public OpenApiInfoAttribute(string title, string version = "1.0.0")
        {
            Title = title;
            Version = version;
        }

        /// <summary>
        ///     Gets the title of the API.
        /// </summary>
        public string Title { get; }

        /// <summary>
        ///     Gets the version of the API.
        /// </summary>
        public string Version { get; }

        /// <summary>
        ///     Gets or sets a description of the API.
        /// </summary>
        public string Description { get; set; }

        /// <summary>
        ///     Gets or sets the URL to the Terms of Service for the API.
        /// </summary>
        public string TermsOfService { get; set; }

        /// <summary>
        ///     Gets or sets the contact name for the API.
        /// </summary>
        public string ContactName { get; set; }

        /// <summary>
        ///     Gets or sets the contact email for the API.
        /// </summary>
        public string ContactEmail { get; set; }

        /// <summary>
        ///     Gets or sets the contact URL for the API.
        /// </summary>
        public string ContactUrl { get; set; }

        /// <summary>
        ///     Gets or sets the license name for the API.
        /// </summary>
        public string LicenseName { get; set; }

        /// <summary>
        ///     Gets or sets the license URL for the API.
        /// </summary>
        public string LicenseUrl { get; set; }
    }
}
