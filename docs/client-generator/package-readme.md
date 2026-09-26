# Salep C# Client Generator

Generate typed C# GraphQL clients from schema and operation files during MSBuild. `Salep.ClientGenerator` is a build-only package: the generated application has no runtime dependency on Salep, its parser, or Roslyn.

## Requirements

Target `net10.0` or `net11.0` and use an SDK/runtime supporting that target. The .NET 11 target currently requires the .NET 11 preview/RC toolchain. The package bundles managed CLI hosts for both targets and works with `dotnet exec` on Windows, Linux, and macOS.

## Install

```sh
dotnet add package Salep.ClientGenerator --version 0.1.0-preview.1
```

Keep the reference private in your project:

```xml
<PackageReference Include="Salep.ClientGenerator" Version="0.1.0-preview.1" PrivateAssets="all" />
```

For Central Package Management, put the version in `Directory.Packages.props` and omit `Version` from the project reference.

## Generate a client

Create `schema.graphql`:

```graphql
type Query { greeting: String! }
```

Create `graphql/Greeting.graphql`:

```graphql
query Greeting { greeting }
```

Create `salep.json` alongside your project:

```json
{
  "version": 1,
  "kind": "client",
  "schema": "./schema.graphql",
  "operations": "./graphql",
  "output": "./Generated",
  "namespace": "MyApp.GraphQL",
  "clientName": "ApiClient",
  "scalarPreset": "builtin"
}
```

Run `dotnet build`. The package generates and includes C# sources automatically. Update the schema, operations, or configuration to change the output.

Generation never edits project files or installs packages. Manage runtime dependencies in your project and, if enabled, Central Package Management.

Schemas with unions or interfaces use Dunet by default; add that runtime dependency when needed. Native C# union output is available with `unionRepresentation: "native"` on .NET 11. NodaTime is optional; disabling it uses built-in date/time types. Generated test projects require their documented test dependencies.

`Salep.GraphQLParser` is a separate package for applications that need GraphQL lexing, parsing, syntax trees, and language utilities directly.

See the [consumer guide](https://github.com/alpercelik/salep/blob/main/docs/client-generator/consumer-guide.md) and [samples](https://github.com/alpercelik/salep/tree/main/Samples) for configuration, custom scalars, serialization, and multi-project generation. Private dependency licenses and notices are included in this package.
