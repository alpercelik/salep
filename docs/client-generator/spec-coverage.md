# Salep C# Client Generator — Feature Coverage

This checklist tracks which GraphQL specification features are exercised by the coverage schema and generated operations, and therefore by the generated C# client and tests. Treat it as a living document and update as coverage expands.

Legend:

- [x] Covered by src/samples/Opinionated/Salep.Samples.Opinionated.Client/schema.coverage.graphql or src/samples/Opinionated/Salep.Samples.Opinionated.Client/graphql operations
- [ ] Not covered yet or not verified

## SDL (Schema Definition Language)

- [x] Schema definition (schema { query/mutation/subscription })
- [x] Schema extension (extend schema)
- [x] Object types
- [x] Input object types
- [x] Enum types
- [x] Scalar types
- [x] Union types
- [x] Interface types
- [x] Interface inheritance (interface A implements B)
- [x] Type extensions (extend type)
- [x] Interface extensions (extend interface)
- [x] Union extensions (extend union)
- [x] Enum extensions (extend enum)
- [x] Input extensions (extend input)
- [x] Scalar extensions (extend scalar)
- [x] Directive definitions (all allowed locations)
- [x] Repeatable directives
- [x] Deprecation directive on fields and enum values
- [x] @specifiedBy on scalars (SDL only)
- [x] Default values on input fields (scalars, lists, objects)
- [x] Default values on field arguments (scalars, lists, objects)

## Executable Document (Operations)

- [x] Queries
- [x] Mutations
- [x] Subscriptions
- [x] Operation names
- [x] Variables
- [x] Variable default values
- [x] Aliases
- [x] Named fragments
- [x] Fragment spreads across files
- [x] Inline fragments
- [x] Multiple inline fragments per union/interface
- [x] Directives on operations
- [x] Directives on fields
- [x] Directives on fragment definitions
- [x] Directives on fragment spreads
- [x] Directives on inline fragments

## Type System Behaviors

- [x] Non-null fields
- [x] List types
- [x] Nested list and non-null combinations
- [x] Union and interface selection sets with __typename
- [x] Custom scalar mapping to CLR types
- [x] NodaTime scalar mapping (Instant)

## Value Coercion and Input Shapes

- [x] Defaulted arguments in operations
- [x] List coercion via list arguments
- [x] Input object defaults
- [x] Nested input objects
- [x] Optional vs required inputs

## Custom Scalar Serialization

- [x] @specifiedBy for scalar definitions
- [x] Configurable scalar mapping and sample values

## Evidence Map (Schema, Operations, Tests)

This section links each spec item to concrete evidence in the schema, operations, client, and tests. Items that are SDL-only (metadata) are marked as schema evidence only.

### SDL (Schema Definition Language)

