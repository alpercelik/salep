# AI Agent Contributor Guide — Salep C# Client Generator

This guide covers Salep C# Client Generator. For the independent language library, see [Salep GraphQL Parser](../parser/README.md); for shared naming and package mapping, see [architecture](../architecture.md).
This guide contains operational invariants, workflows, and diagnostic decision trees for **AI Agents** working on the **Salep** repository.

---

## 1. Prime Directives for AI Agents

1. **The Generator is the Source of Truth**:
   All C# output files (models, operations, client, converters, and tests) in `src/samples/` or generated directories are build artifacts. **NEVER manually edit generated files**. Always modify generator logic in `src/Salep.ClientGenerator/`, then regenerate artifacts.
2. **Schema-Driven Architecture**:
   Never add hardcoded special cases for specific field names or types outside general GraphQL schema and operation rules. All generation features must be generic and driven by GraphQL SDL or operation ASTs.
3. **Roslyn and configuration coverage**: Build typed nodes with official `SyntaxGenerator` and C#-specific `SyntaxFactory`; keep adapters small and configuration decisions in their owning components. Do not restore string-built members or statements. Keep error-diagnostic validation in `Emission/RoslynEmitter.cs`; do not suppress parse failures or restore a process-global callback. Extend `RoslynEmissionTests` with compilation cases when changing configuration behavior. Use `OperationVariablePolicies` for variable filtering/inlining so models, clients, samples, and tests agree.
4. **Deterministic Verification**:
   No task is complete until:
   - All projects in `src/Salep.slnx` compile with 0 errors.
   - Core and MSBuild tests in `src/Salep.ClientGenerator.Tests` and `src/Salep.ClientGenerator.MSBuild.Tests` pass.
   - The end-to-end bootstrap script `./build-salep.sh` (or `pwsh ./build-salep.ps1` on Windows) completes cleanly with 0 test failures.
5. **Never Weaken Tests**:
   Do not delete assertions, weaken expected types, or comment out failing tests to achieve green builds. Fix the root cause in the generator or AST parser.

---

## 2. Agent Operational Workflow

When implementing features, fixing bugs, or refactoring in Salep:

```
[Analyze Issue & Locate Component]
                │
                ▼
[Modify Generator Engine in src/Salep.ClientGenerator/] ◄─────┐
                │                                │
                ▼                                │ Fix logic
[Run Unit Tests: src/Salep.ClientGenerator.Tests]             │
                │ Fail                           │
                ├────────────────────────────────┘
                │ Pass
                ▼
[Regenerate Sample Clients via CLI]
                │
                ▼
[Execute Full Verification: ./build-salep.sh / .ps1]
                │ Fail
                ├────────────────────────────────┘
                │ Pass
                ▼
[Update docs/parser/spec-coverage.md if spec expanded]
                │
                ▼
[Task Complete]
```

---

## 3. How to Regenerate Artifacts

### Via CLI Host
To regenerate sample client artifacts using the standalone CLI:

```bash
dotnet run --project src/Salep.ClientGenerator.Cli/Salep.ClientGenerator.Cli.csproj --framework net10.0 -- \
  --config src/samples/Opinionated/Salep.Samples.Opinionated.Client/salep.json
```

### Via Full Monorepo Bootstrap
To test the generator, pack the NuGet package, restore sample consumers, and validate all tests end-to-end:

**Linux / macOS / Git Bash:**
```bash
./build-salep.sh
```

**Windows PowerShell:**
```powershell
pwsh ./build-salep.ps1
```

---

## 4. Autonomous Diagnostic Decision Tree

When encountering failures during an agent session, follow this decision tree:

### Case A: Compilation Error in Generated Code (`src/samples/Opinionated/Salep.Samples.Opinionated.Client/`)
1. Inspect the error message and line in the generated `.cs` file.
2. Identify which emitter produced the invalid syntax:
   - Schema types/records -> `src/Salep.ClientGenerator/Emission/SchemaTypesEmitter.cs`
   - Operations / request models -> `src/Salep.ClientGenerator/Emission/OperationsEmitter.cs`
   - Discriminated union converters -> `src/Salep.ClientGenerator/Emission/UnionJsonConvertersEmitter.cs`
   - Client methods / HTTP execution -> `src/Salep.ClientGenerator/Emission/GraphQLClientEmitter.cs`
   - xUnit test classes -> `src/Salep.ClientGenerator/Emission/TestsEmitter.cs`
3. Fix the syntax emission logic in the emitter.
4. Re-run `./build-salep.sh` (or `pwsh ./build-salep.ps1`).

### Case B: Test Failure in `GeneratedClient.Tests`
1. Check if the failure is in payload serialization, response deserialization, or mock HTTP handling.
2. If deserialization failed on a union or interface: check `UnionJsonConvertersEmitter.cs` and `[JsonDerivedType]` discriminator logic.
3. If JSON property mapping failed: check `SchemaModel.cs` field naming and `[JsonPropertyName]` emission.
4. If the test expectation itself was invalid: verify whether `TestsEmitter.cs` emitted an expectation inconsistent with the GraphQL schema definition.
5. Fix generator/emitter, regenerate, and verify.

### Case C: MSBuild Target or Incremental Build Issues
1. Inspect `src/Salep.ClientGenerator.MSBuild/Salep.ClientGenerator.props` and `src/Salep.ClientGenerator.MSBuild/Salep.ClientGenerator.targets`.
2. Select exactly one `SalepConfig`, build project references first, verify current dependency manifests, and include the exact generated source list before `CoreCompile`. Do not restore timestamp-only stamp gates.
3. Verify `src/Salep.ClientGenerator.MSBuild.Tests/` using `dotnet run --project src/Salep.ClientGenerator.MSBuild.Tests/Salep.ClientGenerator.MSBuild.Tests.csproj --framework net10.0`.

---

## 5. Build and Test Commands for Agents

| Task | Command |
|------|---------|
| Build solution | `dotnet build src/Salep.slnx -p:EnforceCodeStyleInBuild=false -p:TreatWarningsAsErrors=false` |
| Run Core Unit Tests | `dotnet run --project src/Salep.ClientGenerator.Tests/Salep.ClientGenerator.Tests.csproj --framework net10.0` |
| Run MSBuild Tests | `dotnet run --project src/Salep.ClientGenerator.MSBuild.Tests/Salep.ClientGenerator.MSBuild.Tests.csproj --framework net10.0` |
| Run End-to-End Pipeline (Bash) | `bash ./build-salep.sh` |
| Run End-to-End Pipeline (PowerShell) | `pwsh ./build-salep.ps1` |
| Inspect CLI Help | `dotnet run --project src/Salep.ClientGenerator.Cli/Salep.ClientGenerator.Cli.csproj --framework net10.0 -- --help` |

---

## 6. Spec Coverage Tracking

Whenever GraphQL SDL syntax or operation features are added or changed:
1. Update `src/samples/Opinionated/Salep.Samples.Opinionated.Client/schema.coverage.graphql` or `src/samples/Opinionated/Salep.Samples.Opinionated.Client/graphql/` with the new syntax.
2. Ensure `docs/parser/spec-coverage.md` reflects the updated status and line numbers in the evidence section.
