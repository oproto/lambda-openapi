using System;

namespace Oproto.Lambda.OpenApi.Attributes
{
    /// <summary>
    ///     Configures automatic example generation behavior at the assembly level.
    /// </summary>
    /// <remarks>
    ///     Apply this attribute at the assembly level to control how examples are
    ///     automatically generated for request and response schemas. By default,
    ///     examples are composed from property-level <see cref="OpenApiSchemaAttribute"/>
    ///     example values, but default generation is disabled.
    /// </remarks>
    /// <example>
    ///     <code>
    ///     // Enable both composition and default generation
    ///     [assembly: OpenApiExampleConfig(ComposeFromProperties = true, GenerateDefaults = true)]
    ///     
    ///     // Disable automatic example composition
    ///     [assembly: OpenApiExampleConfig(ComposeFromProperties = false)]
    ///     </code>
    /// </example>
    [AttributeUsage(AttributeTargets.Assembly)]
    public class OpenApiExampleConfigAttribute : Attribute
    {
        /// <summary>
        ///     Gets or sets whether to automatically compose examples from property-level examples.
        /// </summary>
        /// <remarks>
        ///     When enabled, the generator will build complete example objects by combining
        ///     individual property examples defined via <see cref="OpenApiSchemaAttribute.Example"/>.
        /// </remarks>
        /// <value>Default is <c>true</c>.</value>
        public bool ComposeFromProperties { get; set; } = true;

        /// <summary>
        ///     Gets or sets whether to generate default examples for properties without explicit examples.
        /// </summary>
        /// <remarks>
        ///     When enabled, the generator will create sensible default examples based on
        ///     property types, formats, and constraints for properties that don't have
        ///     explicit example values defined.
        /// </remarks>
        /// <value>Default is <c>false</c>.</value>
        public bool GenerateDefaults { get; set; } = false;
    }
}
