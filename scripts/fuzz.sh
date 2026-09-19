#!/usr/bin/env sh
set -eu
seed="${1:-20260925}"
cases="${2:-512}"
GRAPHQL_FUZZ_SEED="$seed" GRAPHQL_FUZZ_CASES="$cases" ./scripts/test.sh --filter FullyQualifiedName~DeterministicParserFuzzTests
