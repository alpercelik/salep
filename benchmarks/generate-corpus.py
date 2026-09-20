#!/usr/bin/env python3
"""Regenerate the checked-in, versioned GraphQL benchmark corpus."""

import hashlib
import json
from pathlib import Path

root = Path(__file__).parent / "corpus" / "v1"
root.mkdir(parents=True, exist_ok=True)

cases = {
    "small-executable": (
        "small-executable",
        True,
        "query Small($id: ID!) { user(id: $id) { id name } }\n",
    ),
    "large-executable": (
        "large-executable",
        True,
        "query Large($filter: Filter!, $limit: Int = 40) {\n"
        + "\n".join(
            f'  alias_{index:02d}: product(index: {index}, filter: $filter, label: "item-{index:02d}") '
            f"{{ id name price {{ amount currency }} }}"
            for index in range(64)
        )
        + "\n}\n",
    ),
    "small-sdl": (
        "small-sdl",
        True,
        'schema { query: Query }\ninput Filter { text: String }\ntype Query { search(filter: Filter): [String!]! }\n',
    ),
    "large-sdl": (
        "large-sdl",
        True,
        'schema { query: Query }\nscalar Date\n'
        + "\n".join(
            f'type Record{index:02d} {{ id: ID! name: String! created: Date '
            + " ".join(f'field_{field:02d}(arg: Int = {field}): String' for field in range(3))
            + " }"
            for index in range(40)
        )
        + "\ntype Query {\n"
        + "\n".join(f"  record_{index:02d}: Record{index:02d}" for index in range(40))
        + "\n}\n",
    ),
    "strings": (
        "strings",
        True,
        "query StringCorpus {\n"
        + "\n".join(
            f'  field_{index:02d}(text: "escaped\\n-{index:02d}-λ-\\u263A", block: """line one\nline two {index:02d}""")'
            for index in range(48)
        )
        + "\n}\n",
    ),
    "fragments": (
        "fragments",
        True,
        "query FragmentCorpus { root {\n"
        + "\n".join(f"  ...Fragment{index:02d}" for index in range(24))
        + "\n} }\n"
        + "\n".join(f"fragment Fragment{index:02d} on Node {{ id field_{index:02d} }}" for index in range(24))
        + "\n",
    ),
    "malformed": (
        "malformed",
        False,
        "query Broken { first(arg: ) { name } second(arg: [1, 2]) }\nquery Later { valid }\n",
    ),
    "adversarial-nesting": (
        "adversarial-nesting",
        True,
        "query Deep { value(arg: " + "[" * 120 + "1" + "]" * 120 + ") }\n",
    ),
}

manifest = {
    "version": 1,
    "specificationTarget": "September 2025 GraphQL specification",
    "requiredCategories": [
        "small-executable",
        "large-executable",
        "small-sdl",
        "large-sdl",
        "strings",
        "fragments",
        "malformed",
        "adversarial-nesting",
    ],
    "cases": [],
}

for case_id, (category, valid, source) in cases.items():
    filename = f"{case_id}.graphql"
    with (root / filename).open("w", encoding="utf-8", newline="\n") as output:
        output.write(source)
    operations = ["lexer", "strict-parse"] if valid else ["lexer", "diagnostic-parse"]
    manifest["cases"].append(
        {
            "id": case_id,
            "category": category,
            "path": filename,
            "valid": valid,
            "operations": operations,
            "sha256": hashlib.sha256(source.encode("utf-8")).hexdigest(),
        }
    )

with (root / "cases.json").open("w", encoding="utf-8", newline="\n") as output:
    output.write(json.dumps(manifest, indent=2) + "\n")
