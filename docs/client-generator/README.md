# Salep C# Client Generator

This guide covers Salep C# Client Generator. For the independent language library, see [Salep GraphQL Parser](../parser/README.md); for shared naming and package mapping, see [architecture](../architecture.md).
A strongly-typed, schema-driven C# GraphQL client generator and MSBuild build tool. Integrated directly into the MSBuild compilation pipeline, it generates types, operations, System.Text.Json converters, discriminated unions (Dunet by default, or native C# 15 unions when configured), tests, and sample usage at build time — with zero runtime reflection, zero runtime dynamic code, and zero runtime dependency on Salep assemblies.

## Documentation & Guides

Salep provides tailored documentation for both human developers and AI agents:

| Guide | Audience & Scope | Description |
| :--- | :--- | :--- |
| **[Design Principles & Architecture](design-principles-and-architecture.md)** | All Audiences | Core design principles, architectural choices, and technical rationale. |
| **[Consumer Guide (.NET Developers)](consumer-guide.md)** | Consuming Developers | Complete guide to integrating the `Salep.ClientGenerator` package, configuration, and using generated clients. |
| **[AI Agent Consumer Guide](agent-consumer-guide.md)** | Consuming AI Agents | High-signal, prompt-ready integration recipes and diagnostics for AI agents consuming Salep. |
| **[Developer Contributor Guide](developer-contributor-guide.md)** | Contributing Developers | Codebase layout, generator pipeline internals, test suites, and how to add new features. |
| **[AI Agent Contributor Guide](agent-contributor-guide.md)** | Contributing AI Agents | Agent operational invariants, diagnostic decision trees, and validation rules for repo contributors. |
| **[Config Inheritance & Dedup](config-inheritance-dedup.md)** | Enterprise / Multi-Project | Profiles for defaults and verified `baseClient` contracts for ownership. |
| **[Opinionated Sample Showcase & Architecture](../../src/samples/Opinionated/README.md)** | Developers & Contributors | Reference multi-tier client implementation, schema coverage, and dogfooding testbed. |
| **[GraphQL Spec Coverage](spec-coverage.md)** | Spec Compliance | Living checklist mapping GraphQL spec features to schema and test evidence. |

---

## Core Design Principles & Choices

1. **Single Unified Package (`Salep.ClientGenerator`)**:
   Distributed as a single build-tool NuGet package. MSBuild targets (`build/`) invoke a target-matched CLI host out-of-process (`tools/net10.0/any/` or `tools/net11.0/any/`), keeping generator internals encapsulated.
2. **Zero Runtime Lock-In**:
   Emitted C# code references only standard runtime packages (such as `Dunet` for the default discriminated-union mode and `System.Text.Json`). Native-union mode does not require Dunet. Consuming applications never reference `Salep.ClientGenerator.dll`, Roslyn, or HotChocolate at runtime.
3. **Out-of-Process Execution via `dotnet exec`**:
   Build-time code generation runs out-of-process, isolating MSBuild from Roslyn/parser assembly conflicts and ensuring deterministic process execution.
4. **First-Class Discriminated Unions**:
   GraphQL unions and interfaces map to type-safe C# discriminated unions with compile-time exhaustive pattern matching.
5. **Deterministic Monorepo Dogfooding**:
   Uses repository-local NuGet feeds (`artifacts/packages/`), Central Package Management (CPM), and package source mapping to guarantee clean package testing without cache poisoning.

---

## Features

- **Schema-driven generation** — types, operations, and client code derived entirely from your `.graphql` schema and operation files
- **Strongly-typed client** — deserialize responses into C# types from your schema, including unions, interfaces, and enums
- **MSBuild integration** — runs as a `CoreCompile` after referenced projects build task; generated files are automatically included in compilation
- **Config inheritance** — profiles support `extends` for defaults; clients use `baseClient` for shared types
- **Ancestor ownership dedup** — prevents re-generating types already owned by ancestor projects in a dependency chain
- **Union & interface handling** — emits Union JSON converters with `[JsonDerivedType]` or custom visitors
- **Native C# 15 unions (opt-in)** — set `"unionRepresentation": "native"` in `salep.json` and target .NET 11 to use compiler-native union types without Dunet
- **NodaTime support** — map GraphQL scalars (e.g., `DateTime`, `Instant`) to NodaTime types with JSON serialization
- **GET-based queries** — supports `useHttpGet` mode for cacheable queries with configurable max URL length
- **Batching** — optionally batch multiple operations into a single HTTP request
- **Test generation** — generate xUnit test projects with deterministic, network-free test HTTP handlers
- **Sample usage output** — optional `Program.cs` sample showing how to call each operation

## Project Structure

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

## Quick Start

### 1. Add the MSBuild package reference

In your consuming `.csproj`:

```xml
<Project Sdk="Microsoft.NET.Sdk">
  <ItemGroup>
    <PackageReference Include="Dunet" /> <!-- required with the default union representation -->
    <PackageReference Include="NodaTime" />
    <PackageReference Include="NodaTime.Serialization.SystemTextJson" />
  </ItemGroup>

  <ItemGroup>
    <PackageReference Include="Salep.ClientGenerator" PrivateAssets="all" />
  </ItemGroup>
</Project>
```

