using System.Collections.Immutable;
using System.Diagnostics;
using System.Text.Json;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.OpenApi.Any;
using Microsoft.OpenApi.Models;
using Microsoft.OpenApi.Writers;

namespace Oproto.Lambda.OpenApi.SourceGenerator;

/// <summary>
///     A source generator that creates OpenAPI schemas from C# types using Roslyn.
///     This generator processes OpenApiSchema and OpenApiExample attributes to generate
///     appropriate schema definitions.
/// </summary>
/// <remarks>
///     The generator performs the following key functions:
///     - Processes Lambda functions to create OpenAPI specifications
///     - Handles different parameter sources (FromRoute, FromQuery, FromHeader)
///     - Generates nested object schemas with proper $ref references
///     - Excludes FromServices parameters
///     - Outputs specifications as assembly attributes
/// </remarks>
[Generator(LanguageNames.CSharp)]
public partial class OpenApiSpecGenerator : IIncrementalGenerator
{
    /// <summary>
    ///     Initializes the incremental source generator by setting up syntax providers and registering source outputs.
    /// </summary>
    /// <param name="context">The initialization context that provides access to compilation and syntax data.</param>
    /// <remarks>
    ///     This method performs the following steps:
    ///     1. Sets up a compilation provider
    ///     2. Creates a syntax provider to detect classes with Lambda attributes
    ///     3. Combines method declarations with compilation information
    ///     4. Registers source output for generating OpenAPI specifications
    /// </remarks>
    public void Initialize(IncrementalGeneratorInitializationContext context)
    {
        // Register compilation provider
        var compilationProvider = context.CompilationProvider;

        // Register syntax provider for methods with Lambda attributes
        var methodDeclarations = context.SyntaxProvider
            .CreateSyntaxProvider(
                (s, _) => s is ClassDeclarationSyntax,
                (ctx, _) =>
                {
                    var classDecl = (ClassDeclarationSyntax)ctx.Node;
                    var semanticModel = ctx.SemanticModel;

                    return HasLambdaAttribute(classDecl, semanticModel, context)
                        ? (classDecl, semanticModel)
                        : (null, null);
                })
            .Where(tuple => tuple.classDecl != null);

        // Create a value provider for collecting OpenAPI specs (includes compilation for later use)
        var openApiSpecs = methodDeclarations.Combine(compilationProvider)
            .Select((tuple, _) =>
            {
                var (classInfo, compilation) = tuple;
                return (Doc: GenerateForClass(classInfo.classDecl!, classInfo.semanticModel!, compilation), Compilation: compilation);
            });

        // Register the final output that will merge and emit the attribute
        context.RegisterSourceOutput(
            openApiSpecs.Collect(), // Collect() will gather all results
            (spc, specs) =>
            {
                if (!specs.Any())
                    return;

                // Get compilation from first spec (all should have same compilation)
                var compilation = specs.FirstOrDefault().Compilation;
                
                // Merge specs and apply assembly-level OpenApiInfo
                var mergedDoc = MergeOpenApiDocs(specs.Select(s => s.Doc).ToImmutableArray(), compilation);
                using var writer = new StringWriter();
                mergedDoc.SerializeAsV3(new OpenApiJsonWriter(writer));
                var mergedJson = writer.ToString();

                var source = $@"
using System;
using Oproto.Lambda.OpenApi.Attributes;

[assembly: OpenApiOutput(@""{EscapeString(mergedJson)}"", ""openapi.json"")]
";
                spc.AddSource("OpenApiOutput.g.cs", source);
            });
    }

