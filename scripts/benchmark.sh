#!/usr/bin/env sh
set -eu

repo_root="$(CDPATH= cd -- "$(dirname -- "$0")/.." && pwd)"
benchmark_tfm="${BENCHMARK_TFM:-net10.0}"
allocation_iterations="${BENCHMARK_ALLOCATION_ITERATIONS:-1000}"
allocation_repetitions="${BENCHMARK_ALLOCATION_REPETITIONS:-3}"

cd "$repo_root"
project="benchmarks/Salep.Parser.Benchmarks/Salep.Parser.Benchmarks.csproj"
dotnet build "$project" --configuration Release --framework "$benchmark_tfm" --disable-build-servers
benchmark_dll="artifacts/bin/Salep.Parser.Benchmarks/release_${benchmark_tfm}/Salep.Parser.Benchmarks.dll"
dotnet exec "$benchmark_dll" --allocation-gate --iterations "$allocation_iterations" --repetitions "$allocation_repetitions"

dotnet exec "$benchmark_dll" "$@"
