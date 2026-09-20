# GraphQL C# Parser

A GraphQL lexer and parser targeting .NET 10 and .NET 11 and the September 2025 GraphQL specification. The repository currently pins the .NET 11 RC SDK to build both targets; update `global.json` to the supported .NET 11 SDK when it is released. The conformance scope and parser-versus-validation boundary are recorded in [docs/spec-coverage.md](docs/spec-coverage.md).
The AST location, immutability, and source-memory rules are recorded in [docs/ast-model.md](docs/ast-model.md).

## Build and test

Run the complete suite from the repository root:

```sh
./scripts/test.sh
```

The script forwards arguments to `dotnet test`, so focused tests can use normal test-runner filters, for example:

```sh
./scripts/test.sh --filter FullyQualifiedName~SourceTextTests
```

Build and verify the NuGet package and package-only consumer on both target frameworks with:

```sh
./scripts/verify-package-compatibility.sh
```

The package is retained at `artifacts/packages/GraphQLParser.0.0.0-verify.1.nupkg`.

Tests live in `tests/GraphQLParser.Tests`. GraphQL inputs are stored under its `Fixtures` directory and copied to the test output directory. Keep minimized defect reproductions there as regression fixtures. The test project can add differential oracle tests as parser coverage grows.

## graphql-js oracle

The reference corpus is pinned to `graphql` 16.14.0 in `package.json` and `package-lock.json`, matching the September 2025 grammar target while using the mature 16.x parser line. Fixtures are owned by this repository; `tests/Fixtures/Oracle/expected/provenance.json` records the oracle package integrity and each source fixture's SHA-256. The generator uses graphql-js as a black box and contains no copied parser implementation.

Install Node dependencies and regenerate the canonical AST or parse-error snapshots with:

```sh
npm ci
npm run oracle:write
```

Check that committed snapshots match the pinned parser and source fixtures with:

```sh
npm run oracle:check
```

The oracle check names each fixture when a snapshot differs. Review regenerated output before committing it. The .NET test suite also checks that each manifest entry has a source and a version-matched expected result.

## Source input ownership

Default document, diagnostic, and schema-coordinate parsing snapshots `SourceText` into immutable storage. Callers may release or mutate their input after parsing. The explicitly named `ParseBorrowed`, `ParseWithDiagnosticsBorrowed`, and `ParseSchemaCoordinateBorrowed` entry points retain caller-owned memory; callers must keep that memory alive and unchanged while returned nodes are in use. Direct lexer use also borrows its `SourceText`. Source offsets are measured in UTF-16 code units.
