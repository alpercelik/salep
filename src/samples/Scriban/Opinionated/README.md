# Opinionated Scriban sample

This profile consumes the `Salep.ClientGenerator` package. Client and Module own local `graphql/` documents and share the exported GraphQL server schema. Generated ownership paths are relative to each `.salep.manifest.json`.

The profile uses Dunet discriminated unions and NodaTime scalar mappings on .NET 10 and .NET 11. The full spec fixture lives in `Client/schema.coverage.graphql`.

Client emits schema/transport types; Module references Client through `baseClient` and does not duplicate them. Client.Tests and Module.Tests compile and run generated xUnit cases. IntegrationTests exercises both generated clients against the repository server using Alba.

Run the complete exact-version package workflow from the repository root:

```bash
./build-salep.sh
```

```powershell
pwsh ./build-salep.ps1
```

Customize generated output through configuration template fragments or hooks; see [template customization](../../../../docs/client-generator/template-customization.md). Never edit `Generated/` or `GeneratedTests/` directly. See [consumer configuration](../../../../docs/client-generator/consumer-guide.md) and [generator verification](../../../../docs/client-generator/generator-parity.md).
