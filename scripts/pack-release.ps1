#!/usr/bin/env pwsh
#Requires -Version 5.1
$ErrorActionPreference = 'Stop'

if ($args.Count -gt 0 -and $args[0] -in @('--help', '-h')) {
    Write-Host 'Usage: pack-release.ps1 <version> [output-directory]'
    exit 0
}
if ($args.Count -lt 1 -or $args.Count -gt 2) {
    [Console]::Error.WriteLine('Usage: pack-release.ps1 <version> [output-directory]')
    exit 2
}
if ($args.Count -eq 2 -and [string]::IsNullOrEmpty([string]$args[1])) {
    [Console]::Error.WriteLine('Output directory cannot be empty.')
    exit 2
}
$Version = [string]$args[0]
if ($Version -cnotmatch '^(0|[1-9][0-9]*)\.(0|[1-9][0-9]*)\.(0|[1-9][0-9]*)(-[0-9A-Za-z-]+(\.[0-9A-Za-z-]+)*)?\z') {
    [Console]::Error.WriteLine('Version must be major.minor.patch with an optional prerelease suffix, without build metadata.')
    exit 2
}
if ($Version.Contains('-')) {
    $Identifiers = $Version.Substring($Version.IndexOf('-') + 1).Split('.')
    foreach ($Identifier in $Identifiers) {
        if ($Identifier -cmatch '^0[0-9]+$') {
            [Console]::Error.WriteLine('Numeric prerelease identifiers cannot have leading zeros.')
            exit 2
        }
    }
}
$RepoRoot = Split-Path -Parent $PSScriptRoot
Push-Location $RepoRoot
try {
    $OutputDirectory = if ($args.Count -eq 2) { [string]$args[1] } else { "artifacts/release/$Version" }
    if (-not [System.IO.Path]::IsPathRooted($OutputDirectory)) {
        $OutputDirectory = Join-Path $RepoRoot $OutputDirectory
    }
    $OutputDirectory = [System.IO.Path]::GetFullPath($OutputDirectory)
    [void][System.IO.Directory]::CreateDirectory($OutputDirectory)
    foreach ($Project in @('src/Salep.GraphQLParser/Salep.GraphQLParser.csproj', 'src/Salep.ClientGenerator.MSBuild/Salep.ClientGenerator.MSBuild.csproj')) {
        dotnet pack $Project --configuration Release --output $OutputDirectory "-p:Version=$Version"
        if ($LASTEXITCODE -ne 0) { exit $LASTEXITCODE }
    }
    foreach ($Package in @("Salep.GraphQLParser.$Version.nupkg", "Salep.GraphQLParser.$Version.snupkg", "Salep.ClientGenerator.$Version.nupkg")) {
        $PackagePath = Join-Path $OutputDirectory $Package
        if (-not [System.IO.File]::Exists($PackagePath)) { throw "Missing release artifact: $Package" }
        Write-Host $PackagePath
    }
}
finally {
    Pop-Location
}
