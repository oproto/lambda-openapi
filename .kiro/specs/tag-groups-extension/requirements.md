# Requirements Document

## Introduction

This feature adds two enhancements to the OpenAPI generation tooling:

1. **Tag Groups Extension (`x-tagGroups`)**: Allows grouping related tags together for better organization in large APIs. This extension is widely supported by documentation tools like Redoc and enables hierarchical navigation of API endpoints. The feature needs to be implemented at both the source generator level and the merge tool level.

2. **Automatic Example Generation**: Enables automatic composition of request/response examples from property-level `[OpenApiSchema]` example values, eliminating the need to manually write JSON strings in `[OpenApiExample]` attributes.

## Glossary

- **Tag_Group**: A named collection of tags that logically belong together (e.g., "User Management" containing "Users", "Authentication", "Roles" tags)
- **Source_Generator**: The Oproto.Lambda.OpenApi.SourceGenerator component that generates OpenAPI specs from Lambda function attributes
- **Merge_Tool**: The Oproto.Lambda.OpenApi.Merge component that combines multiple OpenAPI specs into one
- **x-tagGroups**: An OpenAPI extension (vendor extension) that defines an array of tag group objects at the root level of the OpenAPI document
- **Property_Example**: An example value defined on a model property via the `[OpenApiSchema(Example = "...")]` attribute
- **Composed_Example**: An automatically generated example object built by combining individual property examples

## Requirements

### Requirement 1: Tag Group Definition Attribute

**User Story:** As a developer, I want to define tag groups at the assembly level, so that I can organize my API tags into logical sections.

#### Acceptance Criteria

1. THE Source_Generator SHALL support an `OpenApiTagGroupAttribute` that can be applied at the assembly level
2. WHEN an `OpenApiTagGroupAttribute` is applied, THE Source_Generator SHALL include the group name and associated tags in the attribute definition
3. WHEN multiple `OpenApiTagGroupAttribute` attributes are applied, THE Source_Generator SHALL preserve all tag groups in the order they are defined
4. WHEN a tag group references a tag that does not exist in the API, THE Source_Generator SHALL still include the tag in the group definition

### Requirement 2: Tag Group Output Generation

**User Story:** As a developer, I want the generated OpenAPI spec to include the x-tagGroups extension, so that documentation tools can render my API with proper grouping.

#### Acceptance Criteria

1. WHEN tag groups are defined, THE Source_Generator SHALL output an `x-tagGroups` array at the root level of the OpenAPI document
2. THE Source_Generator SHALL format each tag group object with `name` and `tags` properties
3. WHEN no tag groups are defined, THE Source_Generator SHALL NOT include the `x-tagGroups` extension in the output
4. THE Source_Generator SHALL output tag groups in the same order as the attributes are defined

### Requirement 3: Tag Group Merging

**User Story:** As a developer, I want the merge tool to combine tag groups from multiple API specs, so that my merged API maintains proper organization.

#### Acceptance Criteria

1. WHEN merging specs with tag groups, THE Merge_Tool SHALL combine all tag groups into the merged output
2. WHEN multiple specs define tag groups with the same name, THE Merge_Tool SHALL merge the tags from both groups into a single group
3. WHEN merging tag groups with the same name, THE Merge_Tool SHALL deduplicate tags within the merged group
4. THE Merge_Tool SHALL preserve tag groups from specs that have them even when other specs do not define tag groups
5. WHEN no source specs define tag groups, THE Merge_Tool SHALL NOT include the `x-tagGroups` extension in the merged output

### Requirement 4: Tag Group Ordering in Merge

**User Story:** As a developer, I want control over the order of tag groups in merged output, so that my API documentation has a consistent structure.

#### Acceptance Criteria

1. THE Merge_Tool SHALL preserve the order of tag groups as they appear in the first spec that defines each group
2. WHEN a tag group appears in multiple specs, THE Merge_Tool SHALL use the position from the first occurrence
3. WHEN new tag groups are encountered in subsequent specs, THE Merge_Tool SHALL append them after existing groups

