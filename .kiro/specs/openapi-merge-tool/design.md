# Design Document: OpenAPI Merge Tool

## Overview

This design introduces an OpenAPI merge tool to the Oproto.Lambda.OpenApi ecosystem. The tool enables merging multiple OpenAPI specification files from different microservices into a single unified specification. The implementation consists of two packages: a core merge library (`Oproto.Lambda.OpenApi.Merge`) for programmatic use and a .NET CLI tool (`Oproto.Lambda.OpenApi.Merge.Tool`) for command-line usage.

The merge tool uses the `Microsoft.OpenApi` library for parsing, manipulating, and serializing OpenAPI documents, ensuring standards compliance and proper handling of the OpenAPI object model.

## Architecture

```
┌─────────────────────────────────────────────────────────────────┐
│                    CLI Tool Layer                               │
│  Oproto.Lambda.OpenApi.Merge.Tool                               │
│  ├── Program.cs (entry point, argument parsing)                 │
│  └── Commands/MergeCommand.cs (orchestration)                   │
└─────────────────────────────────────────────────────────────────┘
                              │
                              ▼
┌─────────────────────────────────────────────────────────────────┐
│                    Core Library Layer                           │
│  Oproto.Lambda.OpenApi.Merge                                    │
│  ├── OpenApiMerger.cs (main merge orchestration)                │
│  ├── SchemaDeduplicator.cs (schema conflict handling)           │
│  ├── PathMerger.cs (path merging with prefix support)           │
│  ├── ComponentMerger.cs (security schemes, tags)                │
│  ├── MergeConfiguration.cs (configuration model)                │
│  └── MergeResult.cs (output with warnings)                      │
└─────────────────────────────────────────────────────────────────┘
                              │
                              ▼
┌─────────────────────────────────────────────────────────────────┐
│                    Microsoft.OpenApi                            │
│  ├── OpenApiDocument (document object model)                    │
│  ├── OpenApiStreamReader (parsing JSON/YAML)                    │
│  └── OpenApiJsonWriter (serialization)                          │
└─────────────────────────────────────────────────────────────────┘
```

## Components and Interfaces

### Core Library: Oproto.Lambda.OpenApi.Merge

#### MergeConfiguration

```csharp
namespace Oproto.Lambda.OpenApi.Merge;

/// <summary>
/// Configuration for merging multiple OpenAPI specifications.
/// </summary>
public class MergeConfiguration
{
    /// <summary>
    /// Info block for the merged specification.
    /// </summary>
    public MergeInfoConfiguration Info { get; set; } = new();
    
    /// <summary>
    /// Server definitions for the merged specification.
    /// </summary>
    public List<MergeServerConfiguration> Servers { get; set; } = new();
    
    /// <summary>
    /// Source specifications to merge.
    /// </summary>
    public List<SourceConfiguration> Sources { get; set; } = new();
    
    /// <summary>
    /// Output file path for the merged specification.
    /// </summary>
    public string Output { get; set; } = "merged-openapi.json";
    
    /// <summary>
    /// Strategy for handling schema naming conflicts.
    /// </summary>
    public SchemaConflictStrategy SchemaConflict { get; set; } = SchemaConflictStrategy.Rename;
}

public class MergeInfoConfiguration
{
    public string Title { get; set; }
    public string Version { get; set; }
    public string? Description { get; set; }
}

public class MergeServerConfiguration
{
    public string Url { get; set; }
    public string? Description { get; set; }
}

public enum SchemaConflictStrategy
{
    /// <summary>
    /// Rename conflicting schemas using source name as prefix.
    /// </summary>
    Rename,
    
    /// <summary>
    /// Keep the first schema encountered, ignore subsequent conflicts.
    /// </summary>
    FirstWins,
    
    /// <summary>
    /// Throw an exception on schema conflicts.
    /// </summary>
    Fail
}
```

#### SourceConfiguration

