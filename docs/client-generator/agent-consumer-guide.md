# AI Agent Consumer Guide — Consuming Salep C# Client Generator in .NET Projects

This document provides prompt-ready recipes, conventions, and operational instructions for **AI Agents** integrating or consuming the **Salep** GraphQL client generator package in a target repository.

---

## 1. Fast Integration Protocol (Step-by-Step)

When an AI agent is tasked with adding a GraphQL client to a .NET project using Salep, follow these 6 phases strictly:

```
[Phase 1: Update .csproj / Package References]
                    │
                    ▼
[Phase 2: Add schema.graphql & Operations]
                    │
                    ▼
[Phase 3: Create salep.json Configuration]
                    │
                    ▼
[Phase 4: Run dotnet build (Triggers Code Generation)]
                    │
                    ▼
[Phase 5: Implement Application Code & Dependency Injection]
                    │
                    ▼
[Phase 6: Add Unit Tests with TestHttpMessageHandler]
```

---

## 2. Phase-by-Phase Instructions for Agents

### Phase 1: Update `.csproj` / Package References

1. Locate the target `.csproj` file.
2. Add `Salep.ClientGenerator` with `PrivateAssets="all"`.
3. Add `Dunet` when using the default union representation. Native C# 15 unions instead require .NET 11 and do not need Dunet.
4. If Central Package Management (CPM) is enabled in `Directory.Packages.props`, add package versions there.

```xml
<!-- In TargetProject.csproj -->
<ItemGroup>
  <PackageReference Include="Salep.ClientGenerator" PrivateAssets="all" />
  <PackageReference Include="Dunet" />
</ItemGroup>
```

### Phase 2: Create Schema & Operation Files

1. Ensure the GraphQL SDL schema is placed at `./schema.graphql` (or path configured in `salep.json`).
2. Create operation files under `./graphql/` (e.g., `queries.graphql`, `mutations.graphql`, `fragments.graphql`).
3. **Agent Rule**: Every operation must have an operation name (`query GetUser(...)`, not anonymous `query { ... }`).
4. **Agent Rule**: If selecting a Union or Interface, always include `__typename` and typed inline fragment spreads (`... on TypeA { ... }`).

```graphql
# graphql/queries.graphql
query GetUser($id: ID!) {
  user(id: $id) {
    id
    name
    email
  }
}
```

### Phase 3: Create `salep.json`

Place `salep.json` in the root of the consumer project directory:

```json
{
  "version": 1,
  "kind": "client",
  "schema": "./schema.graphql",
  "operations": "./graphql",
  "output": "./Generated",
  "namespace": "TargetApp.GraphQL",
  "clientName": "TargetGraphQLClient"
}
```

*Note on Paths*: All relative paths in `salep.json` resolve relative to the directory containing `salep.json`.

### Phase 4: Execute Initial Build

Run `dotnet build` to trigger the MSBuild `CoreCompile` after referenced projects build generation target:

```bash
dotnet build
```

This generates C# records and the typed client in `./Generated/` and compiles them into the project.

### Phase 5: Implement Application Code & Dependency Injection

#### Dependency Injection Registration
In ASP.NET Core `Program.cs` or Service Registration:

```csharp
using TargetApp.GraphQL;

builder.Services.AddHttpClient<TargetGraphQLClient>(client =>
{
    client.BaseAddress = new Uri("https://api.example.com/graphql");
});
```

#### Calling Client Methods
```csharp
public class UserClientService
{
    private readonly TargetGraphQLClient _client;

    public UserClientService(TargetGraphQLClient client) => _client = client;

    public async Task<User?> FetchUserAsync(string id, CancellationToken ct = default)
    {
        var response = await _client.GetUserAsync(new() { Id = id }, ct);

        if (response.Errors is { Count: > 0 })
        {
            throw new InvalidOperationException(response.Errors[0].Message);
        }

        return response.Data?.User;
    }
}
```

