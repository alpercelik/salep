# Public language API contract

This inventory defines the compatibility surface for GraphQL syntax parsing and its language-level utilities. The package metadata is pinned at version 16.6.6. Member signatures were reflected from the net10.0 reference assemblies; the metadata declares `netstandard2.0`, `net8.0`, `net9.0`, `net10.0`, and `net11.0` assets. The current project targets net10.0, so framework parity remains implementation and release work.

The normalized, machine-readable signature baseline is [public-api-contract.json](public-api-contract.json). It records 120 public types and 1,000 declared public members, including constructors, overloads, return and parameter types, optional parameters, properties, and constants. All library-owned names use this project's namespace and assembly identity. The baseline contains API metadata only; it contains no implementation source.

## Included surface

- Parser entry points, parser options, experimental language options, syntax exceptions, token kinds, UTF-8 parsing and reader APIs.
- Syntax tree interfaces and nodes for executable definitions, values, type references, SDL definitions and extensions, schema coordinates, names, spans, and locations.
- Node constructors, child enumeration, immutable `With...` update methods, repeatable directive transforms, and value conversion helpers.
- Ordered syntax visitors, visitor actions, navigators, visitor options, generic rewrite interfaces, and rewriter factories.
- Syntax printers, serializers, syntax writers, and writer extensions.

## Excluded surface

HTTP request envelopes, request caches, operation-document hashing, schema-dependent validation, and execution are outside this parser library's contract. The machine-readable inventory enumerates the language-focused public types and keeps excluded areas explicit.

## Contract tests

`PublicApiContractInventoryTests` verifies normalized project identity, declared target frameworks, unique public type names, signature record shape, and the presence of the parser, syntax tree, printer, and rewrite API families. The compatibility milestone adds runtime reflection comparisons between the baseline and the project assembly, plus compile-and-run consumer coverage. Those implementation comparisons are expected to remain incomplete until that milestone is delivered.
