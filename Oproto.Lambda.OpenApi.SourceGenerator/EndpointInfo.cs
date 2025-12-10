using Microsoft.CodeAnalysis;

namespace Oproto.Lambda.OpenApi.SourceGenerator;

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
}
