# Parser release readiness report

## Supported surface

The parser targets the GraphQL specification's September 2025 edition. It parses lexical input, executable documents, and schema definition language (SDL) syntax into an immutable, source-located AST. It does not build or validate schemas, validate executable operations against a schema, or execute requests. The syntax coverage matrix and detailed exclusions are in [spec coverage](spec-coverage.md).

## Differential coverage

The committed oracle is `graphql-js` 16.14.0. All 23 committed fixtures pass: 7 valid fixtures are compared by canonical AST projection and 16 invalid fixtures are compared for rejection behavior. `npm run oracle:check` verifies that committed expected output still matches the pinned oracle; the .NET corpus tests compare parser output and invalid behavior. This is a finite regression corpus, not the complete upstream GraphQL specification test suite and not proof of exhaustive conformance.

## Release-candidate gates

Verified on 2026-09-20:

| Gate | Result |
| --- | --- |
| Clean Release build and full .NET test suite | 181 passed, 0 failed, 0 skipped |
| Pinned oracle fixtures | 23 passed (7 valid, 16 invalid) |
| Deterministic mutation fuzz | 512 cases, seed `20260925`, passed |
| Deep nesting resource stress | 10,000 nested delimiters rejected at configured depth before recursive descent |
| Default source bound | Input one UTF-16 code unit above 1,048,576 rejected before tokenization |
| Repeated/concurrent parsing | 100 sequential and 64 concurrent parses produced identical canonical AST output |
| Benchmark harness smoke | Lexer, strict executable/SDL parsing, and diagnostic parsing completed |

The parser defaults to a 1,048,576 UTF-16 code-unit source limit, 250,000 non-EOF tokens, combined delimiter nesting depth 128, and 100 diagnostics. Applications can configure these bounds through `GraphQLParserOptions`; exceeding a source, token, or nesting bound throws `GraphQLResourceLimitException` in both strict and diagnostic modes. The current full benchmark comparison and the decision not to keep an unproven pooling or interning change are in [performance baseline](performance-baseline.md).

## Remaining limits

- The 23 oracle fixtures are targeted examples, not the entire language's conformance suite. No claim is made that every valid and invalid document in the specification has been independently tested.
- Parsing is syntactic. Schema-dependent validation, operation validation, value coercion, and execution are outside this library's scope.
- The benchmark corpus is small and fixed, and its local throughput values are sensitive to machine load. They are comparative measurements, not performance guarantees.
- `SourceText` and source-backed AST values retain caller-owned memory. Callers must keep that backing storage alive and must not mutate it while the AST is in use.
