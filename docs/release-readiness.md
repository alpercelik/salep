# Parser release readiness report

## Supported surface

The parser targets the GraphQL specification's September 2025 edition. It parses lexical input, executable documents, and schema definition language (SDL) syntax into an immutable, source-located AST. It does not build or validate schemas, validate executable operations against a schema, or execute requests. The syntax coverage matrix and detailed exclusions are in [spec coverage](spec-coverage.md).

## Differential coverage

The reference implementation is pinned to GraphQL.js 16.14.0, source commit `57b385b288150960acd09337adf2fc778abb32ab`. The original oracle has 23 fixtures (7 valid and 16 invalid), all passing. The expanded checked-in corpus captures 1,226 applicable cases from 220 passing upstream tests across 12 pinned language test files: 166 parser, 341 lexer, 16 block-string, 79 utility, 24 schema-coordinate parser, 20 printer, 452 predicate, 120 schema-coordinate lexer, and 8 visitor-result cases. All pass in the C# harness with acceptance, canonical AST, token value, span, diagnostic, utility-result, and printer-output assertions. Source strings are stored as UTF-16 code units, preserving lone surrogates exactly. The source files, hashes, case identities, exclusions, and pinned provenance are recorded in `tests/Fixtures/ReferenceSuite/corpus.json`.

The full pinned upstream test inventory accounts for 1,980 core test identities and three package integration identities, with two additional fuzz programs listed separately. It classifies 220 GraphQL language tests as applicable, 53 error/AST utility tests for public API contract review, and 1,710 schema, validation, execution, infrastructure, and integration tests as outside parser scope. Nine captured assertions across eight test identities remain excluded with explicit reasons: experimental nonstandard syntax, detached hand-built printer inputs, JavaScript-specific invalid-object or constructor diagnostics, and a stringification-only assertion. See [pinned reference suite inventory](reference-suite-inventory.md).

Regenerate and verify the expanded corpus with `npm run reference:write` and `npm run reference:check`, setting `GRAPHQL_JS_CHECKOUT` to a checkout at the pinned commit and `GRAPHQL_JS_NODE` to Node 20. The 12 executed upstream files are `lexer-test.ts`, `parser-test.ts`, `schema-parser-test.ts`, `blockString-test.ts`, `predicates-test.ts`, `printLocation-test.ts`, `printString-test.ts`, `printer-test.ts`, `schema-printer-test.ts`, `schemaCoordinateLexer-test.ts`, `source-test.ts`, and `visitor-test.ts`. The previously untested block-string and schema-coordinate helpers, parser options, source utilities, predicates, printing, and traversal results now have differential assertions. The 23-case oracle remains an independently generated regression set. These selected language suites do not represent every GraphQL.js test or prove exhaustive specification conformance.

## Release-candidate gates

Verified on 2026-09-20:

| Gate | Result |
| --- | --- |
| Release test harness | 1,559 passed, 0 failed, 0 skipped; Release build succeeded |
| Pinned oracle fixtures | 23 passed (7 valid, 16 invalid) |
| GraphQL.js reference language corpus | 1,227 focused checks passed (1,226 cases plus provenance); 220 upstream tests passed |
| Corpus regeneration check | Passed at pinned GraphQL.js commit; 1,226 cases from 220 upstream tests |
| Deterministic mutation fuzz | 512 cases, seed `20260925`, passed |
| Deep nesting resource stress | 10,000 nested delimiters rejected at configured depth before recursive descent |
| Default source bound | Input one UTF-16 code unit above 1,048,576 rejected before tokenization |
| Repeated/concurrent parsing | 100 sequential parses, 64 concurrent parses, and 64 concurrent reads of one owned document produced identical results |
| Package and standalone consumer | Clean local-feed restore/build/run passed; package contains the net10.0 assembly, XML docs, and package readme; 119 API contract types and 992 usable members verified with an empty difference allowlist |
| Benchmark harness smoke | 50 iterations; lexer, strict executable/SDL parsing, and diagnostic parsing completed |

The parser defaults to a 1,048,576 UTF-16 code-unit source limit, 250,000 non-EOF tokens, combined delimiter nesting depth 128, and 100 diagnostics. Applications can configure these bounds through `GraphQLParserOptions`; exceeding a source, token, or nesting bound throws `GraphQLResourceLimitException` in both strict and diagnostic modes. The language utility surface includes GraphQL source metadata, source excerpts, quoted and block-string formatters, AST predicates, a deterministic syntax printer, schema-coordinate parsing, visitor traversal, and immutable visitor rewrites. The current full benchmark comparison and the decision not to keep an unproven pooling or interning change are in [performance baseline](performance-baseline.md).

## Remaining limits

- The 1,226 applicable corpus cases cover 12 pinned GraphQL.js language test files, not the entire GraphQL.js test suite. Nine explicitly excluded assertions are listed in the generated corpus; the broader inventory still contains contract-review and out-of-scope cases.
- Parsing is syntactic. Schema-dependent validation, operation validation, value coercion, and execution are outside this library's scope.
- The benchmark corpus is small and fixed, and its local throughput values are sensitive to machine load. They are comparative measurements, not performance guarantees.
- Default document, diagnostic, and schema-coordinate parsing snapshots input into immutable storage. Explicit borrowed-memory entry points and direct low-level lexer use retain caller storage, which must remain alive and unchanged while returned tokens or AST nodes are in use.
