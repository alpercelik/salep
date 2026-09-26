# Salep architecture and naming

**Salep** is the overall product and solution. Its two components are **Salep GraphQL Parser** and **Salep C# Client Generator**. Use these names in human and agent documentation; use exact project or package identifiers in commands.

| Project | Role | Distribution |
| --- | --- | --- |
| `Salep.GraphQLParser` | GraphQL lexer, parser, AST, diagnostics, visitors, rewrites, and printing | Public `Salep.GraphQLParser` NuGet package |
| `Salep.ClientGenerator` | Schema and operation loading, generation orchestration, and C# emission | Private implementation inside the `Salep.ClientGenerator` package |
| `Salep.ClientGenerator.Cli` | Out-of-process generator host | Private executable inside the `Salep.ClientGenerator` package |
| `Salep.ClientGenerator.MSBuild` | Build targets and generator packaging | Produces the public `Salep.ClientGenerator` NuGet package |

The generator engine directly references `src/Salep.GraphQLParser/Salep.GraphQLParser.csproj`. The parser does not reference the generator. MSBuild invokes `Salep.ClientGenerator.Cli.dll`, which uses the generator engine. Generated clients do not reference these tool assemblies at runtime.

Project, assembly, and namespace names follow their component: `Salep.GraphQLParser` or `Salep.ClientGenerator.*`. Public parser types such as `GraphQLParser` retain their names. The parser API contract inventory retains normalized reference type names for compatibility comparison; these are not package identifiers.

Consumer-facing configuration remains `salep.json`, the public package is `Salep.ClientGenerator`, and its matching build assets are `Salep.ClientGenerator.props` and `Salep.ClientGenerator.targets`. Existing `SalepToolPath` and other MSBuild properties retain their names. Consumers invoking the CLI assembly directly must use its new name.

`src/Salep.slnx` is the authoritative complete solution. `src/Salep.Core.slnf` selects the parser, generator, tests, and benchmarks without samples. Production, test, and package-consumer projects live in `src/`; parser test fixtures live in `src/Salep.GraphQLParser.Tests/Fixtures/`, benchmarks in `benchmarks/`, and examples in `src/samples/`.

[Parser documentation](parser/README.md) owns language behavior and compatibility. [Client generator documentation](client-generator/README.md) owns generated C# behavior and build integration. Their coverage checklists describe different contracts and must remain distinct.

Both public packages have explicit packing metadata and separate package READMEs. See [release preparation](releases.md); only these two projects opt into `IsPackable`.

`src/Salep.GraphQLParser.PublicApiConsumer` is a standalone package-verification fixture, excluded from both solution build lists. Its `PackageReference` intentionally tests the packed parser. The paired `scripts/verify-package-compatibility.*` scripts restore and pack the parser before restoring, building, and running this consumer in isolation.

Solution files and tracked solution-specific IDE settings live in `src/`. Shared `Directory.Build.props`, `Directory.Packages.props`, `NuGet.config`, and `global.json` stay at the repository root so they apply to production projects, tests, benchmarks, and samples. Commands in these guides assume the repository root.

Sample project and assembly names are unique across variants: `Salep.Samples.Opinionated.{Client,Client.Tests,Module,Module.Tests,IntegrationTests}` and `Salep.Samples.MinimalDependencies.{Client,Client.Tests,Module,Module.Tests,IntegrationTests}`. The shared server is `Salep.Samples.GraphQLServer`. These eleven projects live under `src/samples/`; each project directory matches its project filename. Generated API namespaces remain independent of project identity.
