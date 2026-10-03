# Salep.ClientGenerator

Build-time GraphQL C# client generation using Scriban templates.

Add the package to a project that owns the generated client:

```xml
<PackageReference Include="Salep.ClientGenerator" Version="0.1.0" PrivateAssets="all" />
```

Add a version-1 `salep.json` file with `kind` set to `client` or `tests`. The package imports its MSBuild targets automatically and runs generation before compilation. Generated application output has no Salep or Scriban runtime dependency.

Consumers can replace selected methods/properties or add members through empty extension hooks, as well as replace whole embedded templates by adding file paths to the `templates` object in the configuration. Paths are relative to that configuration, and omitted entries keep using the package's embedded templates:

```json
{
  "version": 1,
  "kind": "client",
  "schema": "schema.graphql",
  "operations": "graphql",
  "namespace": "Example.Api",
  "output": "Generated",
  "templates": {
    "client.members": "templates/ClientMembers.scriban-cs",
    "operations.contract-members": "templates/ContractMembers.scriban-cs"
  }
}
```

Export the embedded defaults from the CLI bundled in the NuGet package, then edit the files you want to override:

```bash
dotnet exec "$HOME/.nuget/packages/salep.clientgenerator/0.1.0/tools/net10.0/any/Salep.ClientGenerator.Cli.dll" \
  templates --output-directory templates/salep
```

Use the `net11.0` tool for .NET 11, or query the restored consumer project with `dotnet msbuild MyService.csproj -getProperty:SalepToolPath -p:TargetFramework=net10.0` to locate the actual package cache. Available keys, model scopes, Bash/PowerShell commands and executable examples are in the [consumer customization guide](https://github.com/alpercelik/salep/blob/main/docs/client-generator/template-customization.md). Validate before building by invoking the same DLL with `validate --config salep.json`.

This is the default Scriban C# generator. Scriban is the sole stable implementation.

## Upgrading from the Roslyn implementation

Keep the `Salep.ClientGenerator` package reference and version-1 `salep.json` configuration. Regenerate base clients before modules and generated-test projects. The default tool verifies the previous Roslyn version-1 ownership manifest and rewrites its owned files with Scriban output; unrelated consumer files remain untouched. Tampered manifests and mismatched owners still fail. Previous Scriban configurations may keep their explicit filenames; set `SalepConfigFile` accordingly. The former `.salep-scriban.manifest.json` is upgraded to `.salep.manifest.json` during generation.


For example, `ClientMembers.scriban-cs` can contain `public string ConsumerName => {{ target.string_literal settings.client_name }};`. That adds one member while the default client methods stay intact. The package exports 73 templates. Named includes compose embedded defaults and configured fragments; `include "default:client.read-response"` reuses a default response method without copying it. See [the complete fragment and model contract](https://github.com/alpercelik/salep/blob/main/docs/client-generator/template-customization.md).

Default operation queries are formatted multiline C# raw strings with native generation-host line endings. Constructor, transport, model and operation hooks add source without replacing whole files; profiles can share mappings and tests can specialize inherited mappings. Use a package revision containing the composable template catalog; older packages may support only whole-output overrides.
