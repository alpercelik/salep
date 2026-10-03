# Generator contracts and regression gates

Scriban is the sole stable implementation of `Salep.ClientGenerator`. Before retiring Roslyn, the complete two-backend comparison passed and the verified Scriban results were captured as 69 fixed contract fixtures. The historical filename of this document is retained for existing links. Tests now compare against reviewed expectations rather than rebuilding a second generator.

## Required checks

| Gate | Evidence |
| --- | --- |
| Generated contracts | `GeneratorContractTests` compares five sample/full-spec fixtures against fixed API, operation text, file inventory and generated-test coverage. Fingerprints include attributes, nullability, constructors/default arguments, required properties, accessors, interfaces, generic constraints, enums, converters and native/Dunet unions. Unknown public member kinds fail. |
| Configuration interactions | `GeneratorConfigurationMatrixTests` compares 64 fixtures covering every combination of unused-variable filtering, default inlining, GET, batching, sample emission and raw JSON test literals. Custom names/scalars, indentation 0/2/4/16 and URL bounds 1/2048 are included. Generated clients/tests compile without SDK implicit usings; generated facts execute. |
| Reproducible corpus | Seeds 1701, 90210 and 65537 each generate 12 cases varying aliases, fragments, lists/nullability and extensions. Regeneration is deterministic, and generated facts compile and execute. Retain a seed/case as a dedicated fixture when diagnosing a failure. |
| Ownership and extensions | Changed inherited schema extensions and modified base files reject child generation before writing output. Tests verify recovery of deleted/modified owned files, relative manifest paths, relocation, extension members and legacy ownership migration from a captured version-1 artifact. |
| Invalid input | `GeneratorInputValidationTests` checks malformed SDL/operations, unnamed operations, directive count 10,000/10,001, invalid types/ranges/names, unknown properties, scalar settings and test-suite policy. Invalid configuration writes no output. |
| Runtime | `GeneratedClientBehaviorTests` checks GET/POST, URL encoding and exact URL-length fallback, request JSON, nested input enums/lists/null omission, cancellation, disabled batching, response enums, partial errors, null responses, batching, multipart/JSON incremental responses and valid/missing/unknown polymorphic discriminators. Native union runtime checks execute on .NET 11. |
| Server acceptance | Both surviving profiles link `GraphQLIntegrationTests` and run base/module operations against the real repository server with Alba on .NET 11. Fixed assertions cover defaults, lists, response data, error behavior and subscription delivery. |
| Package layout | `PackageLayoutTests` retains isolated concurrent generator packing, managed CLI distribution/metadata and parser portable symbols with Source Link mapping checks. |
| Packed lifecycle | `build-salep.sh` / `.ps1` pack Scriban and restore exact-version sample consumers. Fresh-cache `npm run packages:verify` consumers on .NET 10/11 verify runtime customization, template export/composition/invalidation, unchanged builds, owned-output recovery, obsolete sample cleanup and concurrent multi-target publication. |
| Cross-platform | `.github/workflows/generator-verification.yml` runs core suites, package/sample/server dogfooding and isolated release consumers on Linux/Bash and Windows/Windows PowerShell. |

Core and MSBuild tests run on .NET 10 and .NET 11. Opinionated samples exercise Dunet/NodaTime on both; MinimalDependencies and native unions require .NET 11 with preview language support. These finite cases do not prove every possible input or complete GraphQL semantic validation.

## Reviewing baseline changes

The fixtures in `src/Salep.ClientGenerator.Tests/Fixtures/Contracts/` are immutable during test execution. A failing comparison must be investigated; do not auto-update snapshots to make tests pass. Review intentional API, document, inventory or coverage changes and update only affected fixtures alongside behavior tests. Query fixture newlines are normalized to LF for portability; separate raw-string/runtime tests verify native output newlines and value preservation. The legacy fixture records a released ownership format and must remain independent of current generation.

## Running and release gates

Run `./build-salep.sh` or `pwsh ./build-salep.ps1`. Run parser and core coverage with `./scripts/test.sh --max-parallel-test-modules 1` or `pwsh ./scripts/test.ps1 --max-parallel-test-modules 1`. Pack a fresh version with `./scripts/pack-release.sh <version>` or `pwsh ./scripts/pack-release.ps1 <version>`, then run `npm run packages:verify -- <version>`.

Before publishing, require passing `Generator verification (ubuntu-latest)` and `Generator verification (windows-latest)` on the exact release commit and configure them as required branch-protection checks. A checked-in workflow and local macOS PowerShell run do not establish remote Linux/Windows execution or branch protection.
