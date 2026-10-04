# Salep C# Client Generator — Design Principles, Architecture, and Choices

The sole generator uses Scriban. This document articulates the design principles, architectural choices, and technical rationale underlying **Salep**, a schema-driven GraphQL client generator and MSBuild build tool for .NET.

---

## 1. System Vision and Core Principles

Salep transforms GraphQL SDL schemas (`.graphql`) and executable operation files into strongly-typed, compile-time verified C# clients and test suites.

### Core Principles

1. **Schema and Operations as Single Source of Truth**:
   The generator is pure and deterministic. Types, operations, serialization rules, and tests are strictly derived from GraphQL documents, eliminating manual boilerplate and schema drift.
2. **Build-Time Generation over Runtime Reflection**:
   Code generation executes at build time (`CoreCompile` after referenced projects build), emitting standard C# records, classes, and JSON converters. The generated client uses System.Text.Json and generated union converters; no Salep runtime assembly is required.
3. **Zero Runtime Coupling**:
   The code generator itself disappears from the runtime architecture. Consuming applications do not reference `Salep.ClientGenerator.dll`, Roslyn, HotChocolate, or MSBuild assemblies at runtime.
4. **First-Class Discriminated Unions**:
   GraphQL unions and interfaces default to idiomatic C# discriminated unions powered by [Dunet](https://github.com/domn1995/dunet). An opt-in `unionRepresentation: "native"` setting emits C# 15 native unions when targeting .NET 11.
5. **Deterministic Monorepo & Multi-Project Support**:
   Salep supports complex enterprise micro-frontends and layered client architectures through profile defaults and transitive `baseClient` ownership contracts.

---

## 2. Architecture Overview

Salep is structured into three internal tiers, exposed to consumers as a single unified build-tool package:

```
                            ┌──────────────────────────────────────────────┐
                            │               Consuming Project              │
                            │  <PackageReference                            │
                            │    Include="Salep.ClientGenerator"            │
                            │                    PrivateAssets="all" />    │
                            └──────────────────────┬───────────────────────┘
                                                   │
                                     Restores build/ assets
                                                   │
                                                   ▼
┌──────────────────────────────────────────────────────────────────────────────────────────────────┐
│                                       Salep Package Assets                                       │
│                                                                                                  │
│   ┌──────────────────────────────────────────────────────────────────────────────────────────┐   │
│   │ build/Salep.ClientGenerator.props & build/Salep.ClientGenerator.targets (MSBuild)         │   │
│   │  • Resolves salep.json, schemas, and operations                                          │   │
│   │  • Declares Incremental Inputs / Outputs                                                 │   │
│   │  • Executes a matching tools/net10.0/any or tools/net11.0/any CLI host ...               │   │
│   └──────────────────────────────────────────────┬───────────────────────────────────────────┘   │
│                                                  │                                               │
│                                           Out-of-Process                                         │
│                                           dotnet exec CLI                                        │
│                                                  │                                               │
│                                                  ▼                                               │
│   ┌──────────────────────────────────────────────────────────────────────────────────────────┐   │
│   │ tools/{net10.0,net11.0}/any/Salep.ClientGenerator.Cli.dll (Executable Host)                              │   │
│   │  • Parses CLI arguments (--config, --working-directory)                     │   │
│   │  • Returns deterministic process exit codes (0 = Success, 1 = Error)                     │   │
│   └──────────────────────────────────────────────┬───────────────────────────────────────────┘   │
│                                                  │                                               │
│                                       Invokes Generator API                                      │
│                                                  │                                               │
│                                                  ▼                                               │
│   ┌──────────────────────────────────────────────────────────────────────────────────────────┐   │
│   │ tools/{net10.0,net11.0}/any/Salep.ClientGenerator.dll (Core Generator Engine)                            │   │
│   │  • Schema Parsing (Salep.GraphQLParser)                                                │   │
│   │  • Semantic Modeling & Type Graph Resolution                                             │   │
│   │  • Scriban Template Rendering (Types, Operations, Converters, Client, xUnit Tests)    │   │
│   │  • Manifest Ownership & Transitive Dedup (.salep.manifest.json)                          │   │
│   └──────────────────────────────────────────────────────────────────────────────────────────┘   │
└──────────────────────────────────────────────────────────────────────────────────────────────────┘
```

---

## 3. Key Design Choices and Rationale

### Choice 1: Single Public NuGet Package (`Salep.ClientGenerator`) vs. Multi-Package Architecture
- **Decision**: Publish a single NuGet package `Salep.ClientGenerator` containing MSBuild targets under `build/` and target-matched binaries under `tools/net10.0/any/` and `tools/net11.0/any/`. The engine project is private; the MSBuild packaging project owns this package ID. Do not publish separate CLI or MSBuild packages.
- **Rationale**:
  - **Consumer Simplicity**: Consumers only need a single `<PackageReference Include="Salep.ClientGenerator" PrivateAssets="all" />`.
  - **Encapsulation**: Internal architectural layers (CLI, Scriban templates, parser dependencies) remain internal implementation details without creating package dependency webs.
  - **Versioning Alignment**: Ensures targets, CLI, and generator engines are always perfectly version-aligned without package diamond dependency conflicts.

