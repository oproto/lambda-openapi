# Oproto Lambda OpenAPI

[![Build Status](https://github.com/oproto/lambda-openapi/workflows/Build%20and%20Test/badge.svg)](https://github.com/oproto/lambda-openapi/actions)
[![NuGet](https://img.shields.io/nuget/v/Oproto.Lambda.OpenApi.svg)](https://www.nuget.org/packages/Oproto.Lambda.OpenApi/)
[![License: MIT](https://img.shields.io/badge/License-MIT-yellow.svg)](https://opensource.org/licenses/MIT)
[![Sponsor](https://img.shields.io/badge/Sponsor-❤-ea4aaa)](https://github.com/sponsors/dguisinger)

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

The library targets `netstandard2.0` for maximum compatibility.

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
- [Examples](Oproto.Lambda.OpenApi.Examples/)
- [Changelog](CHANGELOG.md)

## About

Oproto Lambda OpenAPI is developed and maintained by [Oproto Inc](https://oproto.com), 
a company building modern SaaS solutions for small business finance and accounting.

### Related Projects

- [FluentDynamoDB](https://fluentdynamodb.dev)
- [LambdaGraphQL](https://lambdagraphql.dev)

### Links

- [GitHub Repository](https://github.com/oproto/lambda-openapi)
- [NuGet Package](https://www.nuget.org/packages/Oproto.Lambda.OpenApi/)
- [Issue Tracker](https://github.com/oproto/lambda-openapi/issues)

### Maintainer
- **Dan Guisinger** - [danguisinger.com](https://danguisinger.com)

## ❤️ Support the Project

Oproto maintains this library as part of a broader open-source ecosystem for building high-quality AWS-native .NET applications. If Lambda OpenAPI (or any Oproto library) saves you time or helps your team ship features faster, please consider supporting ongoing development.

Your support helps:
- Fund continued maintenance of the Oproto Lambda ecosystem
- Keep libraries AOT-compatible and aligned with new AWS features
- Improve documentation, samples, and test coverage
- Sustain long-term open-source availability

You can support the project in one of two ways:

**👉 [GitHub Sponsors](https://github.com/sponsors/dguisinger)** — Recurring support for those who want to help sustain long-term development.

**👉 [Buy Me a Coffee](https://buymeacoffee.com/danguisinger)** — A simple, one-time "thanks" for helping you ship faster.

Every bit of support helps keep the project healthy, actively maintained, and open for the community. Thank you!

## Contributing

We welcome contributions! Please see our [Contributing Guide](CONTRIBUTING.md) for details.

## Security

To report a security vulnerability, please see our [Security Policy](SECURITY.md).

## License

This project is licensed under the MIT License - see the [LICENSE](LICENSE) file for details.


---

**Built with ❤️ for the .NET and AWS communities**
