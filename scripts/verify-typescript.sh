#!/usr/bin/env bash
set -euo pipefail
repo_root="$(cd "$(dirname "${BASH_SOURCE[0]}")/.." && pwd)"
sample="$repo_root/src/samples/TypeScript/Node"
dotnet run --project "$repo_root/src/Salep.ClientGenerator.TypeScript.Cli" --framework net10.0 -- generate --config "$sample/salep.typescript.json"
dotnet run --project "$repo_root/src/Salep.ClientGenerator.TypeScript.Cli" --framework net10.0 -- generate --config "$sample/salep.masked.typescript.json"
npm ci --prefix "$sample" --ignore-scripts
npm test --prefix "$sample"
