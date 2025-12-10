namespace Oproto.Lambda.OpenApi.SourceGenerator;

public class DocumentationInfo
{
    public string Summary { get; set; }
    public string Description { get; set; }
    public Dictionary<string, string> ParameterDescriptions { get; set; } = new();
    public string Returns { get; set; }
    public List<XmlExampleInfo> Examples { get; set; } = new();
}

/// <summary>
/// Represents an example extracted from XML documentation.
/// </summary>
public class XmlExampleInfo
{
    public string Value { get; set; }
    public string Name { get; set; }
    public bool IsRequestExample { get; set; }
    public int StatusCode { get; set; } = 200;
}
