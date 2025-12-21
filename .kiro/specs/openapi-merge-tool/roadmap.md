# OpenAPI Merge Tool - Future Roadmap

This document captures potential future enhancements for the OpenAPI merge tool that are out of scope for the initial release.

## Potential Future Features

### Filtering Support

Allow filtering of paths and operations during merge:

```json
{
  "sources": [
    {
      "path": "api.json",
      "filter": {
        "excludeTags": ["internal", "admin"],
        "excludePaths": ["/internal/*", "/_health"],
        "includeTags": ["public"]
      }
    }
  ]
}
```

Use cases:
- Generate separate public vs internal API documentation
- Exclude health check and diagnostic endpoints from public docs
- Create filtered specs for different API consumers

### Tag Prefixing

Add prefix to all tags from a source specification:

```json
{
  "sources": [
    { "path": "users.json", "tagPrefix": "Users - " }
  ]
}
```

Use cases:
- Namespace tags when services have overlapping tag names
- Create hierarchical tag organization in merged docs

### S3-Triggered Lambda Deployment

Reactive merge triggered by S3 uploads:

```
┌─────────────────┐     ┌─────────────────┐
│ Service CI/CD   │────▶│ S3 Bucket       │
│ uploads spec    │     │ specs/*.json    │
└─────────────────┘     └────────┬────────┘
                                 │
                          S3 Event Trigger
                                 │
                                 ▼
                    ┌────────────────────────┐
                    │   Merge Lambda         │
                    │   reads merge.config   │
                    │   outputs merged.json  │
                    └────────────────────────┘
```

Use cases:
- Automatic merge when any service deploys
- Always up-to-date unified documentation
- No manual merge step in CI/CD

### Multiple Output Files

Generate multiple merged outputs with different configurations:

```json
{
  "outputs": [
    {
      "name": "public",
      "file": "openapi-public.json",
      "filter": { "excludeTags": ["internal"] }
    },
    {
      "name": "full",
      "file": "openapi-full.json"
    }
  ]
}
```

### Security Scheme Merging Strategies

More control over how security schemes are combined:

```json
{
  "securitySchemes": {
    "onConflict": "merge-scopes",
    "required": ["oauth2"]
  }
}
```

### Watch Mode

File watcher for development:

```bash
dotnet openapi-merge --config merge.config.json --watch
```

### Validation Mode

Validate merge configuration without producing output:

```bash
dotnet openapi-merge --config merge.config.json --validate
```

### Diff Mode

Show what would change in an existing merged file:

```bash
dotnet openapi-merge --config merge.config.json --diff
```

## Integration Ideas

### API Metadata Sidecar

Future integration with a broader API metadata format that bridges OpenAPI with AWS infrastructure:

```json
{
  "service": "tenants",
  "openApiSpec": "./openapi.json",
  "endpoints": [
    {
      "operationId": "getTenant",
      "lambda": {
        "handler": "Tenants::Tenants.Functions::GetTenant",
        "memorySize": 256
      },
      "apiGateway": {
        "authorizationType": "AWS_IAM"
      }
    }
  ]
}
```

This would enable:
- CDK construct generation from merged metadata
- API Gateway configuration automation
- Lambda integration wiring

### GraphQL Parallel

Similar merge tooling for the upcoming Oproto.Lambda.GraphQL project:
- Merge GraphQL schemas from multiple services
- Schema stitching / federation support
- Resolver configuration merging
