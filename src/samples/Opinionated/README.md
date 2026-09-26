# Salep Sample Integration & Specification Showcase

This directory contains the canonical reference implementation, specification showcase, and integration test suite for **Salep**. It serves two primary purposes:

The minimal dependency project set is in [MinimalDependencies](../MinimalDependencies/README.md). It reuses this sample's operations and the schema exported by [GraphQLServer](../Salep.Samples.GraphQLServer/README.md), omits Dunet and NodaTime, and enables native unions for .NET 11.

1. **Monorepo Dogfooding & Integration Testing**: Exercises the end-to-end packaging, MSBuild code generation targets, and CLI tooling under realistic multi-project consumer conditions.
2. **GraphQL Specification Showcase**: Demonstrates comprehensive GraphQL SDL and executable document features, illustrating how Salep translates advanced schema constructs into clean, idiomatic, and type-safe C# 14 code for .NET 10 and .NET 11.

## GraphQL integration tests

`Salep.Samples.Opinionated.IntegrationTests` uses Alba to start the Hot Chocolate server in memory and exercises every generated query and mutation from both the base and module clients:

```bash
dotnet run --project src/samples/Opinionated/Salep.Samples.Opinionated.IntegrationTests/Salep.Samples.Opinionated.IntegrationTests.csproj --framework net11.0
```

The generated client in this sample includes Dunet and NodaTime integrations.

---

## 1. Project Organization & Architecture

The main dogfooding suite contains five interconnected projects demonstrating a multi-tier client architecture, configuration inheritance, generated tests, and server integration.

```
src/samples/Opinionated/
├── README.md                           # This documentation
├── Directory.Build.props               # Sample-scoped MSBuild configuration
├── Directory.Packages.props            # Sample-scoped Central Package Management
├── Salep.Samples.Opinionated.Client/                    # Tier 1: Base client & shared schema types
│   ├── base.config.json                # Shared configuration (NodaTime, scalars, HTTP options)
│   ├── salep.json                      # Project generator config (consumes base.config.json as a profile)
│   ├── schema.coverage.graphql         # Comprehensive GraphQL coverage fixture
│   ├── graphql/                        # Base GraphQL operations
│   │   ├── fragments.graphql           # Reusable fragment definitions
│   │   ├── query.graphql               # Query operations (aliases, inline fragments, defaults)
│   │   ├── mutation.graphql            # Mutation operations (inputs, aliases)
│   │   └── subscription.graphql        # Subscription operations
│   ├── Generated/                      # Output directory for generated C# code
│   │   ├── SchemaTypes.cs              # C# records for schema types, enums, inputs, interfaces
│   │   ├── UnionJsonConverters.cs      # System.Text.Json converters for Dunet unions
│   │   ├── GraphQLSharedTypes.cs       # Core operation interfaces and response wrappers
│   │   ├── Operations.cs               # Typed operation requests and response records
│   │   ├── GraphQLClient.cs            # Strongly-typed HTTP client (GET, POST, batching)
│   │   └── .salep.manifest.json        # Ownership manifest tracking generated artifacts
│   └── Salep.Samples.Opinionated.Client.csproj          # Consumes Salep via NuGet PackageReference
│
├── Salep.Samples.Opinionated.Module/             # Tier 2: Modular extension client
│   ├── salep.json                      # Config extending base.config.json with type deduplication
│   ├── graphql/                        # Module-specific GraphQL operations
│   │   ├── fragments.graphql           # Local fragment definitions
│   │   ├── query.graphql               # Module query operations
│   │   ├── mutation.graphql            # Module mutation operations
│   │   └── subscription.graphql        # Module subscription operations
│   ├── Generated/                      # Output directory for modular client
│   │   ├── Operations.cs               # Module-specific operations only
│   │   ├── GraphQLClient.cs            # GeneratedModuleClient implementation
│   │   ├── GraphQLSharedTypes.cs       # Shared client contracts
│   │   └── .salep.manifest.json        # Module ownership manifest
│   └── Salep.Samples.Opinionated.Module.csproj   # References Salep.Samples.Opinionated.Client.csproj & Salep NuGet
│
├── Salep.Samples.Opinionated.Client.Tests/              # Tier 1 Test Suite
│   ├── salep.json                      # Config configured with kind: "tests"
│   ├── GeneratedTests/                 # Auto-generated xUnit test suite
│   │   ├── OperationsMetadataTests.cs  # Asserts operation queries, names, variables
│   │   ├── OperationsResponseTests.cs  # Asserts response JSON deserialization
│   │   ├── GraphQLClientPayloadTests.cs# Asserts HTTP payload construction & transport
│   │   ├── UnionConverterTests.cs      # Asserts Dunet union deserialization with __typename
│   │   └── TestHttpMessageHandler.cs   # In-memory mock HTTP handler for isolated tests
│   └── Salep.Samples.Opinionated.Client.Tests.csproj    # xUnit test project
│
├── Salep.Samples.Opinionated.Module.Tests/       # Tier 2 generated test suite
    ├── salep.json                      # Test generator config extending GeneratedClient.Module
    ├── GeneratedTests/                 # Auto-generated module tests
    │   ├── OperationsMetadataTests.cs  # Validates module operations
    │   ├── OperationsResponseTests.cs  # Validates module response models
    │   ├── GraphQLClientPayloadTests.cs# Validates module client execution
    │   └── TestHttpMessageHandler.cs   # Reusable test HTTP handler
    └── Salep.Samples.Opinionated.Module.Tests.csproj # xUnit test project

└── Salep.Samples.Opinionated.IntegrationTests/   # Live generated-client tests against Hot Chocolate
    ├── GraphQLIntegrationTests.cs      # Exercises every query and mutation using Alba
    └── Salep.Samples.Opinionated.IntegrationTests.csproj
```

