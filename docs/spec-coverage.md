# GraphQL parser specification and coverage baseline

## Baseline

- **Conformance target:** GraphQL specification, September 2025 edition: <https://spec.graphql.org/September2025/>.
- **Intended product:** a production-ready, battle-tested C# lexer and parser for executable documents and SDL. This scope does not include schema construction, executable validation, or execution.
- **Oracle:** a pinned version of `graphql-js` used as a black-box comparison implementation. Do not copy its parser implementation. Fixture provenance and version must be committed with the fixtures.
- **Coverage policy:** each grammar row below needs positive, negative, and boundary cases in the continuously runnable test harness. Differential cases are added as each row is implemented; discovered defects become permanent regressions.

This is the initial baseline for implementation. The specification edition is fixed for the first release. Supporting a later edition requires an explicit coverage review and updated fixtures, not an implicit claim that the parser supports “current GraphQL.”

The matrix follows the specification's Language sections 2.1–2.14, Type System sections 3.1–3.13, and Appendix C grammar summary. The prose requirements in those sections are authoritative when shorthand grammar notation needs interpretation. The public parser also provides strict schema-coordinate parsing, opt-in parser helper options, and block-string normalization/printing utilities; these are API utilities rather than document grammar productions.

## Lexical grammar

| Area | Required behavior | Coverage notes |
| --- | --- | --- |
| Source characters | Read Unicode scalar values allowed by the specification; preserve source offsets consistently. | C# strings are UTF-16, so validate or correctly combine surrogate code units; define offset units and line/column mapping. |
| Ignored input | Ignore Unicode BOM at token boundaries, space, horizontal tab, CR, LF, commas, and comments through the next line terminator or EOF. | Test CR, LF, CRLF, repeated commas, BOM at start and between tokens, comment at EOF, and comments containing arbitrary permitted source characters. |
| Punctuators | `! $ & ( ) ... : = @ [ ] { }` and the vertical-bar token (U+007C). | Reject a one- or two-dot sequence and unknown punctuation. |
| Names | ASCII letter or underscore, followed by zero or more ASCII letters, digits, or underscores. | Names are not Unicode identifiers. Test boundaries and reserved contextual names separately in parser productions. |
| Integers | Optional minus followed by zero, or a nonzero digit and digits. | Reject leading zeros, `+`, a bare minus, and an integer prefix followed by an invalid numeric continuation. |
| Floats | Integer component plus a fractional component and/or exponent component, with the grammar's required digits. | Reject incomplete decimal/exponent forms and leading-zero violations. Cover exponent sign and uppercase/lowercase exponent markers. |
| Quoted strings | Decode `\\`, `\/`, `\"`, `\b`, `\f`, `\n`, `\r`, `\t`, fixed-width `\uXXXX`, and variable-width `\u{...}` escapes. | Enforce Unicode scalar constraints. Cover permitted fixed-width surrogate pairs, invalid lone surrogates, out-of-range values, raw line terminators, control characters, and unterminated strings. Preserve raw token span separately from decoded value. |
| Block strings | Accept multiline content and escaped triple quotes; compute the value using the specification's indentation and blank-edge-line algorithm. | `\\n` is literal content in a block string. Test indentation, tabs, blank lines, CR/LF/CRLF, escaped delimiters, empty values, and unterminated strings. Keep raw span separately from normalized value. |

The lexer reports lexical errors with offsets into the original source. Decoded quoted strings and normalized block strings may require allocated storage; zero-copy applies only where the AST value can faithfully reference the source.

## Executable document grammar

An executable document contains one or more executable definitions. Cover every production, preserving source order and locations.

| Production group | Required forms and boundaries |
| --- | --- |
| Operations | Shorthand query selection set; explicit query, mutation, and subscription; optional description, operation name, variable definitions, directives, and selection set. Reject descriptions on query shorthand; descriptions are permitted on explicit operation definitions. Shorthand is only a query selection set, without operation variables or directives. |
| Variable definitions | One or more definitions when parentheses are present; variable, type reference, optional constant default, and constant directives. Variables are not legal in constant-value positions. |
| Selection sets | Braces around **one or more** selections. The source goal's “zero or more” wording is incorrect; `{}` is invalid syntax. |
| Fields | Optional alias, name, optional argument list, directives, and optional nested selection set. Argument lists, when present, contain one or more arguments. |
| Fragments | Fragment definitions with optional descriptions, named spreads, inline fragments with and without a type condition, type conditions, and directives. Apply grammar restrictions such as the reserved fragment name `on` in the relevant production. |
| Directives | One or more directive applications with optional nonempty argument lists; parse names generically without requiring directive definitions. |
| Values | Variable, integer, float, string, boolean, null, enum, list, and input object values. Lists and objects may be empty where their productions permit; object fields, when present, have names and values. The enum production excludes the literal names true, false, and null. |
| Type references | Named types, nested list types, and non-null wrappers; reject invalid repeated non-null wrappers. |
| Document mixtures | The general document grammar can contain executable and type-system definitions. Provide an executable-only parse mode if exposed, and distinguish it from the general document grammar. |

