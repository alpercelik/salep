#!/usr/bin/env bash
set -euo pipefail
repo_root="$(cd "$(dirname "${BASH_SOURCE[0]}")/.." && pwd)"
if [[ $# -lt 1 || $# -gt 2 ]]; then
  echo 'Usage: publish-typescript.sh <runtime-identifier> [output-directory]' >&2
  exit 1
fi
runtime_id="$1"
output_directory="${2:-$repo_root/artifacts/typescript/$runtime_id}"
dotnet publish "$repo_root/src/Salep.ClientGenerator.TypeScript.Cli" --framework net10.0 --configuration Release --runtime "$runtime_id" --self-contained true --output "$output_directory" -p:PublishSingleFile=true -p:IncludeNativeLibrariesForSelfExtract=true -p:PublishTrimmed=false -p:UseSharedCompilation=false -p:DebugType=None -p:GenerateDocumentationFile=false -m:1 /nodeReuse:false
