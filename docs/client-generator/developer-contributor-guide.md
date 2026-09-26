# Salep C# Client Generator — Developer Contributor Guide

Welcome! This guide is for software engineers contributing to the **Salep** codebase. It covers local setup, architecture internals, development workflows, testing, and procedures for adding new code generation capabilities.

---

## 1. Prerequisites and Environment

- **.NET SDK**: .NET 11.0 RC 1 SDK (pinned in `global.json`); the projects target .NET 10.0 and .NET 11.0.
- **Language Level**: C# 14 / `LangVersion latest`.
- **IDE**: JetBrains Rider, Visual Studio 2026 / 2022 v17.12+, or VS Code with C# Dev Kit.
- **OS**: Linux, macOS, or Windows.

---

## 2. Solution Layout

The repository is organized as a clean multi-project solution (`src/Salep.slnx`):

```text
src/
  Salep.GraphQLParser/                         # Standalone GraphQL language library
  Salep.ClientGenerator/                # C# generation engine
    Config/                            # Configuration loading and inheritance
    Diagnostics/                       # Schema and operation diagnostics
    Emission/                          # C# emitters
    Generation/                        # Orchestration and manifests
    Model/                             # Schema model
    Operations/                        # Operation loading
    Utilities/                         # C# naming and formatting
  Salep.ClientGenerator.Cli/            # Out-of-process executable host
  Salep.ClientGenerator.MSBuild/        # Produces the Salep.ClientGenerator NuGet package
  Salep.ClientGenerator.Tests/          # Generator and CLI tests
  Salep.ClientGenerator.MSBuild.Tests/  # Build and packaging tests
  Salep.GraphQLParser.Tests/                   # Parser tests
    Fixtures/                         # Oracle and reference-suite data
  Salep.GraphQLParser.PublicApiConsumer/       # Packed parser verification
  samples/
    Opinionated/
    MinimalDependencies/
    Salep.Samples.GraphQLServer/
benchmarks/
  Salep.GraphQLParser.Benchmarks/
```

Each sample set has a `GeneratedClient.IntegrationTests` project. These tests use Alba to start the Hot Chocolate server in memory and execute all generated query and mutation operations from both the base and module clients. Run them with `dotnet run --project src/samples/Opinionated/Salep.Samples.Opinionated.IntegrationTests/Salep.Samples.Opinionated.IntegrationTests.csproj --framework net11.0` and `dotnet run --project src/samples/MinimalDependencies/Salep.Samples.MinimalDependencies.IntegrationTests/Salep.Samples.MinimalDependencies.IntegrationTests.csproj --framework net11.0` (the repository uses the xUnit Microsoft.Testing.Platform runner).

---

## 3. Development Workflows

### 3.1. Building the Solution and IDE Workflow

Open `src/Salep.slnx` for the complete solution, including samples, or `src/Salep.Core.slnf` for the eight core projects (parser, generator, tests, and benchmarks). The filter selects projects from `src/Salep.slnx`, which is the authoritative normal-build project list. The standalone `Salep.GraphQLParser.PublicApiConsumer` fixture is excluded from both; the paired parser package verification scripts pack the parser before restoring and running it.

Build the core projects with `dotnet build src/Salep.Core.slnf`. `scripts/test.sh` and `scripts/test.ps1` run the core tests. See [script workflows](../script-workflows.md) for all shell/PowerShell pairs and their parity requirements. You can build the entire solution directly in Visual Studio, JetBrains Rider, or via the command line:
```bash
dotnet build src/Salep.slnx
```

* **IDE / Solution Builds**: The `src/samples/Opinionated/` projects consume `Salep.ClientGenerator` via Central Package Management (`src/samples/Opinionated/Directory.Packages.props`) and allow local floating/cached prerelease package resolution (`src/samples/Opinionated/Directory.Build.props` sets `NU1603` in `WarningsNotAsErrors`) so that standard IDE builds succeed without failing on exact-version prerelease mismatches.
* **Note on Preview SDKs**: If running in preview SDKs with strict analyzers, you can disable style warnings via:
```bash
dotnet build src/Salep.slnx -p:EnforceCodeStyleInBuild=false -p:TreatWarningsAsErrors=false
```

### 3.2. Running Tests

Run the core unit tests:
```bash
dotnet test --project src/Salep.ClientGenerator.Tests/Salep.ClientGenerator.Tests.csproj
```

Run the MSBuild integration tests:
```bash
dotnet test --project src/Salep.ClientGenerator.MSBuild.Tests/Salep.ClientGenerator.MSBuild.Tests.csproj
```

