# Public language API contract

This inventory defines the selected compatibility surface for GraphQL syntax parsing and its language-level utilities. The reference metadata is pinned at version 16.6.6. Member signatures were reflected from the `net10.0` reference assemblies; that metadata also declares `netstandard2.0`, `net8.0`, `net9.0`, and `net11.0` assets. This project builds and packages `net10.0` and `net11.0`.

The normalized, machine-readable signature baseline is [public-api-contract.json](public-api-contract.json). It records 119 public types and 992 declared public members, including constructors, overloads, return and parameter types, optional parameters, properties, and constants. All library-owned names use this project's namespace and assembly identity. The baseline contains API metadata only; it contains no implementation source.

## Included surface

- Parser entry points, parser options, experimental language options, syntax exceptions, token kinds, UTF-8 parsing and reader APIs.
- Syntax tree interfaces and nodes for executable definitions, values, type references, SDL definitions and extensions, schema coordinates, names, spans, and locations.
- Node constructors, child enumeration, immutable `With...` update methods, repeatable directive transforms, and value conversion helpers.
- Ordered syntax visitors, visitor actions, navigators, visitor options, generic rewrite interfaces, and rewriter factories.
- Syntax printers, serializers, syntax writers, and writer extensions.

## Excluded surface

HTTP request envelopes, request caches, operation-document hashing and UTF-8 operation-document wrappers, schema-dependent validation, and execution are outside this parser library's contract. The machine-readable inventory enumerates the language-focused public types and keeps excluded areas explicit.

## Contract tests

`PublicApiContractInventoryTests` verifies normalized project identity, declared target frameworks, unique public type names, signature record shape, and the presence of the parser, syntax tree, printer, and rewrite API families. The package-compatibility gate compares all 119 selected types and 992 declared member signatures through runtime reflection, compiles and runs package-only consumers for both target frameworks, and requires an empty difference allowlist.
