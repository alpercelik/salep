#!/usr/bin/env sh
set -eu
iterations="${1:-5000}"
dotnet run --project benchmarks/GraphQLParser.Benchmarks/GraphQLParser.Benchmarks.csproj --configuration Release -- "$iterations"
