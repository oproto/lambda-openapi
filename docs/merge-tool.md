# OpenAPI Merge Tool

The OpenAPI Merge Tool enables merging multiple OpenAPI specification files from different microservices into a single unified specification. This is ideal for organizations with distributed API architectures where each service generates its own OpenAPI spec, but a combined specification is needed for documentation portals, client SDK generation, or API gateway configuration.

## Installation

### Global Tool

Install the merge tool globally to use it from any directory:

```bash
dotnet tool install -g Oproto.Lambda.OpenApi.Merge.Tool
```

After installation, run the tool using:

```bash
dotnet openapi-merge merge [options] [files...]
```

### Local Tool

Install as a local tool for project-specific usage:

```bash
# Create a tool manifest if you don't have one
dotnet new tool-manifest

# Install the tool locally
dotnet tool install Oproto.Lambda.OpenApi.Merge.Tool
```

Run the local tool using:

```bash
dotnet tool run openapi-merge merge [options] [files...]
```

### Library Package

For programmatic usage, install the core library:

```bash
dotnet add package Oproto.Lambda.OpenApi.Merge
```

## CLI Usage

The merge tool supports two modes of operation: direct invocation with command-line arguments, or configuration file-based merging.

### Direct Invocation

Merge multiple OpenAPI files directly from the command line:

```bash
dotnet openapi-merge merge --title "My API" --version "1.0.0" -o merged.json api1.json api2.json api3.json
```

### Configuration File

Use a JSON configuration file for more complex merge scenarios:

```bash
dotnet openapi-merge merge --config merge.config.json
```

### Command Options

| Option | Short | Description |
|--------|-------|-------------|
| `--config` | | Path to merge configuration JSON file |
| `--output` | `-o` | Output file path (default: `merged-openapi.json`) |
| `--title` | | API title for merged specification |
| `--version` | | API version for merged specification |
| `--schema-conflict` | | Strategy for handling schema conflicts: `rename`, `first-wins`, or `fail` (default: `rename`) |
| `--verbose` | `-v` | Show detailed progress and warnings |

### Examples

```bash
# Basic merge with required metadata
dotnet openapi-merge merge --title "Platform API" --version "2.0.0" -o platform-api.json \
  users-api.json products-api.json orders-api.json

# Merge with verbose output
dotnet openapi-merge merge -v --title "My API" --version "1.0.0" api1.json api2.json

# Use first-wins strategy for schema conflicts
dotnet openapi-merge merge --schema-conflict first-wins --title "API" --version "1.0" api1.json api2.json
```

## Configuration File Format

The configuration file provides full control over the merge process, including path prefixes and operation ID prefixes for each source.

### Schema

```json
{
  "info": {
    "title": "string (required)",
    "version": "string (required)",
    "description": "string (optional)"
  },
  "servers": [
    {
      "url": "string (required)",
      "description": "string (optional)"
    }
  ],
  "sources": [
    {
      "path": "string (required)",
      "pathPrefix": "string (optional)",
      "operationIdPrefix": "string (optional)",
      "name": "string (optional)"
    }
  ],
  "output": "string (required)",
  "schemaConflict": "rename | first-wins | fail (optional, default: rename)"
}
```

### Example Configuration

```json
{
  "info": {
    "title": "Acme Platform API",
    "version": "1.0.0",
    "description": "Unified API documentation for all Acme services"
  },
  "servers": [
    { "url": "https://api.acme.com", "description": "Production" },
    { "url": "https://staging-api.acme.com", "description": "Staging" }
  ],
  "sources": [
    { 
      "path": "./services/users/openapi.json", 
      "pathPrefix": "/users",
      "operationIdPrefix": "users_",
      "name": "Users Service" 
    },
    { 
      "path": "./services/products/openapi.json", 
      "pathPrefix": "/products",
      "operationIdPrefix": "products_",
      "name": "Products Service" 
    },
    { 
      "path": "./services/orders/openapi.json", 
      "pathPrefix": "/orders",
      "name": "Orders Service" 
    }
  ],
  "output": "./merged-openapi.json",
  "schemaConflict": "rename"
}
```

### Configuration Properties

#### info (required)

Metadata for the merged specification:

- `title` (required): The title of the merged API
- `version` (required): The version of the merged API
- `description` (optional): A description of the merged API

#### servers (optional)

Array of server definitions for the merged specification. Source servers are ignored; only configured servers appear in the output.

#### sources (required)

Array of source specifications to merge:

- `path` (required): File path to the OpenAPI specification (relative to config file or absolute)
- `pathPrefix` (optional): Prefix to prepend to all paths from this source (e.g., `/users`)
- `operationIdPrefix` (optional): Prefix to prepend to all operationIds from this source (e.g., `users_`)
- `name` (optional): Friendly name for this source, used in warnings and errors (defaults to filename)

#### output (required)

File path for the merged specification output.

#### schemaConflict (optional)

Strategy for handling schema naming conflicts. Default is `rename`.

