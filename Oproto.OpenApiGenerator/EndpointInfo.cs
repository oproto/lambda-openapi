using Microsoft.CodeAnalysis;

namespace Oproto.OpenApiGenerator;

public class EndpointInfo
{
    public string MethodName { get; set; }
    public string HttpMethod { get; set; }
    public string Route { get; set; }
    public string ControllerName { get; set; }
    public List<ParameterInfo> Parameters { get; set; }
    public ApiType ApiType { get; set; }
    public DocumentationInfo Documentation { get; set; }
    public List<ResponseTypeInfo> ResponseTypes { get; set; } = new();
    public ITypeSymbol ReturnType { get; set; }
    public IMethodSymbol MethodSymbol { get; set; }
    public List<string> SecuritySchemes { get; set; } = new();
    public bool RequiresAuthorization { get; set; }
    public bool RequiresApiKey { get; set; }
}
