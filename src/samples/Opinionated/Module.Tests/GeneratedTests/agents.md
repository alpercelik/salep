# AI Agent Directives — Generated GraphQL Test Suite

> **NOTICE FOR AI AGENTS AND DEVELOPERS**:
> This directory contains auto-generated test artifacts produced by Salep.
> Do not manually edit, format, rename, or delete files in this directory. Regeneration overwrites them.

These tests use namespace `Salep.Samples.Opinionated.ModuleClient.Tests` and reference the configured `GraphQLModuleClient` client. The selected suites are: operations, transport, unions.

The transport suite exercises GraphQL request serialization and HTTP behavior with the in-memory `TestHttpMessageHandler`. The operations suite checks operation documents, metadata, variables, and response deserialization. The unions suite checks `__typename` converter registration and concrete variants. The samples suite executes the generated operation sample.
Union values use Dunet discriminated unions.

JSON fixtures use C# raw string literals. Change schemas, operation documents, or generator configuration and regenerate the tests; do not patch generated assertions directly.

Test generation consumes the client configuration and verified generated types. Keep the client built before generating tests so their operation signatures and scalar mappings match the client under test. NuGet references remain owned by the consuming project.
