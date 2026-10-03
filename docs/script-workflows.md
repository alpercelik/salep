# Cross-platform script workflows

Run shell scripts on Linux/macOS (or Git Bash), and PowerShell scripts on Windows without Bash or WSL. The `.ps1` entry points support Windows PowerShell 5.1 and PowerShell 7. With Windows PowerShell, replace `pwsh ./path.ps1` with `powershell -File .\path.ps1`.

All scripts resolve repository paths from their own locations. Use the exact on-disk spelling of path segments, including lowercase `src/samples/`, so commands also work on case-sensitive filesystems. Commands below assume the repository root; from elsewhere, invoke the script by its full path. Install the SDK pinned in `global.json`, the .NET 10/11 runtimes, and Node.js/npm for the oracle and repository checks. Workflows using `--no-restore` require a prior project restore.

| Workflow | Shell | PowerShell |
| --- | --- | --- |
| Core solution tests | `./scripts/test.sh` | `pwsh ./scripts/test.ps1` |
| Deterministic parser fuzzing | `./scripts/fuzz.sh 20260925 512` | `pwsh ./scripts/fuzz.ps1 20260925 512` |
| Allocation gate and benchmarks | `./scripts/benchmark.sh --filter '*Lexer*' --job Dry` | `pwsh ./scripts/benchmark.ps1 --filter '*Lexer*' --job Dry` |
| Packed parser API verification | `./scripts/verify-package-compatibility.sh` | `pwsh ./scripts/verify-package-compatibility.ps1` |
| Public release packages | `./scripts/pack-release.sh 0.1.0-preview.1` | `pwsh ./scripts/pack-release.ps1 0.1.0-preview.1` |
| Salep packages and Scriban sample dogfooding | `./build-salep.sh` | `pwsh ./build-salep.ps1` |

## Arguments and environment

- Test wrappers target `src/Salep.Core.slnf` in Release and forward additional arguments to `dotnet test --solution`. `global.json` selects Microsoft.Testing.Platform for the repository's xUnit v3 projects; use MTP options such as `--filter-class`, rather than VSTest `--filter` expressions.
- Fuzzing takes positional seed and case count, defaulting to `20260925` and `512`. It runs only the parser test project with `--filter-class '*DeterministicParserFuzzTests'`, sets `GRAPHQL_FUZZ_SEED` and `GRAPHQL_FUZZ_CASES` for the test invocation, then restores the caller's environment.
- Benchmarks use `BENCHMARK_TFM` (default `net10.0`), `BENCHMARK_ALLOCATION_ITERATIONS` (`1000`), and `BENCHMARK_ALLOCATION_REPETITIONS` (`3`). Extra arguments go to BenchmarkDotNet after the allocation gate succeeds.
- Package verification checks archive contents, restores into a fresh temporary NuGet cache, verifies the restored DLLs against the package, and builds/runs the consumer on both target frameworks. Temporary cache and consumer outputs are removed on success or failure. PowerShell uses .NET ZIP APIs and SHA-256 instead of Unix archive/comparison tools.
- Salep dogfooding uses `GITHUB_RUN_NUMBER`, or the current Unix timestamp, for a local prerelease `Salep.ClientGenerator` package. Opinionated samples target .NET 10/11; MinimalDependencies uses .NET 11 native unions.
- Salep dogfooding builds each test project with single-node MSBuild, then runs its already-built test assembly through `dotnet run --no-build`. This keeps the repository's Microsoft.Testing.Platform runner while avoiding a second implicit parallel build during test execution.

Release packing requires an explicit version and accepts an optional output directory (relative to the repository root). It restores/packs the two public packages in Release, checks the expected artifacts, and does not publish. Invalid arguments exit with code 2; native build failures preserve their exit code. See [release guidance](releases.md).

`npm run packages:verify -- <version> [package-directory]` verifies release packages in isolated consumers with a fresh cache; it requires Node.js and the pinned .NET SDK. It removes its temporary consumers and cache on success or failure.

The Node-based oracle and inventory commands in `package.json` remain shared entry points and do not require separate shell wrappers.

## Contributor rule

Every `.sh` file must have a same-directory `.ps1` counterpart with equivalent behavior, and vice versa. Update both together, including documentation. Both must stop after a failed native command and return a nonzero exit code. PowerShell must not depend on Unix utilities.

Run `npm run scripts:check` to catch missing counterparts. This check includes tracked and untracked, non-ignored repository files and checks file pairing only. Also validate syntax, command arguments, environment overrides, failure handling, and cleanup in both shells. PowerShell execution on macOS/Linux is useful evidence but does not establish Windows runtime verification.

The parser package consumer is excluded from `src/Salep.slnx` and `src/Salep.Core.slnf`, so normal builds do not require a prebuilt parser package. Both `verify-package-compatibility` scripts restore and pack the parser first, then restore and run the standalone consumer with an isolated package cache and output directory.

## Generator regression gates

`./build-salep.sh` and `pwsh ./build-salep.ps1` test the generator/MSBuild projects, pack Scriban, restore exact-version samples and run generated tests plus server acceptance. The core suite preserves 69 fixed contract fixtures, the complete configuration matrix, seeded compilation cases and transport regressions. After `./scripts/pack-release.sh <version>` or `pwsh ./scripts/pack-release.ps1 <version>`, run `npm run packages:verify -- <version>` for isolated .NET 10/11 consumers, template customization and incremental lifecycle checks. See [generator contracts](client-generator/generator-parity.md).

The `Generator verification` pull-request workflow runs Linux/Bash and Windows/Windows PowerShell gates. Local PowerShell execution does not prove Windows execution. Configure both OS jobs as required branch-protection checks before release. Release scripts produce only `Salep.GraphQLParser` and `Salep.ClientGenerator`.
