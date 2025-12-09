# Oproto Lambda OpenAPI

[![Build Status](https://github.com/oproto/lambda-openapi/workflows/Build%20and%20Test/badge.svg)](https://github.com/oproto/lambda-openapi/actions)
[![NuGet](https://img.shields.io/nuget/v/Oproto.Lambda.OpenApi.svg)](https://www.nuget.org/packages/Oproto.Lambda.OpenApi/)
[![License: MIT](https://img.shields.io/badge/License-MIT-yellow.svg)](https://opensource.org/licenses/MIT)

A .NET source generator that automatically creates OpenAPI specifications from AWS Lambda functions decorated with Lambda Annotations.

## Features

- 🚀 **Source Generator**: Compile-time OpenAPI spec generation
- 🏷️ **Attribute-Based**: Simple attribute decoration for API documentation
- 🔧 **MSBuild Integration**: Seamless integration with your build process
- 📝 **AWS Lambda Support**: Designed specifically for Lambda Annotations
- 🎯 **Type-Safe**: Leverages C# type system for accurate schemas

## Installation

Install the NuGet package in your AWS Lambda project:

```bash
dotnet add package Oproto.Lambda.OpenApi
```

## Quick Start

1. Decorate your Lambda functions with OpenAPI attributes:

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

2. Build your project - the OpenAPI spec will be generated automatically as `openapi.json`

## Documentation

- [Getting Started Guide](docs/getting-started.md)
- [Attribute Reference](docs/attributes.md)
- [Configuration Options](docs/configuration.md)
- [Examples](examples/)

## Contributing

We welcome contributions! Please see our [Contributing Guide](CONTRIBUTING.md) for details.

## License

This project is licensed under the MIT License - see the [LICENSE](LICENSE) file for details.

## About

Oproto Lambda OpenAPI is developed and maintained by Oproto.

### Related Projects

- [AWS Lambda Annotations](https://github.com/aws/aws-lambda-dotnet) - AWS Lambda .NET SDK
- [Microsoft.OpenApi](https://github.com/microsoft/OpenAPI.NET) - OpenAPI SDK for .NET

### Links

- [GitHub Repository](https://github.com/oproto/lambda-openapi)
- [NuGet Package](https://www.nuget.org/packages/Oproto.Lambda.OpenApi/)
- [Issue Tracker](https://github.com/oproto/lambda-openapi/issues)

### Maintainer

This project is maintained by the Oproto team.
