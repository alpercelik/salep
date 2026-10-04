# TypeScript generator design

Salep scans standalone `.graphql` files and static GraphQL documents in `.ts` and `.tsx` sources. Document patterns support recursive globs and brace alternatives. Applications can consume standard typed AST documents through `graphql-request`, use literal source lookup, and optionally use fragment masking helpers.

`Salep.ClientGenerator.Core` owns neutral schema, operation, fragment, response-projection and document-formatting models. The TypeScript library consumes these models and owns its target policies, projections and twenty-two composed Scriban templates. Its CLI remains separate from the C# MSBuild package. Selected operations determine response shapes; schema-wide object types would promise unselected fields.

The generated layout separates schema and operation declarations, typed documents, source lookup, fragment-reference helpers, fetch transport, barrel exports and source schema export. Configuration uses versioned JSON with explicit filesystem permissions. Original Node sample inputs cover masked/unmasked modes, TypeScript compilation, standard document consumption and real HTTP requests.

See [configuration and customization](README.md) and [correctness and coverage](correctness-and-coverage.md) for the supported contract and verification details.