    private OpenApiDocument? GenerateForClass(
        ClassDeclarationSyntax classDecl,
        SemanticModel semanticModel,
        Compilation compilation)
    {
        // Store compilation for use in security definitions
        _currentCompilation = compilation;
        
        var endpoints = new List<EndpointInfo>();

        // Get all method declarations in the class
        var methodDeclarations = classDecl.Members.OfType<MethodDeclarationSyntax>();

        foreach (var methodDecl in methodDeclarations)
        {
            var lambdaAttributes = methodDecl.AttributeLists
                .SelectMany(al => al.Attributes)
                .Where(attr => attr.Name.ToString() == "LambdaFunction" ||
                               attr.Name.ToString() == "LambdaFunctionAttribute");

            if (!lambdaAttributes.Any()) continue;

            // Get method symbol for more detailed analysis
            var methodSymbol = semanticModel.GetDeclaredSymbol(methodDecl);
            if (methodSymbol == null) continue;

            // Extract endpoint info from method
            var endpoint = ExtractEndpointInfo(methodDecl, methodSymbol, semanticModel);
            if (endpoint != null) endpoints.Add(endpoint);
        }

        // Skip if no endpoints were found
        if (!endpoints.Any()) return null;

        var classInfo = new LambdaClassInfo { ServiceName = classDecl.Identifier.ToString(), Endpoints = endpoints };

        try
        {
            var openApiDoc = GenerateOpenApiDocument(classInfo);

            return openApiDoc;
        }
        catch (Exception)
        {
            // Exception swallowed intentionally - source generators should not crash the build
        }

        return null;
    }

    private OpenApiDocument MergeOpenApiDocs(ImmutableArray<OpenApiDocument?> docs, Compilation? compilation)
    {
        // Filter out nulls
        var validDocs = docs.Where(d => d != null).ToList();
        
        // Get API info from assembly-level attribute
        var apiInfo = GetOpenApiInfoFromAssembly(compilation);

        if (!validDocs.Any())
            // Return a minimal valid OpenAPI document if no valid docs
            return new OpenApiDocument
            {
                Info = apiInfo,
                Paths = new OpenApiPaths()
            };

        if (validDocs.Count == 1)
        {
            validDocs[0]!.Info = apiInfo;
            return validDocs[0]!;
        }

        var mergedDoc = new OpenApiDocument
        {
            Info = apiInfo, 
            Paths = new OpenApiPaths()
        };

        foreach (var doc in validDocs)
        {
            // Merge paths
            if (doc.Paths != null)
                foreach (var path in doc.Paths)
                    if (!mergedDoc.Paths.ContainsKey(path.Key))
                        mergedDoc.Paths[path.Key] = path.Value;
                    else
                        // Merge operations for existing paths
                        foreach (var operation in path.Value.Operations)
                            mergedDoc.Paths[path.Key].Operations[operation.Key] = operation.Value;

            // Merge components
            if (doc.Components?.Schemas != null)
            {
                mergedDoc.Components ??= new OpenApiComponents();
                mergedDoc.Components.Schemas ??= new Dictionary<string, OpenApiSchema>();
                foreach (var schema in doc.Components.Schemas) mergedDoc.Components.Schemas[schema.Key] = schema.Value;
            }
        }

        return mergedDoc;
    }

    private static string EscapeString(string str) => str.Replace("\"", "\"\"");
    
    /// <summary>
    /// Reads the [OpenApiInfo] attribute from the assembly to get API title, version, and other metadata.
    /// </summary>
    private OpenApiInfo GetOpenApiInfoFromAssembly(Compilation? compilation)
    {
        var info = new OpenApiInfo
        {
            Title = "API Documentation",
            Version = "1.0.0"
        };
        
        if (compilation == null)
            return info;
            
        foreach (var attr in compilation.Assembly.GetAttributes())
        {
            if (attr.AttributeClass?.Name != "OpenApiInfoAttribute")
                continue;
                
            // Constructor args: (string title, string version = "1.0.0")
            if (attr.ConstructorArguments.Length > 0)
                info.Title = attr.ConstructorArguments[0].Value as string ?? info.Title;
            if (attr.ConstructorArguments.Length > 1)
                info.Version = attr.ConstructorArguments[1].Value as string ?? info.Version;
            
            // Named arguments
            foreach (var namedArg in attr.NamedArguments)
            {
                switch (namedArg.Key)
                {
                    case "Description":
                        info.Description = namedArg.Value.Value as string;
                        break;
                    case "TermsOfService":
                        var tosUrl = namedArg.Value.Value as string;
                        if (!string.IsNullOrEmpty(tosUrl))
                            info.TermsOfService = new Uri(tosUrl);
                        break;
                    case "ContactName":
                    case "ContactEmail":
                    case "ContactUrl":
                        info.Contact ??= new OpenApiContact();
                        if (namedArg.Key == "ContactName")
                            info.Contact.Name = namedArg.Value.Value as string;
                        else if (namedArg.Key == "ContactEmail")
                            info.Contact.Email = namedArg.Value.Value as string;
                        else if (namedArg.Key == "ContactUrl")
                        {
                            var contactUrl = namedArg.Value.Value as string;
                            if (!string.IsNullOrEmpty(contactUrl))
                                info.Contact.Url = new Uri(contactUrl);
                        }
                        break;
                    case "LicenseName":
                    case "LicenseUrl":
                        info.License ??= new OpenApiLicense();
                        if (namedArg.Key == "LicenseName")
                            info.License.Name = namedArg.Value.Value as string;
                        else if (namedArg.Key == "LicenseUrl")
                        {
                            var licenseUrl = namedArg.Value.Value as string;
                            if (!string.IsNullOrEmpty(licenseUrl))
                                info.License.Url = new Uri(licenseUrl);
                        }
                        break;
                }
            }
            
            break; // Only process first OpenApiInfo attribute
        }
        
        return info;
    }

