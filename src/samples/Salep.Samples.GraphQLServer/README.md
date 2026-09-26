# GraphQL Server Sample

This Hot Chocolate server defines its schema directly from C# resolver and type definitions. Every build initializes the executable Hot Chocolate schema and exports it to `Generated/schema.graphql`. Both the `Opinionated` and `MinimalDependencies` clients link to that generated schema as their Salep input; it is not added as a project item.

Run it from the repository root:

```sh
dotnet run --project src/samples/Salep.Samples.GraphQLServer/Salep.Samples.GraphQLServer.csproj
```

The GraphQL endpoint is `/graphql`; Hot Chocolate Nitro is available at `/graphql` and `/graphql/ui`. The server uses in-memory sample data. `createPost` publishes the `postCreated` subscription; the `commentAdded` subscription is available for clients but no sample mutation publishes comments.

The sample server allows a maximum field cost of 5,000 so the client coverage queries can exercise nested lists and union fragments (the users-by-role query has an estimated cost of 2,553). Cost enforcement remains enabled; integration tests verify that an oversized query is still rejected.
