# TypeScript Node sample

Original catalog schema/documents demonstrate nullable fields, nested selections, fragments, input objects, enums, custom scalars, union/interface narrowing, fragment masking and mutations. `Generated/gql/` contains unmasked typed ASTs, lookup, fragment helpers and fetch transport; `MaskedGenerated/gql/` exercises fragment masking. Both export their own schema. Generated files come from Salep; edit the inputs or generator rather than generated output.

From this directory:

```bash
npm ci
npm run generate
npm test
```

The same npm commands work from PowerShell. From any directory, use the paired `scripts/verify-typescript.sh` / `scripts/verify-typescript.ps1` workflows. Tests compile negative type assertions, verify injected transport behavior, and execute generated queries/mutations against a local HTTP graphql-js server. No external service is required.

See [the TypeScript guide](../../../../docs/typescript-client-generator/README.md) for the executable, custom templates and consumer API.

The parity fixtures exercise conditional duplicate parents, constant directives, schema extensions and colliding schema/operation names. `npm test` also runs 37 differential validation scenarios against graphql-js; build the TypeScript CLI first (`npm run generate` or the paired verification scripts). Set `SALEP_TYPESCRIPT_CLI` to a built CLI DLL to override its default repository artifact location.