---

## 2. Multi-Project Architecture & Deduplication

A critical enterprise feature of Salep is **transitive configuration inheritance and artifact deduplication** across multi-project solutions. The sample projects demonstrate how this operates in practice:

### 2.1. Shared Configuration (`base.config.json`)
Located in `Salep.Samples.Opinionated.Client/base.config.json`, this file centralizes organization-wide generator conventions:
- **Schema Location**: Points to `src/samples/Salep.Samples.GraphQLServer/Generated/schema.graphql`, generated from the Hot Chocolate executable schema during the server build. The solution and bootstrap scripts build the server first.
- **NodaTime Integration**: `scalarPreset: "nodatime"` with scalar mappings for `DateTime` and `Instant` to `NodaTime.Instant`.
- **HTTP Transport Options**: Enables HTTP GET caching (`useHttpGet: true`, `maxGetUrlLength: 2048`) and request batching (`enableBatching: true`).
- **Formatting Standards**: Configures raw string literals (`useRawStrings: true`), indentation, and line wrapping.

### 2.2. Base Client (`Salep.Samples.Opinionated.Client/salep.json`)
Consumes `base.config.json` via `"profile": "./base.config.json"`. It emits the complete schema type universe (`SchemaTypes.cs`), union converters (`UnionJsonConverters.cs`), operation models (`Operations.cs`), and the primary `GraphQLClient.cs`. It tracks everything it produces in `Generated/.salep.manifest.json`.

### 2.3. Modular Extension Client (`Salep.Samples.Opinionated.Module/salep.json`)
Demonstrates an independent downstream service or feature module referencing the base client:
- **Configuration Inheritance**: Extends `"../Salep.Samples.Opinionated.Client/base.config.json"`.
- **Type Suppression**: Sets `"generateSchemaTypes": false` and `"generateUnionConverters": false`.
- **Ownership Deduplication**: Because `Salep.Samples.Opinionated.Module.csproj` has a `<ProjectReference Include="..\Salep.Samples.Opinionated.Client\Salep.Samples.Opinionated.Client.csproj" />`, Salep consults the ancestor manifest (`.salep.manifest.json`). Any schema types or shared records already generated by `Salep.Samples.Opinionated.Client` are suppressed, preventing duplicate C# symbol collisions while generating only the new operations and `GeneratedModuleClient`.

---

## 3. Sample Schema Feature Coverage (`schema.coverage.graphql`)

The schema coverage fixture (`src/samples/Opinionated/Salep.Samples.Opinionated.Client/schema.coverage.graphql`) is designed as a specification testbed exercising all major constructs of the GraphQL Specification. It is not the schema input used by the client generator:

### 3.1. Schema Definition & Extensions
- **Root Operation Types**: Declares `query: Query`, `mutation: Mutation`, and `subscription: Subscription`.
- **Schema Directives**: Annotates the schema root with `@schemaTag(name: "base")`.
- **Schema Extensions**: Uses `extend schema @schemaTag(name: "extended")` to demonstrate AST schema extension handling.

### 3.2. Types, Extensions & Interfaces
- **Object Types**: Concrete types `User`, `Post`, `Comment` implementing multiple interfaces with nullable and non-nullable fields.
- **Type Extensions**:
  - `extend type User` adds `nickname: String` and `email: String @auth(role: ADMIN)`.
  - `extend type Post` adds `metadata: Json`.
- **Interface Inheritance**:
  - `Node` defines `id: ID!`.
  - `Named` defines `name: String!`.
  - `Timestamped implements Node` inherits `Node` and adds `createdAt: DateTime!`.
  - `Account implements Node & Named` demonstrates multiple interface implementation.
- **Interface Extensions**: `extend interface Named` adds `legacyName: String @deprecated(reason: "Use name")`.

