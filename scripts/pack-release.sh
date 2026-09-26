#!/usr/bin/env bash
set -euo pipefail

usage() { echo 'Usage: pack-release.sh <version> [output-directory]'; }
if [[ "${1:-}" == '--help' || "${1:-}" == '-h' ]]; then usage; exit 0; fi
if [[ $# -lt 1 || $# -gt 2 ]]; then usage >&2; exit 2; fi
if [[ $# -eq 2 && -z "$2" ]]; then usage >&2; exit 2; fi
version="$1"
if [[ ! "$version" =~ ^(0|[1-9][0-9]*)\.(0|[1-9][0-9]*)\.(0|[1-9][0-9]*)(-[0-9A-Za-z-]+(\.[0-9A-Za-z-]+)*)?$ ]]; then
    echo 'Version must be major.minor.patch with an optional prerelease suffix, without build metadata.' >&2
    exit 2
fi
if [[ "$version" == *-* ]]; then
    IFS='.' read -r -a identifiers <<< "${version#*-}"
    for identifier in "${identifiers[@]}"; do
        if [[ "$identifier" =~ ^0[0-9]+$ ]]; then echo 'Numeric prerelease identifiers cannot have leading zeros.' >&2; exit 2; fi
    done
fi
repo_root="$(cd "$(dirname "${BASH_SOURCE[0]}")/.." && pwd)"
cd "$repo_root"
output="${2:-artifacts/release/$version}"
mkdir -p "$output"
output="$(cd "$output" && pwd)"

for project in src/Salep.GraphQLParser/Salep.GraphQLParser.csproj src/Salep.ClientGenerator.MSBuild/Salep.ClientGenerator.MSBuild.csproj; do
    dotnet pack "$project" --configuration Release --output "$output" "-p:Version=$version"
done
for package in "Salep.GraphQLParser.$version.nupkg" "Salep.GraphQLParser.$version.snupkg" "Salep.ClientGenerator.$version.nupkg"; do
    if [[ ! -f "$output/$package" ]]; then echo "Missing release artifact: $package" >&2; exit 1; fi
    echo "$output/$package"
done
