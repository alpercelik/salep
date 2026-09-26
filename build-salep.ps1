#!/usr/bin/env pwsh
$ErrorActionPreference = "Stop"

$ScriptDir = Split-Path -Parent $MyInvocation.MyCommand.Path
Push-Location $ScriptDir
try {

    # Generate a deterministic unique version for this build invocation
    $BuildId = if ($env:GITHUB_RUN_NUMBER) { $env:GITHUB_RUN_NUMBER } else { [DateTimeOffset]::UtcNow.ToUnixTimeSeconds() }
    $Version = "0.1.0-dev.$BuildId"

    Write-Host "========================================="
    Write-Host "Building Salep (Version: $Version)"
    Write-Host "========================================="

    # 1. Clean / create package output directory
    if (-not (Test-Path "artifacts/packages")) {
        New-Item -ItemType Directory -Force -Path "artifacts/packages" | Out-Null
    }

    # 2. Build and test Salep core and MSBuild integration
    foreach ($Framework in @("net10.0", "net11.0")) {
        Write-Host "--> Testing Salep.ClientGenerator.Tests ($Framework)..."
        dotnet run --project src/Salep.ClientGenerator.Tests/Salep.ClientGenerator.Tests.csproj --framework $Framework `
            -p:EnforceCodeStyleInBuild=false `
            -p:TreatWarningsAsErrors=false
        if ($LASTEXITCODE -ne 0) { exit $LASTEXITCODE }

        Write-Host "--> Testing Salep.ClientGenerator.MSBuild.Tests ($Framework)..."
        dotnet run --project src/Salep.ClientGenerator.MSBuild.Tests/Salep.ClientGenerator.MSBuild.Tests.csproj --framework $Framework `
            -p:EnforceCodeStyleInBuild=false `
            -p:TreatWarningsAsErrors=false
        if ($LASTEXITCODE -ne 0) { exit $LASTEXITCODE }
    }

    # 3. Pack Salep NuGet package
    Write-Host "--> Packing Salep package (Version: $Version)..."
    dotnet pack src/Salep.ClientGenerator.MSBuild/Salep.ClientGenerator.MSBuild.csproj `
        --no-restore `
        -c Release `
        -p:Version="$Version" `
        -o artifacts/packages `
        -p:EnforceCodeStyleInBuild=false `
        -p:TreatWarningsAsErrors=false
    if ($LASTEXITCODE -ne 0) { exit $LASTEXITCODE }

    # 4. Restore sample projects against local feed
    Write-Host "--> Restoring Sample projects..."
    dotnet restore src/samples/Opinionated/Salep.Samples.Opinionated.Client/Salep.Samples.Opinionated.Client.csproj -p:SalepVersion="$Version"
    if ($LASTEXITCODE -ne 0) { exit $LASTEXITCODE }

    dotnet restore src/samples/Opinionated/Salep.Samples.Opinionated.Module/Salep.Samples.Opinionated.Module.csproj -p:SalepVersion="$Version"
    if ($LASTEXITCODE -ne 0) { exit $LASTEXITCODE }

    dotnet restore src/samples/Opinionated/Salep.Samples.Opinionated.Client.Tests/Salep.Samples.Opinionated.Client.Tests.csproj -p:SalepVersion="$Version"
    if ($LASTEXITCODE -ne 0) { exit $LASTEXITCODE }

    dotnet restore src/samples/Opinionated/Salep.Samples.Opinionated.Module.Tests/Salep.Samples.Opinionated.Module.Tests.csproj -p:SalepVersion="$Version"
    if ($LASTEXITCODE -ne 0) { exit $LASTEXITCODE }
    dotnet restore src/samples/Opinionated/Salep.Samples.Opinionated.IntegrationTests/Salep.Samples.Opinionated.IntegrationTests.csproj -p:SalepVersion="$Version"
    if ($LASTEXITCODE -ne 0) { exit $LASTEXITCODE }

    dotnet restore src/samples/MinimalDependencies/Salep.Samples.MinimalDependencies.Client/Salep.Samples.MinimalDependencies.Client.csproj -p:SalepVersion="$Version"
    if ($LASTEXITCODE -ne 0) { exit $LASTEXITCODE }

    dotnet restore src/samples/MinimalDependencies/Salep.Samples.MinimalDependencies.Module/Salep.Samples.MinimalDependencies.Module.csproj -p:SalepVersion="$Version"
    if ($LASTEXITCODE -ne 0) { exit $LASTEXITCODE }

    dotnet restore src/samples/MinimalDependencies/Salep.Samples.MinimalDependencies.Client.Tests/Salep.Samples.MinimalDependencies.Client.Tests.csproj -p:SalepVersion="$Version"
    if ($LASTEXITCODE -ne 0) { exit $LASTEXITCODE }

    dotnet restore src/samples/MinimalDependencies/Salep.Samples.MinimalDependencies.Module.Tests/Salep.Samples.MinimalDependencies.Module.Tests.csproj -p:SalepVersion="$Version"
    if ($LASTEXITCODE -ne 0) { exit $LASTEXITCODE }
    dotnet restore src/samples/MinimalDependencies/Salep.Samples.MinimalDependencies.IntegrationTests/Salep.Samples.MinimalDependencies.IntegrationTests.csproj -p:SalepVersion="$Version"
    if ($LASTEXITCODE -ne 0) { exit $LASTEXITCODE }

    dotnet restore src/samples/Salep.Samples.GraphQLServer/Salep.Samples.GraphQLServer.csproj
    if ($LASTEXITCODE -ne 0) { exit $LASTEXITCODE }

    Write-Host "--> Building GraphQL server sample and exporting schema..."
    dotnet build src/samples/Salep.Samples.GraphQLServer/Salep.Samples.GraphQLServer.csproj --framework net11.0 `
        -p:EnforceCodeStyleInBuild=false `
        -p:TreatWarningsAsErrors=false
    if ($LASTEXITCODE -ne 0) { exit $LASTEXITCODE }

    # 5. Build and test sample projects (code generation occurs automatically via Salep MSBuild targets during build)
    foreach ($Framework in @("net10.0", "net11.0")) {
        Write-Host "--> Testing Opinionated/Salep.Samples.Opinionated.Client.Tests ($Framework)..."
        dotnet run --project src/samples/Opinionated/Salep.Samples.Opinionated.Client.Tests/Salep.Samples.Opinionated.Client.Tests.csproj --framework $Framework `
            -p:SalepVersion="$Version" `
            -p:EnforceCodeStyleInBuild=false `
            -p:TreatWarningsAsErrors=false
        if ($LASTEXITCODE -ne 0) { exit $LASTEXITCODE }

        Write-Host "--> Testing Opinionated/Salep.Samples.Opinionated.Module.Tests ($Framework)..."
        dotnet run --project src/samples/Opinionated/Salep.Samples.Opinionated.Module.Tests/Salep.Samples.Opinionated.Module.Tests.csproj --framework $Framework `
            -p:SalepVersion="$Version" `
            -p:EnforceCodeStyleInBuild=false `
            -p:TreatWarningsAsErrors=false
        if ($LASTEXITCODE -ne 0) { exit $LASTEXITCODE }
    }

    Write-Host "--> Testing Opinionated GraphQL integration..."
    dotnet run --project src/samples/Opinionated/Salep.Samples.Opinionated.IntegrationTests/Salep.Samples.Opinionated.IntegrationTests.csproj --framework net11.0 `
        -p:SalepVersion="$Version" `
        -p:EnforceCodeStyleInBuild=false `
        -p:TreatWarningsAsErrors=false
    if ($LASTEXITCODE -ne 0) { exit $LASTEXITCODE }

    Write-Host "--> Testing MinimalDependencies sample project set..."
    dotnet run --project src/samples/MinimalDependencies/Salep.Samples.MinimalDependencies.Client.Tests/Salep.Samples.MinimalDependencies.Client.Tests.csproj --framework net11.0 `
        -p:SalepVersion="$Version" `
        -p:EnforceCodeStyleInBuild=false `
        -p:TreatWarningsAsErrors=false
    if ($LASTEXITCODE -ne 0) { exit $LASTEXITCODE }

    dotnet run --project src/samples/MinimalDependencies/Salep.Samples.MinimalDependencies.Module.Tests/Salep.Samples.MinimalDependencies.Module.Tests.csproj --framework net11.0 `
        -p:SalepVersion="$Version" `
        -p:EnforceCodeStyleInBuild=false `
        -p:TreatWarningsAsErrors=false
    if ($LASTEXITCODE -ne 0) { exit $LASTEXITCODE }

    dotnet run --project src/samples/MinimalDependencies/Salep.Samples.MinimalDependencies.IntegrationTests/Salep.Samples.MinimalDependencies.IntegrationTests.csproj --framework net11.0 `
        -p:SalepVersion="$Version" `
        -p:EnforceCodeStyleInBuild=false `
        -p:TreatWarningsAsErrors=false
    if ($LASTEXITCODE -ne 0) { exit $LASTEXITCODE }

    Write-Host "========================================="
    Write-Host "Salep build and dogfooding verification successful!"
    Write-Host "========================================="

}
finally {
    Pop-Location
}
