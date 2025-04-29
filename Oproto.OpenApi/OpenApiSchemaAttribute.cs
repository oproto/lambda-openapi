using System;

namespace Oproto.OpenApi
{
    /// <summary>
    ///     Provides additional OpenAPI schema information for properties and parameters.
    /// </summary>
    /// <remarks>
    ///     Use this attribute to specify validation rules, formats, and examples for schema generation.
    /// </remarks>
    [AttributeUsage(AttributeTargets.Property | AttributeTargets.Parameter)]
    public class OpenApiSchemaAttribute : Attribute
    {
        /// <summary>
        ///     Gets or sets the description of the schema property.
        /// </summary>
        public string Description { get; set; }

        /// <summary>
        ///     Gets or sets the format of the schema property (e.g., "date-time", "email", "uuid").
        /// </summary>
        public string Format { get; set; }

        /// <summary>
        ///     Gets or sets an example value for the schema property.
        /// </summary>
        public string Example { get; set; }

        /// <summary>
        ///     Gets or sets a regular expression pattern that the value must match.
        /// </summary>
        public string Pattern { get; set; }

        /// <summary>
        ///     Gets or sets the minimum value for numeric properties.
        /// </summary>
        public double Minimum { get; set; }

        /// <summary>
        ///     Gets or sets the maximum value for numeric properties.
        /// </summary>
        public double Maximum { get; set; }

        /// <summary>
        ///     Gets or sets whether the minimum value is exclusive.
        /// </summary>
        public bool ExclusiveMinimum { get; set; }

        /// <summary>
        ///     Gets or sets whether the maximum value is exclusive.
        /// </summary>
        public bool ExclusiveMaximum { get; set; }

        /// <summary>
        ///     Gets or sets the minimum length for string properties.
        /// </summary>
        public int MinLength { get; set; }

        /// <summary>
        ///     Gets or sets the maximum length for string properties.
        /// </summary>
        public int MaxLength { get; set; }
    }
}
