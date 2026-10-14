# Configuration contracts

New configurations use `version: 2` and an explicit `kind`. Unknown properties, duplicate keys, obsolete options, and wrong-role references fail validation. The [JSON schema](config.schema.json) is also included in the generator package.

## Upgrading consumer configurations

The configuration schema version is separate from the NuGet package version and the output manifest version. The v0.1.0 package used configuration version 1. Configuration version 2 adds `templates` mappings for profiles, clients and tests; existing fields, defaults, scalar definitions and relative references are unchanged. TypeScript has a separate configuration contract, introduced after v0.1.0, which remains version 1.

`generate` and normal MSBuild generation detect supported version-1 configs and rewrite their version to 2 after configuration, GraphQL and output-ownership checks succeed. The upgrader preserves all other bytes, including settings, whitespace, line endings, encoding and BOM. It uses atomic file replacement and records the upgraded bytes in the generated manifest. A publication failure restores any configs rewritten by that invocation. Already-current files are not rewritten. Unsupported future versions are rejected without modification.

Profiles in the selected client's inheritance chain are upgraded too. A module or test project does not rewrite configurations tracked by its authoritative client: build base clients before modules and tests, as usual. External profiles granted through read-only roots remain version 1 and produce an informational log; upgrade them through generation in their owning solution. `validate`, `inputs` and tracking preflight accept version 1 but never upgrade files. This migration does not change source paths to match this repository's sample-folder renames or edit NuGet dependencies.

```json
{
  "version": 2,
  "kind": "client",
  "schema": "./schema.graphql",
  "operations": "./graphql",
  "namespace": "MyService.GraphQL",
  "clientName": "MyGraphQLClient",
  "output": "./Generated"
}
```

| Role | Properties |
| --- | --- |
| `profile` | Optional `extends` profile, `schema`, shared settings below. No output, namespace, client name, or operations. |
| `client` | Optional `profile`, `baseClient`; `schema` locally or from a profile; `operations` (default `./graphql`), `namespace` (default `Salep.Generated`), `clientName` (default `GraphQLClient`), `output` (default `./Generated`), `emitSample` (default false), shared settings. |
| `tests` | Required `client`; `output` (default `./GeneratedTests`), `namespace` (default client namespace plus `.Tests`), `indentSize`, `rawJsonLiterals` (default true), `templates`, `suites`. Client settings are forbidden. |

`operations` is optional. Omit it when generating schema types and transport contracts without operations:

```json
{
  "version": 2,
  "kind": "client",
  "schema": "./schema.graphql",
  "namespace": "MyService.GraphQL",
  "output": "./Generated"
}
```

The default remains `./graphql` for compatibility: if files exist there, they are included. An absent folder or an empty directory/glob emits schema, transport and the shared operation contract without concrete operations. Adding or removing matching operation files is tracked by MSBuild and regenerates the client. Matched documents still undergo normal GraphQL validation.

Client and tests roles also accept `emitAgentInstructions` (default true). All relative paths resolve from the file declaring them. A `baseClient` establishes ownership only; consume the same `profile` explicitly to share settings.

| Shared setting | Default | Meaning |
| --- | --- | --- |
| `unionRepresentation` | `dunet` | `dunet` or `native`; native requires .NET 11 and preview C#. |
| `scalarPreset` | `builtin` | `builtin` or `nodatime`; the latter maps DateTime and Instant to NodaTime.Instant. |
| `scalars` | built-in definitions | GraphQL name to complete `{ "type", "isValueType", "sampleExpression"?, "sampleJson"? }` definition. |
| `omitUnusedVariables` | false | Remove GraphQL variables not used in the operation or its fragments. This does not control null serialization. |
| `inlineDefaultVariables` | false | Substitute GraphQL variable defaults into the document. |
| `useHttpGet` | false | Eligible queries use GET; mutations use POST. |
| `maxGetUrlLength` | 2048 | Positive URL-length limit before GET falls back to POST. |
| `enableBatching` | false | Enable sending an array of operations in one POST. |
| `templates` | embedded defaults | Template-key to file-path overrides; profiles, clients and tests may declare mappings. See [customization](template-customization.md). |
| `indentSize` | 4 | Spaces per indentation level, from 0 through 16. |

Ordinary settings resolve as defaults → profile chain → client. Scalars resolve as built-ins → selected preset → explicit definitions. Each explicit definition replaces the whole entry, including sample values. `sampleExpression` must be valid C# syntax and `sampleJson` must be a string containing valid JSON; provide both when requested generated samples/tests use the scalar. Consumer compilation checks arbitrary external C# type compatibility.

