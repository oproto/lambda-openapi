# Implementation Plan: Deterministic Output

## Overview

This implementation adds deterministic ordering to OpenAPI output and a skip-unchanged feature to the merge tool. The approach is to create a central sorting utility, integrate it into both the merger and source generator, and add file comparison logic to the CLI.

## Tasks

- [x] 1. Create OpenApiDocumentSorter utility class
  - [x] 1.1 Create OpenApiDocumentSorter.cs in Oproto.Lambda.OpenApi.Merge
    - Implement `Sort(OpenApiDocument)` method as entry point
    - Implement `SortPaths()` for alphabetical path ordering
    - Implement `SortSchemas()` for alphabetical schema ordering
    - Implement `SortSchemaProperties()` for alphabetical property ordering within schemas
    - Implement `SortTags()` for alphabetical tag ordering
    - Implement `SortSecuritySchemes()` for alphabetical security scheme ordering
    - Implement `SortOperations()` for HTTP method order (GET, PUT, POST, DELETE, OPTIONS, HEAD, PATCH, TRACE)
    - Implement `SortResponses()` for ascending status code order
    - Implement `SortExamples()` for alphabetical example ordering
    - Implement `SortTagGroups()` for alphabetical tag group and tag-within-group ordering
    - Use `StringComparer.Ordinal` for consistent cross-platform ordering
    - _Requirements: 1.1, 1.2, 2.1, 2.2, 3.1, 3.2, 4.1, 4.2, 5.1, 5.2, 5.3, 7.1, 7.2, 8.1, 8.2, 9.1, 9.2, 11.1, 11.2_

  - [x] 1.2 Write property test for path ordering
    - **Property 1: Path Ordering**
    - **Validates: Requirements 1.1, 1.2**

  - [x] 1.3 Write property test for schema ordering
    - **Property 2: Schema Ordering**
    - **Validates: Requirements 2.1, 2.2**

  - [x] 1.4 Write property test for property ordering within schemas
    - **Property 3: Property Ordering Within Schemas**
    - **Validates: Requirements 3.1, 3.2**

  - [x] 1.5 Write property test for tag ordering
    - **Property 4: Tag Ordering**
    - **Validates: Requirements 4.1, 4.2**

  - [x] 1.6 Write property test for tag group ordering
    - **Property 5: Tag Group Ordering**
    - **Validates: Requirements 5.1, 5.2, 5.3**

  - [x] 1.7 Write property test for security scheme ordering
    - **Property 6: Security Scheme Ordering**
    - **Validates: Requirements 7.1, 7.2**

  - [x] 1.8 Write property test for operation ordering
    - **Property 7: Operation Ordering**
    - **Validates: Requirements 8.1, 8.2**

  - [x] 1.9 Write property test for response ordering
    - **Property 8: Response Ordering**
    - **Validates: Requirements 9.1, 9.2**

  - [x] 1.10 Write property test for example ordering
    - **Property 9: Example Ordering**
    - **Validates: Requirements 11.1, 11.2**

- [x] 2. Integrate sorter into OpenApiMerger
  - [x] 2.1 Modify OpenApiMerger.Merge() to call OpenApiDocumentSorter.Sort()
    - Call Sort() on the merged document before returning
    - Ensure sorting happens after all merge operations complete
    - _Requirements: 1.2, 2.2, 3.2, 4.2, 5.2, 7.2, 8.2, 9.2, 11.2_

  - [x] 2.2 Write property test for output idempotence
    - **Property 10: Output Idempotence (Round-Trip)**
    - **Validates: Requirements 1.3, 2.3, 3.3, 4.3**

- [x] 3. Checkpoint - Ensure all merger tests pass
  - Ensure all tests pass, ask the user if questions arise.

- [x] 4. Integrate sorter into Source Generator
  - [x] 4.1 Add sorting logic to OpenApiSpecGenerator.MergeOpenApiDocs()
    - Implement sorting methods directly in the source generator (cannot reference Merge project)
    - Sort paths alphabetically
    - Sort schemas alphabetically
    - Sort properties within schemas alphabetically
    - Sort tags alphabetically
    - Sort security schemes alphabetically
    - Sort operations by HTTP method order
    - Sort responses by status code
    - Sort examples alphabetically
    - Sort tag groups and tags within groups alphabetically
    - _Requirements: 1.1, 2.1, 3.1, 4.1, 5.1, 7.1, 8.1, 9.1, 11.1_

  - [x] 4.2 Write property test for generator deterministic output
    - Test that generating the same source twice produces identical output
    - **Validates: Requirements 1.1, 2.1, 3.1, 4.1, 5.1, 7.1, 8.1, 9.1, 11.1**

- [x] 5. Checkpoint - Ensure all generator tests pass
  - Ensure all tests pass, ask the user if questions arise.

- [x] 6. Add skip-unchanged feature to merge tool
  - [x] 6.1 Add --force flag to MergeCommand
    - Add `-f, --force` option to command definition
    - Pass force flag to write method
    - _Requirements: 10.3_

  - [x] 6.2 Implement skip-unchanged logic in WriteOpenApiDocumentAsync
    - Read existing file content if file exists
    - Compare with new content
    - Skip write if content matches and force is false
    - Log message when skipping
    - Always write if file doesn't exist
    - _Requirements: 10.1, 10.2, 10.4_

  - [x] 6.3 Write unit tests for skip-unchanged feature
    - Test file is skipped when content matches
    - Test file is written when content differs
    - Test file is written when --force is used
    - Test file is written when it doesn't exist
    - Test log message is output when skipping
    - _Requirements: 10.1, 10.2, 10.3, 10.4_

- [x] 7. Add server order preservation test
  - [x] 7.1 Write property test for server order preservation
    - **Property 11: Server Order Preservation**
    - **Validates: Requirements 6.1, 6.2**

- [x] 8. Final checkpoint - Ensure all tests pass
  - Ensure all tests pass, ask the user if questions arise.

## Notes

- All test tasks are required for comprehensive coverage
- The source generator cannot reference the Merge project, so sorting logic must be duplicated
- Use `StringComparer.Ordinal` for consistent cross-platform string ordering
- Property tests should use FsCheck with minimum 100 iterations
- Each property test should reference its design document property number