### 2. Create a GraphQL schema

Place a `.graphql` SDL schema file in your project:

```graphql
# schema.graphql
type Query {
  user(id: ID!): User
}

type User {
  id: ID!
  name: String!
  email: String
}
```

### 3. Write GraphQL operations

Create `.graphql` operation files:

```graphql
# graphql/GetUser.graphql
query GetUser($id: ID!) {
  user(id: $id) {
    id
    name
    email
  }
}
```

### 4. Create a generator config

```jsonc
// salep.json
{
  "version": 1,
  "kind": "client",
  "schema": "./schema.graphql",
  "operations": "./graphql",
  "output": "./Generated",
  "namespace": "MyApp.GraphQL",
  "emitSample": true,
  "scalarPreset": "nodatime",
  "useHttpGet": true,
  "enableBatching": true,
  "maxGetUrlLength": 2048,
  "clientName": "MyGraphQLClient"
}
```

### 5. Build

The MSBuild task runs `CoreCompile` after referenced projects build. Generated C# files appear in `output` and are automatically included in compilation.

### 6. Use the generated client

```csharp
var client = new MyGraphQLClient(httpClient, new Uri("https://api.example.com/graphql"));
var response = await client.GetUserAsync(new() { Id = "123" });
Console.WriteLine(response.Data.User.Name);
```

## Configuration contracts

See [configuration roles and settings](configuration.md) and the [JSON schema](config.schema.json). Profiles share defaults, `baseClient` establishes generated symbol ownership, and tests reference a verified concrete client.

## GraphQL Spec Coverage

The generator supports a broad subset of the GraphQL specification:

- Schema definitions, extensions, and directives
- Object, input, enum, union, and interface types
- Type extensions for all type kinds
- Interface inheritance (`implements`)
- `@deprecated` on fields and enum values
- `@specifiedBy` on scalars
- Queries, mutations, and subscriptions
- Operation names, variables, and default values
- Aliases, named fragments, and inline fragments
- Fragment spreads across files
- Directives on operations, fields, fragments, and fragment spreads

See [spec-coverage.md](spec-coverage.md) for the full checklist.

## How It Works

1. **MSBuild** triggers `SalepGenerate` before compilation
2. The task loads the JSON config (resolving `extends` chains)
3. The GraphQL schema is parsed with **Salep.GraphQLParser**
4. Operation `.graphql` files are loaded and parsed
5. **SchemaTypesEmitter** generates C# records/enums for schema types
6. **UnionJsonConvertersEmitter** generates JSON converters for unions
7. **OperationsEmitter** generates request/response types per operation
8. **GraphQLClientEmitter** generates the typed client class
9. **TestsEmitter** generates xUnit tests with a deterministic HTTP handler
10. Generated `.cs` files are injected into the compilation via MSBuild items

## Building the Project

### Prerequisites

- .NET 11.0 RC 1 SDK (the repository currently targets .NET 10.0 and .NET 11.0)
- C# 14 / `LangVersion latest`

### Build

```bash
dotnet build
```

### Run tests

```bash
dotnet test
```

### Run the generator standalone

```bash
dotnet run --project src/Salep.ClientGenerator.Cli/Salep.ClientGenerator.Cli.csproj --framework net10.0 -- \
  --config path/to/salep.json
```

### Local package packing and dogfooding bootstrap

To build the core solution, pack the single unified `Salep.ClientGenerator` NuGet package to the local feed (`artifacts/packages/`), and restore/test sample projects end-to-end:

**Linux / macOS / Git Bash:**
```bash
./build-salep.sh
```

**Windows (PowerShell / Windows Terminal):**
```powershell
pwsh ./build-salep.ps1
# or Windows PowerShell:
powershell -ExecutionPolicy Bypass -File .\build-salep.ps1
```

## Development Workflow

- The **generator** is the source of truth; fix issues in the generator, then regenerate outputs
- Never manually edit generated files in `src/samples/Opinionated/Salep.Samples.Opinionated.Client` or `src/samples/Opinionated/Salep.Samples.Opinionated.Client.Tests`
- All behavior is driven by the schema and operation inputs
- If generated code fails to compile: fix the generator, regenerate
- If tests fail: determine if the generator or test generation is wrong, fix the generator, regenerate
- See [agent contributor guide](agent-contributor-guide.md) for generator workflows and [AGENTS.md](../../AGENTS.md) for shared rules

## Dependencies

| Package | Purpose |
|---------|---------|
| `HotChocolate.Language` | GraphQL SDL parsing |
| `Microsoft.CodeAnalysis.CSharp` | Roslyn-based C# code emission |
| `Dunet` | Discriminated union support in generated code (default mode only) |
| `Microsoft.Extensions.FileSystemGlobbing` | Config file glob resolution |
| `Microsoft.Build.Utilities.Core` | MSBuild task infrastructure |
| `NodaTime` / `NodaTime.Serialization.SystemTextJson` | Optional date/time support |

## License

This project is licensed under the MIT License — see the [LICENSE](../../LICENSE) file for details.
