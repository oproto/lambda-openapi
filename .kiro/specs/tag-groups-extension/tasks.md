# Implementation Plan: Tag Groups Extension and Automatic Example Generation

## Overview

This implementation plan covers adding `x-tagGroups` extension support and automatic example generation to the Oproto.Lambda.OpenApi library. The work is organized into phases: attributes, source generator, merge tool, and testing.

## Tasks

- [x] 1. Create new attributes
  - [x] 1.1 Create `OpenApiTagGroupAttribute` in Oproto.Lambda.OpenApi/Attributes
    - Assembly-level attribute with `name` and `params string[] tags` constructor
    - AllowMultiple = true
    - _Requirements: 1.1, 1.2_
  - [x] 1.2 Create `OpenApiExampleConfigAttribute` in Oproto.Lambda.OpenApi/Attributes
    - Assembly-level attribute with `ComposeFromProperties` and `GenerateDefaults` properties
    - Default values: ComposeFromProperties = true, GenerateDefaults = false
    - _Requirements: 9.1, 9.4, 9.5_

- [x] 2. Implement tag groups in source generator
  - [x] 2.1 Add `TagGroupInfo` class to hold tag group data
    - Properties: Name (string), Tags (List<string>)
    - _Requirements: 1.2_
  - [x] 2.2 Implement `GetTagGroupsFromAssembly()` method
    - Read OpenApiTagGroupAttribute from compilation assembly
    - Extract name and tags from constructor arguments
    - Return list preserving attribute order
    - _Requirements: 1.2, 1.3, 1.4_
  - [x] 2.3 Implement `ApplyTagGroupsExtension()` method
    - Create OpenApiArray with tag group objects
    - Add to document.Extensions["x-tagGroups"]
    - Skip if no tag groups defined
    - _Requirements: 2.1, 2.2, 2.3, 2.4, 5.1, 5.2_
  - [x] 2.4 Integrate tag groups into `MergeOpenApiDocs()` method
    - Call GetTagGroupsFromAssembly and ApplyTagGroupsExtension
    - _Requirements: 2.1_
  - [x] 2.5 Write property tests for tag group generation
    - **Property 1: Tag group attribute parsing**
    - **Property 2: Tag group order preservation**
    - **Property 3: Tag group output presence**
    - **Property 4: Tag group JSON structure**
    - **Validates: Requirements 1.2, 1.3, 2.1, 2.2, 2.3, 2.4, 5.1, 5.2**

- [x] 3. Implement tag groups in merge tool
  - [x] 3.1 Implement `ReadTagGroupsExtension()` method in OpenApiMerger
    - Parse x-tagGroups extension from OpenApiDocument
    - Return list of TagGroupInfo objects
    - _Requirements: 5.3_
  - [x] 3.2 Implement `WriteTagGroupsExtension()` method in OpenApiMerger
    - Write tag groups to document.Extensions["x-tagGroups"]
    - Preserve order from input list
    - _Requirements: 5.3_
  - [x] 3.3 Implement `MergeTagGroups()` method in OpenApiMerger
    - Combine tag groups from all source documents
    - Merge same-named groups, deduplicate tags
    - Preserve order (first occurrence wins)
    - _Requirements: 3.1, 3.2, 3.3, 3.4, 4.1, 4.2, 4.3_
  - [x] 3.4 Integrate tag group merging into `Merge()` method
    - Call MergeTagGroups after other merge phases
    - _Requirements: 3.1_
  - [x] 3.5 Write property tests for tag group merging
    - **Property 5: Tag group merge combination**
    - **Property 6: Tag group merge same-name merging**
    - **Property 7: Tag group merge deduplication**
    - **Property 8: Tag group merge preservation**
    - **Property 9: Tag group merge order**
    - **Property 10: Tag group round-trip**
    - **Validates: Requirements 3.1, 3.2, 3.3, 3.4, 4.1, 4.2, 4.3, 5.3**

- [x] 4. Checkpoint - Tag groups complete
  - Ensure all tag group tests pass, ask the user if questions arise.

- [x] 5. Implement example composition in source generator
  - [x] 5.1 Add `ExampleConfig` class to hold configuration
    - Properties: ComposeFromProperties (bool), GenerateDefaults (bool)
    - _Requirements: 9.1_
  - [x] 5.2 Implement `GetExampleConfigFromAssembly()` method
    - Read OpenApiExampleConfigAttribute from compilation assembly
    - Return ExampleConfig with defaults if not present
    - _Requirements: 9.1, 9.4_
  - [x] 5.3 Implement `GetJsonPropertyName()` helper method
    - Check for JsonPropertyName attribute
    - Fall back to camelCase conversion of property name
    - _Requirements: 6.1_
  - [x] 5.4 Implement `ConvertExampleToTypedValue()` method
    - Convert string example to appropriate IOpenApiAny based on property type
    - Handle int, long, double, decimal, float, bool, string
    - Fall back to string on parse failure
    - _Requirements: 7.1, 7.2, 7.3, 7.6_
  - [x] 5.5 Implement `GetPropertyExample()` method
    - Extract Example from OpenApiSchema attribute
    - Convert to typed value using ConvertExampleToTypedValue
    - _Requirements: 6.1, 7.1, 7.2, 7.3_
  - [x] 5.6 Implement `ComposeExampleFromProperties()` method
    - Iterate over type properties
    - Skip OpenApiIgnore properties
    - Build OpenApiObject from property examples
    - Handle nested objects recursively
    - _Requirements: 6.1, 6.2, 6.3, 7.5_
  - [x] 5.7 Write property tests for example composition
    - **Property 11: Example composition from properties**
    - **Property 12: Partial example composition**
    - **Property 15: Example type conversion**
    - **Property 17: Nested object example composition**
    - **Validates: Requirements 6.1, 6.2, 6.3, 7.1, 7.2, 7.3, 7.5**

