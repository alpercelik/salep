#!/usr/bin/env pwsh
#Requires -Version 5.1
$ErrorActionPreference = 'Stop'

$RepoRoot = Split-Path -Parent $PSScriptRoot
Push-Location $RepoRoot
try {
    $PackageDir = Join-Path $RepoRoot 'artifacts/packages'
    $Package = Join-Path $PackageDir 'Salep.GraphQLParser.0.0.0-verify.1.nupkg'
    New-Item -ItemType Directory -Force -Path $PackageDir | Out-Null
    if (Test-Path -LiteralPath $Package) { Remove-Item -LiteralPath $Package }

    dotnet pack (Join-Path $RepoRoot 'src/Salep.GraphQLParser/Salep.GraphQLParser.csproj') `
        --configuration Release `
        --output $PackageDir `
        -p:PackageId=Salep.GraphQLParser `
        -p:PackageVersion=0.0.0-verify.1
    if ($LASTEXITCODE -ne 0) { exit $LASTEXITCODE }
    if (-not (Test-Path -LiteralPath $Package)) { throw 'Parser package was not created.' }

    Add-Type -AssemblyName System.IO.Compression.FileSystem
    $Archive = [System.IO.Compression.ZipFile]::OpenRead($Package)
    $VerificationDir = Join-Path ([System.IO.Path]::GetTempPath()) ('salep-package-' + [Guid]::NewGuid().ToString('N'))
    try {
        $Entries = @($Archive.Entries | ForEach-Object { $_.FullName })
        $RequiredEntries = @('package-readme.md')
        foreach ($Framework in @('net10.0', 'net11.0')) {
            $RequiredEntries += "lib/$Framework/Salep.GraphQLParser.dll"
            $RequiredEntries += "lib/$Framework/Salep.GraphQLParser.xml"
        }
        foreach ($Entry in $RequiredEntries) {
            if ($Entries -cnotcontains $Entry) { throw "Missing package entry: $Entry" }
        }
        $UnexpectedAssemblies = @($Entries | Where-Object {
            $_ -cmatch '^lib/[^/]+/[^/]+\.dll$' -and $_ -cnotmatch '^lib/net(10|11)\.0/Salep\.GraphQLParser\.dll$'
        })
        if ($UnexpectedAssemblies.Count -gt 0) { throw "Unexpected package assemblies: $($UnexpectedAssemblies -join ', ')" }

        New-Item -ItemType Directory -Path $VerificationDir | Out-Null
        $Consumer = Join-Path $RepoRoot 'src/Salep.GraphQLParser.PublicApiConsumer/Salep.GraphQLParser.PublicApiConsumer.csproj'
        $ConsumerOptions = @(
            "-p:NuGetPackageRoot=$VerificationDir/packages",
            "-p:RestorePackagesPath=$VerificationDir/packages",
            "-p:ArtifactsPath=$VerificationDir/artifacts"
        )
        dotnet restore $Consumer @ConsumerOptions --configfile (Join-Path $RepoRoot 'NuGet.config') --force --no-cache
        if ($LASTEXITCODE -ne 0) { exit $LASTEXITCODE }

        foreach ($Framework in @('net10.0', 'net11.0')) {
            $Unpacked = Join-Path $VerificationDir "Salep.GraphQLParser.$Framework.dll"
            [System.IO.Compression.ZipFileExtensions]::ExtractToFile($Archive.GetEntry("lib/$Framework/Salep.GraphQLParser.dll"), $Unpacked)
            $Restored = Join-Path $VerificationDir "packages/salep.graphqlparser/0.0.0-verify.1/lib/$Framework/Salep.GraphQLParser.dll"
            if ((Get-FileHash -LiteralPath $Unpacked -Algorithm SHA256).Hash -ne (Get-FileHash -LiteralPath $Restored -Algorithm SHA256).Hash) {
                throw "Restored parser differs from the freshly packed assembly ($Framework)."
            }
        }
        dotnet build $Consumer @ConsumerOptions --configuration Release --no-restore
        if ($LASTEXITCODE -ne 0) { exit $LASTEXITCODE }
        foreach ($Framework in @('net10.0', 'net11.0')) {
            dotnet run --project $Consumer @ConsumerOptions --configuration Release --framework $Framework --no-build --no-restore
            if ($LASTEXITCODE -ne 0) { exit $LASTEXITCODE }
        }
    }
    finally {
        $Archive.Dispose()
        if (Test-Path -LiteralPath $VerificationDir) { Remove-Item -LiteralPath $VerificationDir -Recurse -Force }
    }

    Write-Host 'Package contents verified: net10.0 and net11.0 assemblies with XML documentation only.'
    Write-Host "Package saved to: $Package"

}
finally {
    Pop-Location
}
