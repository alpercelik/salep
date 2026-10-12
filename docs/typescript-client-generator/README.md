# TypeScript client generator

`src/Salep.ClientGenerator.TypeScript/Salep.ClientGenerator.TypeScript.csproj` owns TypeScript target policy, template projections, embedded Scriban templates and generation. It follows the C# generator's library/CLI pattern and references the parser directly. `Salep.ClientGenerator.Core` shares neutral GraphQL models and filesystem policy between both generators. The executable is `Salep.ClientGenerator.TypeScript.Cli`; both projects appear under `client-generator` in the solution.

## Configuration and generated artifacts

```json
{
  "version": 1,
  "target": "typescript",
  "schema": "schema.graphql",
  "documents": "src/{app,components}/**/*.{ts,tsx,graphql}",
  "output": "src/graphql/generated",
  "fragmentMasking": true,
  "documentTags": ["gql", "graphql", "request"],
  "scalars": { "DateTime": { "input": "string", "output": "string" } }
}
```

Paths resolve from the configuration. `documents` accepts one pattern, an array, a file or a folder. Brace alternatives and recursive globs support the public app's source layout. Standalone GraphQL and static `gql`/`graphql` tags or `graphql()` calls in TS/TSX are scanned without executing source. Comments, ordinary strings, regular-expression literals and unrelated templates are skipped. `${...}` interpolation in a GraphQL template is rejected; use GraphQL fragment spreads across static documents instead. The extractor is not a general TypeScript compiler or arbitrary module loader.

Under the selected output folder:

| File | Contents |
| --- | --- |
| `gql/graphql.ts` | Scalars, input/enum declarations, operation results/variables, fragment types and standard typed AST document constants |
| `gql/gql.ts` | Literal-source overloads and lookup table for `graphql(source)` |
| `gql/fragment-masking.ts` | `FragmentType` and typed `getFragmentData` helpers |
| `gql/index.ts` | Barrel exports |
| `gql/client.ts` | Optional portable fetch client and error types |
| `schema.graphql` | Source schema export, preserving descriptions and comments |
| `.salep.manifest.json` | Relative inputs and owned-output hashes |

The layout separates generated declarations, document lookup, fragment helpers and fetch transport. Standard AST documents work with `graphql-request`, `graphql.print` and other clients accepting `TypedDocumentNode`. Applications need the type package `@graphql-typed-document-node/core` and GraphQL typings to compile these files. Generated fetch transport has no Salep/Scriban runtime dependency. Use your chosen client library's runtime dependencies when consuming the AST directly.

Identical repeated operations/fragments are deduplicated; conflicting definitions fail. Generated operation and fragment symbols retain GraphQL names when no collisions occur. Operations expose `<Name>Result`/`<Name>Variables` and conventional `<Name>Query`/`<Name>QueryVariables` aliases (or Mutation/Subscription). Enum/input names retain their schema names except reserved words or generated-symbol collisions, which receive a deterministic `Gql` prefix and numeric suffix when needed. The allocator checks schema types, operation and fragment symbols, imported helpers and built-in generic types together. Document metadata retains the original GraphQL operation name. Unmapped scalars use `unknown`; scalar mappings describe JSON values and do not add runtime conversions.

## Generate and use

```bash
dotnet run --project src/Salep.ClientGenerator.TypeScript.Cli --framework net10.0 -- generate --config src/samples/TypeScript/Node/salep.typescript.json
dotnet run --project src/Salep.ClientGenerator.TypeScript.Cli --framework net10.0 -- validate --config src/samples/TypeScript/Node/salep.typescript.json
```

`validate` checks schema relationships, executable contracts and the rendered plan without publishing output. Invalid fields, arguments, variables, values, directives, fragment relationships and conflicting selections fail before owned files or manifests change. Fragment libraries may contain unused fragments; unused operation variables are rejected. Validation is shared model logic, with an independent graphql-js comparison suite. Custom scalar coercion remains server-defined; test against your server.

