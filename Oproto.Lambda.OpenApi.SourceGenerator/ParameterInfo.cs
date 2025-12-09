using Microsoft.CodeAnalysis;

namespace Oproto.Lambda.OpenApi.SourceGenerator;

public enum ParameterSource
{
    Path, // For route parameters
    Query,
    Header,
    Body
}

public class ParameterInfo
{
    public string Name { get; set; }
    public ITypeSymbol TypeSymbol { get; set; }
    public ParameterSource Source { get; set; } // FromRoute, FromQuery, etc.
    public string Description { get; set; }
    public DocumentationInfo Documentation { get; set; }
    public bool IsRequired { get; set; }

    public object DefaultValue { get; set; }
    public bool HasDefaultValue { get; set; }

    // Boolean getters for parameter source
    public bool IsFromBody => Source == ParameterSource.Body;
    public bool IsFromRoute => Source == ParameterSource.Path;
    public bool IsFromQuery => Source == ParameterSource.Query;
    public bool IsFromHeader => Source == ParameterSource.Header;
}
