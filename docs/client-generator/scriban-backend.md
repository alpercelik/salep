# Default Scriban backend

Scriban is the sole stable C# generator. The `Salep.ClientGenerator` NuGet package bundles its CLI and MSBuild integration; `.salep.manifest.json` tracks generated ownership.

## Projects

- `src/Salep.ClientGenerator/` contains the target-neutral GraphQL schema and operation models, C# target policies, embedded templates, and generation service.
- `src/Salep.ClientGenerator.Cli/` hosts `generate`, `validate`, and `inputs` commands.
- `src/Salep.ClientGenerator.MSBuild/` contains the props and targets for generating before compile.
- `src/Salep.ClientGenerator.Tests/` exercises parsing, rendering, generated-code compilation, transport behavior, and ownership.
- `src/Salep.ClientGenerator.MSBuild.Tests/` exercises the CLI and imported MSBuild targets, including base-client project references.
- `src/samples/Scriban/` contains isolated consumers using the MinimalDependencies and Opinionated GraphQL schemas and operations.

The generated client projects reference only their chosen C# runtime dependencies. Scriban, Salep's parser, and generator assemblies are build-time tools; generated code does not reference them.

## Configuration

The default backend reads version-1 JSON client and tests configurations. Clients preserve the version-1 configuration contract: clients select a schema, operation files, namespace, output, profile settings, and optional `baseClient`; tests configurations select a client and test suites. Scriban writes its own output manifest so it can validate and remove only files it owns.

```json
{
  "version": 1,
  "kind": "client",
  "schema": "schema.graphql",
  "operations": "graphql",
  "namespace": "Example.Api",
  "clientName": "GraphQLClient",
  "output": "Generated",
  "unionRepresentation": "native",
  "omitUnusedVariables": true,
  "inlineDefaultVariables": false,
  "useHttpGet": true,
  "enableBatching": true,
  "maxGetUrlLength": 2048,
  "templates": {
    "client.members": "templates/ClientMembers.scriban-cs",
    "operations.contract-members": "templates/ContractMembers.scriban-cs"
  }
}
```

The `templates` object is optional and can be used in profiles, client configurations, or tests configurations. Its file paths are relative to the configuration that declares them. A child config overrides matching template keys from its profile or referenced client; all other entries inherit. Every configured template is validated, hashed into the generation manifest, and reported by the `inputs` command so MSBuild rebuilds when a template changes.

The package provides 73 keys: fourteen whole-output templates and 59 smaller fragments/hooks. Prefer a fragment or empty hook for focused customization; use a whole-output key when replacing a complete file. The whole-output keys are:

| Configuration output | Keys |
| --- | --- |
| Client | `schema`, `operations`, `union-converters`, `shared-types`, `client`, `operation-sample`, `client-agent-instructions` |
| Tests | `test-http-handler`, `transport-tests`, `operation-metadata-tests`, `operation-response-tests`, `operations-sample-tests`, `union-converter-tests`, `test-agent-instructions` |

Unspecified templates continue to come from the package's embedded defaults. To copy all defaults for editing, run `salep templates --output-directory templates/salep` with the CLI from the package's `tools/<target-framework>/any` directory. Keep only the selected overrides in the configuration.

Run the built CLI directly with `dotnet exec`:

```bash
dotnet exec artifacts/bin/Salep.ClientGenerator.Cli/debug_net11.0/Salep.ClientGenerator.Cli.dll validate --config salep.json
dotnet exec artifacts/bin/Salep.ClientGenerator.Cli/debug_net11.0/Salep.ClientGenerator.Cli.dll generate --config salep.json
```

NuGet consumers add `Salep.ClientGenerator` as a private build dependency. Its props and targets locate the framework-matched CLI and generate before compilation. Consumers using the source projects can import `Salep.ClientGenerator.props` and `.targets` directly or set `SalepToolPath` to the CLI directory. The targets track schema, operations, configuration, custom templates, manifests, and owned outputs.

## Customization boundary

Templates receive GraphQL schema and executable-document projections plus a `target` service for C# type, name, literal, import, and value-type operations. The public `ScribanTemplateNames` constants define stable override keys for the API. C# naming and scalar mappings can also be customized through `CSharpCodeGenerationOptions`.

The current NuGet package and CLI generate C#. `ICodeGenerationTarget` is the language-policy seam for future backends, but the current C# generator entry points accept `CSharpCodeGenerationTarget`; a consumer-defined target language is not yet loadable through the CLI.

## Sample verification

Each client and module owns its `graphql/` operation inputs and selects `"operations": "graphql"`. They share the server schema at `src/samples/Salep.Samples.GraphQLServer/Generated/schema.graphql` and write to their own `Generated` and `GeneratedTests` directories. Operation manifests therefore use `../graphql/...` for client-local documents; module/test manifests also track their referenced client through its real relative path. Run the generated test projects after packing and restoring:


```bash
dotnet test src/samples/Scriban/MinimalDependencies/Client.Tests/Salep.Samples.MinimalDependencies.Scriban.Client.Tests.csproj --framework net11.0
dotnet test src/samples/Scriban/MinimalDependencies/Module.Tests/Salep.Samples.MinimalDependencies.Scriban.Module.Tests.csproj --framework net11.0
dotnet test src/samples/Scriban/Opinionated/Client.Tests/Salep.Samples.Opinionated.Scriban.Client.Tests.csproj --framework net10.0
dotnet test src/samples/Scriban/Opinionated/Client.Tests/Salep.Samples.Opinionated.Scriban.Client.Tests.csproj --framework net11.0
dotnet test src/samples/Scriban/Opinionated/Module.Tests/Salep.Samples.Opinionated.Scriban.Module.Tests.csproj --framework net10.0
dotnet test src/samples/Scriban/Opinionated/Module.Tests/Salep.Samples.Opinionated.Scriban.Module.Tests.csproj --framework net11.0
```

The Opinionated profile exercises Dunet and NodaTime. MinimalDependencies uses native unions and built-in scalar mappings on .NET 11. When adding sample coverage, update the owning operation documents and reviewed contract fixtures when expectations intentionally change.

## Verification

The 69 fixed contract fixtures preserve APIs, operation documents, file inventories and generated test coverage established before Roslyn retirement. Configuration matrices compile and execute generated tests. Runtime regressions and real server acceptance cover request/response behavior; detached-sample checks verify local operation ownership and relative manifest hashes. See [generator contracts](generator-parity.md) for the complete gates and release requirements.

See [consumer template customization](template-customization.md) for smaller fragment overrides, extension hooks, default composition and the complete key/model contract.
