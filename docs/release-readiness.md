# Parser release readiness report

## Supported surface

The parser targets the GraphQL specification's September 2025 edition. It parses lexical input, executable documents, and schema definition language (SDL) syntax into an immutable, source-located AST. The supported compatibility package is `GraphQLParser` targeting `net10.0`, with this project's `GraphQLParser` namespace and assembly identity. The selected normalized API inventory has 119 public types and 992 declared public members; runtime signature comparison and package-only consumer verification passed with an empty difference allowlist. The supported API families and target limitation are detailed in [public API contract](public-api-contract.md).

The parser does not build or validate schemas, validate executable operations against a schema, or execute requests. The syntax coverage matrix and detailed exclusions are in [spec coverage](spec-coverage.md).

## Differential coverage

The reference implementation is pinned to GraphQL.js 16.14.0, source commit `57b385b288150960acd09337adf2fc778abb32ab`. The original oracle has 23 fixtures (7 valid and 16 invalid), all passing. The expanded checked-in corpus captures 1,226 applicable cases from 220 passing upstream tests across 12 pinned language test files: 166 parser, 341 lexer, 16 block-string, 79 utility, 24 schema-coordinate parser, 20 printer, 452 predicate, 120 schema-coordinate lexer, and 8 visitor-result cases. All pass in the C# harness with acceptance, canonical AST, token value, span, diagnostic, utility-result, and printer-output assertions. Source strings are stored as UTF-16 code units, preserving lone surrogates exactly. The source files, hashes, case identities, exclusions, and pinned provenance are recorded in `tests/Fixtures/ReferenceSuite/corpus.json`.

The full pinned upstream test inventory accounts for 1,980 core test identities and three package integration identities, with two additional fuzz programs listed separately. It classifies 220 GraphQL language tests as applicable, 53 error/AST utility tests for public API contract review, and 1,710 schema, validation, execution, infrastructure, and integration tests as outside parser scope. Nine captured assertions across eight test identities remain excluded with explicit reasons: experimental nonstandard syntax, detached hand-built printer inputs, JavaScript-specific invalid-object or constructor diagnostics, and a stringification-only assertion. See [pinned reference suite inventory](reference-suite-inventory.md).

Regenerate and verify the expanded corpus with `npm run reference:write` and `npm run reference:check`, setting `GRAPHQL_JS_CHECKOUT` to a checkout at the pinned commit and `GRAPHQL_JS_NODE` to Node 20. The 12 executed upstream files are `lexer-test.ts`, `parser-test.ts`, `schema-parser-test.ts`, `blockString-test.ts`, `predicates-test.ts`, `printLocation-test.ts`, `printString-test.ts`, `printer-test.ts`, `schema-printer-test.ts`, `schemaCoordinateLexer-test.ts`, `source-test.ts`, and `visitor-test.ts`. The previously untested block-string and schema-coordinate helpers, parser options, source utilities, predicates, printing, and traversal results now have differential assertions. The 23-case oracle remains an independently generated regression set. These selected language suites do not represent every GraphQL.js test or prove exhaustive specification conformance.

## Release-candidate gates

Verified on 2026-09-20 from the current checkout:

| Gate | Result |
| --- | --- |
| Clean Release rebuild and test harness | Single-node `Rebuild` succeeded with 0 warnings and 0 errors; 1,559 passed, 0 failed, 0 skipped |
| Pinned oracle fixtures | 23 passed (7 valid, 16 invalid) |
| GraphQL.js reference language corpus | 1,227 focused checks passed (1,226 cases plus provenance); 220 upstream tests passed |
| Reference corpus freshness | Passed at pinned source commit; 1,226 cases from 220 upstream tests; 9 explicitly excluded assertions |
| Full upstream test inventory freshness | 1,983 stable identities from 1,980 core tests and 3 package integrations across 119 core test files; 220 applicable, 53 contract-review, 1,710 out of scope |
| Original oracle fixture freshness | All 23 valid/invalid fixture expectations match the pinned package |
| Deterministic mutation fuzz | 512 cases, seed `20260925`, passed |
| Resource-bound tests | 14 focused tests passed, covering configurable and default input/token/depth/diagnostic limits and failure behavior |
| Standalone API consumer | Passed; parsed and traversed representative documents and visited 19 AST nodes |
| Package-only consumer and contents | Temporary local-feed restore/build/run passed with 0 warnings/errors; package contains only the `net10.0` assembly plus XML docs and package readme; 119 types and 992 declared members match with an empty difference allowlist |
| Reproducible benchmark | Corpus v1 integrity passed: 8 categories/cases, 1,000 measured iterations × 3 repetitions, 48 raw samples across 16 operations; all 16 deterministic allocation budgets passed |

The supported language API includes document and schema-coordinate parser entry points and parser options; lexer, tokens, syntax exceptions and diagnostics; immutable AST nodes and source/location helpers; quoted/block-string formatters and predicates; a deterministic printer; and visitor, navigator, and immutable rewrite APIs. UTF-8 reader/parsing APIs are included. HTTP request envelopes, request caches, operation-document hashing and its wrapper, schema-dependent validation, and execution are excluded. The complete selected inventory is [machine-readable](public-api-contract.json) and [described here](public-api-contract.md).

Default document, diagnostic, and schema-coordinate parsing snapshots input into immutable storage. Explicit borrowed-memory entry points and direct low-level lexer use retain caller storage, which must stay alive and unchanged while returned tokens or AST nodes are in use. Performance numbers and deterministic allocation budgets are recorded in [performance baseline](performance-baseline.md); throughput is local comparative data, not a guarantee.

## Remaining limits

- The 1,226 applicable corpus cases cover 12 pinned language test files, not the entire reference test suite. Nine explicitly excluded assertions across eight test identities are listed with reasons; the full inventory separately accounts for 53 contract-review and 1,710 out-of-scope test identities.
- Parsing is syntactic. Schema-dependent validation, operation validation, value coercion, and execution are outside this library's scope.
- The benchmark corpus is versioned and representative but remains small. Allocation thresholds apply only to its 16 case/operation combinations; measured throughput is sensitive to machine load and is not a performance guarantee.
