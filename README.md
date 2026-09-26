# Salep

Salep provides GraphQL tooling for .NET: a standalone lexer and parser, and a schema-driven C# client generator with MSBuild integration.

| Component | Purpose | NuGet package | Documentation |
| --- | --- | --- | --- |
| **Salep GraphQL Parser** | Lexing, parsing, syntax trees, diagnostics, traversal, rewriting, and printing | `Salep.GraphQLParser` | [Parser guide](docs/parser/README.md) |
| **Salep C# Client Generator** | Generate typed C# clients from GraphQL schemas and operations during builds | `Salep.ClientGenerator` | [Client generator guide](docs/client-generator/README.md) |

The parser can be used independently. The client generator references the parser directly; generated applications have no runtime dependency on either implementation assembly. See [architecture and naming](docs/architecture.md) for component ownership and package mapping.

## Solution and projects

Open `src/Salep.slnx` for the complete solution, including samples. `src/Salep.Core.slnf` selects the eight core projects without samples. The package-only `Salep.GraphQLParser.PublicApiConsumer` fixture stays outside both solution build lists; run it through `scripts/verify-package-compatibility.sh` or `scripts/verify-package-compatibility.ps1`, which pack the parser before restoring the consumer.

```text
src/
  Salep.slnx
  Salep.Core.slnf
  Salep.GraphQLParser/
  Salep.ClientGenerator/
  Salep.ClientGenerator.Cli/
  Salep.ClientGenerator.MSBuild/
  Salep.GraphQLParser.Tests/
    Fixtures/                         # Oracle and reference-suite data
  Salep.GraphQLParser.PublicApiConsumer/
  Salep.ClientGenerator.Tests/
  Salep.ClientGenerator.MSBuild.Tests/
  samples/
    Opinionated/
    MinimalDependencies/
    Salep.Samples.GraphQLServer/
benchmarks/
  Salep.GraphQLParser.Benchmarks/
```

## Build and verify

```sh
dotnet build src/Salep.Core.slnf
dotnet test --solution src/Salep.Core.slnf --configuration Release
```

| Workflow | Bash | PowerShell |
| --- | --- | --- |
| Core tests | `./scripts/test.sh` | `pwsh ./scripts/test.ps1` |
| Parser package verification | `./scripts/verify-package-compatibility.sh` | `pwsh ./scripts/verify-package-compatibility.ps1` |
| Generator package and sample dogfooding | `./build-salep.sh` | `pwsh ./build-salep.ps1` |

See [script workflows](docs/script-workflows.md) for all paired commands, defaults, and environment overrides. Run `npm run scripts:check` after script changes and verify behavior in both shells.

## NuGet packages

See [packaging and releases](docs/releases.md) to prepare `Salep.GraphQLParser` and `Salep.ClientGenerator`, including parser symbols, for publication.

## Contributor documentation

- [Repository agent guidance](AGENTS.md)
- [Parser scope and milestones](docs/parser/next-milestones.md)
- [Parser syntax coverage](docs/parser/spec-coverage.md)
- [Client generator contributor guide](docs/client-generator/developer-contributor-guide.md)
- [Client generator agent guide](docs/client-generator/agent-contributor-guide.md)
- [Client generator feature coverage](docs/client-generator/spec-coverage.md)

## License

See [LICENSE](LICENSE).