- [x] 6. Implement example placement and precedence
  - [x] 6.1 Integrate composed examples into schema generation
    - Call ComposeExampleFromProperties when generating request/response schemas
    - Set schema.Example with composed example
    - _Requirements: 6.4_
  - [x] 6.2 Implement explicit example precedence check
    - Check for OpenApiExample attribute before using composed example
    - Use explicit example if present
    - _Requirements: 6.5_
  - [x] 6.3 Write property tests for example placement and precedence
    - **Property 13: Example placement in schemas**
    - **Property 14: Explicit example precedence**
    - **Validates: Requirements 6.4, 6.5**

- [x] 7. Implement default example generation
  - [x] 7.1 Implement `GenerateFormatBasedDefault()` method
    - Generate defaults for email, uuid, date-time, date, uri, hostname, ipv4, ipv6
    - _Requirements: 8.1_
  - [x] 7.2 Implement `GenerateConstraintBasedDefault()` method for numerics
    - Generate value within Minimum/Maximum bounds
    - Use midpoint or minimum if only one bound specified
    - _Requirements: 8.2_
  - [x] 7.3 Implement `GenerateConstraintBasedDefault()` method for strings
    - Generate string with length within MinLength/MaxLength bounds
    - _Requirements: 8.3_
  - [x] 7.4 Implement `GenerateTypePlaceholder()` method
    - Generate type-appropriate placeholder for properties without constraints
    - _Requirements: 8.4_
  - [x] 7.5 Integrate default generation into `GetPropertyExample()`
    - Call default generation methods when GenerateDefaults is enabled
    - _Requirements: 8.1, 8.2, 8.3, 8.4, 8.5_
  - [x] 7.6 Write property tests for default generation
    - **Property 18: Format-based default generation**
    - **Property 19: Constraint-based numeric defaults**
    - **Property 20: Constraint-based string defaults**
    - **Property 21: Type-based placeholder defaults**
    - **Validates: Requirements 8.1, 8.2, 8.3, 8.4**

- [x] 8. Implement array example generation
  - [x] 8.1 Implement array example generation in `ComposeExampleFromProperties()`
    - Detect array/list properties
    - Generate array with single element example
    - _Requirements: 7.4_
  - [x] 8.2 Write property test for array examples
    - **Property 16: Array example generation**
    - **Validates: Requirements 7.4**

- [x] 9. Implement configuration handling
  - [x] 9.1 Integrate ExampleConfig into example generation flow
    - Check ComposeFromProperties before composing
    - Check GenerateDefaults before generating defaults
    - _Requirements: 9.2, 9.3, 9.5_
  - [x] 9.2 Write property tests for configuration
    - **Property 22: Config disables auto-generation**
    - **Property 23: Independent config options**
    - **Validates: Requirements 9.3, 9.5**

- [x] 10. Final checkpoint - All features complete
  - Ensure all tests pass, ask the user if questions arise.

- [x] 11. Update documentation
  - [x] 11.1 Update docs/attributes.md with OpenApiTagGroupAttribute documentation
    - Add to Table of Contents under Assembly-Level Attributes
    - Add full attribute reference section with constructor parameters, properties, usage examples
    - _Requirements: 1.1, 1.2, 1.3_
  - [x] 11.2 Update docs/attributes.md with OpenApiExampleConfigAttribute documentation
    - Add to Table of Contents under Assembly-Level Attributes
    - Add full attribute reference section with properties, usage examples
    - _Requirements: 9.1, 9.4, 9.5_
  - [x] 11.3 Update README.md with tag groups and example generation features
    - Add brief mention of new features in Features section
    - _Requirements: 1.1, 6.1_

- [ ] 12. Update CHANGELOG.md
  - [ ] 12.1 Add new version entry for tag groups and example generation features
    - Document OpenApiTagGroupAttribute and x-tagGroups extension support
    - Document OpenApiExampleConfigAttribute and automatic example generation
    - Document tag group merging in merge tool
    - _Requirements: 1.1, 1.2, 2.1, 3.1, 6.1, 9.1_

- [ ] 13. Final documentation checkpoint
  - Ensure all documentation is complete and accurate, ask the user if questions arise.

## Notes

- All tasks are required for comprehensive implementation
- Each task references specific requirements for traceability
- Checkpoints ensure incremental validation
- Property tests validate universal correctness properties
- Unit tests validate specific examples and edge cases
- The implementation uses FsCheck for property-based testing
