# Canonical AST comparison contract

This contract defines the comparison boundary for C# parser results and the pinned `graphql-js` oracle. It compares syntax trees and parser acceptance. It does not compare schema validation, execution, or runtime object identity.

## Canonical JSON shape

Each syntax node is represented by a JSON object with a `kind` string, grammar-defined scalar fields, ordered child-node arrays, and `loc: [start, end]`. Locations are zero-based, half-open UTF-16 code-unit offsets into the original source, matching JavaScript string indexing and the `SourceText` contract. Both endpoints include all source text consumed by that node, including its closing delimiter where applicable.

The projection rules are:

- Preserve source order in `definitions`, `selections`, `arguments`, `directives`, variable definitions, fields, enum values, union members, and every other grammar list. Array order is significant.
- Preserve each node's kind and grammar-relevant scalar values. Names preserve their text. Integer and float values preserve their source spelling as strings, avoiding numeric precision or formatting changes. Quoted and block string values use the decoded/normalized value, while `loc` continues to identify the original spelling.
- Emit all child-list fields as arrays, including empty lists. Emit optional child nodes as JSON `null` when absent. This keeps examples and later fixtures structurally comparable.
- Compare JSON objects by member name and value; member order and insignificant JSON whitespace do not matter. Do not sort arrays or normalize source locations.
- Keep parser acceptance/error comparison separate from AST comparison. A failed parse has no canonical document AST.

The `graphql-js` adapter must omit information that has no stable C# counterpart: `loc.source` and its `body`, `name`, and `locationOffset`; JavaScript object identity/prototypes; parser-internal token objects; and any serializer-generated metadata. It must retain each node's `kind`, meaningful syntax fields, children, and `loc.start`/`loc.end`. In particular, two equivalent `Source` instances are not distinguished by identity.

Fixtures store the exact UTF-8 source text, including any final line feed, and canonical expected JSON. Oracle-generated fixtures record the pinned `graphql-js` package version and generation command alongside the corpus. Hand-authored examples below are normative examples of the projection shape; the fenced source blocks each end with a line feed, which is included in the document span.

`OracleCorpusTests.EveryValidFixtureHasTheSameCanonicalAstAsGraphqlJs` parses every fixture marked valid and compares its C# projection with the pinned snapshot. Failure output separates parser rejection of an oracle-valid fixture (a grammar/parser failure), serializer exceptions, and the first structural, decoded-value, or source-location mismatch path. The comprehensive executable and SDL fixtures keep the supported production branches in the differential corpus.

## Executable example

Source (`query Q { hello }`):

```graphql
query Q { hello }
```

Canonical output:

```json
{
  "kind": "Document",
  "loc": [0, 18],
  "definitions": [
    {
      "kind": "OperationDefinition",
      "loc": [0, 17],
      "operation": "query",
      "name": { "kind": "Name", "loc": [6, 7], "value": "Q" },
      "variableDefinitions": [],
      "directives": [],
      "selectionSet": {
        "kind": "SelectionSet",
        "loc": [8, 17],
        "selections": [
          {
            "kind": "Field",
            "loc": [10, 15],
            "alias": null,
            "name": { "kind": "Name", "loc": [10, 15], "value": "hello" },
            "arguments": [],
            "directives": [],
            "selectionSet": null
          }
        ]
      }
    }
  ]
}
```

## SDL example

Source (`type Query { x: Int }`):

```graphql
type Query { x: Int }
```

Canonical output:

```json
{
  "kind": "Document",
  "loc": [0, 22],
  "definitions": [
    {
      "kind": "ObjectTypeDefinition",
      "loc": [0, 21],
      "description": null,
      "name": { "kind": "Name", "loc": [5, 10], "value": "Query" },
      "interfaces": [],
      "directives": [],
      "fields": [
        {
          "kind": "FieldDefinition",
          "loc": [13, 19],
          "description": null,
          "name": { "kind": "Name", "loc": [13, 14], "value": "x" },
          "arguments": [],
          "type": {
            "kind": "NamedType",
            "loc": [16, 19],
            "name": { "kind": "Name", "loc": [16, 19], "value": "Int" }
          },
          "directives": []
        }
      ]
    }
  ]
}
```

These examples intentionally include source spans for every node and preserve empty arrays and absent optional children. Future node types follow the same rules; their grammar-specific child field names are defined with the AST taxonomy.
