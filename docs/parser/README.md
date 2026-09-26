# Salep GraphQL Parser

Salep GraphQL Parser is a standalone GraphQL language library for .NET 10 and .NET 11. The project, assembly, namespace, and NuGet package are named `Salep.GraphQLParser`.

It includes lexing, executable-document and SDL parsing, immutable syntax trees, source locations, diagnostics, visitors, rewriting, printing, and UTF-8 helpers. Schema-dependent validation, coercion, and execution are outside its scope.

- [Package usage](package-readme.md)
- [AST model and ownership](ast-model.md)
- [Public API contract](public-api-contract.md)
- [Syntax coverage](spec-coverage.md)
- [Scope and milestones](next-milestones.md)
- [Original implementation plan](goal.md)
- [Reference suite inventory](reference-suite-inventory.md)
- [Fuzzing](fuzzing.md)
- [Performance baseline](performance-baseline.md)
- [Release readiness](release-readiness.md)

Contributors and agents start with [repository guidance](../../AGENTS.md). Run the paired [test and package verification workflows](../script-workflows.md); package verification uses `src/Salep.GraphQLParser.PublicApiConsumer` independently of source project references. This consumer is excluded from both solution build lists; the package verification scripts restore and pack the parser before building it. Parser behavior changes require positive, negative, boundary, regression, and reproducible oracle coverage.

The [Salep C# Client Generator](../client-generator/README.md) consumes this library and has its own documentation and feature coverage checklist.