    private EndpointInfo? ExtractEndpointInfo(
        MethodDeclarationSyntax methodDecl,
        IMethodSymbol methodSymbol,
        SemanticModel semanticModel)
    {
        // First check if it's a Lambda function
        var lambdaAttribute = methodDecl.AttributeLists
            .SelectMany(al => al.Attributes)
            .FirstOrDefault(attr => attr.Name.ToString() == "LambdaFunction" ||
                                    attr.Name.ToString() == "LambdaFunctionAttribute");

        if (lambdaAttribute == null)
            return null;

        // Then look for HttpApi or RestApi attributes
        var httpApiAttribute = methodDecl.AttributeLists
            .SelectMany(al => al.Attributes)
            .FirstOrDefault(attr => attr.Name.ToString() == "HttpApi" ||
                                    attr.Name.ToString() == "HttpApiAttribute");

        var restApiAttribute = methodDecl.AttributeLists
            .SelectMany(al => al.Attributes)
            .FirstOrDefault(attr => attr.Name.ToString() == "RestApi" ||
                                    attr.Name.ToString() == "RestApiAttribute");

        // If neither HTTP nor REST API attribute is present, return null
        if (httpApiAttribute == null && restApiAttribute == null)
            return null;

        string? httpMethod = null;
        string? route = null;
        var apiType = ApiType.Http; // default

        if (httpApiAttribute?.ArgumentList != null)
        {
            var args = httpApiAttribute.ArgumentList.Arguments;
            if (args.Count >= 2)
            {
                // Extract HTTP method from enum
                if (args[0].Expression is MemberAccessExpressionSyntax memberAccess)
                {
                    var methodName = memberAccess.Name.Identifier.Text;
                    httpMethod = ConvertHttpMethod(methodName);
                }

                route = ExtractStringValue(args[1].Expression);
            }

            apiType = ApiType.Http;
        }
        else if (restApiAttribute?.ArgumentList != null)
        {
            var args = restApiAttribute.ArgumentList.Arguments;
            if (args.Count >= 2)
            {
                if (args[0].Expression is MemberAccessExpressionSyntax memberAccess)
                {
                    var methodName = memberAccess.Name.Identifier.Text;
                    httpMethod = ConvertHttpMethod(methodName);
                }

                route = ExtractStringValue(args[1].Expression);
            }

            apiType = ApiType.Rest;
        }

        // If we couldn't extract the required information, return null
        if (httpMethod == null || route == null)
            return null;

        // Create endpoint info
        var endpoint = new EndpointInfo
        {
            MethodName = methodDecl.Identifier.ToString(),
            HttpMethod = httpMethod,
            Route = route,
            Parameters = ExtractParameters(methodSymbol),
            ReturnType = methodSymbol.ReturnType,
            ApiType = apiType
        };

        return endpoint;
    }


    private string? ConvertHttpMethod(string enumValue)
    {
        return enumValue switch
        {
            "Get" => "GET",
            "Post" => "POST",
            "Put" => "PUT",
            "Delete" => "DELETE",
            "Patch" => "PATCH",
            "Head" => "HEAD",
            "Options" => "OPTIONS",
            _ => enumValue?.ToUpperInvariant()
        };
    }


