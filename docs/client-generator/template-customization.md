# Customize generated code through Scriban

`Salep.ClientGenerator` generates C# through 73 embedded Scriban templates: fourteen whole-output templates, plus declarations, methods, properties, test cases and empty extension hooks. Add a `templates` mapping to your existing version-1 `salep.json` and override only the pieces you need. Unspecified keys retain their defaults.

These features describe the package built from this revision. Use a release containing composable templates or a package built from this checkout; installing an older published version does not add the new fragment keys. The package ID is `Salep.ClientGenerator`. The former `Salep.ClientGenerator.Scriban` ID is no longer produced, and Scriban is the sole stable generator.

## Choose the smallest customization

| Goal | Recommended key or option |
| --- | --- |
| Add a client member | `client.members` |
| Initialize a header or serializer setting | `client.constructor-body` |
| Add checks before sending or inspect a response | `client.before-send`, `client.after-response` |
| Decorate a schema declaration | `schema.object-annotations`, `schema.input-annotations`, `schema.interface-annotations`, `schema.enum-annotations` |
| Add a model member | `schema.object-members`, `schema.input-members`, `operations.response-object-members` |
| Customize one property | `schema.object-property`, `schema.input-property`, `operations.variables-property`, `operations.response-object-property` |
| Add operation metadata | `operations.contract-members` |
| Change the operation contract or query literal | `operations.contract` |
| Replace one client method | `client.read-response`, `client.execute`, `client.post` or another method fragment |
| Customize one generated test case | `tests.operation-metadata-case`, `tests.operation-response-case` or a `tests.transport-*` key |
| Change a complete file layout or header | A whole-output key such as `client`, `schema` or `operations` |
| Change scalar types, unions, operation normalization or transport switches | Existing configuration options; see [the consumer guide](consumer-guide.md) |

