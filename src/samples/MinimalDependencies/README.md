# Minimal Dependencies Sample

This project set reuses the GraphQL operation documents from `src/samples/Opinionated` and points Salep at the same schema exported by `src/samples/Salep.Samples.GraphQLServer/Generated/schema.graphql`. It omits Dunet and NodaTime, using native C# unions and built-in .NET date types instead. This keeps the generated client free of those extra runtime dependencies while exercising the same GraphQL surface. `schema.coverage.graphql` remains as a specification coverage fixture, not the generator input.

All projects target .NET 11 and use preview language features. `Salep.Samples.MinimalDependencies.Client` generates native C# union types; `Salep.Samples.MinimalDependencies.Module` demonstrates the same multi-project setup; two test projects cover generated operation and serialization behavior, and `Salep.Samples.MinimalDependencies.IntegrationTests` covers live GraphQL execution.

## GraphQL integration tests

`Salep.Samples.MinimalDependencies.IntegrationTests` uses Alba to start the shared Hot Chocolate server in memory and exercises every generated query and mutation from both the base and module clients:

```bash
dotnet run --project src/samples/MinimalDependencies/Salep.Samples.MinimalDependencies.IntegrationTests/Salep.Samples.MinimalDependencies.IntegrationTests.csproj --framework net11.0
```

This sample uses native C# unions and built-in .NET types without Dunet or NodaTime.

Run the sample after packing Salep to a local feed:

```bash
dotnet restore src/samples/MinimalDependencies/Salep.Samples.MinimalDependencies.Client.Tests/Salep.Samples.MinimalDependencies.Client.Tests.csproj -p:SalepVersion=<local-version>
dotnet restore src/samples/MinimalDependencies/Salep.Samples.MinimalDependencies.IntegrationTests/Salep.Samples.MinimalDependencies.IntegrationTests.csproj -p:SalepVersion=<local-version>
dotnet run --project src/samples/MinimalDependencies/Salep.Samples.MinimalDependencies.Client.Tests/Salep.Samples.MinimalDependencies.Client.Tests.csproj --framework net11.0 -p:SalepVersion=<local-version>
dotnet run --project src/samples/MinimalDependencies/Salep.Samples.MinimalDependencies.Module.Tests/Salep.Samples.MinimalDependencies.Module.Tests.csproj --framework net11.0 -p:SalepVersion=<local-version>
dotnet run --project src/samples/MinimalDependencies/Salep.Samples.MinimalDependencies.IntegrationTests/Salep.Samples.MinimalDependencies.IntegrationTests.csproj --framework net11.0 -p:SalepVersion=<local-version>
```
