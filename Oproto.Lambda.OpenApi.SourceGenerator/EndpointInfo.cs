using Microsoft.CodeAnalysis;
using Microsoft.OpenApi.Models;

namespace Oproto.Lambda.OpenApi.SourceGenerator;

/// <summary>
/// Represents information about a response header defined via [OpenApiResponseHeader] attribute.
/// </summary>
public class ResponseHeaderInfo
{
    public string Name { get; set; }
    public int StatusCode { get; set; } = 200;
    public string Description { get; set; }
    public ITypeSymbol TypeSymbol { get; set; }
    public bool Required { get; set; }
}

/// <summary>
/// Represents information about an example defined via [OpenApiExample] attribute or XML documentation.
/// </summary>
public class ExampleInfo
{
    public string Name { get; set; }
    public string Value { get; set; }
    public int StatusCode { get; set; } = 200;
    public bool IsRequestExample { get; set; }
    public ExampleSource Source { get; set; } = ExampleSource.Attribute;
}

/// <summary>
/// Indicates the source of an example (attribute or XML documentation).
/// </summary>
public enum ExampleSource
{
    Attribute,
    XmlDocumentation
}

public class EndpointInfo
{
    public string MethodName { get; set; }
    public string HttpMethod { get; set; }
    public string Route { get; set; }
    public string ControllerName { get; set; }
    public List<ParameterInfo> Parameters { get; set; }
    public ApiType ApiType { get; set; }
    public DocumentationInfo Documentation { get; set; }
    public ITypeSymbol ReturnType { get; set; }
    public IMethodSymbol MethodSymbol { get; set; }
    public List<string> SecuritySchemes { get; set; } = new();
    public bool RequiresAuthorization { get; set; }
    public bool RequiresApiKey { get; set; }
    public string OperationId { get; set; }
    public bool IsDeprecated { get; set; }
    public string DeprecationMessage { get; set; }
    public List<string> Tags { get; set; } = new();
    public OpenApiExternalDocs ExternalDocs { get; set; }
    public List<ResponseHeaderInfo> ResponseHeaders { get; set; } = new();
    public List<ExampleInfo> Examples { get; set; } = new();
}