- Schema definition and extension: [src/samples/Opinionated/Salep.Samples.Opinionated.Client/schema.coverage.graphql](../../src/samples/Opinionated/Salep.Samples.Opinionated.Client/schema.coverage.graphql#L1-L7)
- Object, input, enum, scalar, union, interface types: [src/samples/Opinionated/Salep.Samples.Opinionated.Client/schema.coverage.graphql](../../src/samples/Opinionated/Salep.Samples.Opinionated.Client/schema.coverage.graphql#L17-L118)
- Interface inheritance: [src/samples/Opinionated/Salep.Samples.Opinionated.Client/schema.coverage.graphql](../../src/samples/Opinionated/Salep.Samples.Opinionated.Client/schema.coverage.graphql#L40-L49)
- Type and interface extensions: [src/samples/Opinionated/Salep.Samples.Opinionated.Client/schema.coverage.graphql](../../src/samples/Opinionated/Salep.Samples.Opinionated.Client/schema.coverage.graphql#L51-L111)
- Union, enum, input, scalar extensions: [src/samples/Opinionated/Salep.Samples.Opinionated.Client/schema.coverage.graphql](../../src/samples/Opinionated/Salep.Samples.Opinionated.Client/schema.coverage.graphql#L20-L83)
- Directive definitions, locations, repeatable: [src/samples/Opinionated/Salep.Samples.Opinionated.Client/schema.coverage.graphql](../../src/samples/Opinionated/Salep.Samples.Opinionated.Client/schema.coverage.graphql#L9-L15)
- Deprecation on fields and enum values: [src/samples/Opinionated/Salep.Samples.Opinionated.Client/schema.coverage.graphql](../../src/samples/Opinionated/Salep.Samples.Opinionated.Client/schema.coverage.graphql#L25-L26), [src/samples/Opinionated/Salep.Samples.Opinionated.Client/schema.coverage.graphql](../../src/samples/Opinionated/Salep.Samples.Opinionated.Client/schema.coverage.graphql#L51-L53), [src/samples/Opinionated/Salep.Samples.Opinionated.Client/schema.coverage.graphql](../../src/samples/Opinionated/Salep.Samples.Opinionated.Client/schema.coverage.graphql#L92-L93)
- @specifiedBy on scalars: [src/samples/Opinionated/Salep.Samples.Opinionated.Client/schema.coverage.graphql](../../src/samples/Opinionated/Salep.Samples.Opinionated.Client/schema.coverage.graphql#L17-L19)
- Default values on input fields: [src/samples/Opinionated/Salep.Samples.Opinionated.Client/schema.coverage.graphql](../../src/samples/Opinionated/Salep.Samples.Opinionated.Client/schema.coverage.graphql#L58-L83)
- Default values on field arguments: [src/samples/Opinionated/Salep.Samples.Opinionated.Client/schema.coverage.graphql](../../src/samples/Opinionated/Salep.Samples.Opinionated.Client/schema.coverage.graphql#L121-L135)

### Executable Document (Operations)

- Queries, operation names, variables, variable defaults: [src/samples/Opinionated/Salep.Samples.Opinionated.Client/graphql/query.graphql](../../src/samples/Opinionated/Salep.Samples.Opinionated.Client/graphql/query.graphql#L1-L94)
- Mutations and variable defaults: [src/samples/Opinionated/Salep.Samples.Opinionated.Client/graphql/mutation.graphql](../../src/samples/Opinionated/Salep.Samples.Opinionated.Client/graphql/mutation.graphql#L1-L39)
- Subscriptions: [src/samples/Opinionated/Salep.Samples.Opinionated.Client/graphql/subscription.graphql](../../src/samples/Opinionated/Salep.Samples.Opinionated.Client/graphql/subscription.graphql#L1-L19)
- Aliases: [src/samples/Opinionated/Salep.Samples.Opinionated.Client/graphql/query.graphql](../../src/samples/Opinionated/Salep.Samples.Opinionated.Client/graphql/query.graphql#L12-L20), [src/samples/Opinionated/Salep.Samples.Opinionated.Client/graphql/mutation.graphql](../../src/samples/Opinionated/Salep.Samples.Opinionated.Client/graphql/mutation.graphql#L13-L23)
- Named fragments and spreads (across files): [src/samples/Opinionated/Salep.Samples.Opinionated.Client/graphql/fragments.graphql](../../src/samples/Opinionated/Salep.Samples.Opinionated.Client/graphql/fragments.graphql#L1-L23), [src/samples/Opinionated/Salep.Samples.Opinionated.Client/graphql/query.graphql](../../src/samples/Opinionated/Salep.Samples.Opinionated.Client/graphql/query.graphql#L1-L35)
- Inline fragments and multiple inline fragments per type: [src/samples/Opinionated/Salep.Samples.Opinionated.Client/graphql/query.graphql](../../src/samples/Opinionated/Salep.Samples.Opinionated.Client/graphql/query.graphql#L23-L76)
- Directives on operations/fields/fragments/spreads/inline fragments: [src/samples/Opinionated/Salep.Samples.Opinionated.Client/graphql/query.graphql](../../src/samples/Opinionated/Salep.Samples.Opinionated.Client/graphql/query.graphql#L1-L76), [src/samples/Opinionated/Salep.Samples.Opinionated.Client/graphql/fragments.graphql](../../src/samples/Opinionated/Salep.Samples.Opinionated.Client/graphql/fragments.graphql#L1-L15)

### Client Request Construction (JSON)

- Request payload shape (`query`, `operationName`, `variables`): [src/samples/Opinionated/Salep.Samples.Opinionated.Client/Generated/GraphQLClient.cs](../../src/samples/Opinionated/Salep.Samples.Opinionated.Client/Generated/GraphQLClient.cs), [src/samples/Opinionated/Salep.Samples.Opinionated.Client.Tests/GeneratedTests/GraphQLClientPayloadTests.cs](../../src/samples/Opinionated/Salep.Samples.Opinionated.Client.Tests/GeneratedTests/GraphQLClientPayloadTests.cs#L18-L37)

### Response Parsing and Type Behaviors

- Operation response deserialization (queries/mutations/subscriptions): [src/samples/Opinionated/Salep.Samples.Opinionated.Client.Tests/GeneratedTests/OperationsResponseTests.cs](../../src/samples/Opinionated/Salep.Samples.Opinionated.Client.Tests/GeneratedTests/OperationsResponseTests.cs#L16-L333)
- Union discrimination with `__typename`: [src/samples/Opinionated/Salep.Samples.Opinionated.Client.Tests/GeneratedTests/UnionConverterTests.cs](../../src/samples/Opinionated/Salep.Samples.Opinionated.Client.Tests/GeneratedTests/UnionConverterTests.cs#L18-L87)
- Enum and scalar JSON handling (string enums, DateTime/Instant JSON): [src/samples/Opinionated/Salep.Samples.Opinionated.Client/Generated/GraphQLClient.cs](../../src/samples/Opinionated/Salep.Samples.Opinionated.Client/Generated/GraphQLClient.cs), [src/samples/Opinionated/Salep.Samples.Opinionated.Client.Tests/GeneratedTests/OperationsResponseTests.cs](../../src/samples/Opinionated/Salep.Samples.Opinionated.Client.Tests/GeneratedTests/OperationsResponseTests.cs#L16-L333)

### Value Coercion and Input Shapes

- Variable defaults and input object defaults (document-level evidence): [src/samples/Opinionated/Salep.Samples.Opinionated.Client/graphql/query.graphql](../../src/samples/Opinionated/Salep.Samples.Opinionated.Client/graphql/query.graphql#L7-L82), [src/samples/Opinionated/Salep.Samples.Opinionated.Client/graphql/mutation.graphql](../../src/samples/Opinionated/Salep.Samples.Opinionated.Client/graphql/mutation.graphql#L25-L35)
- Nested input objects and lists (schema evidence): [src/samples/Opinionated/Salep.Samples.Opinionated.Client/schema.coverage.graphql](../../src/samples/Opinionated/Salep.Samples.Opinionated.Client/schema.coverage.graphql#L58-L83)
- Client variable serialization (JSON evidence): [src/samples/Opinionated/Salep.Samples.Opinionated.Client.Tests/GeneratedTests/GraphQLClientPayloadTests.cs](../../src/samples/Opinionated/Salep.Samples.Opinionated.Client.Tests/GeneratedTests/GraphQLClientPayloadTests.cs#L18-L37)
- Default values are emitted only when present in the schema or operation documents; if no defaults exist, the generated `Query` text does not include them. Example with defaults: [src/samples/Opinionated/Salep.Samples.Opinionated.Client/Generated/Operations.cs](../../src/samples/Opinionated/Salep.Samples.Opinionated.Client/Generated/Operations.cs)

### Custom Scalar Serialization

- Scalar definitions with @specifiedBy (schema evidence): [src/samples/Opinionated/Salep.Samples.Opinionated.Client/schema.coverage.graphql](../../src/samples/Opinionated/Salep.Samples.Opinionated.Client/schema.coverage.graphql#L17-L19)
- NodaTime scalar mapping and JSON options (client evidence): [src/samples/Opinionated/Salep.Samples.Opinionated.Client/Generated/GraphQLClient.cs](../../src/samples/Opinionated/Salep.Samples.Opinionated.Client/Generated/GraphQLClient.cs), [src/samples/Opinionated/Salep.Samples.Opinionated.Client.Tests/GeneratedTests/UnionConverterTests.cs](../../src/samples/Opinionated/Salep.Samples.Opinionated.Client.Tests/GeneratedTests/UnionConverterTests.cs#L89-L99)

### Coverage Boundaries (Evidence-Based)

- Directive behavior, validation, and execution semantics are server-side and not asserted by the client; evidence shows directives only appear in SDL/operations, not runtime tests: [src/samples/Opinionated/Salep.Samples.Opinionated.Client/schema.coverage.graphql](../../src/samples/Opinionated/Salep.Samples.Opinionated.Client/schema.coverage.graphql#L9-L15), [src/samples/Opinionated/Salep.Samples.Opinionated.Client/graphql/query.graphql](../../src/samples/Opinionated/Salep.Samples.Opinionated.Client/graphql/query.graphql#L1-L76)
- GraphQL error details beyond `message` (e.g., `path`, `locations`, `extensions`) are not modeled in the client response type: [src/samples/Opinionated/Salep.Samples.Opinionated.Client/Generated/GraphQLClient.cs](../../src/samples/Opinionated/Salep.Samples.Opinionated.Client/Generated/GraphQLClient.cs)

## Notes

- This checklist reflects the current coverage schema and operations. If you add new spec features or change coverage scenarios, update this file.
- The generated tests are schema-driven; ensure the schema and operations intentionally include each feature.
- Generation now uses per-project salep.json files; the Salep.ClientGenerator.Cli CLI requires `--config`.
- salep.json supports inheritance through versioned profiles; clients explicitly reference a profile and a separate baseClient contract.
- Relative paths in inherited config values are resolved from the config file that defines them.
- Ownership dedup is transitive across inherited configs: descendants suppress artifacts already owned by ancestors (schema types, converters, shared operation contract, duplicate operations).
- The generator writes `.salep.manifest.json` in each output directory to track locally owned generated artifacts for ancestor-aware suppression.
- For complete multi-project setup examples (`Project1 -> Project2 -> Project3` and corresponding test projects), see [docs/config-inheritance-dedup.md](config-inheritance-dedup.md).

## Response contract regression coverage

`src/Salep.ClientGenerator.Tests/ResponseContractTests.cs` compiles generated response code and verifies JSON names for variables, input fields, response fields, and enums; root fragment expansion and field merging; nested aliases with nullable lists; aliased union/interface variants and invalid discriminators; fragment `__typename` insertion; and equivalent file/directory/glob operation input.

The parser package gate (`scripts/verify-package-compatibility.sh` / `.ps1`) uses an isolated package cache and consumer output directory, and compares the restored parser DLLs with the freshly packed DLLs before compiling the consumer.

## Diagnostic regression coverage

`src/Salep.ClientGenerator.Tests/DiagnosticsReporterTests.cs` verifies that directive definitions and unused scalar declarations do not warn, while unmapped scalars referenced by object/interface fields, arguments, inputs, and operation variables still warn. Coverage includes nested list/non-null wrappers, explicit mappings, case sensitivity, ordering, and duplicate references. This does not imply client-side execution of schema directives.