```csharp
namespace Oproto.Lambda.OpenApi.Merge;

/// <summary>
/// Configuration for a single source OpenAPI specification.
/// </summary>
public class SourceConfiguration
{
    /// <summary>
    /// File path to the OpenAPI specification.
    /// </summary>
    public string Path { get; set; }
    
    /// <summary>
    /// Optional prefix to prepend to all paths from this source.
    /// </summary>
    public string? PathPrefix { get; set; }
    
    /// <summary>
    /// Optional prefix to prepend to all operationIds from this source.
    /// </summary>
    public string? OperationIdPrefix { get; set; }
    
    /// <summary>
    /// Friendly name for this source (used in warnings/errors).
    /// Defaults to filename if not specified.
    /// </summary>
    public string? Name { get; set; }
}
```

#### OpenApiMerger

```csharp
namespace Oproto.Lambda.OpenApi.Merge;

using Microsoft.OpenApi.Models;

/// <summary>
/// Merges multiple OpenAPI documents into a single unified specification.
/// </summary>
public class OpenApiMerger
{
    /// <summary>
    /// Merge multiple OpenAPI documents based on configuration.
    /// </summary>
    /// <param name="config">Merge configuration</param>
    /// <param name="documents">Source documents with their configurations</param>
    /// <returns>Merge result containing the merged document and any warnings</returns>
    public MergeResult Merge(
        MergeConfiguration config, 
        IEnumerable<(SourceConfiguration Source, OpenApiDocument Document)> documents);
}
```

#### MergeResult

```csharp
namespace Oproto.Lambda.OpenApi.Merge;

using Microsoft.OpenApi.Models;

/// <summary>
/// Result of a merge operation.
/// </summary>
public class MergeResult
{
    /// <summary>
    /// The merged OpenAPI document.
    /// </summary>
    public OpenApiDocument Document { get; init; }
    
    /// <summary>
    /// Warnings generated during the merge process.
    /// </summary>
    public IReadOnlyList<MergeWarning> Warnings { get; init; } = Array.Empty<MergeWarning>();
    
    /// <summary>
    /// Whether the merge completed successfully (may still have warnings).
    /// </summary>
    public bool Success { get; init; }
}

public class MergeWarning
{
    public MergeWarningType Type { get; init; }
    public string Message { get; init; }
    public string? SourceName { get; init; }
}

public enum MergeWarningType
{
    PathConflict,
    SchemaConflict,
    SchemaRenamed,
    OperationIdConflict,
    SecuritySchemeConflict
}
```

#### SchemaDeduplicator

```csharp
namespace Oproto.Lambda.OpenApi.Merge;

using Microsoft.OpenApi.Models;

/// <summary>
/// Handles schema deduplication and conflict resolution during merge.
/// </summary>
internal class SchemaDeduplicator
{
    private readonly SchemaConflictStrategy _strategy;
    private readonly Dictionary<string, (OpenApiSchema Schema, string SourceName)> _schemas = new();
    private readonly Dictionary<string, Dictionary<string, string>> _renames = new();
    
    public SchemaDeduplicator(SchemaConflictStrategy strategy);
    
    /// <summary>
    /// Add a schema, handling conflicts according to the configured strategy.
    /// </summary>
    /// <returns>The final schema name and any warning generated</returns>
    public (string FinalName, MergeWarning? Warning) AddSchema(
        string name, 
        OpenApiSchema schema, 
        string sourceName);
    
    /// <summary>
    /// Get all schema renames for a source (for updating $ref references).
    /// </summary>
    public IReadOnlyDictionary<string, string> GetRenames(string sourceName);
    
    /// <summary>
    /// Get all deduplicated schemas.
    /// </summary>
    public IReadOnlyDictionary<string, OpenApiSchema> GetSchemas();
    
    /// <summary>
    /// Check if two schemas are structurally equivalent.
    /// </summary>
    internal static bool AreStructurallyEqual(OpenApiSchema a, OpenApiSchema b);
}
```

#### PathMerger

