# Pinned reference suite inventory

The inventory at [test-inventory.json](../tests/Fixtures/ReferenceSuite/test-inventory.json) accounts for the pinned GraphQL.js 16.14.0 test suite at commit `57b385b288150960acd09337adf2fc778abb32ab`.

It records 1,980 core Mocha test identities from 119 test files and three package integration identities. Each identity has a stable ID, source file, full title, feature area, applicability decision, and reason. The pinned suite currently has zero pending tests; the aggregate pending count is recorded without trying to associate statuses with duplicate test titles. Two separately invoked fuzz programs are listed with their invocation and scope. The inventory also records hashes for every `src` TypeScript input, test configuration and loader, package test definitions, and integration fixture metadata. Generation fails if discovered files differ from the pinned Git tree or their contents differ from the pinned commit.

The current classification is:

| Applicability | Identities | Meaning |
| --- | ---: | --- |
| Applicable | 220 | GraphQL language lexing, parsing, AST, source, printing, traversal, and language helper behavior. |
| Contract review | 53 | Error and AST utility APIs to resolve against the selected public API contract before claiming parity. |
| Out of scope | 1,710 | Schema semantics, validation, execution, runtime internals, test infrastructure, and downstream package integration. |

These counts account for tests; they do not mean the cases have passed in this C# implementation. The checked-in differential corpus currently contains 413 passing cases from the language tests; see [release readiness](release-readiness.md) for its exact coverage and remaining implementation work. The 53 contract-review cases are not silently skipped: milestone 11 resolves whether they belong to the selected public API surface.

Regenerate or check the inventory from a read-only checkout at the pinned commit. Node 20 is required by the upstream test runner:

```sh
GRAPHQL_JS_CHECKOUT=/path/to/graphql-js-v16.14.0 \
GRAPHQL_JS_NODE=/path/to/node20 \
npm run reference:inventory:write

GRAPHQL_JS_CHECKOUT=/path/to/graphql-js-v16.14.0 \
GRAPHQL_JS_NODE=/path/to/node20 \
npm run reference:inventory:check
```

The exporter uses Mocha's dry-run reporter to enumerate test names without running test bodies. It reads integration identities from their source declarations and fixture metadata because those tests build and install packages. It verifies the relevant source files against Git before reading them and does not write to the upstream checkout.
