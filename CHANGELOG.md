# Changelog

All notable changes to this project will be documented in this file.

The format is based on [Keep a Changelog](https://keepachangelog.com/en/1.1.0/),
and this project adheres to [Semantic Versioning](https://semver.org/spec/v2.0.0.html).

## [Unreleased]

### Added

- **Examples Project** (`Oproto.Lambda.OpenApi.Examples`)
  - Complete CRUD API example demonstrating all library features
  - `ProductFunctions` class with GET, POST, PUT, DELETE operations
  - Model classes (`Product`, `CreateProductRequest`, `UpdateProductRequest`) showcasing `OpenApiSchema` and `OpenApiIgnore` attributes
  - Generated `openapi.json` demonstrating output

- **Documentation**
  - `docs/attributes.md` - Complete attribute reference for all six library attributes
  - `docs/configuration.md` - MSBuild configuration options, output path customization, and troubleshooting guide
  - Updated `docs/getting-started.md` with cross-references to new documentation

### Changed

- Solution now includes the examples project for reference and validation