```csharp
namespace Oproto.Lambda.OpenApi.Merge;

using Microsoft.OpenApi.Models;

/// <summary>
/// Handles path merging with prefix support and conflict detection.
/// </summary>
internal class PathMerger
{
    private readonly OpenApiPaths _paths = new();
    private readonly List<MergeWarning> _warnings = new();
    
    /// <summary>
    /// Add paths from a source document.
    /// </summary>
    public void AddPaths(
        OpenApiPaths sourcePaths, 
        SourceConfiguration sourceConfig,
        IReadOnlyDictionary<string, string> schemaRenames);
    
    /// <summary>
    /// Get the merged paths.
    /// </summary>
    public OpenApiPaths GetPaths();
    
    /// <summary>
    /// Get warnings generated during path merging.
    /// </summary>
    public IReadOnlyList<MergeWarning> GetWarnings();
}
```

### CLI Tool: Oproto.Lambda.OpenApi.Merge.Tool

#### Program.cs

```csharp
using System.CommandLine;
using Oproto.Lambda.OpenApi.Merge.Tool.Commands;

var rootCommand = new RootCommand("OpenAPI specification merge tool");

rootCommand.AddCommand(new MergeCommand());

return await rootCommand.InvokeAsync(args);
```

#### MergeCommand

```csharp
namespace Oproto.Lambda.OpenApi.Merge.Tool.Commands;

using System.CommandLine;

public class MergeCommand : Command
{
    public MergeCommand() : base("merge", "Merge multiple OpenAPI specifications")
    {
        // Config-based invocation
        var configOption = new Option<FileInfo?>(
            "--config",
            "Path to merge configuration JSON file");
        
        // Direct invocation options
        var outputOption = new Option<FileInfo>(
            new[] { "-o", "--output" },
            () => new FileInfo("merged-openapi.json"),
            "Output file path");
        
        var titleOption = new Option<string?>(
            "--title",
            "API title for merged specification");
        
        var versionOption = new Option<string?>(
            "--version",
            "API version for merged specification");
        
        var schemaConflictOption = new Option<SchemaConflictStrategy>(
            "--schema-conflict",
            () => SchemaConflictStrategy.Rename,
            "Strategy for handling schema conflicts");
        
        var verboseOption = new Option<bool>(
            new[] { "-v", "--verbose" },
            "Show detailed progress and warnings");
        
        // Positional argument for direct file list
        var filesArgument = new Argument<FileInfo[]>(
            "files",
            "OpenAPI specification files to merge")
        {
            Arity = ArgumentArity.ZeroOrMore
        };
        
        AddOption(configOption);
        AddOption(outputOption);
        AddOption(titleOption);
        AddOption(versionOption);
        AddOption(schemaConflictOption);
        AddOption(verboseOption);
        AddArgument(filesArgument);
        
        this.SetHandler(ExecuteAsync, configOption, outputOption, titleOption, 
            versionOption, schemaConflictOption, verboseOption, filesArgument);
    }
    
    private async Task<int> ExecuteAsync(
        FileInfo? config,
        FileInfo output,
        string? title,
        string? version,
        SchemaConflictStrategy schemaConflict,
        bool verbose,
        FileInfo[] files);
}
```

## Data Models

### Configuration File Schema (merge.config.json)

