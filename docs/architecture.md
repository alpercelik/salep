# Architecture and public packages

Salep contains the independent GraphQL language library and the Scriban C# client generator.

| Projects | Responsibility | Public package |
| --- | --- | --- |
| `Salep.GraphQLParser` | Lexer, parser and immutable syntax trees | `Salep.GraphQLParser` |
| `Salep.ClientGenerator` | Neutral GraphQL models, C# target policies, Scriban templates and generation | Private tool |
| `Salep.ClientGenerator.Cli` | Configuration validation, generation, inputs and template export | Private tool |
| `Salep.ClientGenerator.MSBuild` | Props, targets and bundled framework-matched CLI | `Salep.ClientGenerator` |

The generator directly references `src/Salep.GraphQLParser/Salep.GraphQLParser.csproj`. The parser does not reference the generator. Only the parser and generator MSBuild distribution projects are packable. Generated applications have no Salep or Scriban runtime dependency. Compiler libraries used in generator tests compile generated consumers; they are not bundled with the generator tool.

The Roslyn implementation and its sample projects have been retired. Scriban is the sole stable generator and retains the `Salep.ClientGenerator` package ID, `SalepConfig`/`SalepToolPath` MSBuild contract, `salep.json` and `.salep.manifest.json` names. Regeneration verifies and upgrades released version-1 ownership manifests, preserving unowned consumer files. Regenerate base clients before modules and tests during an upgrade.

`src/Salep.slnx` is the complete solution. `src/Salep.Core.slnf` selects the parser, generator, CLI, MSBuild, tests and parser benchmarks without samples. `Salep.GraphQLParser.PublicApiConsumer` stays outside both and runs only through the paired package verification scripts.

Samples live under `src/samples/Opinionated/` and `src/samples/MinimalDependencies/`. Each owns its operation inputs. Both share `src/samples/Salep.Samples.GraphQLServer/`. Root build/package configuration supplies dependency versions. `build-salep.sh` and `build-salep.ps1` restore samples against the exact package version packed by that invocation.

See [parser documentation](parser/README.md), [generator documentation](client-generator/README.md), [generator verification](client-generator/generator-parity.md), [template customization](client-generator/template-customization.md) and [releases](releases.md).