```typescript
import {
  GraphQLClient, ReadBookDocument, BookSummaryFragmentDocument,
  getFragmentData
} from './Generated/gql/index.js';

const client = new GraphQLClient('https://example.test/graphql', fetch, {
  Authorization: 'Bearer token'
});
const response = await client.execute(ReadBookDocument, { id: 'book-1' });
if (response.selected) {
  const book = getFragmentData(BookSummaryFragmentDocument, response.selected);
  console.log(book.title);
}
```

With the standard library, import `GraphQLClient` from `graphql-request` instead and call `client.request(ReadBookDocument, { id: 'book-1' })`. Its result/variables infer from the typed AST. TS/TSX sources can import the generated `graphql` helper and pass a literal operation string; recognized source strings resolve to their typed documents. Unknown strings return `undefined` at runtime and have type `unknown`. A source containing several operations resolves to its first operation, matching the lookup pattern inspected in the reference; use named document constants to select another operation.

Fragment masking defaults to true: fields selected only through named fragments are hidden behind a typed fragment reference until `getFragmentData` is called. This is a compile-time boundary; the helper returns the supplied JSON object. Disable it with `"fragmentMasking": false` to expose expanded fields directly. Salep uses its own fragment-reference branding; it does not depend on generated masking code from another generator. Deferred fragment readiness and optimizer/plugin integrations are not provided.

Aliases, nested list nullability, default/optional variables and abstract `__typename` branches retain their GraphQL meanings. Variable `@skip`/`@include` fields are conservatively optional, including children contributed by conditional duplicate parent selections. Constant conditions retain required included fields and omit excluded fields. Operation and fragment types resolve object/interface/union extensions consistently. The formatter adds discriminants to abstract selections in outgoing operations. `execute` requires `{}` for no variables, returns data, and throws `GraphQLRequestError` for GraphQL errors, HTTP failures or absent data. Partial data with errors is not returned by this convenience client. Headers and `AbortSignal` can be supplied per request; fetch/network failures propagate. Subscription documents require a streaming client, and the fetch client rejects them.

## Own templates and customization

Templates live in `src/Salep.ClientGenerator.TypeScript/Templates/`; C# templates are independent. Export all twenty-two registered templates:

```bash
dotnet run --project src/Salep.ClientGenerator.TypeScript.Cli --framework net10.0 -- templates --output-directory src/samples/TypeScript/Node/template-export
```

Export one with `template --template-key property --output path/to/Property.scriban-ts`. Map selected keys in the configuration:

```json
"templates": {
  "property": "templates/Property.scriban-ts",
  "client": "templates/Client.scriban-ts"
}
```

A decorator can delegate to the embedded fragment:

```scriban
// consumer property policy
{{ include "default:property" }}
```

| Keys | Model |
| --- | --- |
| `documentation` | `lines` (safe description lines), `indent` |
| `graphql` | `schema_types`, `operations`, `fragments` composed declarations |
| `schema`, `operations` | `declarations` array |
| `schema.scalars` | `scalars` with `name`, `input`, `output` |
| `schema.enum` | `name`, union expression `values` |
| `schema.input-oneof` | `name`, `variants` with rendered `properties` and union `suffix` |
| `schema.input`, `operations.variables` | `name`, rendered `properties` |
| `property` | quoted `name`, optional-marker `optional`, TypeScript `type` |
| `operations.response` | `name`, type expression `type` |
| `operations.response-object` | rendered `properties` |
| `operations.operation` | `name`, `kind`, rendered `variables`, `response`, `document` |
| `operations.document` | `name`, standard AST JSON `ast`, `result`, `variables`, `document_type` |
| `lookup` | `entries` with source-string literal `source` and document `name` |
| `fragment-masking`, `index`, `client` | Empty model; customize the owning module |