```json
{
  "$schema": "https://json-schema.org/draft/2020-12/schema",
  "type": "object",
  "required": ["info", "sources", "output"],
  "properties": {
    "info": {
      "type": "object",
      "required": ["title", "version"],
      "properties": {
        "title": { "type": "string" },
        "version": { "type": "string" },
        "description": { "type": "string" }
      }
    },
    "servers": {
      "type": "array",
      "items": {
        "type": "object",
        "required": ["url"],
        "properties": {
          "url": { "type": "string" },
          "description": { "type": "string" }
        }
      }
    },
    "sources": {
      "type": "array",
      "items": {
        "type": "object",
        "required": ["path"],
        "properties": {
          "path": { "type": "string" },
          "pathPrefix": { "type": "string" },
          "operationIdPrefix": { "type": "string" },
          "name": { "type": "string" }
        }
      }
    },
    "output": { "type": "string" },
    "schemaConflict": {
      "type": "string",
      "enum": ["rename", "first-wins", "fail"]
    }
  }
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
    { "path": "./tenants/openapi.json", "name": "Tenants" },
    { "path": "./users/openapi.json", "pathPrefix": "/admin", "operationIdPrefix": "admin_", "name": "Users" },
    { "path": "./contacts/openapi.json", "name": "Contacts" }
  ],
  "output": "./merged-openapi.json",
  "schemaConflict": "rename"
}
```


## Correctness Properties

*A property is a characteristic or behavior that should hold true across all valid executions of a system-essentially, a formal statement about what the system should do. Properties serve as the bridge between human-readable specifications and machine-verifiable correctness guarantees.*

### Property 1: Path Preservation
*For any* set of source OpenAPI documents, the merged document SHALL contain all paths from all sources (with any configured prefixes applied).
**Validates: Requirements 1.1, 1.2**

### Property 2: Schema Preservation
*For any* set of source OpenAPI documents with unique schema names, the merged document SHALL contain all schemas from all sources in the components/schemas section.
**Validates: Requirements 1.3**

### Property 3: Security Scheme Preservation
*For any* set of source OpenAPI documents, the merged document SHALL contain all unique security schemes from all sources.
**Validates: Requirements 1.4**

### Property 4: Tag Preservation
*For any* set of source OpenAPI documents, the merged document SHALL contain all unique tags from all sources.
**Validates: Requirements 1.5**

### Property 5: Server Override
*For any* merge configuration with servers defined, the merged document SHALL contain only the configured servers and none from source documents.
**Validates: Requirements 1.6, 5.2**

### Property 6: Path Prefix Application
*For any* source configuration with a pathPrefix, all paths from that source in the merged document SHALL start with that prefix.
**Validates: Requirements 2.1, 2.2**

### Property 7: Path Identity Without Prefix
*For any* source configuration without a pathPrefix, all paths from that source SHALL appear unchanged in the merged document.
**Validates: Requirements 2.3**

### Property 8: Path Conflict Warning
*For any* merge where two sources produce the same path (after prefix application), the merge result SHALL contain a warning identifying the conflict.
**Validates: Requirements 2.4, 8.1**

### Property 9: OperationId Prefix Application
*For any* source configuration with an operationIdPrefix, all operationIds from that source in the merged document SHALL start with that prefix.
**Validates: Requirements 3.1**

### Property 10: OperationId Identity Without Prefix
*For any* source configuration without an operationIdPrefix, all operationIds from that source SHALL appear unchanged in the merged document.
**Validates: Requirements 3.2**

### Property 11: OperationId Conflict Warning
*For any* merge where duplicate operationIds exist after merging, the merge result SHALL contain a warning identifying the conflict.
**Validates: Requirements 3.3, 8.3**

### Property 12: Schema Structural Deduplication
*For any* two sources defining schemas with the same name and identical structure, the merged document SHALL contain exactly one copy of that schema.
**Validates: Requirements 4.1**

### Property 13: Schema Rename on Conflict
*For any* two sources defining schemas with the same name but different structures (with rename strategy), the merged document SHALL contain both schemas with the conflicting one renamed using source name as prefix.
**Validates: Requirements 4.2, 8.2**

### Property 14: Schema First-Wins on Conflict
*For any* two sources defining schemas with the same name but different structures (with first-wins strategy), the merged document SHALL contain only the first schema and produce a warning.
**Validates: Requirements 4.3**

### Property 15: Schema Fail on Conflict
*For any* two sources defining schemas with the same name but different structures (with fail strategy), the merge operation SHALL throw an exception.
**Validates: Requirements 4.4**

