# AI Agent Directives — Generated GraphQL Test Suite

> **NOTICE FOR AI AGENTS & DEVELOPERS**:
> This directory contains **auto-generated test artifacts** produced by **Salep**.
> **STRICTLY DO NOT MANUALLY EDIT, FORMAT, RENAME, OR DELETE ANY FILES IN THIS DIRECTORY.**
> Any manual changes will be overwritten and lost on the next build.

---

### Authoritative Client Contract

This configuration has `kind: "tests"` and references a concrete `client`. Union representation, scalars, operation signatures, and transport behavior come exclusively from its verified manifest. Change those settings in the client or its profile, then rebuild the client before generating tests. NuGet references remain developer-owned.

### Purpose of Generated Tests

The test suite in this directory provides automated verification for:
- Operation metadata (operation names, document hashes, variable schemas).
- JSON payload serialization and GraphQL-over-HTTP transport protocol compliance.
- Response deserialization, nullability handling, and custom scalar conversions.
- Polymorphic union deserialization via `__typename` discriminant mapping.

---

### In-Memory Mock Testing with `TestHttpMessageHandler`

When writing application-level unit tests for components that consume the GraphQL client:
- Do not spin up an external HTTP server or mock client methods directly.
- Use the generated `TestHttpMessageHandler` to return mock responses:
  ```csharp
  var handler = new TestHttpMessageHandler(_ =>
      TestHttpMessageHandler.JsonResponse("{\"data\":null}"));
  var httpClient = new HttpClient(handler);
  var client = new GeneratedModuleClient(httpClient, new Uri("http://localhost/graphql"));
  ```