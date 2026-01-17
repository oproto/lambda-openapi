# Implementation Plan: Dictionary Schema Support

## Overview

This implementation adds proper dictionary type handling to the Oproto.Lambda.OpenApi source generator. The work is organized into three main phases: dictionary type detection, schema generation, and comprehensive testing.

## Tasks

- [ ] 1. Implement dictionary type detection
  - [ ] 1.1 Add `IsDictionaryType` method to `OpenApiSpecGenerator_Types.cs`
    - Implement detection for `Dictionary<K,V>`, `IDictionary<K,V>`, `IReadOnlyDictionary<K,V>`
    - Extract key and value type symbols from type arguments
    - Check for interface implementation for custom dictionary types
    - _Requirements: 1.1, 1.2, 1.3, 1.4, 1.5_

- [ ] 2. Implement dictionary schema generation
  - [ ] 2.1 Add `TryCreateDictionarySchema` method to `OpenApiSpecGenerator_Schema.cs`
    - Create schema with `type: "object"` and `additionalProperties`
    - Recursively call `CreateSchema` for the value type
    - Apply `[OpenApiSchema]` attributes (Description, Example) to dictionary schema
    - _Requirements: 2.1, 2.2, 2.3, 2.4, 2.5, 3.1, 3.2, 3.3, 6.1, 6.2_

  - [ ] 2.2 Integrate dictionary detection into `CreateSchema` pipeline
    - Add `TryCreateDictionarySchema` call after `TryCreateCollectionSchema` and before complex type handling
    - Ensure dictionaries don't fall through to `CreateComplexTypeSchema`
    - _Requirements: 5.1, 5.2, 5.3_

  - [ ] 2.3 Handle nullable dictionary types
    - Ensure nullable dictionaries (`Dictionary<K,V>?`) produce `nullable: true` in schema
    - Handle nullable reference type annotations on dictionary properties
    - _Requirements: 4.1, 4.2_

- [ ] 3. Checkpoint - Verify implementation compiles
  - Run `dotnet build` on the source generator project
  - Ensure no compiler warnings or errors
  - Ensure all tests pass, ask the user if questions arise

- [ ] 4. Add unit tests for dictionary schema generation
  - [ ] 4.1 Add basic dictionary type tests to `OpenApiGeneratorTests.cs`
    - Test `Dictionary<string, string>` produces correct schema
    - Test `Dictionary<string, int>` produces integer additionalProperties
    - Test `Dictionary<string, bool>` produces boolean additionalProperties
    - Test `Dictionary<string, decimal>` produces number additionalProperties
    - Test `Dictionary<string, DateTime>` produces string with date-time format
    - _Requirements: 2.1, 2.2, 2.3, 2.4, 2.5_

  - [ ] 4.2 Add complex value type tests
    - Test `Dictionary<string, ComplexType>` produces $ref in additionalProperties
    - Test `Dictionary<string, List<string>>` produces nested array schema
    - Test `Dictionary<string, Dictionary<string, int>>` produces nested dictionary schema
    - _Requirements: 3.1, 3.2, 3.3_

  - [ ] 4.3 Add nullable dictionary tests
    - Test nullable dictionary property produces `nullable: true`
    - Test `Dictionary<string, T>?` produces `nullable: true`
    - _Requirements: 4.1, 4.2_

  - [ ] 4.4 Add dictionary interface tests
    - Test `IDictionary<string, T>` is detected as dictionary
    - Test `IReadOnlyDictionary<string, T>` is detected as dictionary
    - _Requirements: 1.2, 1.3_

  - [ ] 4.5 Add attribute support tests
    - Test `[OpenApiSchema(Description = "...")]` applies to dictionary schema
    - Test `[OpenApiSchema(Example = "...")]` applies to dictionary schema
    - _Requirements: 6.1, 6.2_

- [ ] 5. Add property-based tests for dictionary handling
  - [ ] 5.1 Write property test for dictionary type detection
    - **Property 1: Dictionary Type Detection**
    - **Validates: Requirements 1.1, 1.2, 1.3, 1.4, 1.5**

  - [ ] 5.2 Write property test for dictionary schema structure
    - **Property 2: Dictionary Schema Structure**
    - **Validates: Requirements 5.2**

  - [ ] 5.3 Write property test for simple value type mapping
    - **Property 3: Simple Value Type Schema**
    - **Validates: Requirements 2.1, 2.2, 2.3, 2.4, 2.5**

  - [ ] 5.4 Write property test for complex value type references
    - **Property 4: Complex Value Type Reference**
    - **Validates: Requirements 3.1**

  - [ ] 5.5 Write property test for nullable dictionary handling
    - **Property 5: Nullable Dictionary Handling**
    - **Validates: Requirements 4.1, 4.2**

- [ ] 6. Final checkpoint - Ensure all tests pass
  - Run full test suite with `dotnet test`
  - Verify no regressions in existing functionality
  - Ensure all tests pass, ask the user if questions arise

## Notes

- Each task references specific requirements for traceability
- Checkpoints ensure incremental validation
- Property tests validate universal correctness properties
- Unit tests validate specific examples and edge cases
