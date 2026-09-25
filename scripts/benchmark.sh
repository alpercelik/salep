#!/usr/bin/env sh
set -eu
repo_root="$(CDPATH= cd -- "$(dirname -- "$0")/.." && pwd)"
iterations="${1:-1000}"
repetitions="${2:-3}"
output="${3:-benchmarks/results/latest.json}"
cd "$repo_root"
mkdir -p "$(dirname -- "$output")"
dotnet run --project benchmarks/Salep.Parser.Benchmarks/Salep.Parser.Benchmarks.csproj --configuration Release -- "$iterations" "$repetitions" "$output"
