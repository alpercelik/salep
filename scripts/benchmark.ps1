#!/usr/bin/env pwsh
#Requires -Version 5.1
$ErrorActionPreference = 'Stop'

$RepoRoot = Split-Path -Parent $PSScriptRoot
$BenchmarkFramework = if ([string]::IsNullOrEmpty($env:BENCHMARK_TFM)) { 'net10.0' } else { $env:BENCHMARK_TFM }
$AllocationIterations = if ([string]::IsNullOrEmpty($env:BENCHMARK_ALLOCATION_ITERATIONS)) { '1000' } else { $env:BENCHMARK_ALLOCATION_ITERATIONS }
$AllocationRepetitions = if ([string]::IsNullOrEmpty($env:BENCHMARK_ALLOCATION_REPETITIONS)) { '3' } else { $env:BENCHMARK_ALLOCATION_REPETITIONS }

Push-Location $RepoRoot
try {
    $Project = 'benchmarks/Salep.GraphQLParser.Benchmarks/Salep.GraphQLParser.Benchmarks.csproj'
    dotnet build $Project --configuration Release --framework $BenchmarkFramework --disable-build-servers
    if ($LASTEXITCODE -ne 0) { exit $LASTEXITCODE }

    $BenchmarkDll = "artifacts/bin/Salep.GraphQLParser.Benchmarks/release_$BenchmarkFramework/Salep.GraphQLParser.Benchmarks.dll"
    dotnet exec $BenchmarkDll --allocation-gate --iterations $AllocationIterations --repetitions $AllocationRepetitions
    if ($LASTEXITCODE -ne 0) { exit $LASTEXITCODE }

    dotnet exec $BenchmarkDll @args
    exit $LASTEXITCODE
}
finally {
    Pop-Location
}
