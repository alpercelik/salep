# NuGet packaging and releases

Salep has two public packages. All other projects are non-packable by default.

| Package | Packing project | Contents |
| --- | --- | --- |
| `Salep.GraphQLParser` | `src/Salep.GraphQLParser/Salep.GraphQLParser.csproj` | Standalone `net10.0` and `net11.0` libraries, XML API documentation, README and MIT license |
| `Salep.ClientGenerator` | `src/Salep.ClientGenerator.MSBuild/Salep.ClientGenerator.MSBuild.csproj` | MSBuild assets, portable managed CLI hosts and private dependencies for both frameworks, README, license and dependency notices |

`Salep.GraphQLParser` also produces a portable-PDB `.snupkg` containing Source Link metadata. `Salep.ClientGenerator` deliberately exposes no runtime library assets or NuGet dependencies: its generator, parser and Roslyn binaries are private build tools. Use `PrivateAssets="all"` on a consumer's `Salep.ClientGenerator` reference. The managed CLI requires the matching .NET runtime; .NET 11 consumers currently need the preview/RC toolchain.

## Version and repository metadata

`Directory.Build.props` defines the default development version (`0.1.0`) and shared author, MIT license, repository URL, and source-debugging properties. The package projects define descriptions, tags, and READMEs. The canonical repository is `https://github.com/alpercelik/salep`; package metadata, README links, and Source Link mappings use this location.

The release scripts require an explicit version and apply it to both packages and their assemblies. Accepted versions are `major.minor.patch` with an optional SemVer prerelease suffix; build metadata and leading zeros in numeric identifiers are rejected. The first planned public version is `0.1.0`. Future prerelease versions can use a suffix such as `-preview.1`.

## Verify and pack

Install the SDK pinned in `global.json` and both supported runtimes. Run core tests, parser package compatibility, and complete generator dogfooding before preparing a release:

| Workflow | Bash | PowerShell |
| --- | --- | --- |
| Core tests | `./scripts/test.sh --max-parallel-test-modules 1` | `pwsh ./scripts/test.ps1 --max-parallel-test-modules 1` |
| Parser package/API gate | `./scripts/verify-package-compatibility.sh` | `pwsh ./scripts/verify-package-compatibility.ps1` |
| Generator package and sample gate | `./build-salep.sh` | `pwsh ./build-salep.ps1` |
| Release artifacts | `./scripts/pack-release.sh 0.1.0` | `pwsh ./scripts/pack-release.ps1 0.1.0` |

After packing, run `npm run packages:verify -- 0.1.0` (same command in both shells). This restores each package from the release directory into a fresh temporary cache outside the repository, compiles and runs consumers on both frameworks, verifies the parser API contract, and checks that generated applications have no runtime tool dependencies. Salep package IDs are mapped exclusively to the local release feed; NuGet.org is available only for SDK framework packs that are not installed locally. Pass a second package-directory argument when using a custom output directory.

The packing scripts restore and pack only the two public projects in Release. They work from any current directory, stop on failures, and require all three expected output files. An optional second argument selects an output directory; relative output paths are relative to the repository root. They do not change sample package pins, push packages, or create tags/releases.

Default outputs:

```text
artifacts/release/0.1.0/
  Salep.GraphQLParser.0.1.0.nupkg
  Salep.GraphQLParser.0.1.0.snupkg
  Salep.ClientGenerator.0.1.0.nupkg
```

`PackageLayoutTests` checks both archives' metadata and layout, portable parser symbols with Source Link mappings, and the generator's managed-only CLI distribution. The package-only parser consumer checks the public API contract and behavior. Generator dogfooding restores `Salep.ClientGenerator` packages into the samples rather than substituting project references. Release output is ignored by Git; repository-local verification packages under `artifacts/packages/` remain a separate workflow.

For a final release, rebuild from the clean, committed release source so the repository commit recorded in the package and symbols identifies that source. Preserve these verified artifacts for upload. Confirm that your NuGet account owns or can register both package IDs and that the chosen version has not already been published.

## Publish through GitHub Trusted Publishing

The `.github/workflows/publish-nuget.yml` workflow runs when a GitHub Release is published. It installs the pinned .NET 11 RC SDK and .NET 10 SDK, runs the core tests, parser package compatibility gate, generator sample dogfooding, release packing, and fresh-consumer verification. Only after those checks pass does it request a short-lived NuGet publishing key through GitHub OIDC and publish both packages. The parser `.snupkg` is kept beside its `.nupkg` and is pushed with it. No long-lived NuGet API key is stored in GitHub.

Before the first release, configure the trust once:

1. Sign in to NuGet.org with the account that should own both package IDs. Confirm `Salep.GraphQLParser` and `Salep.ClientGenerator` are available to that account.
2. In NuGet.org account settings, open **Trusted Publishing** and create a GitHub Actions policy with repository owner `alpercelik`, repository `salep`, workflow file `publish-nuget.yml`, and environment `release`. Enter the workflow filename only; do not include `.github/workflows/`. Scope it to push new packages and versions for `Salep.GraphQLParser` and `Salep.ClientGenerator`.
3. In GitHub repository settings, create the `release` environment. Require a reviewer and restrict deployment to release tags if those controls are available for your repository.
4. Add a GitHub Actions secret named `NUGET_USER` containing the NuGet.org profile username (not the email address).
5. Merge the workflow and documentation to the default branch. Create and publish a GitHub Release from the intended source commit with tag `v0.1.0`. The workflow derives package version `0.1.0` from that tag and waits for the `release` environment approval before publishing.

Future package versions use matching `v<version>` GitHub Release tags. NuGet issues a temporary key valid for one hour during the workflow; request it close to the push step. Never add a long-lived API key to repository files or workflow variables.

See Microsoft's [NuGet Trusted Publishing guide](https://learn.microsoft.com/en-us/nuget/nuget-org/trusted-publishing), [NuGet pack properties](https://learn.microsoft.com/nuget/reference/msbuild-targets), and [Source Link guidance](https://learn.microsoft.com/dotnet/standard/library-guidance/sourcelink).
