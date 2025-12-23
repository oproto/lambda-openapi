namespace Oproto.Lambda.OpenApi.SourceGenerator;

/// <summary>
/// Configuration for automatic example generation.
/// </summary>
internal class ExampleConfig
{
    /// <summary>
    /// Whether to compose examples from property-level examples.
    /// </summary>
    public bool ComposeFromProperties { get; set; } = true;

    /// <summary>
    /// Whether to generate default examples for properties without explicit examples.
    /// </summary>
    public bool GenerateDefaults { get; set; } = false;
}
