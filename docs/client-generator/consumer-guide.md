# Salep C# Client Generator — Consumer Guide for .NET Developers

This guide provides a comprehensive tutorial for developers consuming the **Salep.ClientGenerator** NuGet package in .NET applications to generate strongly-typed GraphQL clients.

---

## 1. Overview & Key Benefits

**Salep** generates compile-time safe, strongly-typed C# GraphQL clients directly from your `.graphql` schema and operation documents during MSBuild compilation.

### Key Advantages
- **Compile-Time Safety**: Typed C# models for all queries, mutations, subscriptions, and schema types.
- **Zero Runtime Reflection**: Pure System.Text.Json serialization with compile-time generated converters.
- **First-Class Discriminated Unions**: Emits idiomatic Dunet unions by default, or native C# 15 unions when opted in.
- **Zero Salep Runtime Lock-in**: Consuming projects do not reference Salep assemblies at runtime (`PrivateAssets="all"`).
- **Automated Mock Test Generation**: Optionally generates ready-to-run xUnit tests with an in-memory HTTP handler.

---

## 2. Installation and Setup

### Native C# 15 union output

To use compiler-native unions instead of Dunet, set `"unionRepresentation": "native"` in `salep.json`, target `net11.0`, set `<LangVersion>preview</LangVersion>`, and build with the .NET 11 SDK. This option applies to GraphQL unions and generated interface-result unions. See [MinimalDependencies](../../src/samples/MinimalDependencies/README.md) for a sample that reuses the main schema and operations without Dunet or NodaTime.

### 2.1. Add Package References to `.csproj`

In your consumer `.csproj`, add `Salep.ClientGenerator` as a build-time dependency. Add `Dunet` when using the default union representation; omit it for native C# 15 unions:

```xml
<Project Sdk="Microsoft.NET.Sdk">

  <PropertyGroup>
    <TargetFramework>net10.0</TargetFramework>
    <ImplicitUsings>enable</ImplicitUsings>
    <Nullable>enable</Nullable>
  </PropertyGroup>

  <ItemGroup>
    <!-- Salep: Build tool only (PrivateAssets="all") -->
    <PackageReference Include="Salep.ClientGenerator" PrivateAssets="all" />

    <!-- Runtime dependencies used by generated code -->
    <PackageReference Include="Dunet" />
    
    <!-- Optional: If using NodaTime scalar mapping -->
    <PackageReference Include="NodaTime" Condition="'$(UseNodaTime)' == 'true'" />
    <PackageReference Include="NodaTime.Serialization.SystemTextJson" Condition="'$(UseNodaTime)' == 'true'" />
  </ItemGroup>

</Project>
```

*If using Central Package Management (CPM)* in `Directory.Packages.props`:
```xml
<ItemGroup>
  <PackageVersion Include="Salep.ClientGenerator" Version="0.1.0" />
  <PackageVersion Include="Dunet" Version="1.11.2" />
  <PackageVersion Include="NodaTime" Version="3.2.1" />
  <PackageVersion Include="NodaTime.Serialization.SystemTextJson" Version="1.3.0" />
</ItemGroup>
```

---

## 3. Recommended Project Directory Layout

Organize your schema, operations, and configuration alongside your C# code:

```text
MyService/
├── MyService.csproj
├── salep.json                  # Salep configuration
├── schema.graphql              # Backend GraphQL SDL schema
├── graphql/                    # Operation documents (.graphql)
│   ├── fragments.graphql
│   ├── GetUserProfile.graphql
│   └── CreateOrder.graphql
└── Generated/                  # Emitted C# files (auto-included in build)
    ├── SchemaTypes.cs
    ├── Operations.cs
    ├── UnionJsonConverters.cs
    └── GraphQLClient.cs
```

---

## 4. Defining Schema & Operations

### Step 1: Place GraphQL Schema (`schema.graphql`)

```graphql
schema {
  query: Query
  mutation: Mutation
}

type Query {
  user(id: ID!): User
  search(query: String!): [SearchResult!]!
}

type Mutation {
  updateUserEmail(id: ID!, email: String!): User!
}

type User {
  id: ID!
  name: String!
  email: String
  role: UserRole!
}

enum UserRole {
  ADMIN
  MEMBER
  GUEST
}

union SearchResult = User | Organization

type Organization {
  id: ID!
  companyName: String!
}
```

### Step 2: Write Operations (`graphql/operations.graphql`)

```graphql
# Queries with parameters
query GetUser($id: ID!) {
  user(id: $id) {
    id
    name
    email
    role
  }
}

# Queries selecting Unions
query SearchAll($query: String!) {
  search(query: $query) {
    __typename
    ... on User {
      id
      name
    }
    ... on Organization {
      id
      companyName
    }
  }
}

# Mutations
mutation UpdateEmail($id: ID!, $email: String!) {
  updateUserEmail(id: $id, email: $email) {
    id
    email
  }
}
```

