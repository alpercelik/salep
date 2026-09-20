# Parser release readiness report

## Supported surface

The parser targets the GraphQL specification's September 2025 edition. It parses lexical input, executable documents, and schema definition language (SDL) syntax into an immutable, source-located AST. It does not build or validate schemas, validate executable operations against a schema, or execute requests. The syntax coverage matrix and detailed exclusions are in [spec coverage](spec-coverage.md).

## Differential coverage

The reference implementation is pinned to GraphQL.js 16.14.0, source commit `57b385b288150960acd09337adf2fc778abb32ab`. The original oracle has 23 fixtures (7 valid and 16 invalid), all passing. The expanded checked-in corpus captures 413 applicable cases from 148 passing upstream tests: 110 parser cases, 287 lexer cases, and 16 block-string cases. All pass in the C# harness with acceptance, canonical AST, token value, span, error category, and error offset checks. Source strings are stored as UTF-16 code units, preserving lone surrogates exactly. The source files, hashes, case identities, exclusions, and pinned provenance are recorded in `tests/Fixtures/ReferenceSuite/corpus.json`.

The full pinned upstream test inventory accounts for 1,980 core test identities and three package integration identities, with two additional fuzz programs listed separately. It classifies 220 GraphQL language tests as applicable, 53 error/AST utility tests for public API contract review, and 1,710 schema, validation, execution, infrastructure, and integration tests as outside parser scope. Inventory counts are not pass counts; only the 413 cases above currently have differential C# results. See [pinned reference suite inventory](reference-suite-inventory.md).

Regenerate and verify the expanded corpus with `npm run reference:write` and `npm run reference:check`, setting `GRAPHQL_JS_CHECKOUT` to a checkout at the pinned commit and `GRAPHQL_JS_NODE` to Node 20. The four executed upstream files are `lexer-test.ts`, `parser-test.ts`, `schema-parser-test.ts`, and `blockString-test.ts`. Thirty upstream helper cases are excluded from the document corpus: 15 block-string formatting/predicate cases, 12 schema-coordinate helper calls, two legacy fragment-variable option cases, and one location-suppression option case. Dedicated C# API tests cover these helper behaviors separately from the 413 document and token cases. The 23-case oracle is retained as an independently generated regression set. These selected language suites do not represent every GraphQL.js test or prove exhaustive specification conformance.

## Release-candidate gates

Verified on 2026-09-20:

| Gate | Result |
| --- | --- |
| Clean Release build and full .NET test suite | 601 passed, 0 failed, 0 skipped |
| Pinned oracle fixtures | 23 passed (7 valid, 16 invalid) |
| GraphQL.js reference language corpus | 413 passed (110 parser, 287 lexer, 16 block-string); 148 upstream tests passed |
| Corpus regeneration check | Passed at pinned GraphQL.js commit |
| Deterministic mutation fuzz | 512 cases, seed `20260925`, passed |
| Deep nesting resource stress | 10,000 nested delimiters rejected at configured depth before recursive descent |
| Default source bound | Input one UTF-16 code unit above 1,048,576 rejected before tokenization |
| Repeated/concurrent parsing | 100 sequential and 64 concurrent parses produced identical canonical AST output |
| Benchmark harness smoke | 50 iterations; lexer, strict executable/SDL parsing, and diagnostic parsing completed |

The parser defaults to a 1,048,576 UTF-16 code-unit source limit, 250,000 non-EOF tokens, combined delimiter nesting depth 128, and 100 diagnostics. Applications can configure these bounds through `GraphQLParserOptions`; exceeding a source, token, or nesting bound throws `GraphQLResourceLimitException` in both strict and diagnostic modes. The language utility surface includes GraphQL source metadata, source excerpts, quoted and block-string formatters, AST predicates, a deterministic syntax printer, and read-only visitor traversal. Editable visitor rewrites and the namespace-adjusted full public API inventory remain in the public API compatibility milestone. The current full benchmark comparison and the decision not to keep an unproven pooling or interning change are in [performance baseline](performance-baseline.md).

## Remaining limits

- The 413 applicable corpus cases cover four pinned GraphQL.js language test files, not the entire GraphQL.js test suite. Thirty helper cases remain outside that corpus and are covered by dedicated API tests; the full inventory still includes unimplemented and out-of-scope cases.
- Parsing is syntactic. Schema-dependent validation, operation validation, value coercion, and execution are outside this library's scope.
- The benchmark corpus is small and fixed, and its local throughput values are sensitive to machine load. They are comparative measurements, not performance guarantees.
- `SourceText` and source-backed AST values retain caller-owned memory. Callers must keep that backing storage alive and must not mutate it while the AST is in use.