### Requirement 5: Tag Group Serialization

**User Story:** As a developer, I want tag groups to be properly serialized in both JSON and YAML formats, so that my OpenAPI specs are valid and readable.

#### Acceptance Criteria

1. THE Source_Generator SHALL serialize tag groups as a JSON array under the `x-tagGroups` key
2. WHEN serializing tag groups, THE Source_Generator SHALL output each group with `name` as a string and `tags` as an array of strings
3. THE Merge_Tool SHALL preserve the `x-tagGroups` extension format when reading and writing OpenAPI documents


### Requirement 6: Automatic Example Composition

**User Story:** As a developer, I want the generator to automatically create request/response examples from my property-level example values, so that I don't have to manually write and maintain JSON example strings.

#### Acceptance Criteria

1. WHEN a request or response type has properties with `[OpenApiSchema(Example = "...")]` values, THE Source_Generator SHALL compose a complete example object from those property examples
2. WHEN all properties of a type have example values defined, THE Source_Generator SHALL generate a fully populated example
3. WHEN some properties have example values and others do not, THE Source_Generator SHALL include only the properties that have examples in the composed example
4. THE Source_Generator SHALL use the composed example as the default example for request bodies and response schemas
5. WHEN an explicit `[OpenApiExample]` attribute is also present, THE Source_Generator SHALL use the explicit example instead of the composed example

### Requirement 7: Example Value Type Conversion

**User Story:** As a developer, I want my property example values to be correctly typed in the generated JSON, so that numeric and boolean examples appear as proper JSON types rather than strings.

#### Acceptance Criteria

1. WHEN a property is of numeric type (int, long, decimal, double, float), THE Source_Generator SHALL convert the example string to a JSON number
2. WHEN a property is of boolean type, THE Source_Generator SHALL convert the example string to a JSON boolean
3. WHEN a property is of string type, THE Source_Generator SHALL keep the example as a JSON string
4. WHEN a property is of array type with element examples, THE Source_Generator SHALL generate an array example
5. WHEN a property is of a complex object type, THE Source_Generator SHALL recursively compose examples from nested property examples
6. IF example value conversion fails, THEN THE Source_Generator SHALL fall back to using the raw string value

### Requirement 8: Default Example Generation

**User Story:** As a developer, I want the generator to create sensible default examples for properties without explicit example values, so that I get useful examples with minimal configuration.

#### Acceptance Criteria

1. WHEN a string property has no example but has a `Format` specified (e.g., "email", "uuid", "date-time"), THE Source_Generator SHALL generate a format-appropriate default example
2. WHEN a numeric property has no example but has `Minimum` and/or `Maximum` constraints, THE Source_Generator SHALL generate an example within those bounds
3. WHEN a string property has no example but has `MinLength` and/or `MaxLength` constraints, THE Source_Generator SHALL generate an example string of appropriate length
4. WHEN a property has no example and no constraints, THE Source_Generator SHALL generate a type-appropriate placeholder value
5. WHERE the `AutoGenerateExamples` option is disabled, THE Source_Generator SHALL NOT generate default examples for properties without explicit example values

### Requirement 9: Example Generation Configuration

**User Story:** As a developer, I want to control whether automatic example generation is enabled, so that I can opt-in or opt-out based on my project needs.

#### Acceptance Criteria

1. THE Source_Generator SHALL support an assembly-level attribute to enable or disable automatic example generation
2. WHEN automatic example generation is enabled, THE Source_Generator SHALL compose examples from property-level values
3. WHEN automatic example generation is disabled, THE Source_Generator SHALL only use explicit `[OpenApiExample]` attributes
4. THE Source_Generator SHALL enable automatic example composition by default
5. THE Source_Generator SHALL allow disabling default value generation separately from example composition
