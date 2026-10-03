# Runnable consumer template customization examples

This directory is a small GraphQL client fixture. Copy it to a scratch directory before generating so output is kept outside the repository documentation. Its checked-in inputs are also used by `TemplateDocumentationExamplesTests` to compile generated clients and execute requests.

Use a `Salep.ClientGenerator` package built from this revision or a release containing the 73-template catalog. The managed CLI is bundled under `tools/net10.0/any/Salep.ClientGenerator.Cli.dll` or `tools/net11.0/any/Salep.ClientGenerator.Cli.dll`. Find your restored package using `dotnet nuget locals global-packages --list`, or query `SalepToolPath` on a restored consumer project as shown in [the customization guide](../../template-customization.md#export-defaults-from-the-installed-package).

Bash, from your scratch copy of this directory:

```bash
salep_cli='/path/to/salep.clientgenerator/<version>/tools/net10.0/any/Salep.ClientGenerator.Cli.dll'
dotnet exec "$salep_cli" validate --config salep.json --target-framework net10.0
dotnet exec "$salep_cli" generate --config salep.json --target-framework net10.0
dotnet exec "$salep_cli" generate --config salep.tests.json --target-framework net10.0 --reference-config salep.json
dotnet exec "$salep_cli" generate --config salep.wrapper.json --target-framework net10.0
dotnet exec "$salep_cli" generate --config salep.escaped-query.json --target-framework net10.0
```

PowerShell:

```powershell
$salepCli = 'C:\path\to\salep.clientgenerator\<version>\tools\net10.0\any\Salep.ClientGenerator.Cli.dll'
dotnet exec $salepCli validate --config salep.json --target-framework net10.0
dotnet exec $salepCli generate --config salep.json --target-framework net10.0
dotnet exec $salepCli generate --config salep.tests.json --target-framework net10.0 --reference-config salep.json
dotnet exec $salepCli generate --config salep.wrapper.json --target-framework net10.0
dotnet exec $salepCli generate --config salep.escaped-query.json --target-framework net10.0
```

Replace the placeholder DLL path with the actual package path. Standalone test generation supplies `--reference-config salep.json` explicitly; MSBuild normally obtains that contract from the project-reference chain. Select the `net11.0` tool and framework to use .NET 11 instead. These fixtures contain no unions and require no additional union/scalar packages. To compile the emitted clients, include the appropriate generated directory in a .NET 10 or .NET 11 C# project. Generated tests additionally need xUnit; keep package references in your own project. The fixture itself is a set of generator inputs, not a .NET project.

| File | Result |
| --- | --- |
| [salep.json](salep.json) | Writes `Generated/` in `Example.Api`. Selects the shared profile and replaces its `client.members` mapping locally; `ConsumerName` returns `ExampleClient`. |
| [profile/templates.json](profile/templates.json) | Supplies relative paths for client, transport, model and operation fragments. |
| [salep.wrapper.json](salep.wrapper.json) | Writes `WrappedGenerated/` in `Example.Wrapped`. Keeps the profile member (`shared-profile`), owns the client file header and wraps the embedded response reader. |
| [salep.escaped-query.json](salep.escaped-query.json) | Writes `EscapedGenerated/` in `Example.Escaped`. Replaces the operation contract to use an escaped literal instead of the raw default; the runtime query is identical. |
| [salep.tests.json](salep.tests.json) | Writes `GeneratedTests/` after the `Example.Api` client exists. Adds a comment around each default operation-metadata test case. |

Inspect `Generated/GraphQLClient.cs`, `Generated/SchemaTypes.cs` and `Generated/Operations.cs` to see the member/header/model/query customizations. Change `templates/local-client-members.scriban-cs`, rerun generation, and inspect the new member. When these files are part of a real NuGet consumer, MSBuild tracks the configured fragment and regenerates at the next build. Removing a mapping restores that fragment's embedded default while preserving the remaining profile mappings.

`operations.contract` delegates to its embedded default, which still selects your `operations.contract-members` override. `schema.object-property` also delegates to its default, retaining the generated property type and JSON name. The object metadata member uses `JsonIgnore`, so it does not add a GraphQL JSON field.

The `.salep.manifest.json` files use paths relative to their own output directories. For example, `Generated/.salep.manifest.json` refers to `../graphql/query.graphql` and `../templates/local-client-members.scriban-cs`. Profiles do not rebase their own paths onto the child config.