### 3.3. Discriminated Unions & Extensions
- **Union Definitions**: `union SearchResult = User | Post`.
- **Union Extensions**: `extend union SearchResult = Comment`.
- **C# Mapping**: Salep generates compile-time safe discriminated unions using **Dunet**, accompanied by custom `System.Text.Json` converters reading `__typename` to instantiate the appropriate union branch.

### 3.4. Enums & Extensions
- **Enum Definitions**: `enum Role { ADMIN, USER, GUEST @deprecated(...) }`.
- **Enum Extensions**: `extend enum Role { SUPERADMIN @tag(name: "enumValue") }`.
- **C# Mapping**: Emits standard C# enums configured with `JsonStringEnumConverter` for string-based JSON serialization.

### 3.5. Custom Scalars & `@specifiedBy`
- **Scalar Types**: `DateTime`, `Json`, and `BigInt`.
- **Specification URLs**: `@specifiedBy(url: "https://example.com/datetime")`.
- **Scalar Extensions**: `extend scalar BigInt @tag(name: "extendedScalar")`.
- **CLR Mappings**: `DateTime` is mapped to `NodaTime.Instant` via `base.config.json`, while `Json` maps to `System.Text.Json.Nodes.JsonNode` or string equivalents.

### 3.6. Input Objects & Default Values
- **Input Object Types**: `MetadataInput`, `CreatePostInput`, `SearchInput`, `UserFilter`.
- **Input Extensions**: `extend input CreatePostInput { notify: Boolean = true }`.
- **Nested Inputs & Defaults**: `CreatePostInput` includes default values for scalars (`content: String = "default"`), lists (`tags: [String!] = ["a", "b"]`), and nested objects (`meta: MetadataInput = { rating: 4, flags: ["m"] }`).
- **Field Argument Defaults**: Arguments on `Query.users` (`role: Role = USER, filter: UserFilter = { roles: [USER] }`) and `Mutation.deletePosts` (`ids: [ID!]! = ["1"]`).

### 3.7. Directive Definitions & Locations
The schema defines custom directives exercising valid directive locations:
- `@schemaTag(name: String!) repeatable on SCHEMA`
- `@tag(name: String!) repeatable on SCHEMA | SCALAR | OBJECT | INTERFACE | UNION | ENUM | ENUM_VALUE | FIELD_DEFINITION | ARGUMENT_DEFINITION | INPUT_OBJECT | INPUT_FIELD_DEFINITION`
- `@upper on FIELD_DEFINITION`
- `@auth(role: Role = USER) on OBJECT | FIELD_DEFINITION`
- `@opTag(name: String = "op") repeatable on QUERY | MUTATION | SUBSCRIPTION | FIELD | FRAGMENT_DEFINITION | FRAGMENT_SPREAD | INLINE_FRAGMENT`

---

## 4. GraphQL Operations Feature Coverage

The operations located in `src/samples/Opinionated/Salep.Samples.Opinionated.Client/graphql/` exercise executable document capabilities:

### 4.1. Queries (`query.graphql`)
- **Variables & Defaults**: `UsersByRole` defines typed variables with defaults (`$role: Role = ADMIN, $ids: [ID!] = ["1"]`).
- **Field Aliasing**: Aliasing query root fields to disambiguate multiple invocations (`admins: users(...)`, `guests: users(...)`).
- **Directives on Operations & Fields**: Uses `@opTag` on queries, fields, and fragment spreads, as well as `@skip(if: false)` and `@include(if: true)`.
- **Inline Fragments & Discrimination**:
  - `SearchUsersAndPosts`: Queries union `SearchResult` requesting `__typename` and branching via `... on User`, `... on Post`, and `... on Comment`.
  - `NodeById`: Queries interface `Node` requesting concrete implementations via inline fragments.
  - `SearchWithMultipleInlineFragments`: Demonstrates multiple inline fragments matching the same type with differing field sets (`... on User { ...UserCore }` and `... on User @opTag { posts { ... } }`).
- **Unused Variable Handling**: `UnusedVar` verifies that generator variable pruning correctly handles unused operation parameters.

### 4.2. Mutations (`mutation.graphql`)
- **Input Object Construction**: `CreatePostSimple` and `CreatePostWithContent` construct complex input variables from operation arguments.
- **Aliased Mutations**: `CreatePostAliased` executes multiple mutations in a single request with aliases (`first: createPost(...)`, `second: createPost(...)`).
- **Complex Default Variables**: `UpdatePostWithDefaults` uses nested input object defaults in operation variable declarations.

### 4.3. Subscriptions (`subscription.graphql`)
- **Subscription Payloads**: `PostCreatedFull`, `PostCreatedMinimal`, and `CommentAdded` demonstrate streaming operation models for real-time GraphQL events.