## SDL grammar

Cover schema definitions, every type definition, directive definitions, and every corresponding permitted extension. Preserve descriptions, order, directives, and locations.

| Production group | Required forms and boundaries |
| --- | --- |
| Schema | Schema definition with root operation type definitions; schema extension forms and their grammar-required content. |
| Scalar | Scalar definition and scalar extension; optional description and directives. |
| Object | Object definition and extension; implemented interfaces, directives, and field definitions. |
| Interface | Interface definition and extension; implemented interfaces, directives, and field definitions. |
| Union | Union definition and extension; directives and member types. |
| Enum | Enum definition and extension; directives and enum value definitions. |
| Input object | Input object definition and extension; directives and input value definitions. Parse `@oneOf` using the ordinary directive syntax. |
| Fields and input values | Field arguments, argument/input-value definitions, types, optional constant defaults, directives, and descriptions. |
| Directives | Directive definition, optional arguments, optional `repeatable`, and one or more executable or type-system directive locations separated by a vertical bar. |
| Type extensions | Enforce the grammar for each extension kind, including its required added content; do not enforce semantic existence or uniqueness of the extended type in the parser. |

Descriptions are string or block-string values associated with the following definition. SDL parsing preserves syntax and structure; it does not decide whether a schema definition is semantically valid.

## Parser boundary

The parser is responsible for token and grammar acceptance, source locations, source-ordered AST structure, constant-value contexts, and descriptive syntax/lexical errors. It does not perform these separate GraphQL validation or execution rules:

- operation and fragment uniqueness, fragment reachability/cycles, variable declaration/use compatibility, and subscription field constraints;
- field, argument, input-object field, and directive existence or uniqueness against a schema;
- type compatibility, interface implementation compatibility, union membership validity, schema root validity, or extension target existence;
- whether directive uses comply with their declared locations and repeatability, or the semantic meaning of `@oneOf` and `@specifiedBy`;
- request execution, value coercion, or response production.

The grammar still determines syntax restrictions. For example, a directive definition must have a syntactically valid location list, and variables cannot occur in a constant-value production. Do not relax syntax merely because semantic validation is out of scope.

## Corrections and design risks in `docs/goal.md`

1. The selection-set description says “zero or more”; the September 2025 grammar requires one or more.
2. The string escape list omits `\\b`, `\\f`, and variable-width Unicode escapes. It needs scalar validity, UTF-16 surrogate handling, and invalid escape cases.
3. Block-string processing omits the escaped triple-quote production; tests must follow the normative block-string algorithm, not only common-indent examples.
4. “Zero-copy” cannot describe every evaluated string value. Escapes and block-string normalization change the value; keep raw span and decoded value distinct and measure allocation goals against correctness.
5. `IReadOnlyList<T>` does not by itself make an AST immutable when its backing list can still change. Choose an immutable ownership strategy and test it.
6. The AST examples are illustrative, not a complete taxonomy. The grammar matrix is authoritative for missing nodes, descriptions, directive locations, enum values, and SDL fields.
7. Parsing an AST and matching its serialized JSON do not prove validation parity. Normalize node kinds, child order, decoded values, and locations; omit reference-runtime object metadata and compare parser acceptance separately from validation.
8. Parser recovery must use grammar-aware synchronization with guaranteed progress and bounded diagnostics. A bare field name is not a reliable recovery boundary.
9. “100% of fixture tests” must name the pinned corpus and version. Report corpus coverage and exclusions rather than implying all GraphQL programs or validation rules are covered.

## Implementation evidence

Current status: the September 2025 lexical, executable, and SDL grammar implementation, immutable AST, canonical parser-to-oracle comparison, diagnostic recovery, measured performance baseline, deterministic mutation fuzzing, and configurable resource limits are implemented. In addition to the 23 targeted oracle fixtures, `tests/Fixtures/ReferenceSuite/corpus.json` captures 413 applicable cases from 148 passing GraphQL.js 16.14.0 language tests: 110 parser, 287 lexer, and 16 block-string cases. It preserves exact UTF-16 source code units (including lone surrogates), test identities, expected results, and explicit exclusions. Reproduce and freshness-check it with `npm run reference:write` and `npm run reference:check`, setting `GRAPHQL_JS_CHECKOUT` to the pinned source checkout and `GRAPHQL_JS_NODE` to Node 20. The full upstream test identity accounting is tracked separately in [the pinned reference suite inventory](reference-suite-inventory.md); inventory classification does not imply C# conformance. All 413 generated cases and all 23 targeted oracle fixtures pass. These finite corpora and the parser-only scope do not establish exhaustive GraphQL conformance or validation parity. See [release readiness](release-readiness.md) for verified gates and exclusions.
