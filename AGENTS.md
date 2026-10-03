# Repository contributor guidance

Salep contains two components: Salep GraphQL Parser and Salep C# Client Generator. See `docs/architecture.md` for naming and public package mapping. Keep changes in the owning project and preserve the complete solution in `src/Salep.slnx`. `src/Salep.Core.slnf` selects the core projects without samples; keep its project paths synchronized when changing core project membership.

## Salep GraphQL Parser

- Follow `docs/parser/goal.md` and `docs/parser/next-milestones.md` for parser scope and readiness criteria.
- Keep the graphql-js oracle reproducible and add positive, negative, boundary, and regression coverage for parser behavior changes.
- Keep package verification separate from source project references; `src/Salep.GraphQLParser.PublicApiConsumer` validates the packed parser package. Keep it outside `src/Salep.slnx` and `src/Salep.Core.slnf`; run it only through the paired `scripts/verify-package-compatibility.*` workflow, which restores and packs the parser first.

## Salep C# Client Generator

- The default Scriban generator in `src/Salep.ClientGenerator/` is the source of truth for generated sample code. Do not hand-edit generated outputs.
- `Salep.ClientGenerator` must reference `src/Salep.GraphQLParser/Salep.GraphQLParser.csproj` directly.
- Follow `docs/client-generator/agent-contributor-guide.md` and `docs/client-generator/developer-contributor-guide.md` for Salep workflows. Its GraphQL spec checklist is `docs/client-generator/spec-coverage.md`.
- **Scriban template authoring is mandatory style:** In every template under `src/Salep.ClientGenerator/Templates/` (including templates that emit Markdown), put each block-level `for`, `if`, `else`, and `end` tag on its own line. Indent the outer tag to the generated code or Markdown location it controls (for example, 8 spaces inside a method body), then indent each nested control tag one level deeper than its parent. Do not leave tags flush-left just because whitespace control removes their indentation from output. Use `{{~ ... ~}}` on tag-only lines so directive lines do not leak into generated output. Add `# begin <kind> <label>` to each block opener and the matching `# end <kind> <label>` to its `end`. Keep emitted code or Markdown on separate lines, aligned to the generated output. Inline only short conditional substitutions that are part of one output statement or sentence; do not inline loops. If a loop builds a single declaration, compute that projection in the C# template model. Verify rendered output through the Scriban tests.
- Run `./build-salep.sh` or `pwsh ./build-salep.ps1` for the full Salep package and sample dogfooding workflow when validating Salep changes.

## Public NuGet packages

- Only `Salep.GraphQLParser` and `Salep.ClientGenerator.MSBuild` are public packable projects; their package IDs are `Salep.GraphQLParser` and `Salep.ClientGenerator`. The latter contains Scriban. Scriban is the sole generator implementation. Preserve the reviewed API/query/inventory fixtures and compiled runtime regressions in `Salep.ClientGenerator.Tests`.
- Follow `docs/releases.md` for release metadata, package-only verification, and paired release packing commands. Keep private tooling portable and dependency notices synchronized with bundled versions.

## Cross-platform script parity

- Use exact on-disk casing in every path, including lowercase `src/samples/`; case-insensitive macOS/Windows path lookup is not evidence that a reference works on case-sensitive filesystems. Record case-only renames through an intermediate path in Git.

- Every repository `.sh` script must have a same-directory, same-basename `.ps1` counterpart, and vice versa. Add, change, or remove both in the same change.
- Keep commands, target frameworks, defaults, argument forwarding, environment overrides, validation, cleanup, and failure exit codes equivalent. Resolve repository paths from the script location so workflows work from any current directory.
- PowerShell scripts must work without Bash, Git Bash, WSL, or Unix utilities. Support Windows PowerShell 5.1 and PowerShell 7; use .NET APIs for archive and file operations.
- Document both invocations in [script workflows](docs/script-workflows.md) and relevant workflow guides. Run `npm run scripts:check` after script changes, validate syntax and behavior in both shells, and report separately whether Windows itself was exercised. The pairing check does not prove behavior parity.
