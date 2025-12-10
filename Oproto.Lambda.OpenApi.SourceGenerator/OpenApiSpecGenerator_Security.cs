using Microsoft.CodeAnalysis;
using Microsoft.OpenApi.Models;

namespace Oproto.Lambda.OpenApi.SourceGenerator;

public partial class OpenApiSpecGenerator
{
    private Compilation? _currentCompilation;

    /// <summary>
    /// Reads security scheme attributes from the assembly and adds them to the OpenAPI document.
    /// If no security scheme attributes are defined, no security schemes are added.
    /// </summary>
    private void AddSecurityDefinitions(OpenApiDocument document)
    {
        if (_currentCompilation == null)
            return;

        var securitySchemes = new Dictionary<string, OpenApiSecurityScheme>();

        foreach (var attribute in _currentCompilation.Assembly.GetAttributes())
        {
            if (attribute.AttributeClass?.Name != "OpenApiSecuritySchemeAttribute")
                continue;

            var scheme = ParseSecuritySchemeAttribute(attribute);
            if (scheme != null)
            {
                securitySchemes[scheme.Value.schemeId] = scheme.Value.securityScheme;
            }
        }

        if (securitySchemes.Count > 0)
        {
            document.Components.SecuritySchemes = securitySchemes;
        }
    }

    private (string schemeId, OpenApiSecurityScheme securityScheme)? ParseSecuritySchemeAttribute(AttributeData attribute)
    {
        if (attribute.ConstructorArguments.Length == 0)
            return null;

        var schemeId = attribute.ConstructorArguments[0].Value?.ToString();
        if (string.IsNullOrEmpty(schemeId))
            return null;

        var securityScheme = new OpenApiSecurityScheme();

        foreach (var namedArg in attribute.NamedArguments)
        {
            switch (namedArg.Key)
            {
                case "Type":
                    var typeValue = (int)(namedArg.Value.Value ?? 0);
                    securityScheme.Type = typeValue switch
                    {
                        0 => Microsoft.OpenApi.Models.SecuritySchemeType.ApiKey,
                        1 => Microsoft.OpenApi.Models.SecuritySchemeType.Http,
                        2 => Microsoft.OpenApi.Models.SecuritySchemeType.OAuth2,
                        3 => Microsoft.OpenApi.Models.SecuritySchemeType.OpenIdConnect,
                        _ => Microsoft.OpenApi.Models.SecuritySchemeType.ApiKey
                    };
                    break;

                case "ApiKeyName":
                    securityScheme.Name = namedArg.Value.Value?.ToString();
                    break;

                case "ApiKeyLocation":
                    var locationValue = (int)(namedArg.Value.Value ?? 0);
                    securityScheme.In = locationValue switch
                    {
                        0 => ParameterLocation.Header,
                        1 => ParameterLocation.Query,
                        2 => ParameterLocation.Cookie,
                        _ => ParameterLocation.Header
                    };
                    break;

                case "Description":
                    securityScheme.Description = namedArg.Value.Value?.ToString();
                    break;

                case "HttpScheme":
                    securityScheme.Scheme = namedArg.Value.Value?.ToString();
                    break;

                case "BearerFormat":
                    securityScheme.BearerFormat = namedArg.Value.Value?.ToString();
                    break;

                case "OpenIdConnectUrl":
                    var openIdUrl = namedArg.Value.Value?.ToString();
                    if (!string.IsNullOrEmpty(openIdUrl) && Uri.TryCreate(openIdUrl, UriKind.Absolute, out var openIdUri))
                    {
                        securityScheme.OpenIdConnectUrl = openIdUri;
                    }
                    break;
            }
        }

        if (securityScheme.Type == Microsoft.OpenApi.Models.SecuritySchemeType.OAuth2)
        {
            ConfigureOAuth2Flows(attribute, securityScheme);
        }

        return (schemeId, securityScheme);
    }

