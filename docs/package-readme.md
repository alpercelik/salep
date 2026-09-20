# GraphQLParser

A .NET 10 library for lexing and parsing GraphQL executable documents and schema definition language (SDL). It provides syntax-tree traversal, immutable rewrites, source locations, diagnostics, printing, and UTF-8 input helpers.

```csharp
using GraphQLParser;

var source = new SourceText("query { viewer { id } }".AsMemory());
DocumentNode document = GraphQLParser.GraphQLParser.Parse(source);
```

Parsing checks GraphQL syntax. Schema-dependent validation, value coercion, and execution are not included.

Default parse entry points snapshot source into immutable storage. The explicitly borrowed `ParseBorrowed`, `ParseWithDiagnosticsBorrowed`, and `ParseSchemaCoordinateBorrowed` methods retain caller-owned memory; keep its backing storage alive and unchanged while the returned syntax tree is in use.
