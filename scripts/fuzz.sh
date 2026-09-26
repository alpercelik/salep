#!/usr/bin/env sh
set -eu
script_dir=$(CDPATH= cd -- "$(dirname -- "$0")" && pwd)
seed="${1:-20260925}"
cases="${2:-512}"
repo_root=$(CDPATH= cd -- "$script_dir/.." && pwd)
cd "$repo_root"
GRAPHQL_FUZZ_SEED="$seed" GRAPHQL_FUZZ_CASES="$cases" dotnet test \
  --project "$repo_root/src/Salep.GraphQLParser.Tests/Salep.GraphQLParser.Tests.csproj" --configuration Release \
  --filter-class '*DeterministicParserFuzzTests'