Unmapped keys retain defaults. Includes accept registered keys or `default:<key>`, never arbitrary paths. `default:` bypasses an override so decorators do not recursively select themselves. Overrides participate in input hashing, with paths relative to their declaring configuration. Put control tags on separate lines, align them with the output they control, and label block openers/end tags. Type expressions and ASTs belong in projections; source declarations and properties belong in templates.

## Filesystem, verification and executable

The nearest `.sln`/`.slnx` folder is the boundary, falling back to the configuration folder. `--solution-directory` selects it explicitly; repeatable `--read-root` grants external reads only. Symlinks/junctions, traversal and portable-invalid path components are rejected. Inputs and output must share a filesystem volume. Manifest paths use `/` and are relative to the manifest. Publication preserves unowned files and unchanged timestamps, refuses modified owned files, and serializes writes with a lock. Verified legacy flat `graphql.ts` output migrates to the new inventory. Hashes are integrity checks, not signatures or an OS sandbox against concurrent hostile processes.

Configuration paths and document globs accept `/` and `\` separators on every platform. Components ending in a dot or space are rejected before filesystem resolution can turn them into aliases; `.` and `..` navigation remains subject to the boundary checks.

```bash
./scripts/verify-typescript.sh
./scripts/publish-typescript.sh osx-arm64
```

```powershell
pwsh ./scripts/verify-typescript.ps1
pwsh ./scripts/publish-typescript.ps1 win-x64
```

The [original Node fixtures](../../src/samples/TypeScript/Node/README.md) generate masked and unmasked variants, compile strict positive/negative type checks, and run real HTTP graphql-js and graphql-request tests. The referenced consumer-platform project is read-only and contributes no copied source/schema/documents to these fixtures. See [the investigation](investigation.md).

Publishing emits a runtime-specific self-contained `salep-typescript` executable under `artifacts/typescript/<RID>`; a second argument selects another directory. Use `salep-typescript generate --config salep.typescript.json` from npm/CI without a .NET runtime. Repository source scripts use `dotnet run` and require the SDK. Trimming/AOT remain disabled for the shared Scriban/configuration runtime. Embedded licenses are available through `salep-typescript notices`. Local PowerShell runs do not prove native Windows behavior.

See [correctness and coverage](correctness-and-coverage.md) for validation behavior, regression coverage and future capabilities.

## Extraction tags and schema documentation

`documentTags` optionally replaces the default `gql`/`graphql` identifiers. Supply unique JavaScript identifiers, for example `["request", "document"]`. Both tagged templates and one-template-argument calls are supported. An imported helper can be aliased to the configured identifier. Unconfigured tags are ignored, and recognized GraphQL templates still reject dynamic interpolation. Names are case sensitive; dotted expressions are not configurable tag names.

Schema descriptions generate JSDoc on enum/input declarations, input fields and selected response fields. Multiline descriptions retain line breaks; comment terminators are made safe. Override the `documentation` template to change comment style or map it to an empty file to suppress comments. Its model contains `lines` and `indent`; description handling does not alter GraphQL requests or JSON shapes.

## Schema sources

`schema` accepts one source or an ordered array. Local sources may be SDL or introspection JSON (`data.__schema`, `__schema`, or a schema object). Multiple SDL files compose definitions and extensions; conflicting definitions fail validation. Local paths retain the same solution/read-root boundaries and relative ownership tracking.

```json
"schema": ["schema/base.graphql", "schema/extensions.graphql"]
```

A URL string downloads SDL or introspection JSON using GET. A remote descriptor can instead POST a schema introspection request, with explicit request headers:

```json
"schema": {
  "url": "https://example.test/graphql",
  "method": "introspection",
  "headers": { "Authorization": "Bearer your-schema-token" }
}
```

`method` defaults to `get`; `introspection` requests schema descriptions, deprecated fields/input values, repeatable directives and OneOf metadata. The endpoint must support those introspection fields. Requests have a 30-second deadline and a 16 MiB response limit. The default transport does not follow redirects. Every generation or validation reloads remote sources. Failed requests or invalid schemas publish nothing. Code hosting the generator can supply `SchemaHttpClient` through `TypeScriptGeneratorOptions` for its own transport policy.

The manifest fingerprints remote SDL under opaque configuration-position IDs such as `schema/0` in `RemoteInputs`; it does not store remote URLs, query strings or headers. Local `Inputs` remain relative filesystem paths. Keep actual credentials out of checked-in configuration; callers can provide transport authentication through their own HTTP client.

## OneOf inputs

An input marked `@oneOf` must have nullable fields without defaults. Values must select exactly one non-null field; variables used for a selected member must be non-null. Generated TypeScript uses an exclusive union with the other keys typed as optional `never`:

```graphql
input BookChoice @oneOf { id: ID title: String }
```

```typescript
const choice: BookChoice = { id: 'book-1' };
// A second selected key, null member, or empty object is rejected by TypeScript.
```

Schema JSON conversion retains `isOneOf`. Customize this output independently with `schema.input-oneof`.

## Source diagnostics

Executable validation and GraphQL syntax/lexical errors identify the source relative to the configuration folder as `file(line,column)`. TS/TSX locations refer to the original source file, including literals after imports and decoded escapes such as `\n`; CRLF is handled as one line break. Source names and locations stay in neutral model metadata, independent of generated output templates. Nested fragment-cycle and unknown-fragment errors retain the spread's source location. Extraction errors identify the offending TypeScript location. Columns count UTF-16 code units, matching .NET and TypeScript tooling.

## Persisted operation inventory

Set `"persistedOperations": true` to generate an owned `operations.json` inventory and an `operationId` on each operation document's `__meta__`. The inventory contains `version: 1`, `algorithm: "sha256"`, and an `operations` object keyed by lowercase SHA-256 IDs. Each entry contains the original operation name, operation type and complete normalized query, including transitive fragments. Hashing uses UTF-8 with LF line endings so IDs agree across operating systems. Changing a referenced fragment changes the ID.

Upload this inventory to a server that implements your persisted-operation protocol. Applications can read `ReadDocument.__meta__.operationId` to construct that protocol's request. The generated fetch client continues sending full queries; enabling the inventory does not configure server registration or automatic persisted-query negotiation. Disabling the option removes the previous owned inventory after checking that it has not been edited. Fragment-only documents have no operation ID.

### Schema documentation and deprecations

Generated JSDoc preserves custom scalar descriptions, enum and enum-value descriptions, input object and input-field descriptions, and selected response-field descriptions. Enum values retain documentation beside their string-literal union member. Input fields and selected response fields retain `@deprecated` with the schema reason (or `No longer supported` when omitted). Aliased response fields use the original schema field's documentation. Comments normalize newlines and escape comment terminators. Override the `documentation` template to change comment rendering independently of declarations.

## Per-operation output

`"outputLayout": "per-operation"` emits common schema types and fragment documents into `gql/shared.ts` and one file per operation under `gql/operations/operation-<allocated-name>.ts`. The default `"combined"` keeps all declarations in `gql/graphql.ts`. In both layouts `gql/graphql.ts` exports the public declarations, so existing lookup, client and fragment-helper imports continue to work. Applications can also import one document directly from its operation module. Operation filenames use allocated TypeScript names and `/` separators. Names that differ only by casing receive stable hash suffixes to avoid collisions on case-insensitive filesystems.

Switching layouts removes previous owned modules after verifying their manifest hashes. Consumer files remain unowned. The `partition.operation` template accepts `imports` (comma-separated shared type names) and `declarations`; `partition.barrel` accepts `modules` (operation filenames without extensions). The existing `graphql` template supplies the common module, with an empty `operations` projection in this layout.

## Typed introspection operations

Operations may select `__schema` and `__type(name: String!)` on the query root, including a custom-named root. Standard introspection objects, nullable wrappers, `__TypeKind` and `__DirectiveLocation` enum values are available to response projection. This includes deprecated input values, repeatable directives, `specifiedByURL` and `isOneOf`. Normal selection, argument and variable validation applies. These built-in types do not permit consumer schema definitions to use reserved `__` names. Remote servers must support the requested introspection fields; server policy can disable introspection.

### Local execution runtime

`gql/local-execution.ts` exports `resolveLocalSelections`, `LocalSelection`, `LocalValue`, and `ResolverMap<TContext>`. This primitive accepts a response object, a local selection plan, variables, resolver functions keyed by type and field, and application context. It supports async results, nested lists, aliases, argument values, include/skip conditions and type branches. It copies response objects and rejects missing selected resolvers. The `local-execution` template can be overridden independently. The Node sample runs its regression cases through `npm test`. Automatic planning and resolver execution are enabled when `clientSchema` is configured and an operation has `@client` selections.

## Client schema and resolver execution

`clientSchema` accepts the same source forms as `schema` and composes local definitions/extensions for validation and result types. Mark local selections with `@client`. The generator emits `gql/resolvers.ts` with `LocalResolvers<TContext>` and partial parent contracts. Import these types directly from that module. Generated documents retain the complete typed AST and include `__meta__.localExecution` with a separate server query, selection plan and variable defaults.

Construct `GraphQLClient` with resolver functions and context as the fourth and fifth arguments after endpoint, fetch and headers. Resolver functions receive parent, arguments and context. `execute` removes local selections and variables from the server query, then resolves selected local fields. An entirely local operation skips HTTP. Missing resolvers reject execution. Mark local field selections, enclosing inline fragments or fragment spreads with `@client`; generation rejects unmarked local fields before publication. Children of an already local field use the object returned by its resolver and do not require separate markers.

With client selections, persisted IDs and inventories describe the server query. Entirely local operations have no persisted ID or inventory entry. Alternative transports receive the complete document AST; use `__meta__.localExecution.query` when constructing a server request and apply the local selection plan separately. The Node sample covers mixed server/local execution and local-only execution.

Introspection JSON diagnostics identify the source file and label projected GraphQL locations as `[converted SDL]`. The Node sample tests remote SDL and introspection loading through a real HTTP server, including request headers and credential-free manifest provenance.

Resolvers that return interface or union values should provide a concrete `__typename` so local fragment branches can be selected. Resolver parents are partial objects; use explicitly selected server fields or application context rather than assuming every schema field was fetched. Local resolvers execute after the remote response; their own exceptions reject `execute`. This helper does not provide a normalized cache.

Local execution merges active field selections by response key across fields and fragments before invoking resolvers. A repeated field resolves once and its child selections combine; separate aliases resolve separately. Conditions and concrete type branches are evaluated before merging. Selected `__typename` fields, including aliases, use the returned concrete discriminator or the known concrete schema type. Abstract interface/union results must supply a concrete discriminator; execution rejects an unavailable abstract `__typename` instead of guessing a type.

Local arguments and variables follow GraphQL input coercion, including singleton-to-list conversion, built-in scalar checks, enum validation, nested input defaults and OneOf constraints. Custom scalar coercion remains application-defined. Local non-null fields and list members reject null/undefined results, matching the client's rejection of erroneous responses. `RequestOptions.signal` applies to entirely local operations and asynchronous resolvers as well as HTTP requests. Cancellation rejects execution promptly; it does not forcibly stop application code already running inside a resolver.

Built-in local result scalars and enum members are completed against their schema types; nullable absent results become null. ID integer literals preserve their original decimal text. Interface and union objects must report an allowed concrete `__typename`. Concrete local objects use their declared schema type for typename output, fragment matching and resolver dispatch, even when a returned object carries a stale discriminator. Aliases change response keys without changing the parent object seen by subsequent resolvers. Resolver-returned objects use schema field names and are projected to the selected response fields; remote objects use the server’s response keys.

Schema sources must contain only type-system definitions/extensions; document inputs must contain only operations/fragments. Syntax, semantic and parser-resource-limit failures report the original input path and mapped location, including embedded TypeScript documents. Validation stops before generated outputs are published.