    private string ExtractStringValue(ExpressionSyntax expression)
    {
        if (expression is LiteralExpressionSyntax literal) return literal.Token.ValueText;

        return null;
    }

    private List<ParameterInfo> ExtractParameters(IMethodSymbol methodSymbol)
    {
        var parameters = new List<ParameterInfo>();

        foreach (var parameter in methodSymbol.Parameters)
        {
            // Skip service parameters
            if (parameter.GetAttributes().Any(a => a.AttributeClass?.Name == "FromServicesAttribute"))
                continue;

            var fromRouteAttr = parameter.GetAttributes()
                .FirstOrDefault(a => a.AttributeClass?.Name == "FromRouteAttribute");
            var fromQueryAttr = parameter.GetAttributes()
                .FirstOrDefault(a => a.AttributeClass?.Name == "FromQueryAttribute");
            var fromHeaderAttr = parameter.GetAttributes()
                .FirstOrDefault(a => a.AttributeClass?.Name == "FromHeaderAttribute");
            var fromBodyAttr = parameter.GetAttributes()
                .FirstOrDefault(a => a.AttributeClass?.Name == "FromBodyAttribute");

            if (fromRouteAttr != null)
            {
                parameters.Add(new ParameterInfo
                {
                    Name = parameter.Name,
                    TypeSymbol = parameter.Type,
                    Source = ParameterSource.Path,
                    IsRequired = true
                });
            }
            else if (fromQueryAttr != null)
            {
                parameters.Add(new ParameterInfo
                {
                    Name = parameter.Name, TypeSymbol = parameter.Type, Source = ParameterSource.Query
                });
            }
            else if (fromHeaderAttr != null)
            {
                var headerName = fromHeaderAttr.NamedArguments
                    .FirstOrDefault(na => na.Key == "Name")
                    .Value.Value?.ToString() ?? parameter.Name;

                parameters.Add(new ParameterInfo
                {
                    Name = headerName, TypeSymbol = parameter.Type, Source = ParameterSource.Header
                });
            }
            else if (fromBodyAttr != null || IsComplexType(parameter.Type))
            {
                // Handle both explicit [FromBody] and implicit complex types
                parameters.Add(new ParameterInfo
                {
                    Name = parameter.Name, TypeSymbol = parameter.Type, Source = ParameterSource.Body
                });
            }
        }

        return parameters;
    }

    private bool IsComplexType(ITypeSymbol type)
    {
        // Check if it's a class (not string) or a collection type
        if (type.TypeKind == TypeKind.Class && type.SpecialType != SpecialType.System_String)
            return true;

        if (type.TypeKind == TypeKind.Array)
            return true;

        // Check for generic collections
        if (type is INamedTypeSymbol namedType && namedType.IsGenericType)
        {
            var genericTypeDef = namedType.ConstructedFrom;
            if (genericTypeDef.ToString().StartsWith("System.Collections"))
                return true;
        }

        return false;
    }

    /// <summary>
    /// Unwraps async return types (Task&lt;T&gt;, ValueTask&lt;T&gt;) to expose the inner type T.
    /// </summary>
    /// <param name="typeSymbol">The type symbol to unwrap.</param>
    /// <returns>
    /// The inner type T for Task&lt;T&gt; or ValueTask&lt;T&gt;,
    /// null for non-generic Task or ValueTask (indicating void/no content),
    /// or the original type if not an async wrapper.
    /// </returns>
    private ITypeSymbol? UnwrapAsyncType(ITypeSymbol? typeSymbol)
    {
        if (typeSymbol == null)
            return null;
            
        if (typeSymbol is INamedTypeSymbol namedType && namedType.IsGenericType)
        {
            // Check the original definition's name to identify Task<T> or ValueTask<T>
            var originalDef = namedType.OriginalDefinition;
            var fullName = originalDef.ToDisplayString();
            
            // Check for Task<T> or ValueTask<T>
            if (fullName == "System.Threading.Tasks.Task<TResult>" ||
                fullName == "System.Threading.Tasks.ValueTask<TResult>")
            {
                var innerType = namedType.TypeArguments[0];
                
                // Handle nested async types (Task<Task<T>> edge case)
                return UnwrapAsyncType(innerType) ?? innerType;
            }
        }
        
        // Check for non-generic Task/ValueTask
        var displayName = typeSymbol.ToDisplayString();
        if (displayName == "System.Threading.Tasks.Task" ||
            displayName == "System.Threading.Tasks.ValueTask")
        {
            return null; // Indicates void/no content
        }
        
        return typeSymbol;
    }


