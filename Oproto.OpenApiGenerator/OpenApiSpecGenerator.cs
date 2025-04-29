using System.Collections.Immutable;
using System.Diagnostics;
using System.Text.Json;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.OpenApi.Any;
using Microsoft.OpenApi.Models;
using Microsoft.OpenApi.Writers;

namespace Oproto.OpenApiGenerator;

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

        // Create a value provider for collecting OpenAPI specs
        var openApiSpecs = methodDeclarations.Combine(compilationProvider)
            .Select((tuple, _) =>
            {
                var (classInfo, compilation) = tuple;
                return GenerateForClass(classInfo.classDecl!, classInfo.semanticModel!);
            });

        // Register the final output that will merge and emit the attribute
        context.RegisterSourceOutput(
            openApiSpecs.Collect(), // Collect() will gather all results
            (spc, specs) =>
            {
                if (!specs.Any())
                    return;

                // Merge specs and generate final output
                var mergedDoc = MergeOpenApiDocs(specs);
                using var writer = new StringWriter();
                mergedDoc.SerializeAsV3(new OpenApiJsonWriter(writer));
                var mergedJson = writer.ToString();

                var source = $@"
using System;
using Oproto.OpenApi;

[assembly: OpenApiOutput(@""{EscapeString(mergedJson)}"", ""openapi.json"")]
";
                spc.AddSource("OpenApiOutput.g.cs", source);
            });
    }

    private OpenApiDocument? GenerateForClass(
        ClassDeclarationSyntax classDecl,
        SemanticModel semanticModel)
    {
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
        catch (Exception ex)
        {
            Debug.WriteLine($"Exception details: {ex}");
            Debug.WriteLine($"Exception trace: {ex.StackTrace}");
        }

        return null;
    }

    private OpenApiDocument MergeOpenApiDocs(ImmutableArray<OpenApiDocument> docs)
    {
        // Filter out nulls
        var validDocs = docs.Where(d => d != null).ToList();

        if (!validDocs.Any())
            // Return a minimal valid OpenAPI document if no valid docs
            return new OpenApiDocument
            {
                Info = new OpenApiInfo { Title = "API Documentation", Version = "1.0.0" },
                Paths = new OpenApiPaths()
            };

        if (validDocs.Count == 1)
            return validDocs[0];

        var mergedDoc = new OpenApiDocument
        {
            Info = new OpenApiInfo { Title = "API Documentation", Version = "1.0.0" }, Paths = new OpenApiPaths()
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
                    Debug.WriteLine($"Found HTTP method from enum: {methodName} -> {httpMethod}");
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

        Debug.WriteLine("Extracted method info:");
        Debug.WriteLine($"  HTTP Method: {httpMethod}");
        Debug.WriteLine($"  Route: {route}");
        Debug.WriteLine($"  API Type: {apiType}");

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


    private (string method, string route, ApiType apiType)? GetApiInfo(ISymbol methodSymbol)
    {
        // Check for HttpApi attribute
        var httpAttribute = methodSymbol.GetAttributes()
            .FirstOrDefault(a => a.AttributeClass?.Name == "HttpApiAttribute");

        if (httpAttribute != null)
            return (
                httpAttribute.ConstructorArguments[0].Value?.ToString() ?? "GET",
                httpAttribute.ConstructorArguments[1].Value?.ToString() ?? "/",
                ApiType.Http
            );

        // Check for RestApi attribute
        var restAttribute = methodSymbol.GetAttributes()
            .FirstOrDefault(a => a.AttributeClass?.Name == "RestApiAttribute");

        if (restAttribute != null)
            return (
                restAttribute.ConstructorArguments[0].Value?.ToString() ?? "GET",
                restAttribute.ConstructorArguments[1].Value?.ToString() ?? "/",
                ApiType.Rest
            );

        return null;
    }

    private LambdaClassInfo? GetLambdaClassInfo(GeneratorSyntaxContext context)
    {
        // Only process class declarations
        if (context.Node is not ClassDeclarationSyntax classDeclaration)
        {
            Debug.WriteLine("Not a class declaration");
            return null;
        }

        // Get the semantic model
        var semanticModel = context.SemanticModel;
        var classSymbol = ModelExtensions.GetDeclaredSymbol(semanticModel, classDeclaration);

        if (classSymbol == null)
        {
            Debug.WriteLine("Could not get class symbol");
            return null;
        }

        Debug.WriteLine($"Processing class: {classSymbol.Name}");

        // Find methods with [LambdaFunction] attribute
        var methods = classDeclaration.Members
            .OfType<MethodDeclarationSyntax>()
            .ToList();

        Debug.WriteLine($"Found {methods.Count} methods");

        var methodsWithAttributes = methods
            .Select(method => new
            {
                Method = method, Symbol = ModelExtensions.GetDeclaredSymbol(semanticModel, method) as IMethodSymbol
            })
            .Where(m => m.Symbol != null)
            .ToList();

        Debug.WriteLine($"Found {methodsWithAttributes.Count} methods with symbols");

        foreach (var m in methodsWithAttributes)
        {
            var attrs = m.Symbol!.GetAttributes().ToList();
            Debug.WriteLine($"Method {m.Method.Identifier.Text} has {attrs.Count} attributes:");
            foreach (var attr in attrs) Debug.WriteLine($"  - {attr.AttributeClass?.Name}");
        }

        var lambdaMethods = methodsWithAttributes
            .Where(m => m.Symbol!.GetAttributes()
                .Any(a => a.AttributeClass?.Name == "LambdaFunctionAttribute"))
            .ToList();

        Debug.WriteLine($"Found {lambdaMethods.Count} Lambda methods");

        if (!lambdaMethods.Any())
        {
            Debug.WriteLine("No Lambda methods found, returning null");
            return null;
        }

        var endpoints = lambdaMethods
            .Select(m =>
            {
                var apiInfo = GetApiInfo(m.Symbol!);
                if (apiInfo == null)
                {
                    Debug.WriteLine($"No API info found for method {m.Method.Identifier.Text}");
                    return null;
                }

                Debug.WriteLine($"Creating endpoint info for {m.Method.Identifier.Text}");
                return new EndpointInfo
                {
                    MethodName = m.Method.Identifier.Text,
                    HttpMethod = apiInfo.Value.method,
                    Route = apiInfo.Value.route,
                    Parameters = GetParameters(m.Symbol!),
                    ApiType = apiInfo.Value.apiType,
                    ReturnType = m.Symbol!.ReturnType,
                    MethodSymbol = m.Symbol,
                    SecuritySchemes = new List<string>(),
                    RequiresAuthorization = false
                };
            })
            .Where(e => e != null)
            .Select(e => e!)
            .ToList();

        if (!endpoints.Any())
        {
            Debug.WriteLine("No endpoints created, returning null");
            return null;
        }

        Debug.WriteLine($"Created {endpoints.Count} endpoints");
        return new LambdaClassInfo { ServiceName = classSymbol.Name, Endpoints = endpoints };
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
        var responses = new OpenApiResponses { ["200"] = CreateResponse(200, endpoint.ReturnType) };

        // Add error responses
        responses["400"] = CreateResponse(400);
        responses["401"] = CreateResponse(401);
        responses["403"] = CreateResponse(403);
        responses["500"] = CreateResponse(500);

        return responses;
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