Empty hooks add code at fixed locations. A replacement fragment owns the declaration, method or property it replaces. A whole-output template owns the complete file, including its header. The [complete key list](#fragment-and-extension-hook-keys) below identifies each default and its scope.

## Start with one hook

In the project that references `Salep.ClientGenerator` with `PrivateAssets="all"`, create this `salep.json` alongside the schema and local GraphQL documents:

```json
{
  "version": 1,
  "kind": "client",
  "schema": "schema.graphql",
  "operations": "graphql",
  "output": "Generated",
  "namespace": "Example.Api",
  "clientName": "ExampleClient",
  "templates": {
    "client.members": "templates/client-members.scriban-cs"
  }
}
```

Create `templates/client-members.scriban-cs`:

```scriban
    public string ConsumerName => {{ target.string_literal settings.client_name }};
```

Run `dotnet build`. The generated client now exposes `ConsumerName`, whose value is `ExampleClient`, while the other members come from the defaults. Template output becomes ordinary compiled C#; generated applications need no Scriban or Salep runtime reference. Remove the mapping to restore the default at the next build. Do not hand-edit generated files or manifests.

## Export defaults from the installed package

Run these commands from the consumer project directory after adding the package. The bundled CLI is a managed DLL, not a separately installed global tool. `SalepToolPath` locates the actual restored package, including custom NuGet cache locations.

Bash:

```bash
dotnet restore MyService.csproj
salep_tool_dir="$(dotnet msbuild MyService.csproj -getProperty:SalepToolPath -p:TargetFramework=net10.0)"
dotnet exec "$salep_tool_dir/Salep.ClientGenerator.Cli.dll" templates --output-directory templates/salep
dotnet exec "$salep_tool_dir/Salep.ClientGenerator.Cli.dll" validate --config salep.json --target-framework net10.0
```

PowerShell:

```powershell
dotnet restore MyService.csproj
$salepToolDir = (dotnet msbuild MyService.csproj -getProperty:SalepToolPath -p:TargetFramework=net10.0).Trim()
$salepCli = Join-Path $salepToolDir 'Salep.ClientGenerator.Cli.dll'
dotnet exec $salepCli templates --output-directory templates/salep
dotnet exec $salepCli validate --config salep.json --target-framework net10.0
```

Select `net11.0` for a .NET 11 consumer. Native unions additionally require `--language-version preview` for standalone validation/generation; the MSBuild integration forwards the project's language version. The package's CLI resides under `tools/<target-framework>/any/Salep.ClientGenerator.Cli.dll`. A direct `dotnet exec` with that DLL is equivalent to the `salep` command shorthand used elsewhere in the documentation.

Export writes all 73 files, including empty hooks. Editing an exported file changes nothing until you map its key in `salep.json`. Keep only the files you maintain, and export to a separate directory when inspecting a newer package so you do not overwrite your customizations.

## Add constructor and transport behavior

Map the following files to `client.constructor-body`, `client.before-send` and `client.after-response` respectively.

Constructor hook, after the default serializer initialization:

```scriban
        _httpClient.DefaultRequestHeaders.Add("X-Application", "my-app");
```

Before each GET/POST send, including batching and incremental transport:

```scriban
        cancellationToken.ThrowIfCancellationRequested();
```

After a response arrives, before status validation:

```scriban
        global::System.Diagnostics.Debug.WriteLine(response.StatusCode);
```

`cancellationToken` and `response` here are C# locals available at those insertion points. They are emitted literally, not evaluated by Scriban. Other request locals vary between GET, POST, batch and incremental paths; use the exported method to check scope before referring to them. Avoid creating a `request` variable dependency in a hook used by all send paths. A shared `HttpClient` retains constructor-added default headers; choose its lifetime and header policy accordingly.

## Decorate a model and preserve serialization

Map `schema.object-annotations` to a file containing:

```scriban
[global::System.Diagnostics.DebuggerDisplay({{ target.string_literal type.name }})]
```

Map `schema.object-members` to add metadata that is excluded from JSON:

```scriban
    [global::System.Text.Json.Serialization.JsonIgnore]
    public string GraphQlTypeName => {{ target.string_literal type.name }};
```

Map `schema.object-property` to decorate each schema-object property while retaining its existing type, name and serialization attribute:

```scriban
    // GraphQL field: {{ type.name }}.{{ field.name }}
{{ include "default:schema.object-property" }}
```

There are separate hooks for inputs, interfaces, operation variables and operation response projections. For a projected response property, select `operations.response-object-property` and use its `field.property_name` view. A schema-object override does not automatically replace a response-projection override.

## Add operation metadata and customize query representation

Map `operations.contract-members` to:

```scriban
    public string DocumentName => {{ target.string_literal operation.name }};
```

To decorate the entire contract while keeping the default raw query literal and your member hook, map `operations.contract` to:

```scriban
// Operation document: {{ operation.name }}
{{ include "default:operations.contract" }}
```

Default contracts generate formatted multiline C# raw strings:

```csharp
    public string Query =>
        """
        query CurrentUser {
          user {
            id
            name
          }
        }
        """;
```

The formatter preserves selection order, includes referenced fragments, applies configured variable/typename policies and uses two spaces per GraphQL nesting level. It formats the parsed document; it does not preserve source comments or every original whitespace choice. Generated source uses the newline of the operating system running generation. C# removes the common closing-delimiter indentation, leaving the intended GraphQL indentation in the runtime string. JSON serialization or URL encoding then handles that query normally. .NET 10 and .NET 11 consumers support raw strings.

The contract model exposes:

| Member | Meaning |
| --- | --- |
| `operation.query` | Complete formatted GraphQL text |
| `operation.query_literal` | Preformatted multiline C# raw literal, including the common eight-space indentation |
| `operation.query_lines` | GraphQL lines without the C# nesting prefix |
| `operation.query_delimiter` | At least three quotes, longer than any quote sequence in the query |

When writing a replacement contract, the default query statement is:

```scriban
    public string Query =>
{{ operation.query_literal }};
```

Place the literal interpolation at column zero: the model already supplies its C# indentation, and Scriban otherwise adds another prefix to subsequent lines. Configured `indentSize` adjusts the common C# indentation without changing GraphQL nesting. Blank lines remain blank.

If you prefer an escaped C# literal, export the contract, retain its other members and replace just that statement with:

```scriban
    public string Query => {{ target.string_literal operation.query }};
```

The runnable [escaped-contract example](examples/template-customization/templates/escaped-contract.scriban-cs) contains the complete replacement, selected by [salep.escaped-query.json](examples/template-customization/salep.escaped-query.json). Raw and escaped variants produce the same query value and requests; the representation is a template choice, not a separate configuration flag.

## Replace a method or own a whole file

For a comment or declaration decoration around the response reader, map `client.read-response` to:

```scriban
    // Consumer response-reader wrapper
{{ include "default:client.read-response" }}
```

To change the method body, edit the exported `CSharpClientReadResponse.scriban-cs` and map that key. Preserve the signature expected by the other client methods, or update those callers through their own fragments. To execute custom statements inside the default response method, use `client.after-response`.

For a consumer-owned file header, map the whole-output key `client` to:

```scriban
// Consumer-owned header
#nullable enable
{{ include "default:client" }}
```

The embedded client still selects your configured method and member fragments. Whole-output overrides do not receive the generated header automatically; fragment overrides under a default wrapper do.

Ordinary `include "client.read-response"` selects your configured override when present and the embedded default otherwise. `include "default:client.read-response"` bypasses that one override, preventing self-recursion. Nested includes in the selected default still honor other overrides. Includes accept registered keys, not filenames: arbitrary helper files or custom include names are not loaded. All participating override files must be explicitly selected through registered keys in the configuration.

## Share overrides through profiles and specialize tests

A profile at `profile/templates.json` can declare:

```json
{
  "version": 1,
  "kind": "profile",
  "templates": {
    "client.members": "../templates/client-members.scriban-cs",
    "operations.contract-members": "../templates/contract-members.scriban-cs"
  }
}
```

Select it with `"profile": "profile/templates.json"` in the client. A client's own mapping for `client.members` replaces that key while retaining the profile's `operations.contract-members`. Profile chains use `extends`; the client selects the profile with `profile`. Paths remain relative to the JSON file that declares them, including inherited paths. See [configuration inheritance](config-inheritance-dedup.md) for profiles and verified base-client ownership.

For generated tests, use a separate configuration:

```json
{
  "version": 1,
  "kind": "tests",
  "client": "salep.json",
  "namespace": "Example.Api.Tests",
  "output": "GeneratedTests",
  "templates": {
    "tests.operation-metadata-case": "templates/metadata-case.scriban-cs"
  }
}
```

Its metadata-case file can contain:

```scriban
    // Metadata regression for {{ operation.name }}
{{ include "default:tests.operation-metadata-case" }}
```

Tests inherit the referenced client's template mappings and replace matching keys locally. Tests and client outputs must be separate directories. Generate the client before its tests. For standalone CLI generation, pass `--reference-config salep.json` alongside `--config salep.tests.json`; for MSBuild consumers, add the appropriate project reference and keep test framework/runtime packages in the developer-owned project files. Custom test output does not install those dependencies.

## Try the executable examples

The [example directory](examples/template-customization/README.md) contains actual configurations, a schema, a GraphQL operation and template files used by automated generator tests.

| Configuration | Demonstrates |
| --- | --- |
| [salep.json](examples/template-customization/salep.json) | Profile inheritance, local override precedence, client/transport/model hooks, property decoration and raw operation contracts |
| [salep.wrapper.json](examples/template-customization/salep.wrapper.json) | A whole-file header wrapper plus a response-method wrapper that delegates to its default |
| [salep.escaped-query.json](examples/template-customization/salep.escaped-query.json) | An escaped query literal with the same runtime query value as the raw default |
| [salep.tests.json](examples/template-customization/salep.tests.json) | A generated metadata-case override and inherited client mappings |

Copy the directory to a scratch location, generate the client first and then its tests. Use the package's `dotnet exec` commands from the example README. The regression fixture compiles the generated clients, executes GET and POST requests, checks constructor/header and member hooks, compares query values, verifies ignored model metadata, and parses the customized generated tests.

## Rebuilds, manifests and diagnostics

MSBuild tracks the schema, operation documents, configuration/profile/client chain and every configured template file. Editing a nested fragment triggers regeneration without overriding its whole-output parent. Removing a mapping restores the default. An unchanged build skips rewriting owned output. Exported but unconfigured files do not participate.

`.salep.manifest.json` records paths relative to the directory containing that manifest. For a project with `graphql/query.graphql` and output `Generated`, the key is `../graphql/query.graphql`; a profile template outside the project may legitimately need more `../` segments. Shared schemas and referenced clients retain their true relative paths. The sample clients and modules own their local `graphql` directories. Do not shorten paths by removing directory segments: the path must still resolve to the hashed file.

Run `validate` with the same framework/language options as generation, then build and exercise the affected request/response path. Validation parses/renders templates and checks configuration and ownership; it does not replace compilation or runtime tests. Undefined variables and unknown include keys fail, while incompatible C# emitted by a syntactically valid template fails during consumer compilation. Errors are reported before generation publishes new output. Template evaluation uses strict variables, a 100,000 loop limit and a 128 recursion limit by default; no command-line setting changes those limits. Overrides are re-read for each generation.

Consumer overrides own their emitted behavior. A property override must preserve required JSON mapping unless changing it is deliberate; a transport override must preserve the contracts its callers need. At upgrades, export defaults to a fresh directory, compare the fragments you replaced, and rebuild/run your consumer checks. Stable keys avoid copying entire files, but the package's template model and runtime contracts still matter.

## Template authoring and target helpers

Scriban evaluates the explicit model at each insertion point. Use `target.string_literal` for text embedded in C#, and target naming/type helpers rather than reproducing C# escaping in your template.

| Helper or value | Use |
| --- | --- |
| `target.type_name name`, `target.interface_name name` | C# declaration names |
| `target.property_name name`, `target.method_name name`, `target.parameter_name name` | C# member and parameter names |
| `target.enum_value_name name` | C# enum member names |
| `target.type field.type`, `target.is_value_type field.type` | Render a supplied type reference or inspect its C# value-type policy |
| `target.string_literal text` | Escape a regular C# string literal |
| `target.default_namespace`, `target.imports` | Namespace and import projections supplied by the target |

Exported templates show the context available for each fragment. Template names and model members use the documented spelling; strict evaluation fails on an unknown member. Keep emitted C# indentation aligned with the exported default. Put block-level `for`, `if`, `else` and `end` directives on separate lines, indent nested directives, and use `{{~ ... ~}}` plus matching `# begin`/`# end` labels as the defaults do. Inline only short conditional substitutions within one output statement. Use the provided C# projections for declarations that require several values; the raw `query_literal` projection is already fully indented.

## Model scope and placement

Every fragment receives the enclosing template's model. Context depends on where it is included:

| Keys | Additional context |
| --- | --- |
| `client.*` | `settings`, `target`; emitted methods can use existing client fields |
| `client.constructor-body` | Constructor C# locals `httpClient`, `endpoint`; serializer initialization has completed |
| `client.before-send` | C# `cancellationToken`; inserted before every GET/POST send, including batching and incremental transport; other locals vary by send path |
| `client.after-response` | C# `response`, `cancellationToken`; invoked in the common response reader and batch/incremental readers before status validation |
| `schema.object`, `schema.input`, `schema.interface`, `schema.enum`, `schema.union` and their hooks | Current `type`, plus `schema_types`, `csharp`, `target` |
| `schema.object-property`, `schema.input-property` | Current `type` and `field` |
| `operations.contract*`, `operations.variables*` | Current `operation`; variable-property fragments also receive `variable` |
| `operations.response-*` | Current `operation` and response `type`; response-object-property also receives `field` |
| `converters.declaration`, `converters.read`, `converters.write` | Current union/interface-result `type`, `csharp`, `target` |
| `tests.operation-*-case` | Current `operation`, plus test `settings` and `target` |
| `tests.transport-*` | Test `settings` and `target`, with existing transport fixtures in scope |
| `shared.*`, `operations.facade`, `converters.registry` | The enclosing whole-file model; no single current declaration implied |

Root keys use their existing schema/operation/settings model. Fragments do not receive raw GraphQL source and should use the provided target helpers for C# names, types and literals. Newline and indentation are template output: preserve the placement used by exported defaults. Client generation applies the configured `indentSize` after rendering. Whole-file overrides retain their previous ownership of file headers; default wrappers and fragment overrides keep the generated file header.

An override replaces exactly its selected part. If it removes required members, changes serialization, or changes signatures, the consumer owns that behavior and must validate the consuming build and runtime. Keep defaults for behavior that does not need customization. Empty output is a valid override, useful for suppressing an optional annotation or case.

## Whole-output keys

| Output family | Keys |
| --- | --- |
| Client | `schema`, `operations`, `union-converters`, `shared-types`, `client`, `operation-sample`, `client-agent-instructions` |
| Tests | `test-http-handler`, `transport-tests`, `operation-metadata-tests`, `operation-response-tests`, `operations-sample-tests`, `union-converter-tests`, `test-agent-instructions` |

## Fragment and extension-hook keys

Empty files in this table are additive hooks. Other files contain the existing default declaration, property, method or case. Each filename is exported by the CLI.

| Key | Exported filename | Default |
| --- | --- | --- |
| `client.annotations` | `CSharpClientAnnotations.scriban-cs` | Empty hook |
| `client.members` | `CSharpClientMembers.scriban-cs` | Empty hook |
| `client.constructor-body` | `CSharpClientConstructorBody.scriban-cs` | Empty hook |
| `client.before-send` | `CSharpClientBeforeSend.scriban-cs` | Empty hook |
| `client.after-response` | `CSharpClientAfterResponse.scriban-cs` | Empty hook |
| `client.fields` | `CSharpClientFields.scriban-cs` | Replacement fragment |
| `client.constructor` | `CSharpClientConstructor.scriban-cs` | Replacement fragment |
| `client.execute` | `CSharpClientExecute.scriban-cs` | Replacement fragment |
| `client.batch` | `CSharpClientBatch.scriban-cs` | Replacement fragment |
| `client.incremental` | `CSharpClientIncremental.scriban-cs` | Replacement fragment |
| `client.post` | `CSharpClientPost.scriban-cs` | Replacement fragment |
| `client.read-response` | `CSharpClientReadResponse.scriban-cs` | Replacement fragment |
| `client.get-helpers` | `CSharpClientGetHelpers.scriban-cs` | Replacement fragment |
| `client.multipart-helpers` | `CSharpClientMultipartHelpers.scriban-cs` | Replacement fragment |
| `schema.object` | `CSharpSchemaObject.scriban-cs` | Replacement fragment |
| `schema.input` | `CSharpSchemaInput.scriban-cs` | Replacement fragment |
| `schema.interface` | `CSharpSchemaInterface.scriban-cs` | Replacement fragment |
| `schema.enum` | `CSharpSchemaEnum.scriban-cs` | Replacement fragment |
| `schema.union` | `CSharpSchemaUnion.scriban-cs` | Replacement fragment |
| `schema.object-property` | `CSharpSchemaObjectProperty.scriban-cs` | Replacement fragment |
| `schema.object-annotations` | `CSharpSchemaObjectAnnotations.scriban-cs` | Empty hook |
| `schema.object-members` | `CSharpSchemaObjectMembers.scriban-cs` | Empty hook |
| `schema.input-property` | `CSharpSchemaInputProperty.scriban-cs` | Replacement fragment |
| `schema.input-annotations` | `CSharpSchemaInputAnnotations.scriban-cs` | Empty hook |
| `schema.input-members` | `CSharpSchemaInputMembers.scriban-cs` | Empty hook |
| `schema.interface-annotations` | `CSharpSchemaInterfaceAnnotations.scriban-cs` | Empty hook |
| `schema.interface-members` | `CSharpSchemaInterfaceMembers.scriban-cs` | Empty hook |
| `schema.enum-annotations` | `CSharpSchemaEnumAnnotations.scriban-cs` | Empty hook |
| `operations.contract` | `CSharpOperationsContract.scriban-cs` | Replacement fragment |
| `operations.variables` | `CSharpOperationsVariables.scriban-cs` | Replacement fragment |
| `operations.response-converter` | `CSharpOperationsResponseConverter.scriban-cs` | Replacement fragment |
| `operations.response-object` | `CSharpOperationsResponseObject.scriban-cs` | Replacement fragment |
| `operations.facade` | `CSharpOperationsFacade.scriban-cs` | Replacement fragment |
| `operations.contract-annotations` | `CSharpOperationsContractAnnotations.scriban-cs` | Empty hook |
| `operations.contract-members` | `CSharpOperationsContractMembers.scriban-cs` | Empty hook |
| `operations.variables-annotations` | `CSharpOperationsVariablesAnnotations.scriban-cs` | Empty hook |
| `operations.variables-members` | `CSharpOperationsVariablesMembers.scriban-cs` | Empty hook |
| `operations.variables-property` | `CSharpOperationsVariablesProperty.scriban-cs` | Replacement fragment |
| `operations.response-object-annotations` | `CSharpOperationsResponseObjectAnnotations.scriban-cs` | Empty hook |
| `operations.response-object-members` | `CSharpOperationsResponseObjectMembers.scriban-cs` | Empty hook |
| `operations.response-object-property` | `CSharpOperationsResponseObjectProperty.scriban-cs` | Replacement fragment |
| `shared.operation-interface` | `CSharpSharedOperationInterface.scriban-cs` | Replacement fragment |
| `shared.request` | `CSharpSharedRequest.scriban-cs` | Replacement fragment |
| `shared.response` | `CSharpSharedResponse.scriban-cs` | Replacement fragment |
| `shared.errors` | `CSharpSharedErrors.scriban-cs` | Replacement fragment |
| `converters.registry` | `CSharpConvertersRegistry.scriban-cs` | Replacement fragment |
| `converters.declaration` | `CSharpConvertersDeclaration.scriban-cs` | Replacement fragment |
| `converters.read` | `CSharpConvertersRead.scriban-cs` | Replacement fragment |
| `converters.write` | `CSharpConvertersWrite.scriban-cs` | Replacement fragment |
| `tests.operation-response-case` | `CSharpTestsOperationResponseCase.scriban-cs` | Replacement fragment |
| `tests.operation-metadata-case` | `CSharpTestsOperationMetadataCase.scriban-cs` | Replacement fragment |
| `tests.transport-post` | `CSharpTestsTransportPost.scriban-cs` | Replacement fragment |
| `tests.transport-null-variables` | `CSharpTestsTransportNullVariables.scriban-cs` | Replacement fragment |
| `tests.transport-get` | `CSharpTestsTransportGet.scriban-cs` | Replacement fragment |
| `tests.transport-batch` | `CSharpTestsTransportBatch.scriban-cs` | Replacement fragment |
| `tests.transport-incremental` | `CSharpTestsTransportIncremental.scriban-cs` | Replacement fragment |
| `tests.transport-errors` | `CSharpTestsTransportErrors.scriban-cs` | Replacement fragment |
| `tests.transport-error-details` | `CSharpTestsTransportErrorDetails.scriban-cs` | Replacement fragment |
| `tests.transport-null-responses` | `CSharpTestsTransportNullResponses.scriban-cs` | Replacement fragment |

See [the NSwag analysis and design](template-customization-analysis.md) for the source-based investigation and [the default backend](scriban-backend.md) for generation and package workflows.