### Choice 2: Out-of-Process CLI Execution (`dotnet exec`) vs. In-Process MSBuild Task
- **Decision**: Execute generation out-of-process via `dotnet exec "$(SalepToolPath)/Salep.ClientGenerator.Cli.dll"` instead of loading a custom `Microsoft.Build.Utilities.Task` inside the MSBuild process.
- **Rationale**:
  - **Process & Assembly Isolation**: MSBuild runs inside its own runtime host (`devenv.exe`, `dotnet build`, Rider backend). In-process tasks frequently suffer from assembly locking, conflicting versions of `System.Text.Json`, `Microsoft.CodeAnalysis`, or parser libraries.
  - **Hot Reloading & Tool Independence**: Running out-of-process allows the CLI to be executed, debugged, and tested independently from MSBuild across CI, IDE tooling, and scripts.
  - **Deterministic Exit & Memory Safety**: Process cleanup is handled cleanly by the OS upon generation completion.

### Choice 3: `build/` Assets Only vs. `buildTransitive/` Propagation
- **Decision**: Place props and targets under `build/` (`build/Salep.ClientGenerator.props`, `build/Salep.ClientGenerator.targets`) and explicitly avoid `buildTransitive/`.
- **Rationale**:
  - Code generation should only run in projects that explicitly opt in with `<PackageReference Include="Salep.ClientGenerator" PrivateAssets="all" />`.
  - Transitive build propagation (`buildTransitive/`) would cause all downstream consuming projects in a solution to attempt code generation even if they do not define GraphQL operations or configurations.

### Choice 4: Zero Salep Runtime Dependencies vs. `Salep.Runtime`
- **Decision**: Emitted code references only standard or community-standard runtime libraries (such as `Dunet` in the default union mode, `NodaTime` if configured, and `System.Text.Json`). Native C# 15 union mode removes the Dunet requirement. There is no `Salep.Runtime.dll`.
- **Rationale**:
  - **Zero Lock-In**: Generated code remains lightweight and directly inspectable C#.
  - **Supply Chain Hygiene**: Applications do not inherit extra framework runtime dependencies.
  - **Compatibility**: Avoids framework-level version mismatches across heterogeneous microservices.

### Choice 5: Monorepo Dogfooding via CPM & Local Package Feed vs. `ProjectReference` Switching
- **Decision**: Dogfood `Salep.ClientGenerator` with a local NuGet feed (`artifacts/packages/`), root Central Package Management and paired `build-salep.sh` / `build-salep.ps1` workflows that restore samples against the exact invocation version.
- **Rationale**:
  - **True Consumer Fidelity**: `ProjectReference` bypasses MSBuild package props/targets resolution, meaning local tests would not test what external consumers actually experience.
  - **Cache Poisoning Prevention**: Fixed versions (like `1.0.0-local`) cause NuGet global package cache poisoning and restore race conditions. Salep uses unique build versions (`-p:SalepVersion=...`) ensuring atomic restores.
  - **Package Source Mapping**: Configured in `NuGet.config` to map `Salep.ClientGenerator` -> `local` and `*` -> `nuget.org`, eliminating NuGet multi-feed warning `NU1507`.

### Choice 6: Multi-Project Configuration Inheritance & Ancestor Ownership Dedup
- **Decision**: `salep.json` supports `"baseClient": "../Base/salep.json"` and automatic tracking via `.salep.manifest.json`.
- **Rationale**:
  - In enterprise architectures, multiple microservice client projects or modular frontends share a common GraphQL backend schema.
  - Without dedup, each module would regenerate identical C# type records for shared schema types, resulting in C# type identity collisions and compiler errors (`CS0433: type exists in both assemblies`).
  - Salep tracks generated type ownership across the inheritance graph, suppressing duplicate types in child projects while preserving access to ancestor-generated types.

---

## 4. Execution & Code Generation Lifecycle

The MSBuild build-time execution lifecycle proceeds as follows:

```
[1. MSBuild Evaluation]
     │ Loads the selected versioned config and verifies dependency contracts
     │
[2. Incremental Check]
     │ Compares timestamps of verified inputs and owned outputs (including templates) against salep.generated.stamp under IntermediateOutputPath
     │ If up-to-date -> Skip to [7. Compilation]
     │
[3. Out-of-Process CLI Execution]
     │ Invokes: dotnet exec Salep.ClientGenerator.Cli.dll generate --config salep.json
     │
[4. Semantic Loading & Inheritance Resolution]
     │ Parses GraphQL SDL schema (Salep.GraphQLParser)
     │ Resolves profiles and verifies referenced client contracts (.salep.manifest.json)
     │
[5. Code Emission]
     │ Emits SchemaTypes.cs (C# records, enums, input types)
     │ Emits Operations.cs (request/response models, parameter records)
     │ Emits UnionJsonConverters.cs (System.Text.Json converters)
     │ Emits GraphQLClient.cs (generic typed ExecuteAsync transport)
     │ Emits Tests (xUnit test classes with deterministic mock HTTP handler)
     │
[6. Manifest Generation]
     │ Writes .salep.manifest.json recording owned types and operations
     │ Writes timestamp marker to salep.generated.stamp under IntermediateOutputPath
     │
[7. Compilation]
     │ Injects emitted .cs files into @(Compile)
     │ Roslyn compiles consumer assembly cleanly
```
