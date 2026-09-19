# GraphQL C# Parser

A GraphQL lexer and parser targeting .NET 10 LTS and the September 2025 GraphQL specification. The conformance scope and parser-versus-validation boundary are recorded in [docs/spec-coverage.md](docs/spec-coverage.md).

## Build and test

Run the complete suite from the repository root:

```sh
./scripts/test.sh
```

The script forwards arguments to `dotnet test`, so focused tests can use normal test-runner filters, for example:

```sh
./scripts/test.sh --filter FullyQualifiedName~SourceTextTests
```

Tests live in `tests/GraphQLParser.Tests`. GraphQL inputs are stored under its `Fixtures` directory and copied to the test output directory. Keep minimized defect reproductions there as regression fixtures. The test project can add differential oracle tests as parser coverage grows.

## Source input ownership

The source abstraction accepts `ReadOnlyMemory<char>` and returns slices over the caller's storage without copying. The caller must keep the backing storage alive and unchanged while parsing and while source-backed AST nodes are in use. Source offsets are measured in UTF-16 code units.