## Schema Conflict Strategies

When multiple source specifications define schemas with the same name, the merge tool uses one of three strategies to resolve the conflict:

### rename (default)

When two sources define schemas with the same name but different structures, the conflicting schema is renamed using the source name as a prefix. All `$ref` references are automatically updated.

**Example**: If both `users-api.json` and `products-api.json` define a `Response` schema with different structures:
- First schema remains as `Response`
- Second schema becomes `Products_Response`
- All references in the products API are updated to `#/components/schemas/Products_Response`

### first-wins

The first schema encountered is kept, and subsequent schemas with the same name are ignored. A warning is generated for each ignored schema.

**Use case**: When you know schemas with the same name are intentionally identical across services.

### fail

The merge operation fails with an error when schema conflicts are detected.

**Use case**: When you want strict control and prefer to manually resolve conflicts.

## Merge Behavior

### Paths

- All paths from all sources are combined into the merged specification
- Path prefixes are applied if configured
- Duplicate paths (after prefix application) generate a warning and the duplicate is skipped

### Schemas

- Schemas with identical names and structures are deduplicated (only one copy appears)
- Schemas with identical names but different structures are handled according to the conflict strategy
- All `$ref` references are updated when schemas are renamed

### Security Schemes

- All unique security schemes from all sources are combined
- Duplicate security scheme names keep the first definition

### Tags

- All unique tags from all sources are combined
- Tag definitions (descriptions, external docs) are preserved

### Servers

- Source server definitions are ignored
- Only servers defined in the configuration appear in the merged output

### Operation IDs

- Operation ID prefixes are applied if configured
- Duplicate operation IDs generate a warning but are not automatically resolved

## Error Handling and Exit Codes

### Exit Codes

| Code | Meaning |
|------|---------|
| 0 | Success (may have warnings) |
| 1 | Configuration error (missing file, invalid JSON, missing required fields) |
| 2 | Merge error (schema conflict with `fail` strategy) |
| 3 | Input validation error (invalid OpenAPI spec) |

### Common Errors

#### Source file not found

```
Error: Source file not found: ./services/users/openapi.json
```

Ensure the path in your configuration is correct and the file exists.

#### Invalid JSON

```
Error: Failed to parse ./api.json: Unexpected character at position 42
```

The source file contains invalid JSON. Validate the file with a JSON linter.

#### Invalid OpenAPI specification

```
Error: ./api.json is not a valid OpenAPI specification: Missing required field 'info'
```

The source file is valid JSON but not a valid OpenAPI specification.

#### Missing configuration fields

```
Error: Configuration is missing required fields: info.title, info.version
```

Ensure your configuration file includes all required fields.

#### Schema conflict (with fail strategy)

```
Error: Schema conflict: 'Response' is defined differently in 'Users Service' and 'Products Service'
```

When using `--schema-conflict fail`, any schema naming conflict causes the merge to fail.

## Programmatic Usage

The core library can be used programmatically in your .NET applications:

```csharp
using Microsoft.OpenApi.Models;
using Microsoft.OpenApi.Readers;
using Oproto.Lambda.OpenApi.Merge;

// Create configuration
var config = new MergeConfiguration
{
    Info = new MergeInfoConfiguration
    {
        Title = "My API",
        Version = "1.0.0",
        Description = "Merged API specification"
    },
    Servers = new List<MergeServerConfiguration>
    {
        new() { Url = "https://api.example.com", Description = "Production" }
    },
    SchemaConflict = SchemaConflictStrategy.Rename
};

// Load source documents
var sources = new List<(SourceConfiguration, OpenApiDocument)>();
var reader = new OpenApiStreamReader();

foreach (var sourcePath in new[] { "api1.json", "api2.json" })
{
    using var stream = File.OpenRead(sourcePath);
    var doc = reader.Read(stream, out var diagnostic);
    
    sources.Add((
        new SourceConfiguration { Path = sourcePath, Name = Path.GetFileNameWithoutExtension(sourcePath) },
        doc
    ));
}

// Merge
var merger = new OpenApiMerger();
var result = merger.Merge(config, sources);

// Check for warnings
foreach (var warning in result.Warnings)
{
    Console.WriteLine($"Warning: {warning.Message}");
}

// Use the merged document
OpenApiDocument mergedDoc = result.Document;
```

## Best Practices

1. **Use meaningful source names**: Configure the `name` property for each source to make warnings and errors more readable.

2. **Apply path prefixes**: Use path prefixes to namespace APIs from different services and avoid path conflicts.

3. **Use operation ID prefixes**: Apply operation ID prefixes to ensure unique operation IDs across services, which is important for SDK generation.

4. **Start with rename strategy**: The `rename` strategy is the safest default, automatically handling conflicts while preserving all schemas.

5. **Review warnings**: Always check the output for warnings, especially when merging many specifications.

6. **Version control your config**: Keep your merge configuration file in version control alongside your source specifications.