A separate test project references the generated client project and selects its contract:

```json
{
  "version": 2,
  "kind": "tests",
  "client": "../MyService.Client/salep.json",
  "namespace": "MyService.Client.Tests",
  "suites": ["transport", "operations", "unions"]
}
```

Default suites are `transport`, `operations`, and `unions`; `samples` is added only if the client emits samples. Explicitly requesting unavailable samples is an error. Test generation derives scalar types, union representation, operation signatures, and transport expectations from the verified client contract. Referenced union ownership never suppresses union tests.

Use `salep validate --config salep.json` (or the bundled CLI DLL with `dotnet`) to validate without publishing generated output. Generation accepts configuration selection and a working directory, with no schema, output, or behavior overrides. MSBuild selects exactly one configuration, generates after referenced projects build, verifies configured dependencies against the project-reference chain, and includes exact files from the output manifest. `SalepEnabled=false` disables generation.

Generation renders and validates before writing, serializes writes to each output directory, removes only previously owned files, and publishes its manifest last. Missing, stale, incompatible, or modified dependency outputs fail with structured `SALEPS` diagnostics; build the authoritative client first. Output directories must not overlap. Keep NuGet dependencies and versions in developer-owned project/package files. No legacy aliases or automatic package editing are supported.

## Diagnostics

CLI failures return a nonzero exit code and JSON diagnostics with `Code`, `ConfigurationPath`, `Property`, `Message`, and `Guidance`. MSBuild also reports dependency-context failures; configuration selection errors are plain MSBuild errors.

| Code | Meaning and correction |
| --- | --- |
| `SALEPS0001` | Unsupported or incomplete CLI arguments; use configuration selection and `--help`. |
| `SALEPS1001` | Invalid configuration, duplicate keys, or missing input; correct the identified property/path. |
| `SALEPS1002` | Reference cycle; break the profile or client cycle. |
| `SALEPS1003` | Unknown, obsolete, or wrong-role property; remove it or configure it in the authoritative client. |
| `SALEPS1004` | Wrong referenced role or attempted profile generation; select the required role. |
| `SALEPS1005` | Unavailable samples, incomplete scalar sample data, or unnamed operations; complete the requested contract. |
| `SALEPS1006` | Input/rendering/output failure at the CLI boundary; inspect the detailed message. |
| `SALEPS1007` | Path boundary violation; keep reads/writes within the solution or explicitly grant external read roots. |
| `SALEPS2001` | Missing, incompatible, or unreadable manifest; generate the dependency first, repairing invalid ownership artifacts if necessary. |
| `SALEPS2002` | Stale configuration/input/output contract; rebuild the authoritative client before dependents. |
| `SALEPS2003` | Conflicting output ownership/path; use separate output directories and preserve unowned files. |
| `SALEPS2004` | Incompatible shared schema, scalar, union, operation, or qualified symbol; align the referenced contract. |
| `SALEPS3001` | Native unions require `net11.0` and `<LangVersion>preview</LangVersion>`. |
| `SALEPS3002` | Configured dependency is absent from project references; add the corresponding `ProjectReference`. |

## Path selection

The CLI accepts `--working-directory`, `--solution-directory`, and repeatable `--read-root` arguments. MSBuild exposes `SalepSolutionDirectory` and `SalepReadRoot` items. These are invocation-level permissions, not JSON configuration properties. See [filesystem boundaries](consumer-guide.md#filesystem-boundaries) for discovery, relative-path resolution, shared solution files, external read grants and link restrictions.

Schema and operation syntax, semantic and parser-resource-limit errors stop generation before output publication. `SALEPS1008` identifies GraphQL input errors and includes the original source path, line and column when available. Both `generate` and `validate` perform these checks. C# deliberately permits unused variable declarations to preserve the `omitUnusedVariables` option; unknown variables and incompatible variable types still fail validation.

CLI tracking outputs (`--inputs-file`, `--sources-file`, `--stamp-file`) must be separate from generator inputs, the running tool directory, owned current/stale outputs, control files and each other. Case aliases, physical path aliases and file/directory conflicts are rejected before publication. Previously owned files that become inputs are protected from stale-output deletion. Operation ownership signatures use canonical operation content and reachable fragments, independent of input filenames or source coordinates.