    private void ConfigureOAuth2Flows(AttributeData attribute, OpenApiSecurityScheme securityScheme)
    {
        var authUrl = GetNamedArgumentValue(attribute, "AuthorizationUrl");
        var tokenUrl = GetNamedArgumentValue(attribute, "TokenUrl");
        var scopesStr = GetNamedArgumentValue(attribute, "Scopes");
        var scopes = ParseScopes(scopesStr);

        securityScheme.Flows = new OpenApiOAuthFlows();

        if (!string.IsNullOrEmpty(authUrl) && !string.IsNullOrEmpty(tokenUrl))
        {
            securityScheme.Flows.AuthorizationCode = new OpenApiOAuthFlow
            {
                AuthorizationUrl = Uri.TryCreate(authUrl, UriKind.Absolute, out var aUri) ? aUri : null,
                TokenUrl = Uri.TryCreate(tokenUrl, UriKind.Absolute, out var tUri) ? tUri : null,
                Scopes = scopes
            };
        }
        else if (!string.IsNullOrEmpty(tokenUrl))
        {
            securityScheme.Flows.ClientCredentials = new OpenApiOAuthFlow
            {
                TokenUrl = Uri.TryCreate(tokenUrl, UriKind.Absolute, out var tUri) ? tUri : null,
                Scopes = scopes
            };
        }
        else if (!string.IsNullOrEmpty(authUrl))
        {
            securityScheme.Flows.Implicit = new OpenApiOAuthFlow
            {
                AuthorizationUrl = Uri.TryCreate(authUrl, UriKind.Absolute, out var aUri) ? aUri : null,
                Scopes = scopes
            };
        }
    }

    private string? GetNamedArgumentValue(AttributeData attribute, string name)
    {
        foreach (var namedArg in attribute.NamedArguments)
        {
            if (namedArg.Key == name)
                return namedArg.Value.Value?.ToString();
        }
        return null;
    }

    private Dictionary<string, string> ParseScopes(string? scopesStr)
    {
        var scopes = new Dictionary<string, string>();
        if (string.IsNullOrEmpty(scopesStr))
            return scopes;

        var pairs = scopesStr.Split(',');
        foreach (var pair in pairs)
        {
            var parts = pair.Split(new[] { ':' }, 2);
            if (parts.Length == 2)
            {
                scopes[parts[0].Trim()] = parts[1].Trim();
            }
            else if (parts.Length == 1 && !string.IsNullOrWhiteSpace(parts[0]))
            {
                scopes[parts[0].Trim()] = parts[0].Trim();
            }
        }
        return scopes;
    }

    private void AddSecurityRequirement(OpenApiOperation operation, EndpointInfo endpoint)
    {
        var securityRequirements = new List<OpenApiSecurityRequirement>();

        if (RequiresOAuth(endpoint))
        {
            securityRequirements.Add(new OpenApiSecurityRequirement
            {
                {
                    new OpenApiSecurityScheme
                    {
                        Reference = new OpenApiReference { Type = ReferenceType.SecurityScheme, Id = "oauth2" }
                    },
                    GetRequiredScopes(endpoint)
                }
            });
        }

        if (RequiresApiKey(endpoint))
        {
            securityRequirements.Add(new OpenApiSecurityRequirement
            {
                {
                    new OpenApiSecurityScheme
                    {
                        Reference = new OpenApiReference { Type = ReferenceType.SecurityScheme, Id = "apiKey" }
                    },
                    Array.Empty<string>()
                }
            });
        }

        if (securityRequirements.Any())
        {
            operation.Security = securityRequirements;
        }
    }

    private bool RequiresOAuth(EndpointInfo endpoint) =>
        endpoint.RequiresAuthorization && endpoint.SecuritySchemes?.Any() == true;

    private bool RequiresApiKey(EndpointInfo endpoint) => endpoint.RequiresApiKey;

    private string[] GetRequiredScopes(EndpointInfo endpoint)
    {
        var scopes = new List<string>();

        var authorizeAttr = endpoint.MethodSymbol?.GetAttributes()
            .FirstOrDefault(attr => attr.AttributeClass?.Name == "AuthorizeAttribute");

        if (authorizeAttr != null)
        {
            foreach (var namedArg in authorizeAttr.NamedArguments)
            {
                if (namedArg.Key == "Roles" && namedArg.Value.Value is string roles)
                {
                    scopes.AddRange(roles.Split(',').Select(r => r.Trim()));
                }
            }
        }

        return scopes.Any() ? scopes.ToArray() : new[] { "read" };
    }
}
