# Salep C# Client Generator contributor guide

Use the .NET SDK pinned by `global.json`. Core projects target .NET 10 and .NET 11; compiler-native unions require .NET 11 and preview language support. Open `src/Salep.slnx` for the full solution or `src/Salep.Core.slnf` for core projects and benchmarks. The standalone parser package consumer remains outside both.

## Project ownership

| Location | Responsibility |
| --- | --- |
| `src/Salep.GraphQLParser/` | Independent language library |
| `src/Salep.ClientGenerator/Generation/` | Configuration, orchestration, input discovery and ownership manifests |
| `src/Salep.ClientGenerator/Model/` | Target-neutral schema/executable models and GraphQL semantics |
| `src/Salep.ClientGenerator/Targets/` | C# policies, template projections, sample values and rendering |
| `src/Salep.ClientGenerator/Templates/` | Embedded whole-output templates and composable fragments/hooks |
| `src/Salep.ClientGenerator.Cli/` | Out-of-process CLI |
| `src/Salep.ClientGenerator.MSBuild/` | Public package, props and targets |
| `src/Salep.ClientGenerator.Tests/` | Contract fixtures, rendering, compiled runtime and configuration regressions |
| `src/Salep.ClientGenerator.MSBuild.Tests/` | CLI/MSBuild integration |
| `src/samples/` | Opinionated and MinimalDependencies package consumers |
| `src/samples/Salep.Samples.GraphQLServer/` | Shared real server and exported schema |

Scriban is the sole stable generator. Applications reference runtime dependencies such as System.Text.Json, Dunet and NodaTime only when selected; generator/parser/template assemblies remain private tools. Compiler libraries in tests validate generated consumers and are not part of the tool distribution.

## Generation flow

```text
GraphQL SDL + operations + version-1 JSON configuration
  -> Salep.GraphQLParser syntax trees
  -> GraphQlModelFactory neutral semantic models
  -> CSharpCodeGenerationTarget and template model factories
  -> ScribanCSharpTemplateGenerator embedded/custom composition
  -> generated C# and verified .salep.manifest.json ownership
  -> consumer compiler and runtime tests
```

Keep source adapters, neutral models, C# policy and templates independent. Template customization changes output; it must not redefine GraphQL semantics. Use the [customization guide](template-customization.md) for stable keys, model scopes, nested defaults and extension hooks. Follow the mandatory block-tag style in [agent guidance](agent-contributor-guide.md). Embedded default templates are the source of truth; never patch generated sample files manually.

`ScribanGenerator` resolves profiles and referenced clients, checks ownership and tracks schemas, operations, configurations and custom templates. `.salep.manifest.json` paths are relative to that file. Base clients publish schema/transport contracts; modules reference those contracts without duplicating shared types. Regenerate base clients before modules and generated tests. Keep incremental invalidation based on verified inputs and owned output hashes.

## Local workflow

Build the core filter with `dotnet build src/Salep.Core.slnf`. Run focused tests through the xUnit Microsoft.Testing.Platform host:

```bash
dotnet run --project src/Salep.ClientGenerator.Tests --framework net10.0
dotnet run --project src/Salep.ClientGenerator.MSBuild.Tests --framework net10.0
```

Repeat on `net11.0`. Run all core suites with `./scripts/test.sh --max-parallel-test-modules 1` or `pwsh ./scripts/test.ps1 --max-parallel-test-modules 1`.

For a source CLI run:

```bash
dotnet run --project src/Salep.ClientGenerator.Cli --framework net10.0 -- generate --config src/samples/Opinionated/Client/salep.json
```

For complete package and sample verification, run `./build-salep.sh` or `pwsh ./build-salep.ps1`. The paired workflows test core/MSBuild on both frameworks, pack `Salep.ClientGenerator`, restore the exact version into samples, export the server schema, and run generated client/module tests plus server acceptance. Opinionated runs on .NET 10/11; MinimalDependencies and server acceptance run on .NET 11. Shared acceptance tests start Hot Chocolate in memory with Alba.

Package layout checks pack concurrently with separate `--artifacts-path` roots for restore, compilation and CLI staging. Keep `scriban_cli_publish` under the invocation’s intermediate output rather than a shared directory.

For release verification, run `./scripts/pack-release.sh <version>` or `pwsh ./scripts/pack-release.ps1 <version>`, then `npm run packages:verify -- <version>` for fresh-cache consumers. Parser package compatibility uses `./scripts/verify-package-compatibility.sh` or `pwsh ./scripts/verify-package-compatibility.ps1`.

## Adding behavior

1. Update configuration resolution and `docs/client-generator/config.schema.json` for new options. Update neutral models for GraphQL behavior, or C# target policy for naming/scalars/type representation.
2. Project values into the owning template model and add the smallest useful template fragment. Keep existing keys/model scopes compatible; verify overrides and `default:` composition.
3. Add positive, negative, boundary and regression tests appropriate to the behavior. Extend configuration interactions, compile generated output and assert observable runtime effects.
4. Review affected fixtures under `Fixtures/Contracts/`: these 69 baselines preserve verified APIs, queries, inventories and generated-test coverage. Do not auto-refresh expectations. Preserve native and Dunet modes and no-implicit-usings compilation.
5. Update the owning sample inputs, the full `src/samples/Opinionated/Client/schema.coverage.graphql` fixture and [spec evidence](spec-coverage.md) as relevant. Regenerate through the package/MSBuild workflow.
6. Update consumer documentation, template examples and paired scripts whenever their contract changes.

See [generator verification](generator-parity.md) for fixed contract, seeded, runtime, ownership and package gates. Finite fixture coverage is not exhaustive proof of all GraphQL inputs.

## Contribution checks

- Full solution builds; core/MSBuild tests pass on both target frameworks.
- Full package/sample workflow passes, with generated output produced from current source.
- Template/customization and fresh-package checks pass when changed.
- Bash and PowerShell changes remain equivalent; run `npm run scripts:check` and validate both shells. Report Windows execution separately.
- Review expected-contract changes deliberately and synchronize documentation/notices.

Only `Salep.GraphQLParser` and `Salep.ClientGenerator` are published. Follow [releases](../releases.md) for metadata, notices, isolated package verification and remote CI requirements.