#### Handling Discriminated Unions with Dunet
```csharp
// For GraphQL union SearchResult = User | Organization
var searchResponse = await _client.SearchAsync(new() { Query = "test" });

foreach (var item in searchResponse.Data.Search)
{
    item.Match(
        user => ProcessUser(user),
        org => ProcessOrg(org)
    );
}
```

### Phase 6: Implement Unit Tests with `TestHttpMessageHandler`

```csharp
[Fact]
public async Task FetchUserAsync_ReturnsUser_WhenResponseSuccessful()
{
    var handler = new TestHttpMessageHandler();
    handler.EnqueueResponse(new GetUserQueryResponse
    {
        Data = new()
        {
            User = new() { Id = "u1", Name = "Alice", Email = "alice@example.com" }
        }
    });

    var httpClient = new HttpClient(handler) { BaseAddress = new Uri("http://mock/graphql") };
    var client = new TargetGraphQLClient(httpClient);

    var service = new UserClientService(client);
    var user = await service.FetchUserAsync("u1");

    Assert.NotNull(user);
    Assert.Equal("Alice", user.Name);
}
```

---

## 3. In-Situ Directory Guardrails (`Generated/agents.md`)

Salep automatically emits an `agents.md` file directly inside the output directory (e.g. `./Generated/agents.md` and `./GeneratedTests/agents.md`).

When exploring or modifying a codebase consuming Salep:
1. **Never edit files in `Generated/`**: Treat any directory containing `agents.md` and `.salep.manifest.json` as strictly read-only build artifacts.
2. **Follow upstream modification paths**: To make changes, edit the GraphQL SDL schema (`schema.graphql`), operation documents (`graphql/*.graphql`), or configuration (`salep.json`), then re-run `dotnet build`.
3. **Opt-out configuration**: If desired, emission can be disabled in `salep.json` via `"emitAgentInstructions": false`.

---

## 4. Agent Troubleshooting & Recovery Playbook

| Diagnostic Symptom | Root Cause | Agent Action |
| :--- | :--- | :--- |
| `CS0246: The type or namespace name 'UnionAttribute' could not be found` | `Dunet` package missing from project | Add `<PackageReference Include="Dunet" />` to `.csproj`. |
| `Salep: Base generator config file not found` | Invalid `"extends"` path in `salep.json` | Verify parent path is relative to current `salep.json`. |
| `CS0433: The type 'X' exists in both ProjectA and ProjectB` | Type duplication in layered projects | In child `salep.json`, add `"baseClient": "../ProjectA/salep.json"`. |
| `No operations found matching path` | Invalid `operations` in `salep.json` | Verify directory exists and contains `.graphql` files. |
| `NullReferenceException` on `response.Data.Field` | GraphQL execution returned errors | Always check `if (response.Errors is { Count: > 0 })` before accessing `response.Data`. |


## Consumer template overrides

Use `Salep.ClientGenerator` with version-1 `salep.json`. The current package catalog has 73 embedded templates; prefer empty member/annotation/transport hooks or one declaration/method/property fragment before replacing a whole file. Map registered keys to files relative to the JSON declaring them; profiles and referenced-client test mappings inherit, and local matching keys replace inherited entries. Includes use registered keys, with `default:<key>` delegating to one embedded default. Arbitrary filesystem includes are unsupported.

Export and validate with the package's framework-matched `Salep.ClientGenerator.Cli.dll` through `dotnet exec`; locate it with `SalepToolPath` on the restored project. Do not assume a globally installed `salep` tool or the former Scriban package ID. See [the customization tutorial](template-customization.md) and its [executable examples](examples/template-customization/README.md) for tested snippets, model scopes and rebuild checks.

Generated operation queries are formatted multiline raw strings. `operation.query` retains plain GraphQL text; `operation.query_literal` is the fully indented raw literal projection. Interpolate that projection at column zero to avoid Scriban adding another indentation prefix. Validate overridden C# by building the consumer and exercising the changed transport/serialization path. Template validation alone does not prove compilation or runtime behavior.

Manifests record input paths relative to their own directory. Scriban sample operation inputs are project-local; shared schemas and base-client/test dependencies can legitimately traverse multiple parent directories. Regenerate through the generator rather than editing manifests or generated source.
