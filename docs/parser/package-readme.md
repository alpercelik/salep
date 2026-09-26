# Salep GraphQL Parser

A .NET 10 and .NET 11 library for lexing and parsing GraphQL executable documents and schema definition language (SDL). It provides syntax-tree traversal, immutable rewrites, source locations, diagnostics, printing, and UTF-8 input helpers.

Install the `Salep.GraphQLParser` NuGet package:

```sh
dotnet add package Salep.GraphQLParser --version 0.1.0-preview.1
```

Target `net10.0` or `net11.0`; .NET 11 currently requires its preview/RC toolchain. This standalone library does not require the Salep client generator. Portable symbols are distributed in the companion `.snupkg` for source debugging.

```csharp
using Salep.GraphQLParser;

var source = new SourceText("query { viewer { id } }".AsMemory());
DocumentNode document = GraphQLParser.Parse(source);
```

Parsing checks GraphQL syntax. Schema-dependent validation, value coercion, and execution are not included.

Default parse entry points snapshot source into immutable storage. The explicitly borrowed `ParseBorrowed`, `ParseWithDiagnosticsBorrowed`, and `ParseSchemaCoordinateBorrowed` methods retain caller-owned memory; keep its backing storage alive and unchanged while the returned syntax tree is in use.

See the [parser documentation](https://github.com/alpercelik/salep/tree/main/docs/parser) for API contracts, supported syntax, and verification workflows.
