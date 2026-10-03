# Third-party software in Salep.ClientGenerator

The `Salep.ClientGenerator` package bundles private build-time dependencies with its managed CLI:

| Component | Version | Project | License |
| --- | --- | --- | --- |
| Scriban | 7.5.0 | https://github.com/scriban/scriban | MIT |
| Microsoft.Extensions.FileSystemGlobbing | 10.0.12 | https://github.com/dotnet/runtime | MIT |

The bundled FileSystemGlobbing notice is included under `licenses/runtime-THIRD-PARTY-NOTICES.txt`. The Scriban project is MIT licensed; see the upstream repository for its license text. Update this inventory whenever the bundled dependency versions change. Generated applications do not acquire runtime references to these build-time assemblies.
