#!/usr/bin/env pwsh
#Requires -Version 5.1
param(
    [string]$Seed = '20260925',
    [string]$Cases = '512'
)
$ErrorActionPreference = 'Stop'

if ([string]::IsNullOrEmpty($Seed)) { $Seed = '20260925' }
if ([string]::IsNullOrEmpty($Cases)) { $Cases = '512' }
$PreviousSeed = $env:GRAPHQL_FUZZ_SEED
$PreviousCases = $env:GRAPHQL_FUZZ_CASES
$RepoRoot = Split-Path -Parent $PSScriptRoot
Push-Location $RepoRoot
try {
    $env:GRAPHQL_FUZZ_SEED = $Seed
    $env:GRAPHQL_FUZZ_CASES = $Cases
    dotnet test --project (Join-Path $RepoRoot 'src/Salep.GraphQLParser.Tests/Salep.GraphQLParser.Tests.csproj') --configuration Release `
        --filter-class '*DeterministicParserFuzzTests'
    exit $LASTEXITCODE
}
finally {
    Pop-Location
    $env:GRAPHQL_FUZZ_SEED = $PreviousSeed
    $env:GRAPHQL_FUZZ_CASES = $PreviousCases
}
