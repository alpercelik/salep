# AI Agent Contributor Guide — Salep C# Client Generator

This guide covers Salep C# Client Generator. For the independent language library, see [Salep GraphQL Parser](../parser/README.md); for shared naming and package mapping, see [architecture](../architecture.md).
This guide contains operational invariants, workflows, and diagnostic decision trees for **AI Agents** working on the **Salep** repository.

---

## 1. Prime Directives for AI Agents

1. **The Generator is the Source of Truth**:
   All C# output files (models, operations, client, converters, and tests) in `src/samples/` or generated directories are build artifacts. **NEVER manually edit generated files**. Always modify default Scriban generator logic in `src/Salep.ClientGenerator/`, then regenerate artifacts.
2. **Schema-Driven Architecture**:
   Never add hardcoded special cases for specific field names or types outside general GraphQL schema and operation rules. All generation features must be generic and driven by GraphQL SDL or operation ASTs.
3. **Model and template ownership**: Keep source adapters, neutral GraphQL models, C# target policies and Scriban templates separate. Resolve semantics in `GraphQlModelFactory` and target model factories; customize output through the template catalog. Preserve variable filtering/inlining across models, clients, samples and tests. Review contract fixture changes explicitly; tests must never refresh them automatically.
4. **Deterministic Verification**:
   No task is complete until:
   - All projects in `src/Salep.slnx` compile with 0 errors.
   - Core and MSBuild tests in `src/Salep.ClientGenerator.Tests`, `src/Salep.ClientGenerator.MSBuild.Tests` pass.
   - The end-to-end bootstrap script `./build-salep.sh` (or `pwsh ./build-salep.ps1` on Windows) completes cleanly with 0 test failures.
5. **Never Weaken Tests**:
   Do not delete assertions, weaken expected types, or comment out failing tests to achieve green builds. Fix the root cause in the generator or AST parser.

6. **Scriban Template Readability (Required)**:
   For every template under `src/Salep.ClientGenerator/Templates/`, including Markdown output, put each block-level `for`, `if`, `else`, and `end` tag on its own line. Indent the outer tag to the generated code or Markdown location it controls (for example, 8 spaces inside a method body), then indent each nested control tag one level deeper than its parent. Do not leave tags flush-left just because whitespace control removes their indentation from output. Use `{{~ ... ~}}` on tag-only lines so directive lines do not leak into output. Add `# begin <kind> <label>` to each block opener and the corresponding `# end <kind> <label>` to its `end`. Put emitted C# or Markdown on separate lines, indented for the generated output. Inline only short conditional substitutions that form part of one output statement or sentence. Do not inline loops; compute a C# projection in the model if a loop produces part of one declaration. Run the Scriban tests to validate rendered output after template changes.

---

## 2. Agent Operational Workflow

When implementing features, fixing bugs, or refactoring in Salep:

```
[Analyze Issue & Locate Component]
                │
                ▼
[Modify Default Generator in src/Salep.ClientGenerator/] ◄─────┐
                │                                             │
                ▼                                             │ Fix logic
[Run Unit Tests: src/Salep.ClientGenerator.Tests]             │
                │ Fail                                        │
                ├─────────────────────────────────────────────┘
                │ Pass
                ▼
[Regenerate Sample Clients via CLI]
                │
                ▼
[Execute Full Verification: ./build-salep.sh / .ps1]
                │ Fail
                ├──────────────────────────────────┘
                │ Pass
                ▼
[Update docs/client-generator/spec-coverage.md if spec expanded]
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
  generate --config src/samples/Opinionated/Client/salep.json
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

### Case A: Compilation Error in Generated Code (`src/samples/Opinionated/Client/`)
1. Inspect the error message and line in the generated `.cs` file.
2. Identify the owning template and model factory:
   - Schema types: `Templates/SchemaTypes.scriban-cs` and `Targets/CSharpSchemaTemplateModelFactory.cs`.
   - Operations: `Templates/Operations.scriban-cs` and `Targets/CSharpOperationTemplateModelFactory.cs`.
   - Converters: `Templates/UnionJsonConverters.scriban-cs`.
   - Transport: `Templates/GraphQLClient.scriban-cs` and its fragments.
   - Generated tests: the corresponding test template and test projection.
3. Trace configuration through `Generation/ScribanGenerator.cs`; fix the source, add a compiled regression and regenerate through MSBuild.
4. Run the full Bash or PowerShell dogfooding workflow.

### Case B: Generated test failures

Check request serialization, response JSON names, polymorphic `__typename` and sample values against the GraphQL model. Fix the owning projection or template, regenerate and run the test. Generated expectations must describe the operation contract, including nullability and aliases.

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
1. Update `src/samples/Opinionated/Client/schema.coverage.graphql` or `src/samples/Opinionated/Client/graphql/` with the new syntax.
2. Ensure `docs/client-generator/spec-coverage.md` reflects the updated status and line numbers in the evidence section.