---

## 5. Configuration Reference (`salep.json`)

Every configuration requires `version: 1` and an explicit `kind`. Unknown properties, duplicate keys, obsolete options, and wrong-role references fail validation. The [JSON schema](config.schema.json) is also included in the generator package.

```json
{
  "version": 1,
  "kind": "client",
  "schema": "./schema.graphql",
  "operations": "./graphql",
  "namespace": "MyService.GraphQL",
  "clientName": "MyGraphQLClient",
  "output": "./Generated"
}
```

| Role | Properties |
| --- | --- |
| `profile` | Optional `extends` profile, `schema`, shared settings below. No output, namespace, client name, or operations. |
| `client` | Optional `profile`, `baseClient`; `schema` locally or from a profile; `operations` (default `./graphql`), `namespace` (default `Salep.Generated`), `clientName` (default `GraphQLClient`), `output` (default `./Generated`), `emitSample` (default false), shared settings. |
| `tests` | Required `client`; `output` (default `./GeneratedTests`), `namespace` (default client namespace plus `.Tests`), `indentSize`, `rawJsonLiterals` (default true), `suites`. Client settings are forbidden. |

Client and tests roles also accept `emitAgentInstructions` (default true). All relative paths resolve from the file declaring them. A `baseClient` establishes ownership only; consume the same `profile` explicitly to share settings.

| Shared setting | Default | Meaning |
| --- | --- | --- |
| `unionRepresentation` | `dunet` | `dunet` or `native`; native requires .NET 11 and preview C#. |
| `scalarPreset` | `builtin` | `builtin` or `nodatime`; the latter maps DateTime and Instant to NodaTime.Instant. |
| `scalars` | built-in definitions | GraphQL name to complete `{ "type", "isValueType", "sampleExpression"?, "sampleJson"? }` definition. |
| `omitUnusedVariables` | false | Remove GraphQL variables not used in the operation or its fragments. This does not control null serialization. |
| `inlineDefaultVariables` | false | Substitute GraphQL variable defaults into the document. |
| `useHttpGet` | false | Eligible queries use GET; mutations use POST. |
| `maxGetUrlLength` | 2048 | Positive URL-length limit before GET falls back to POST. |
| `enableBatching` | false | Enable sending an array of operations in one POST. |
| `indentSize` | 4 | Spaces per indentation level, from 0 through 16. |

Ordinary settings resolve as defaults → profile chain → client. Scalars resolve as built-ins → selected preset → explicit definitions. Each explicit definition replaces the whole entry, including sample values. `sampleExpression` must be valid C# syntax and `sampleJson` must be a string containing valid JSON; provide both when requested generated samples/tests use the scalar. Consumer compilation checks arbitrary external C# type compatibility.

A separate test project references the generated client project and selects its contract:

```json
{
  "version": 1,
  "kind": "tests",
  "client": "../MyService.Client/salep.json",
  "namespace": "MyService.Client.Tests",
  "suites": ["transport", "operations", "unions"]
}
```

Default suites are `transport`, `operations`, and `unions`; `samples` is added only if the client emits samples. Explicitly requesting unavailable samples is an error. Test generation derives scalar types, union representation, operation signatures, and transport expectations from the verified client contract. Referenced union ownership never suppresses union tests.

Use `salep validate --config salep.json` (or the bundled CLI DLL with `dotnet`) to validate without writing. Generation accepts configuration selection and a working directory, with no schema, output, or behavior overrides. MSBuild selects exactly one configuration, generates after referenced projects build, verifies configured dependencies against the project-reference chain, and includes exact files from the output manifest. `SalepEnabled=false` disables generation.

Generation renders and validates before writing, serializes writes to each output directory, removes only previously owned files, and publishes its manifest last. Missing, stale, incompatible, or modified dependency outputs fail with structured `SALEP` diagnostics; build the authoritative client first. Output directories must not overlap. Keep NuGet dependencies and versions in developer-owned project/package files. No legacy aliases or automatic package editing are supported.

---

## 6. Using the Generated Client in C#

### 6.1. Dependency Injection Setup (ASP.NET Core / Generic Host)

Register the client with `IHttpClientFactory` in `Program.cs`:

```csharp
using MyService.GraphQL;

var builder = WebApplication.CreateBuilder(args);

// Register typed client with HttpClient
builder.Services.AddHttpClient<MyGraphQLClient>(client =>
{
    client.BaseAddress = new Uri("https://api.example.com/graphql");
    client.DefaultRequestHeaders.Add("Authorization", "Bearer token-value");
});

var app = builder.Build();
```

### 6.2. Executing Queries and Mutations

Inject and execute queries with full compile-time safety:

```csharp
public class UserService
{
    private readonly MyGraphQLClient _client;

    public UserService(MyGraphQLClient client)
    {
        _client = client;
    }

    public async Task<User?> GetUserAsync(string userId, CancellationToken ct = default)
    {
        // Parameter types are strongly typed records
        var response = await _client.GetUserAsync(new() { Id = userId }, ct);

        // Check for GraphQL errors
        if (response.Errors is { Count: > 0 })
        {
            var errorMsg = string.Join(", ", response.Errors.Select(e => e.Message));
            throw new InvalidOperationException($"GraphQL Error: {errorMsg}");
        }

        return response.Data?.User;
    }
}
```

### 6.3. Exhaustive Pattern Matching on Unions (Dunet)

When querying unions or interfaces, Salep generates Dunet discriminated unions:

```csharp
var searchResult = await _client.SearchAllAsync(new() { Query = "Acme" });

foreach (var item in searchResult.Data.Search)
{
    // Match requires handling all union branches exhaustively
    string summary = item.Match(
        user => $"Found user: {user.Name} (Role: {user.Role})",
        org => $"Found organization: {org.CompanyName}"
    );

    Console.WriteLine(summary);
}
```

---

## 7. Automated Unit Testing with Mock HTTP Handler

In a separate `kind: "tests"` project, Salep generates xUnit test suites and an in-memory `TestHttpMessageHandler` to test your application without network dependencies:

```csharp
// Example using generated TestHttpMessageHandler
var handler = new TestHttpMessageHandler();

// Queue an expected response
handler.EnqueueResponse(new GetUserQueryResponse
{
    Data = new()
    {
        User = new() { Id = "123", Name = "Alice", Role = UserRole.ADMIN }
    }
});

var httpClient = new HttpClient(handler) { BaseAddress = new Uri("http://mock/graphql") };
var client = new MyGraphQLClient(httpClient);

var result = await client.GetUserAsync(new() { Id = "123" });
Assert.Equal("Alice", result.Data.User.Name);
```

---

## 8. Multi-project clients

Use a shared profile for defaults and `baseClient` to reuse generated types. Add the matching `ProjectReference` and build the base client first. See [ownership and profiles](config-inheritance-dedup.md).

## 9. Troubleshooting & Common Pitfalls

| Issue | Cause | Solution |
| :--- | :--- | :--- |
| **CS0246: The type or namespace 'UnionAttribute' could not be found** | Missing `Dunet` package | Add `<PackageReference Include="Dunet" />` to consumer `.csproj`. |
| **Generated files not compiling on first build** | MSBuild restore ordering | Build once with `dotnet build` to trigger `CoreCompile` after referenced projects build code emission. |
| **CS0433: Type exists in both assemblies** in multi-project setup | Duplicate type generation across projects | Set `"baseClient": "../Parent/salep.json"` in child project to enable ancestor ownership dedup. |
| **GraphQL schema syntax errors** | Invalid SDL in `schema.graphql` | Validate schema syntax against GraphQL specification. |
| **Incremental build not updating** | Output marker timestamp mismatch | Run `dotnet clean` or `dotnet build-server shutdown` and rebuild. |

## Response names and selections

Generated properties and enum members retain their exact GraphQL JSON names, including underscores. Root fields selected through named or inline fragments are included once, with repeated selections merged.

Selections containing nested aliases use operation-specific response records so each alias is available as a property; ordinary selections retain the shared schema types. Aliased union/interface selections use concrete response records and a generated converter keyed by `__typename`. The generator adds an unconditional, unaliased `__typename` to abstract selections, including selections inside named fragments.

## Generator warnings for schema metadata and custom scalars

Directive definitions describe server behavior and do not require generated C# declarations. Their presence does not produce a warning; the client generator does not implement server-side directive execution.

An unmapped custom scalar produces a warning when referenced by an object/interface field, field argument, input field, or operation variable, including nested list and non-null types. Scalars declared only as schema metadata (including directive-definition arguments) do not produce C# members and do not warn. This check covers schema contracts even when a particular operation does not select those fields.

For a used custom scalar, provide a complete entry in `scalars` with `type`, `isValueType`, and appropriate `sampleExpression` and `sampleJson` values. An unmapped scalar still falls back to `string`; do not add an arbitrary mapping solely to silence the warning.

### Generated C# validation

Salep validates every emitted C# file with Roslyn before returning it to the output writer. Malformed syntax fails generation with compiler diagnostic IDs and source locations. Check custom namespace/client names, scalar type mappings, and scalar sample expressions when a diagnostic points to configured code. Roslyn runs only in the build tool; generated applications do not gain a Roslyn dependency. Normal consumer compilation still checks type references and required packages.

## Dependency ownership

Salep generation writes generated artifacts only; it never installs packages or edits project files or `Directory.Packages.props`. Developers own runtime and test package references and their versions. When enabling `scalarPreset: "nodatime"` or custom `NodaTime.*` scalar mappings, add `NodaTime` and `NodaTime.Serialization.SystemTextJson` to each consuming project that needs them. With Central Package Management, declare versionless `PackageReference` items in the project and versions in `Directory.Packages.props`, as shown above.