### Property 16: Reference Rewriting
*For any* schema that is renamed during merge, all $ref references to that schema throughout the merged document SHALL be updated to the new name.
**Validates: Requirements 4.5**

### Property 17: Info Configuration
*For any* merge configuration with info properties, the merged document SHALL use those exact values for title, version, and description.
**Validates: Requirements 5.1**

## Error Handling

### Input Validation Errors

| Error Condition | Behavior |
|-----------------|----------|
| Source file not found | Throw `FileNotFoundException` with file path |
| Source file not valid JSON | Throw `JsonException` with parse error details |
| Source file not valid OpenAPI | Throw `OpenApiValidationException` with validation errors |
| Config file not found | Throw `FileNotFoundException` with file path |
| Config file missing required fields | Throw `ConfigurationException` listing missing fields |

### Merge Conflicts

| Conflict Type | Behavior |
|---------------|----------|
| Path conflict | Skip duplicate path, add warning to result |
| OperationId conflict | Keep both (may cause invalid spec), add warning |
| Schema conflict (rename) | Rename schema, update refs, add warning |
| Schema conflict (first-wins) | Keep first, add warning |
| Schema conflict (fail) | Throw `SchemaMergeException` |
| Security scheme conflict | Keep first, add warning |

### CLI Exit Codes

| Code | Meaning |
|------|---------|
| 0 | Success (may have warnings) |
| 1 | Configuration error (missing file, invalid JSON, missing required fields) |
| 2 | Merge error (schema conflict with fail strategy) |
| 3 | Input validation error (invalid OpenAPI spec) |

## Testing Strategy

### Unit Tests

Unit tests verify individual components in isolation:

- **MergeConfiguration**: Deserialization from JSON, default values, validation
- **SchemaDeduplicator**: Structural equality comparison, rename logic, reference tracking
- **PathMerger**: Prefix application, conflict detection
- **SourceConfiguration**: Path normalization, name defaulting

### Property-Based Tests

Using FsCheck for .NET property-based testing. Each property test runs minimum 100 iterations.

**Test Configuration:**
```csharp
[Property(MaxTest = 100)]
```

**Generators needed:**
- `OpenApiDocumentGenerator`: Generates valid OpenAPI documents with random paths, schemas, operations
- `SourceConfigurationGenerator`: Generates source configs with optional prefixes
- `MergeConfigurationGenerator`: Generates merge configs with various strategies

**Property tests to implement:**

1. **Path preservation property** - Tag: Feature: openapi-merge-tool, Property 1: Path Preservation
2. **Schema preservation property** - Tag: Feature: openapi-merge-tool, Property 2: Schema Preservation
3. **Path prefix application property** - Tag: Feature: openapi-merge-tool, Property 6: Path Prefix Application
4. **Schema structural equality property** - Tag: Feature: openapi-merge-tool, Property 12: Schema Structural Deduplication
5. **Schema rename property** - Tag: Feature: openapi-merge-tool, Property 13: Schema Rename on Conflict
6. **Reference rewriting property** - Tag: Feature: openapi-merge-tool, Property 16: Reference Rewriting

### Integration Tests

Integration tests verify end-to-end behavior:

- **Config-based merge**: Load config file, merge sources, verify output file
- **CLI direct invocation**: Pass files as arguments, verify merge
- **Error scenarios**: Missing files, invalid JSON, invalid OpenAPI
- **Warning output**: Verify warnings written to stderr

### Test Data

Create test fixtures in `Oproto.Lambda.OpenApi.Merge.Tests/TestData/`:

```
TestData/
├── valid/
│   ├── simple-api.json
│   ├── with-schemas.json
│   ├── with-security.json
│   └── with-tags.json
├── conflicts/
│   ├── duplicate-paths/
│   ├── duplicate-schemas-identical/
│   └── duplicate-schemas-different/
├── configs/
│   ├── basic.json
│   ├── with-prefixes.json
│   └── invalid-missing-info.json
└── invalid/
    ├── not-json.txt
    └── not-openapi.json
```