Run all tests across the repository:
```bash
dotnet test --solution src/Salep.slnx
```

Package layout tests deliberately run concurrent packs. Each invocation uses its own `--artifacts-path` for restore, compilation, and CLI publish staging, as well as a separate package output directory. Keep `_SalepPackCliTools` rooted in `IntermediateOutputPath`; a fixed repository `obj/Release/cli_publish` directory races across framework runners. Fresh artifact roots require restore, so these tests must not use `--no-restore`.

### 3.3. Running the Standalone CLI

To run the CLI against a test schema without packaging:
```bash
dotnet run --project src/Salep.ClientGenerator.Cli/Salep.ClientGenerator.Cli.csproj --framework net10.0 -- \
  --config src/samples/Opinionated/Salep.Samples.Opinionated.Client/salep.json
```

### 3.4. Full End-to-End Bootstrap Script

The repository includes deterministic bootstrap scripts (`./build-salep.sh` for Linux/macOS and `./build-salep.ps1` for Windows PowerShell) to run the full dogfooding cycle:
1. Builds and runs unit tests in `Salep.ClientGenerator.Tests` and `Salep.ClientGenerator.MSBuild.Tests`.
2. Packages `Salep.ClientGenerator.<version>.nupkg` into `artifacts/packages/`.
3. Restores and builds the `src/samples/Opinionated/` consuming projects against the local NuGet feed.
4. Executes generated tests for both sample sets (`src/samples/Opinionated/` and `src/samples/MinimalDependencies/`).

Execute before submitting any pull request:

**Linux / macOS / Git Bash:**
```bash
./build-salep.sh
```

**Windows PowerShell:**
```powershell
pwsh ./build-salep.ps1
# or:
powershell -ExecutionPolicy Bypass -File .\build-salep.ps1
```

---

## 4. Code Generation Engine Internals

```text
GraphQL SDL + Operation Files
            │
            ▼
   Salep.GraphQLParser (Parser)
            │
            ▼
    SchemaModel & OperationLoader (Semantic Graph)
            │
            ▼
   SalepGenerator (Orchestrator)
      ├── GenerationManifest (.salep.manifest.json check & transitive dedup)
      ├── SchemaTypesEmitter (Records, Enums, Inputs)
      ├── OperationsEmitter (Operation request/response models)
      ├── UnionJsonConvertersEmitter (System.Text.Json converters)
      ├── GraphQLClientEmitter (Strongly-typed async client methods)
      └── TestsEmitter (Deterministic xUnit tests + MockHttpHandler)
            │
            ▼
   Typed Roslyn nodes (SyntaxGenerator + SyntaxFactory)
            │
            ▼
   Validated, formatted C# source (RoslynEmitter)
```

### Key Components

- **`src/Salep.ClientGenerator/Model/SchemaModel.cs`**: In-memory representation of GraphQL types, fields, arguments, directives, and inheritance relations.
- **`src/Salep.ClientGenerator/Generation/SalepGenerator.cs`**: The main entry point. Orchestrates loading configs, invoking emitters, and writing generated files and `.salep.manifest.json`.
- **`src/Salep.ClientGenerator/Generation/GenerationManifest.cs`**: Records all types and operations generated in a project. Used by child projects with `"baseClient"` to avoid duplicate code generation.
- **`Emission/RoslynEmitter.cs`**: Accepts typed members only, constructs the file, validates diagnostics and formats with the configured indentation and LF line endings. It reparses the completed file with C# 14 or preview for native unions. Consumer compilation supplies semantic validation.
- **`Emission/Syntax/Cs.*.cs`**: Small typed adapters over Microsoft's Roslyn APIs. `SyntaxGenerator` constructs common declarations, statements, calls and literals. `SyntaxFactory` handles C#-specific records, accessors, patterns, raw strings and native unions. There is no string writer or runtime parsing of generated members/statements. Type specifications and configured scalar sample expressions are validated parsing boundaries; GraphQL documents and JSON fixture payloads remain data strings.
- **`Emission/Client/`**: Separate HTTP GET, POST/batch, incremental/multipart, operation API, shared-model and sample components. `GraphQLClientEmitter` orchestrates files; configuration decisions stay with the owning component.
- **`Emission/Syntax/JsonConverterSyntax.cs`**: Shared converter construction for Dunet unions, native unions and operation-specific polymorphic responses.
- **`Emission/Testing/`**: Separate payload, operation, union and error test emission, plus schema-driven sample values and JSON fixtures. `TestsEmitter` orchestrates files.

