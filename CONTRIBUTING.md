# Contributing to Oproto OpenAPI Generator

Thank you for your interest in contributing to Oproto OpenAPI Generator! This document provides guidelines and information for contributors.

## Code of Conduct

This project adheres to a code of conduct. By participating, you are expected to uphold this code.

## How to Contribute

### Reporting Issues

- Use the GitHub issue tracker to report bugs or request features
- Before creating an issue, please search existing issues to avoid duplicates
- Provide as much detail as possible, including:
  - Steps to reproduce the issue
  - Expected vs actual behavior
  - Environment details (.NET version, OS, etc.)
  - Sample code if applicable

### Development Setup

1. Fork the repository
2. Clone your fork locally
3. Ensure you have .NET 6.0 and 8.0 SDKs installed
4. Run `dotnet restore` to install dependencies
5. Run `dotnet build` to build the solution
6. Run `dotnet test` to execute tests

### Making Changes

1. Create a feature branch from `main`
2. Make your changes following the coding standards below
3. Add or update tests as needed
4. Ensure all tests pass
5. Update documentation if necessary
6. Commit your changes with clear, descriptive messages

### Coding Standards

- Follow standard C# conventions and .NET guidelines
- Use meaningful names for variables, methods, and classes
- Add XML documentation comments for public APIs
- Keep methods focused and reasonably sized
- Write unit tests for new functionality
- Ensure code is formatted consistently (use `dotnet format`)

### Pull Request Process

1. Update the README.md with details of changes if applicable
2. Update version numbers following semantic versioning
3. Ensure the PR description clearly describes the problem and solution
4. Link any relevant issues
5. Request review from maintainers

### Testing

- Write unit tests for new features
- Ensure existing tests continue to pass
- Aim for good test coverage of new code
- Test on multiple .NET versions when possible

### Documentation

- Update XML documentation for public APIs
- Update README.md for user-facing changes
- Add examples for new features
- Keep documentation clear and concise

## Release Process

Releases are handled by maintainers:

1. Version numbers follow semantic versioning (MAJOR.MINOR.PATCH)
2. Releases are tagged and published via GitHub Actions
3. NuGet packages are automatically published on release

## Questions?

If you have questions about contributing, please:
- Check existing documentation
- Search closed issues for similar questions
- Open a new issue with the "question" label

Thank you for contributing!