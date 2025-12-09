using Microsoft.OpenApi.Models;

namespace Oproto.Lambda.OpenApi.SourceGenerator;

public partial class OpenApiSpecGenerator
{
    // First, let's define possible security schemes
    private void AddSecurityDefinitions(OpenApiDocument document)
    {
        document.Components.SecuritySchemes = new Dictionary<string, OpenApiSecurityScheme>
        {
            ["oauth2"] = new()
            {
                Type = SecuritySchemeType.OAuth2,
                Flows = new OpenApiOAuthFlows
                {
                    AuthorizationCode = new OpenApiOAuthFlow
                    {
                        AuthorizationUrl = new Uri("https://auth.example.com/oauth2/authorize"),
                        TokenUrl = new Uri("https://auth.example.com/oauth2/token"),
                        Scopes = new Dictionary<string, string>
                        {
                            ["read"] = "Read access", ["write"] = "Write access"
                        }
                    }
                }
            },
            ["apiKey"] = new()
            {
                Type = SecuritySchemeType.ApiKey, Name = "x-api-key", In = ParameterLocation.Header
            }
        };
    }

    // Then, modify AddSecurityRequirement to handle different security types
    private void AddSecurityRequirement(OpenApiOperation operation, EndpointInfo endpoint)
    {
        Console.WriteLine($"Adding security requirements for endpoint: {endpoint.Route}");
        Console.WriteLine($"RequiresApiKey: {endpoint.RequiresApiKey}");
        Console.WriteLine($"RequiresOAuth: {RequiresOAuth(endpoint)}");
        var securityRequirements = new List<OpenApiSecurityRequirement>();

        // Check for authorization requirements based on attributes or other properties
        if (RequiresOAuth(endpoint))
        {
            Console.WriteLine("Adding OAuth security requirement");
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

        Console.WriteLine("Adding API Key security requirement");
        if (RequiresApiKey(endpoint))
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

        Console.WriteLine($"Total security requirements: {securityRequirements.Count}");
        if (securityRequirements.Any())
        {
            Console.WriteLine("Setting operation.Security");
            operation.Security = securityRequirements;
        }
    }

    private bool RequiresOAuth(EndpointInfo endpoint) =>
        endpoint.RequiresAuthorization && endpoint.SecuritySchemes?.Any() == true;

    private bool RequiresApiKey(EndpointInfo endpoint) => endpoint.RequiresApiKey;

    private string[] GetRequiredScopes(EndpointInfo endpoint)
    {
        // Extract required scopes from attributes or other metadata
        var scopes = new List<string>();

        // Example: Check authorize attribute for policy or roles
        var authorizeAttr = endpoint.MethodSymbol.GetAttributes()
            .FirstOrDefault(attr => attr.AttributeClass?.Name == "AuthorizeAttribute");

        if (authorizeAttr != null)
            // Check for roles or policies in the attribute
            foreach (var namedArg in authorizeAttr.NamedArguments)
                if (namedArg.Key == "Roles" && namedArg.Value.Value is string roles)
                    scopes.AddRange(roles.Split(',').Select(r => r.Trim()));

        // Add other authorization schemes as needed
        return scopes.Any() ? scopes.ToArray() : new[] { "read" }; // Default to "read" if no specific scopes
    }
}