### 4.4. Named Fragments (`fragments.graphql`)
- **Cross-File Fragment Spreads**: Fragments defined in `fragments.graphql` (`UserCore`, `PostCore`, `CommentCore`) are spread across operations in `query.graphql`, `mutation.graphql`, and `subscription.graphql`.
- **Nested Fragments**: `PostCore` spreads `UserCore` inside its `author` selection set.

---

## 5. Generated Client Features & Runtime Execution

The generated C# artifacts in `Generated/` provide a high-performance client runtime:

### 5.1. Discriminated Unions with Dunet
GraphQL unions and interfaces are emitted as Dunet discriminated unions:
```csharp
// Exhaustive compile-time pattern matching over union results:
var resultText = searchResult.Match(
    user => $"User: {user.Value.Name}",
    post => $"Post: {post.Value.Title}",
    comment => $"Comment: {comment.Value.Body}"
);
```
Serialization is handled by `UnionJsonConverters.cs`, which reads the `__typename` JSON property emitted by the GraphQL server.

### 5.2. Transport Execution Strategies
The generated `GraphQLClient` supports multiple execution strategies:
- **HTTP GET Queries**: When `_useHttpGet = true`, query operations are automatically encoded into URL query strings (`?query=...&operationName=...&variables=...`) to leverage HTTP caching intermediaries, provided the URL length does not exceed `maxGetUrlLength` (2048 characters).
- **HTTP POST Fallback**: Queries exceeding the length threshold, mutations, and requests with complex variables execute via HTTP POST with `application/json`.
- **Batching**: `ExecuteBatchAsync` transmits multiple operations in a single HTTP request as a JSON array payload.
- **Incremental / Multipart Streaming**: `ExecuteIncrementalAsync` consumes `multipart/mixed` streaming responses using `IAsyncEnumerable<GraphQLResponse<TResponse>>`.

---

## 6. Automated Testing with `TestHttpMessageHandler`

The test projects (`Salep.Samples.Opinionated.Client.Tests` and `Salep.Samples.Opinionated.Module.Tests`) are generated automatically when `kind: "tests"` is configured in `salep.json`:

- **Isolated In-Memory Testing**: Includes `TestHttpMessageHandler.cs`, allowing the generated client to be tested without spinning up an external HTTP server or network connection.
- **Payload Verification**: `GraphQLClientPayloadTests.cs` asserts that HTTP methods, headers, operation names, and serialized JSON payloads conform to GraphQL-over-HTTP specifications.
- **Model Verification**: `OperationsResponseTests.cs` and `OperationsMetadataTests.cs` assert round-trip deserialization, ensuring nullability and scalar mappings behave as expected.
- **Union Verification**: `UnionConverterTests.cs` exercises serialization and deserialization of all union cases.

---

## 7. How to Build and Run the Sample

### Full Monorepo Dogfooding Verification
To build the Salep toolchain, pack the NuGet package locally, restore the sample projects against the local feed, and run all unit/sample tests:

**Linux / macOS / Git Bash:**
```bash
./build-salep.sh
```

**Windows PowerShell:**
```powershell
pwsh ./build-salep.ps1
```

### Manual Code Regeneration via CLI
To regenerate code for the sample projects using the standalone CLI host:

```bash
# Regenerate Tier 1 (Base Client)
dotnet run --project src/Salep.ClientGenerator.Cli/Salep.ClientGenerator.Cli.csproj -- \
  --config src/samples/Opinionated/Salep.Samples.Opinionated.Client/salep.json

# Regenerate Tier 2 (Modular Client)
dotnet run --project src/Salep.ClientGenerator.Cli/Salep.ClientGenerator.Cli.csproj -- \
  --config src/samples/Opinionated/Salep.Samples.Opinionated.Module/salep.json

# Regenerate Test Suites
dotnet run --project src/Salep.ClientGenerator.Cli/Salep.ClientGenerator.Cli.csproj -- \
  --config src/samples/Opinionated/Salep.Samples.Opinionated.Client.Tests/salep.json

dotnet run --project src/Salep.ClientGenerator.Cli/Salep.ClientGenerator.Cli.csproj -- \
  --config src/samples/Opinionated/Salep.Samples.Opinionated.Module.Tests/salep.json
```

### Direct Project Build
The projects hook into Salep's MSBuild targets automatically. When building via standard .NET CLI, code generation triggers incrementally:

```bash
dotnet build src/samples/Opinionated/Salep.Samples.Opinionated.Client/Salep.Samples.Opinionated.Client.csproj -p:EnforceCodeStyleInBuild=false -p:TreatWarningsAsErrors=false
dotnet run --project src/samples/Opinionated/Salep.Samples.Opinionated.Client.Tests/Salep.Samples.Opinionated.Client.Tests.csproj
```