    private bool HasLambdaAttribute(ClassDeclarationSyntax classDecl, SemanticModel semanticModel,
        IncrementalGeneratorInitializationContext context)
    {
        return classDecl.Members.OfType<MethodDeclarationSyntax>()
            .Any(method =>
            {
                var methodSymbol = ModelExtensions.GetDeclaredSymbol(semanticModel, method);
                if (methodSymbol == null)
                    return false;

                var attributes = methodSymbol.GetAttributes();
                foreach (var attr in attributes)
                {
                    var attrName = attr.AttributeClass?.Name;
                    var fullName = attr.AttributeClass?.ToDisplayString();

                    if (attrName == "LambdaFunctionAttribute" ||
                        attrName == "LambdaFunction" ||
                        fullName == "Amazon.Lambda.Annotations.LambdaFunctionAttribute")
                        return true;
                }

                return false;
            });
    }

    public OpenApiDocument GenerateOpenApiDocument(LambdaClassInfo classInfo)
    {
        var document = new OpenApiDocument
        {
            Info = new OpenApiInfo { Title = classInfo.ServiceName, Version = "1.0" },
            Paths = new OpenApiPaths(),
            Components = _components
        };


        AddSecurityDefinitions(document);

        foreach (var endpoint in classInfo.Endpoints)
        {
            var path = new OpenApiPathItem();
            var operation = CreateOperation(endpoint);

            // Add security requirement to each operation
            AddSecurityRequirement(operation, endpoint);

            // Add API Gateway integration
            operation.Extensions.Add("x-amazon-apigateway-integration", new OpenApiObject
            {
                ["type"] = new OpenApiString("aws_proxy"),
                ["httpMethod"] = new OpenApiString("POST"),
                ["uri"] = new OpenApiString("${LambdaFunctionArn}"),
                ["payloadFormatVersion"] = new OpenApiString(
                    endpoint.ApiType == ApiType.Http ? "2.0" : "1.0")
            });

            // Add operation to path
            switch (endpoint.HttpMethod.ToUpperInvariant())
            {
                case "GET":
                    path.Operations[OperationType.Get] = operation;
                    break;
                case "POST":
                    path.Operations[OperationType.Post] = operation;
                    break;
                case "PUT":
                    path.Operations[OperationType.Put] = operation;
                    break;
                case "DELETE":
                    path.Operations[OperationType.Delete] = operation;
                    break;
                case "PATCH":
                    path.Operations[OperationType.Patch] = operation;
                    break;
                case "HEAD":
                    path.Operations[OperationType.Head] = operation;
                    break;
                case "OPTIONS":
                    path.Operations[OperationType.Options] = operation;
                    break;
            }

            // Add or merge the path into the document
            var routePath = endpoint.Route;
            if (!document.Paths.ContainsKey(routePath))
                document.Paths.Add(routePath, path);
            else
                // Merge operations if the path already exists
                foreach (var op in path.Operations)
                    document.Paths[routePath].Operations[op.Key] = op.Value;
        }

        return document;
    }


    private List<ParameterInfo> GetParameters(ISymbol methodSymbol)
    {
        // Need to cast to IMethodSymbol to get parameters
        if (methodSymbol is not IMethodSymbol method)
            return new List<ParameterInfo>();

        return method.Parameters
            .Select(p =>
            {
                var pi = new ParameterInfo
                {
                    Name = p.Name,
                    TypeSymbol = p.Type,
                    Source = GetParameterSource(p),
                    HasDefaultValue = p.HasExplicitDefaultValue
                };
                if (pi.HasDefaultValue)
                {
                    if (p.Type.IsValueType && p.ExplicitDefaultValue == null)
                        // For value types with default value but null ExplicitDefaultValue,
                        // we need to use the type's default value
                        pi.DefaultValue = GetDefaultValueForType(p.Type);
                    else
                        pi.DefaultValue = p.ExplicitDefaultValue!;
                }

                return pi;
            })
            .ToList();
    }

