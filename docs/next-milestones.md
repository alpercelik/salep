# GraphQL language and API parity milestones

All planned milestones are now complete. The release gates and exact supported surface are recorded in [release readiness](release-readiness.md); individual implementation and verification evidence remains in the completed Backlog tasks.

## Objective and boundary

Deliver a production-ready C# GraphQL language library aligned with the pinned September 2025 specification. The library covers lexing, parsing, syntax trees, source locations, printing, and language-level helpers. Schema-dependent validation, operation validation, value coercion, and execution remain outside its scope. Compatibility work must be based on independently observed public API contracts and black-box behavior; no third-party implementation source is imported.

Repository documents, fixtures, and implementation must remain generic GraphQL language work. Do not import or name an external consuming application or an external implementation in repository artifacts. Use this library's own namespace and assembly identities; compare public type and member contracts after that namespace adjustment. Consumers will update their imports when switching packages.

The continuously runnable test harness is a gate for every task. Each completed implementation task adds relevant positive, negative, boundary, and regression cases, records focused and full-suite results, and is committed separately.

## Milestone 10: Account for the complete reference language suite

1. Inventory every test in the pinned reference repository and generate a stable case manifest. Each case has a source file, test identity, feature category, and outcome: applicable, implemented, or excluded with a precise scope reason. The manifest must fail freshness checks when upstream case identities change.
2. Expand the oracle to every applicable language test, beyond the four files currently captured. Add independently generated expectations for acceptance, AST shape, locations, diagnostics, and output where relevant. Every applicable case must pass; no unexplained skips are allowed.
3. Resolve the 30 currently excluded cases: support the language APIs and options that fit this library, including schema-coordinate parsing, location suppression, opt-in legacy fragment variables, and block-string printing and predicates. Keep nonstandard syntax behind an explicit option and document its behavior.
4. Cover language-level printing, source, location, visitor, and predicate behavior when present in the pinned suite. Keep an explicit scope classification for validation, execution, and other non-language suites; do not describe their exclusion as passing conformance.

## Milestone 11: Public language API compatibility

1. Produce a public API contract inventory from package metadata and independently written consumer examples. Record entry points, options, AST node types, interfaces, constructors, members, immutable rewrite methods, formatting, exceptions, and target frameworks. Keep the inventory generic in repository documentation; see [the normalized signature baseline](public-api-contract.json) and [its scope notes](public-api-contract.md).
2. Implement the required API surface with original code under project-owned namespaces and assemblies. Preserve the library's existing API where practical and define explicit adapters or a compatibility layer when signatures differ.
3. Add a standalone compile-and-run consumer fixture that uses only the replacement packages. It must exercise parsing, AST traversal and construction, immutable rewrites, printing, diagnostics, and representative executable and SDL documents. Compare observable results through golden fixtures and structural assertions.
4. Verify package contents, framework targets, namespace-adjusted public API signatures, and a clean restore/build without access to the original packages at test time. Record any intentional API differences in a machine-checkable allowlist; the release gate requires that list to be empty for the selected compatibility surface.

## Milestone 12: Ownership, stress, performance, release

1. Make the default parse path own immutable source storage so callers may mutate or release their input after parsing. Keep any borrowed-memory path explicit and documented. Add mutation, lifetime, and concurrent-access regressions for source-backed AST values.
2. Expand benchmarks to a versioned, representative corpus: small and large executable documents, SDL, strings, fragments, malformed inputs, and adversarial nesting. Measure allocation and throughput in repeated runs on the same machine, retaining raw results and environment details. Use reproducible regression thresholds; do not present local rates as universal guarantees.
3. Run clean Release builds, the full conformance and consumer suites, deterministic fuzzing, resource-bound tests, and package verification. Update the coverage matrix and release report with exact test counts, exclusions, supported options, ownership contract, and measured performance limits.

## Release decision

The release gates passed for the chosen language API contract and every applicable case in the pinned reference language corpus. The full pinned test inventory accounts for every upstream identity and labels each as applicable, contract-review, or out of scope. The parser targets the pinned grammar and language utilities; it does not provide schema validation or execution. Verified build, test, package, fuzz, resource, and benchmark results are in [release readiness](release-readiness.md).
