# TypeScript correctness and coverage

The generator validates schema relationships and executable contracts before publishing output. Shared schema semantics include definitions and extensions; response projection preserves selection conditions; deterministic symbol allocation prevents generated-name collisions.

## Selection conditions

```graphql
query Check($show: Boolean!) {
  item(id: "1") { id }
  item(id: "1") @include(if: $show) { name }
}
```

For `item: Item!`, `id: ID!` and `name: String!`, the merged response requires `id` and makes `name` optional. When `show` is false, the server returns only `id`. Parent occurrence conditions propagate into contributed child selections.

Variable `@skip`/`@include` conditions produce conservative optional fields. Constant conditions retain required included fields and omit excluded fields. An unconditional duplicate selection keeps its scalar field required. Fragment dependencies remain in document ASTs even when their spread is constantly excluded.

## Validation before publication

Invalid schema relationships, input/output types, required input cycles, field selections, arguments, variables, literal values, directives, fragment relationships and conflicting response names fail before owned output or manifests change. Nested fragment cycles are checked before response projection. Fragment libraries may contain unused fragments; unused operation variables are rejected.

Regression cases include missing required arguments, undefined or incompatible variables, missing object subselections, scalar subselections, conflicting aliases and arguments, duplicate inputs, invalid enum values, integer boundaries, invalid directive locations and required input fields. Valid defaults, repeatable directives and unused fragment libraries have positive coverage.

Custom scalar runtime coercion remains server-defined. Standard introspection operations have typed response projection and argument validation. OneOf schema/literal/member-variable rules are validated and generated as exclusive input unions. Server integration tests remain necessary.

## Extensions and generated names

Object/interface implementation relationships and union members resolve across schema extensions. Operation shapes, standalone fragments and fragment references use the same relationships.

The symbol allocator reserves schema types, operation families, fragment families, helper names and built-in generic types. Collisions receive a deterministic `Gql` prefix and numeric suffix when needed. Document metadata retains the original GraphQL operation name.

## Verification

[TypeScriptSelectionTests.cs](../../src/Salep.ClientGenerator.Tests/TypeScriptSelectionTests.cs) covers scalar/list nullability, nested enum lists, aliases, merged selections, transitive fragments, abstract branches, variable conditions and scalar input/output mappings.

[TypeScriptParityRegressionTests.cs](../../src/Salep.ClientGenerator.Tests/TypeScriptParityRegressionTests.cs) covers the corrected behavior and checks that rejected generation preserves existing owned files and manifests.

The Node sample compiles negative type assertions, exercises conditional children and extension fragments through HTTP, and compares 45 positive, negative and boundary validation scenarios with graphql-js. Its original inputs live under `src/samples/TypeScript/Node/`. These checks establish behavior for the tested contract rather than complete coverage of every schema feature.

Multiple local/remote schema sources, introspection JSON and OneOf inputs are covered by `TypeScriptSchemaSourceTests`. Configurable extraction tags and generated schema JSDoc are covered by `TypeScriptCapabilityTests` and the original sample inputs.

## Additional capabilities

Source-relative diagnostics are covered by `TypeScriptDiagnosticTests`, including escaped newlines and CRLF.

Client-only resolver integration is enabled by `clientSchema` and explicit `@client` selections. The current configurable output comprises typed ASTs, source lookup, fragment helpers, an optional fetch client and a schema export.

Per-operation output and transitions back to combined output are covered by `TypeScriptCapabilityTests`. Persisted inventories are checked against document IDs and are removed when disabled.

## Local document planning

The library's `TypeScriptClientDocumentPlanner.RemoteQuery` separates selections marked `@client` from a server operation, expands reachable fragments into conditional inline fragments, and omits variables used only by local fields. An entirely local operation returns null. Remote parents whose children are all local retain a `__typename` selection so the parent can still be fetched. Tests validate the resulting remote document against the server schema. Generated fetch-client execution uses this plan when client schema inputs and local selections are present.

`TypeScriptSourceRenderer.ResolverTypes(localTypes)` renders a `LocalResolvers<TContext>` contract for fields defined by local schema models. It preserves argument requiredness/defaults, mapped scalar types, nullable results, and synchronous or Promise results. `ResolverParentTypes` represents partial parent values: selections and resolver chains can supply only part of an object. The `resolvers` template accepts `parents` (name/properties) and `resolvers` (name/fields, each field with name/arguments/result). The generated resolver contracts can be supplied to the fetch client alongside context.

The `local-execution` template supplies `resolveLocalSelections` for generated execution plans. It resolves asynchronous field functions, nested lists, aliases, variable/literal arguments, selection conditions and concrete type branches, returning new objects rather than mutating a server response. Missing resolvers fail explicitly. Generated operation metadata carries these plans; the Node sample verifies mixed server/local execution and local-only operations.

Local execution regressions cover repeated fields across fragments, inactive occurrences, separate aliases, combined nested selections, concrete `__typename` synthesis and abstract discriminator preservation/rejection. The HTTP sample also executes a fully generated repeated local object field and checks one resolver invocation with combined children.

Local input metadata preserves integer literal text, including ID values beyond JavaScript’s exact integer range. Input completion applies singleton-list coercion, nested defaults and required-field checks; omitted nullable variables inside list literals become null. Generated-client regressions exercise these behaviors through local-only operations.

Local result completion validates nullability, list members, built-in scalars, enum members and abstract type discriminators. Resolver parents retain their original field values when another selection uses a conflicting alias. Nullable missing values become null. Abort signals cover local-only operations and pending asynchronous resolvers; application code already running inside a resolver remains responsible for stopping its own work.

[TypeScriptInputReviewRegressionTests.cs](../../src/Salep.ClientGenerator.Tests/TypeScriptInputReviewRegressionTests.cs) checks exact integer/default metadata and abstract output members. [TypeScriptGenerationReviewRegressionTests.cs](../../src/Salep.ClientGenerator.Tests/TypeScriptGenerationReviewRegressionTests.cs) checks JSX text versus expressions, generic TypeScript syntax, keyword escaping and resolver/helper name collisions. The Node sample compiles schemas with these colliding names in both output variants.

Document loading enables JSX syntax for `.tsx` files and treats `.ts` files as TypeScript, preserving angle-bracket assertions even when later strings contain closing-tag text. Direct extractor callers can select the same behavior through its optional `jsx` argument.

The Node sample continuously compiles combined unmasked output and per-operation masked output, including resolver helper name collisions. Publication rejects case-aliased input/output overlaps conservatively on every platform, before modifying inputs or generated files.

Input schema validation rejects recursive expansion of input-field defaults before generation. Static documents inside ordinary JavaScript template interpolations are extracted, while regular expression literals remain excluded from document discovery. Concrete local object identity follows the declared output type.

Embedded static template strings use JavaScript’s cooked newline behavior: raw CRLF and CR become LF, while explicit escaped carriage returns retain their meaning. Diagnostic locations continue to refer to original file offsets. Schema checks include directive dependency cycles, built-in directive argument constraints and duplicate fields inside custom scalar literals.

Introspection `includeDeprecated` arguments follow the normative schema in [GraphQL September 2025, section 4.2](https://spec.graphql.org/September2025/#sec-Schema-Introspection): non-null Boolean with a false default. Explicit null fails; omission remains valid. Older server contracts and the specification’s appendix may expose the nullable form.