    private ParameterSource GetParameterSource(IParameterSymbol parameter)
    {
        var fromRoute = parameter.GetAttributes()
            .FirstOrDefault(a => a.AttributeClass?.Name == "FromRouteAttribute");
        if (fromRoute != null)
            return ParameterSource.Path;

        var fromQuery = parameter.GetAttributes()
            .FirstOrDefault(a => a.AttributeClass?.Name == "FromQueryAttribute");
        if (fromQuery != null)
            return ParameterSource.Query;

        var fromBody = parameter.GetAttributes()
            .FirstOrDefault(a => a.AttributeClass?.Name == "FromBodyAttribute");
        if (fromBody != null)
            return ParameterSource.Body;

        return ParameterSource.Header;
    }

    private OpenApiOperation CreateOperation(EndpointInfo endpoint)
    {
        if (endpoint == null) throw new ArgumentNullException(nameof(endpoint));

        var operation = new OpenApiOperation();

        try
        {
            operation.Summary = GetMethodSummary(endpoint);
            operation.Description = GetMethodDescription(endpoint);
            operation.Parameters = CreateParameters(endpoint.Parameters);
            operation.Responses = CreateResponses(endpoint);
            operation.Tags = CreateTags(endpoint);

            // Add request body for POST/PUT methods
            if (!string.IsNullOrEmpty(endpoint.HttpMethod) &&
                (endpoint.HttpMethod.Equals("POST", StringComparison.OrdinalIgnoreCase) ||
                 endpoint.HttpMethod.Equals("PUT", StringComparison.OrdinalIgnoreCase)))
                operation.RequestBody = CreateRequestBody(endpoint);
        }
        catch (Exception ex)
        {
            throw new InvalidOperationException(
                $"Error creating operation for endpoint: {endpoint.HttpMethod} - {ex.Message}", ex);
        }

        return operation;
    }

    private IList<OpenApiParameter> CreateParameters(List<ParameterInfo> parameters)
    {
        return parameters
            .Where(p => !p.IsFromBody) // Skip body parameters as they go in RequestBody
            .Select(CreateParameter)
            .ToList();
    }

    private string GetMethodSummary(EndpointInfo endpoint)
    {
        // Extract the first line or summary from XML documentation
        return endpoint.Documentation?.Summary ?? $"{endpoint.HttpMethod} {endpoint.MethodName}";
    }

    private string? GetMethodDescription(EndpointInfo endpoint)
    {
        // Extract the detailed description from XML documentation
        return endpoint.Documentation?.Description;
    }

    private List<OpenApiTag> CreateTags(EndpointInfo endpoint)
    {
        // You might want to group endpoints by controller/category
        return new List<OpenApiTag> { new() { Name = endpoint.ControllerName ?? "Default" } };
    }

    private OpenApiParameter CreateParameter(ParameterInfo param)
    {
        var parameter = new OpenApiParameter
        {
            Name = param.Name,
            In = param.IsFromRoute ? ParameterLocation.Path :
                param.IsFromQuery ? ParameterLocation.Query : ParameterLocation.Header,
            Required = param.IsFromRoute || param.IsRequired, // Path parameters are always required
            Schema = CreateSchema(param.TypeSymbol),
            Description =
                param?.Documentation?.ParameterDescriptions.GetValueOrDefault(param.Name) ??
                string.Empty // Add parameter documentation
        };

        if (param.HasDefaultValue) parameter.Schema.Default = new OpenApiString(param.DefaultValue?.ToString());

        // Optionally add default value if it exists
        if (param.DefaultValue != null) parameter.Schema.Default = new OpenApiString(param.DefaultValue.ToString());

        return parameter;
    }

    private OpenApiRequestBody CreateRequestBody(EndpointInfo endpoint)
    {
        var bodyParameter = endpoint.Parameters.FirstOrDefault(p => p.IsFromBody);
        if (bodyParameter == null)
            return null;

        return new OpenApiRequestBody
        {
            Required = true,
            Content = new Dictionary<string, OpenApiMediaType>
            {
                ["application/json"] = new() { Schema = CreateSchema(bodyParameter.TypeSymbol) }
            }
        };
    }