- **`src/Salep.ClientGenerator/Emission/OperationVariablePolicies.cs`**: Shared default-variable inlining and unused-variable filtering used by operation models, clients, samples, and tests. Fragment-referenced variables are retained; default inlining remains disabled for operations containing fragment spreads.

`SyntaxGenerator` is obtained once from the C# language service; no mutable workspace documents or per-run configuration are shared. Workspaces, composition and Humanizer dependencies ship privately with the CLI. They are not generated-application runtime dependencies. Keep their inventory and license files synchronized in [third-party notices](third-party-notices.md).

Use [RoslynQuoter](https://github.com/KirillOsenkov/RoslynQuoter) as an optional development aid for discovering factory calls, then validate against the pinned Roslyn version. Prefer existing `SyntaxGenerator` APIs before adding an adapter. Use `SyntaxFactory` where precise C# syntax is required. Do not introduce a separate syntax model or a general-purpose builder framework. CLI/MSBuild hosting remains separate from syntax construction; an incremental source generator would be a separate integration using the same core.


---

## 5. Guide: Adding a New Feature to Salep

When extending the generator (for example, adding support for a new GraphQL directive, scalar type, or client option):

### Step 1: Update Semantic Model & Config
- If the feature requires configuration, update `src/Salep.ClientGenerator/Config/GeneratorConfig.cs` with the new JSON property.
- If the feature introduces new GraphQL concepts, update `src/Salep.ClientGenerator/Model/SchemaModel.cs` or `src/Salep.ClientGenerator/Operations/OperationLoader.cs`.

### Step 2: Implement Code Emission
- Update the owning component under `Emission/Client/`, `Emission/Testing/`, or the model emitters. Keep file-level emitters as orchestration.
- Resolve configuration and GraphQL semantics before constructing nodes. Share semantic policies when an option affects clients, models and tests.
- Return typed Roslyn nodes and pass them through `RoslynEmitter`; never interpolate C# member bodies into strings. Keep parsing of user-provided types/sample expressions at the validated boundary.
- Add configuration interaction cases to `RoslynEmissionTests`, including disabled options, malformed configuration and repeated/concurrent generation where relevant.
- Ensure generated code follows standard C# idioms:
  - C# 10+ record types with init properties for models.
  - Zero reference to `Salep.ClientGenerator` runtime assemblies in generated C#.
  - Exhaustive pattern matching for Dunet unions.

### Step 3: Add Unit Tests in `Salep.ClientGenerator.Tests`
- Add tests in `Salep.ClientGenerator.Tests/` using schema-driven test cases (e.g., in `CoverageExpansionTests.cs` or dedicated test classes).
- Verify both positive and negative cases.

The `RoslynEmissionTests` compilation matrix covers 32 combinations of HTTP GET, batching, raw strings, NodaTime, and generated tests/sample code, with custom scalar mappings and client names. It compiles without SDK implicit usings. Additional cases cover malformed syntax/configuration, operation-only output, default-only variables, fragment variables, and both union modes. Native unions compile on .NET 11; Dunet generation and both union runtimes are verified by the package/sample workflow.

### Step 4: Update Sample Projects and Spec Checklist
- Update `src/samples/Opinionated/Salep.Samples.Opinionated.Client/schema.coverage.graphql` or `src/samples/Opinionated/Salep.Samples.Opinionated.Client/graphql/` if the feature expands spec coverage.
- Update `docs/parser/spec-coverage.md` to link the new feature to test evidence.
- Run `./build-salep.sh` to regenerate sample artifacts and verify compilation.

---

## 6. NuGet packages and release preparation

The public packages are `Salep.GraphQLParser` and `Salep.ClientGenerator`. The generator engine and CLI are private implementation projects and are not independently published. Follow [NuGet packaging and releases](../releases.md) for verification, explicit-version packing, artifacts, and publishing commands. The paired `scripts/pack-release.sh` and `scripts/pack-release.ps1` workflows prepare both packages locally; no GitHub Actions publication workflow is currently checked in.

---

## 7. Contribution Checklist

Before submitting a Pull Request:
- [ ] Solution compiles cleanly: `dotnet build src/Salep.slnx`
- [ ] All unit and integration tests pass: `dotnet test --solution src/Salep.slnx`
- [ ] End-to-end bootstrap script succeeds: `./build-salep.sh` (or `pwsh ./build-salep.ps1`)
- [ ] No generated files in `src/samples/Opinionated/` or `src/samples/MinimalDependencies/` were modified manually (all updates must originate from `SalepGenerator`).
- [ ] Documentation updated if CLI flags, configuration keys, or MSBuild properties were changed.
