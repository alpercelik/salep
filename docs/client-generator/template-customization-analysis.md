# Consumer template customization: NSwag analysis and Salep design

The NSwag clone was inspected read-only at commit `63daf8fcc3a25151b62eb4b326a1e8ea048a0d41`. No NSwag source or templates were copied. Salep continues to use Scriban and its own GraphQL-to-neutral-model-to-C# pipeline.

## What NSwag does

NSwag separates configuration, projected template models, named template lookup, and output composition:

- [`CSharpGeneratorBaseSettings`](https://github.com/RicoSuter/NSwag/blob/63daf8fcc3a25151b62eb4b326a1e8ea048a0d41/src/NSwag.CodeGeneration.CSharp/CSharpGeneratorBaseSettings.cs) installs a template factory covering both NSwag and NJsonSchema embedded templates. The settings remain distinct from the template models.
- [`CodeGeneratorCommandBase`](https://github.com/RicoSuter/NSwag/blob/63daf8fcc3a25151b62eb4b326a1e8ea048a0d41/src/NSwag.Commands/Commands/CodeGeneration/CodeGeneratorCommandBase.cs) exposes `TemplateDirectory`; [`NSwagDocumentBase`](https://github.com/RicoSuter/NSwag/blob/63daf8fcc3a25151b62eb4b326a1e8ea048a0d41/src/NSwag.Commands/NSwagDocumentBase.cs) resolves C# template-directory paths relative to the document and verifies that directories exist.
- [`DefaultTemplateFactory`](https://github.com/RicoSuter/NSwag/blob/63daf8fcc3a25151b62eb4b326a1e8ea048a0d41/src/NSwag.CodeGeneration/DefaultTemplateFactory.cs) supplies NSwag embedded resources and delegates the rest to NJsonSchema. The clone pins NJsonSchema 11.6.1. Its [factory implementation](https://github.com/RicoSuter/NJsonSchema/blob/v11.6.1/src/NJsonSchema.CodeGeneration/DefaultTemplateFactory.cs) searches configured directories for a named `.liquid` file, then falls back to an embedded resource. Nested templates receive a child scope of the current render context. That implementation also permits explicitly selecting an embedded default.
- [`File.liquid`](https://github.com/RicoSuter/NSwag/blob/63daf8fcc3a25151b62eb4b326a1e8ea048a0d41/src/NSwag.CodeGeneration.CSharp/Templates/File.liquid) composes file-level header/footer extension points and generated declarations.
- [`Client.Class.liquid`](https://github.com/RicoSuter/NSwag/blob/63daf8fcc3a25151b62eb4b326a1e8ea048a0d41/src/NSwag.CodeGeneration.CSharp/Templates/Client.Class.liquid) calls smaller constructor, documentation, annotation, parameter, request, response, and class-body templates. Empty templates such as `Client.Class.Body` and `Client.Class.BeforeSend` are intentional insertion points.
- [`CSharpClientGeneratorSettings`](https://github.com/RicoSuter/NSwag/blob/63daf8fcc3a25151b62eb4b326a1e8ea048a0d41/src/NSwag.CodeGeneration.CSharp/CSharpClientGeneratorSettings.cs) and the generated partial client provide another customization path through base classes and prepare-request/process-response methods. These generated-code APIs are distinct from template replacement.

The reusable principle is granular composition with default fallback, not the number of files or NSwag's template syntax. An additive extension point avoids forcing a consumer to maintain a copy of a large upstream template. Replacing a method remains available when its behavior must change substantially.

## Salep's starting point

Salep already had fourteen embedded output templates, stable `templates` configuration keys, an export command, profile/client/test inheritance, paths relative to the declaring configuration, and template files recorded as generation/MSBuild inputs. These are useful consumer contracts and remain intact.

The missing seam was composition: `client`, `schema`, `operations`, converters and generated tests were rendered as whole files. A consumer changing one property annotation or response method had to replace much more code than the intended change.

## Implemented Salep design

The fourteen original keys stay available. Named fragments and empty hooks increase the catalog to 73 entries. All entries are embedded in the same Scriban-based `Salep.ClientGenerator` package and exported by the existing `templates` command.

| Boundary | Consumer control |
| --- | --- |
| Whole output | Existing keys still replace an entire file |
| Declaration/method/case | Replace one model declaration, property, operation contract, converter method, transport method or generated test case |
| Additive hook | Add annotations/members, constructor customization, or before-send/after-response code without copying the containing declaration |
| Embedded default | `include "default:<key>"` reads that default while its nested includes still honor configured fragment overrides |

A render-local Scriban loader resolves only registered keys. Normal includes select configured content first and embedded content otherwise. The `default:` namespace bypasses replacement of that one entry. Unknown keys and arbitrary filesystem include paths fail with source locations. Include parse/runtime errors reach the configuration diagnostics before outputs are published. The existing recursion and loop limits apply within includes, and no parsed-template cache is shared between renders or configurations.

Salep retains explicit file mappings rather than adding directory discovery. This makes every configured override a known input, preserves path/inheritance behavior, and avoids hidden file selection depending on the execution directory. Consumers can export all defaults, but should map only files they want to maintain.

Type mapping, nullability, variable policy, naming and serializer policy remain in the existing neutral models and C# target services. Template fragments receive their enclosing model and loop variables; they do not parse GraphQL source.

This change does not add NSwag-style inheritance/partial-method APIs to generated clients. That would alter the generated public API and requires a separate design. Empty template hooks provide consumer customization while preserving the existing generated API by default.

## Verification

Composition tests exercise declaration/property/operation context, whole-file wrappers, explicit default selection, profile inheritance, unknown/path includes, parse errors, strict variables, recursion and loop limits, and complete export. MSBuild tests exercise fragment tracking, unchanged builds and regeneration after a fragment edit. Fresh-cache public-package consumers export templates, replace a response method with a wrapper around its default, add a member, execute it on .NET 10/11, and rebuild after the member fragment changes. The normal differential and sample/server workflows remain the default-behavior compatibility gate.

The consumer contract and complete key list are documented in [template customization](template-customization.md).