    private OpenApiResponses CreateResponses(EndpointInfo endpoint)
    {
        var responses = new OpenApiResponses();
        
        // Check for [OpenApiResponseType] attributes first
        var responseTypeAttributes = GetOpenApiResponseTypeAttributes(endpoint.MethodSymbol);
        
        if (responseTypeAttributes.Any())
        {
            // Use explicit response type attributes
            foreach (var (statusCode, responseType, description) in responseTypeAttributes)
            {
                var response = new OpenApiResponse 
                { 
                    Description = description ?? GetResponseDescription(statusCode) 
                };
                
                if (responseType != null)
                {
                    response.Content = new Dictionary<string, OpenApiMediaType>
                    {
                        ["application/json"] = new() { Schema = CreateSchema(responseType) }
                    };
                }
                
                responses[statusCode.ToString()] = response;
            }
        }
        else
        {
            // Unwrap async return types (Task<T>, ValueTask<T>) to get the actual response type
            var unwrappedReturnType = UnwrapAsyncType(endpoint.ReturnType);
            
            // Check if return type is IHttpResult or IResult (Lambda Annotations types)
            // These are infrastructure types that don't represent the actual response body
            var isHttpResultType = IsHttpResultType(unwrappedReturnType);
            
            if (unwrappedReturnType == null || isHttpResultType)
            {
                // No response body - use 204 No Content for void, or 200 with no schema for IHttpResult
                if (unwrappedReturnType == null)
                {
                    responses["204"] = CreateResponse(204);
                }
                else
                {
                    // IHttpResult - we don't know the actual response type, so omit the schema
                    responses["200"] = CreateResponse(200);
                }
            }
            else
            {
                responses["200"] = CreateResponse(200, unwrappedReturnType);
            }
        }

        // Add error responses if not already specified
        if (!responses.ContainsKey("400"))
            responses["400"] = CreateResponse(400);
        if (!responses.ContainsKey("401"))
            responses["401"] = CreateResponse(401);
        if (!responses.ContainsKey("403"))
            responses["403"] = CreateResponse(403);
        if (!responses.ContainsKey("500"))
            responses["500"] = CreateResponse(500);

        return responses;
    }
    
    /// <summary>
    /// Checks if the type is IHttpResult, IResult, or similar Lambda Annotations result types.
    /// </summary>
    private bool IsHttpResultType(ITypeSymbol? typeSymbol)
    {
        if (typeSymbol == null)
            return false;
            
        var typeName = typeSymbol.ToDisplayString();
        
        // Check for common Lambda Annotations result types
        return typeName.Contains("IHttpResult") ||
               typeName.Contains("Amazon.Lambda.Annotations.APIGateway.IHttpResult") ||
               typeName == "Amazon.Lambda.Annotations.APIGateway.HttpResults";
    }
    
    /// <summary>
    /// Extracts [OpenApiResponseType] attributes from a method.
    /// </summary>
    private List<(int StatusCode, ITypeSymbol? ResponseType, string? Description)> GetOpenApiResponseTypeAttributes(IMethodSymbol? methodSymbol)
    {
        var results = new List<(int, ITypeSymbol?, string?)>();
        
        if (methodSymbol == null)
            return results;
            
        foreach (var attr in methodSymbol.GetAttributes())
        {
            if (attr.AttributeClass?.Name != "OpenApiResponseTypeAttribute")
                continue;
                
            // Constructor args: (Type responseType, int statusCode = 200)
            var responseType = attr.ConstructorArguments.Length > 0 
                ? attr.ConstructorArguments[0].Value as ITypeSymbol 
                : null;
            var statusCode = attr.ConstructorArguments.Length > 1 
                ? (int)(attr.ConstructorArguments[1].Value ?? 200) 
                : 200;
            
            // Named argument: Description
            string? description = null;
            foreach (var namedArg in attr.NamedArguments)
            {
                if (namedArg.Key == "Description")
                    description = namedArg.Value.Value as string;
            }
            
            results.Add((statusCode, responseType, description));
        }
        
        return results;
    }

