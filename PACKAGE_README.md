# Oproto Lambda OpenAPI

<p align="center">
  <img src="https://raw.githubusercontent.com/oproto/lambda-openapi/main/docs/assets/logo.svg" alt="Oproto Lambda OpenAPI" width="400">
</p>

A .NET source generator that automatically creates OpenAPI specifications from AWS Lambda functions decorated with Lambda Annotations.

## Features

- 🚀 **Source Generator**: Compile-time OpenAPI spec generation
- 🏷️ **Attribute-Based**: Simple attribute decoration for API documentation
- 🔧 **MSBuild Integration**: Seamless integration with your build process
- 📝 **AWS Lambda Support**: Designed specifically for Lambda Annotations
- 🎯 **Type-Safe**: Leverages C# type system for accurate schemas
- ⚡ **AOT Compatible**: Works with Native AOT compilation

## Requirements

- .NET 6.0 or later (compatible with .NET 6, 7, 8, 9, 10+)
- [AWS Lambda Annotations](https://github.com/aws/aws-lambda-dotnet/tree/master/Libraries/src/Amazon.Lambda.Annotations) package

## Quick Start

1. Install the package:

```bash
dotnet add package Oproto.Lambda.OpenApi
```

2. Decorate your Lambda functions with OpenAPI attributes:

```csharp
using Oproto.Lambda.OpenApi.Attributes;

[LambdaFunction]
[OpenApiOperation("GetUser", "Retrieves user information")]
[OpenApiTag("Users")]
public async Task<APIGatewayProxyResponse> GetUser(
    [FromRoute] string userId,
    [FromQuery] bool includeDetails = false)
{
    // Your implementation
}
```

3. Build your project - the OpenAPI spec will be generated automatically as `openapi.json`

## Documentation

- [Getting Started Guide](https://github.com/oproto/lambda-openapi/blob/main/docs/getting-started.md)
- [Attribute Reference](https://github.com/oproto/lambda-openapi/blob/main/docs/attributes.md)
- [Configuration Options](https://github.com/oproto/lambda-openapi/blob/main/docs/configuration.md)
- [Examples](https://github.com/oproto/lambda-openapi/tree/main/Oproto.Lambda.OpenApi.Examples)
- [Changelog](https://github.com/oproto/lambda-openapi/blob/main/CHANGELOG.md)

## Links

- [GitHub Repository](https://github.com/oproto/lambda-openapi)
- [Issue Tracker](https://github.com/oproto/lambda-openapi/issues)

## License

MIT License - see [LICENSE](https://github.com/oproto/lambda-openapi/blob/main/LICENSE) for details.

---

**Built with ❤️ by [Oproto Inc](https://oproto.com) for the .NET and AWS communities**
