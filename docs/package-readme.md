# GraphQLParser

A .NET 10 library for lexing and parsing GraphQL executable documents and schema definition language (SDL). It provides syntax-tree traversal, immutable rewrites, source locations, diagnostics, printing, and UTF-8 input helpers.

```csharp
using GraphQLParser;

var source = new SourceText("query { viewer { id } }".AsMemory());
DocumentNode document = GraphQLParser.GraphQLParser.Parse(source);
```

Parsing checks GraphQL syntax. Schema-dependent validation, value coercion, and execution are not included.

`SourceText` and source-backed syntax nodes retain caller-owned memory. Keep its backing storage alive and unchanged while parsing and while the resulting syntax tree is in use.