    private OpenApiResponse CreateResponse(int statusCode, ITypeSymbol? returnType = null)
    {
        var response = new OpenApiResponse { Description = GetResponseDescription(statusCode) };

        if (returnType != null && statusCode is 200 or 201)
            response.Content = new Dictionary<string, OpenApiMediaType>
            {
                ["application/json"] = new() { Schema = CreateSchema(returnType) }
            };

        // Add error response schema
        if (statusCode >= 400)
            response.Content = new Dictionary<string, OpenApiMediaType>
            {
                ["application/json"] = new()
                {
                    Schema = new OpenApiSchema
                    {
                        Type = "object",
                        Properties = new Dictionary<string, OpenApiSchema>
                        {
                            ["message"] = new() { Type = "string" },
                            ["errorCode"] = new() { Type = "string" }
                        }
                    }
                }
            };

        return response;
    }


    private string GetResponseDescription(int statusCode) => statusCode switch
    {
        200 => "Success",
        201 => "Created",
        204 => "No Content",
        400 => "Bad Request",
        401 => "Unauthorized",
        403 => "Forbidden",
        404 => "Not Found",
        500 => "Internal Server Error",
        _ => "Unknown"
    };

    private OpenApiSchema CreateComplexTypeSchema(ITypeSymbol typeSymbol)
    {
        var schema = new OpenApiSchema { Type = "object", Properties = new Dictionary<string, OpenApiSchema>() };

        if (typeSymbol is INamedTypeSymbol namedTypeSymbol)
            foreach (var member in namedTypeSymbol.GetMembers().OfType<IPropertySymbol>().Where(ShouldIncludeProperty))
            {
                // Check for OpenApiIgnore attribute first
                if (member.GetAttributes().Any(a =>
                        a.AttributeClass?.Name is "OpenApiIgnore" or "OpenApiIgnoreAttribute"))
                    continue;

                var propertySchema = CreateSchema(member.Type);
                if (propertySchema != null)
                {
                    // Get attributes from both the current property and its base if it's an override
                    var attributes = member.GetAttributes().ToList();
                    if (member.IsOverride && member.OverriddenProperty != null)
                        attributes.InsertRange(0, member.OverriddenProperty.GetAttributes());

                    // Apply any OpenApiSchema attributes to the property
                    var schemaAttribute = attributes
                        .FirstOrDefault(a => a.AttributeClass?.Name is "OpenApiSchema" or "OpenApiSchemaAttribute");

                    if (schemaAttribute != null)
                        foreach (var namedArg in schemaAttribute.NamedArguments)
                            switch (namedArg.Key)
                            {
                                case "Example":
                                    if (namedArg.Value.Value is string exampleValue &&
                                        !string.IsNullOrEmpty(exampleValue))
                                        try
                                        {
                                            using var doc = JsonDocument.Parse(exampleValue);
                                            // Convert JsonElement to appropriate OpenApi type
                                            propertySchema.Example = JsonElementToOpenApiAny(doc.RootElement);
                                        }
                                        catch
                                        {
                                            // Fallback to string if parsing fails
                                            propertySchema.Example = new OpenApiString(exampleValue);
                                        }

                                    break;
                                case "Description":
                                    propertySchema.Description = (string)namedArg.Value.Value;
                                    break;
                                case "MinLength":
                                    propertySchema.MinLength = (int)namedArg.Value.Value;
                                    break;
                                case "MaxLength":
                                    propertySchema.MaxLength = (int)namedArg.Value.Value;
                                    break;
                                case "Minimum":
                                    propertySchema.Minimum = Convert.ToDecimal(namedArg.Value.Value);
                                    break;
                                case "Maximum":
                                    propertySchema.Maximum = Convert.ToDecimal(namedArg.Value.Value);
                                    break;
                            }

                    schema.Properties[member.Name] = propertySchema;
                }
            }

        if (schema.Type == null) schema.Type = "object";

        return schema;
    }

    private bool ShouldIncludeProperty(IPropertySymbol property)
    {
        // Exclude compiler-generated properties
        if (property.IsImplicitlyDeclared ||
            property.Name.Equals("EqualityContract", StringComparison.OrdinalIgnoreCase))
            return false;

        // Add any other exclusions here

        return true;
    }
}
