namespace Oproto.OpenApiGenerator;

public class DocumentationInfo
{
    public string Summary { get; set; }
    public string Description { get; set; }
    public Dictionary<string, string> ParameterDescriptions { get; set; } = new();

    public Dictionary<string, ExampleInfo> Examples { get; set; } = new();
    public string Returns { get; set; }
}
