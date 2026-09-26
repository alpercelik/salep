#!/usr/bin/env pwsh
#Requires -Version 5.1
$ErrorActionPreference = 'Stop'

$RepoRoot = Split-Path -Parent $PSScriptRoot
Push-Location $RepoRoot
try {
    dotnet test --solution (Join-Path $RepoRoot 'src/Salep.Core.slnf') --configuration Release @args
    exit $LASTEXITCODE
}
finally {
    Pop-Location
}
