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

    function Invoke-TestProject {
        param(
            [Parameter(Mandatory = $true)][string]$ProjectPath,
            [Parameter(Mandatory = $true)][string]$Framework,
            [string[]]$BuildProperties = @()
        )

        $BuildArguments = @("build", $ProjectPath, "--framework", $Framework, "-m:1", "/nodeReuse:false", "/p:UseSharedCompilation=false") + $BuildProperties
        & dotnet @BuildArguments
        if ($LASTEXITCODE -ne 0) { throw "Build failed for $ProjectPath ($Framework) with exit code $LASTEXITCODE." }

        & dotnet run --no-build --no-restore --project $ProjectPath --framework $Framework @BuildProperties
        if ($LASTEXITCODE -ne 0) { throw "Tests failed for $ProjectPath ($Framework) with exit code $LASTEXITCODE." }
    }

    # 1. Clean / create package output directory
    if (-not (Test-Path "artifacts/packages")) {
        New-Item -ItemType Directory -Force -Path "artifacts/packages" | Out-Null
    }

    # 2. Build and test Salep core and MSBuild integration
    foreach ($Framework in @("net10.0", "net11.0")) {

        Write-Host "--> Testing Salep.ClientGenerator.Tests ($Framework)..."
        Invoke-TestProject "src/Salep.ClientGenerator.Tests/Salep.ClientGenerator.Tests.csproj" $Framework @("-p:EnforceCodeStyleInBuild=false", "-p:TreatWarningsAsErrors=false")

        Write-Host "--> Testing Salep.ClientGenerator.MSBuild.Tests ($Framework)..."
        Invoke-TestProject "src/Salep.ClientGenerator.MSBuild.Tests/Salep.ClientGenerator.MSBuild.Tests.csproj" $Framework @("-p:EnforceCodeStyleInBuild=false", "-p:TreatWarningsAsErrors=false")
    }

    # 3. Pack the public Scriban generator package
    Write-Host "--> Packing Salep package (Version: $Version)..."
    dotnet pack src/Salep.ClientGenerator.MSBuild/Salep.ClientGenerator.MSBuild.csproj `
        --no-restore `
        -m:1 `
        /nodeReuse:false `
        /p:UseSharedCompilation=false `
        -c Release `
        -p:Version="$Version" `
        -o artifacts/packages `
        -p:EnforceCodeStyleInBuild=false `
        -p:TreatWarningsAsErrors=false
    if ($LASTEXITCODE -ne 0) { exit $LASTEXITCODE }

    # 4. Restore sample projects against local feed
    Write-Host "--> Restoring Sample projects..."

    dotnet restore src/samples/Salep.Samples.GraphQLServer/Salep.Samples.GraphQLServer.csproj -m:1 /nodeReuse:false --source https://api.nuget.org/v3/index.json
    if ($LASTEXITCODE -ne 0) { exit $LASTEXITCODE }

    Write-Host "--> Building GraphQL server sample and exporting schema..."
    dotnet build src/samples/Salep.Samples.GraphQLServer/Salep.Samples.GraphQLServer.csproj --framework net11.0 -m:1 /nodeReuse:false /p:UseSharedCompilation=false `
        -p:EnforceCodeStyleInBuild=false `
        -p:TreatWarningsAsErrors=false
    if ($LASTEXITCODE -ne 0) { exit $LASTEXITCODE }

    # 5. Build and test the Scriban samples. MinimalDependencies uses native unions and therefore targets net11.0 only.
    Write-Host "--> Restoring Scriban sample projects..."
    dotnet restore src/samples/Scriban/Opinionated/Client.Tests/Salep.Samples.Opinionated.Scriban.Client.Tests.csproj -m:1 /nodeReuse:false --source artifacts/packages --source https://api.nuget.org/v3/index.json -p:SalepVersion="$Version" -p:NuGetAudit=false
    if ($LASTEXITCODE -ne 0) { exit $LASTEXITCODE }
    dotnet restore src/samples/Scriban/Opinionated/Module.Tests/Salep.Samples.Opinionated.Scriban.Module.Tests.csproj -m:1 /nodeReuse:false --source artifacts/packages --source https://api.nuget.org/v3/index.json -p:SalepVersion="$Version" -p:NuGetAudit=false
    if ($LASTEXITCODE -ne 0) { exit $LASTEXITCODE }
    dotnet restore src/samples/Scriban/MinimalDependencies/Client.Tests/Salep.Samples.MinimalDependencies.Scriban.Client.Tests.csproj -m:1 /nodeReuse:false --source artifacts/packages --source https://api.nuget.org/v3/index.json -p:SalepVersion="$Version" -p:NuGetAudit=false
    if ($LASTEXITCODE -ne 0) { exit $LASTEXITCODE }
    dotnet restore src/samples/Scriban/MinimalDependencies/Module.Tests/Salep.Samples.MinimalDependencies.Scriban.Module.Tests.csproj -m:1 /nodeReuse:false --source artifacts/packages --source https://api.nuget.org/v3/index.json -p:SalepVersion="$Version" -p:NuGetAudit=false
    if ($LASTEXITCODE -ne 0) { exit $LASTEXITCODE }

    Write-Host "--> Testing Scriban Opinionated sample projects..."
    foreach ($Framework in @("net10.0", "net11.0")) {
        Invoke-TestProject "src/samples/Scriban/Opinionated/Client.Tests/Salep.Samples.Opinionated.Scriban.Client.Tests.csproj" $Framework @("-p:SalepVersion=$Version", "-p:EnforceCodeStyleInBuild=false", "-p:TreatWarningsAsErrors=false")

        Invoke-TestProject "src/samples/Scriban/Opinionated/Module.Tests/Salep.Samples.Opinionated.Scriban.Module.Tests.csproj" $Framework @("-p:SalepVersion=$Version", "-p:EnforceCodeStyleInBuild=false", "-p:TreatWarningsAsErrors=false")
    }

    Write-Host "--> Testing Scriban MinimalDependencies sample projects (net11.0)..."
    Invoke-TestProject "src/samples/Scriban/MinimalDependencies/Client.Tests/Salep.Samples.MinimalDependencies.Scriban.Client.Tests.csproj" "net11.0" @("-p:SalepVersion=$Version", "-p:EnforceCodeStyleInBuild=false", "-p:TreatWarningsAsErrors=false")
    Invoke-TestProject "src/samples/Scriban/MinimalDependencies/Module.Tests/Salep.Samples.MinimalDependencies.Scriban.Module.Tests.csproj" "net11.0" @("-p:SalepVersion=$Version", "-p:EnforceCodeStyleInBuild=false", "-p:TreatWarningsAsErrors=false")

    # Run the same server acceptance fixture for both Scriban profiles.
    foreach ($Profile in @("Opinionated", "MinimalDependencies")) {
        $Project = "src/samples/Scriban/$Profile/IntegrationTests/Salep.Samples.$Profile.Scriban.IntegrationTests.csproj"
        dotnet restore $Project -m:1 /nodeReuse:false --source artifacts/packages --source https://api.nuget.org/v3/index.json -p:SalepVersion="$Version"
        if ($LASTEXITCODE -ne 0) { exit $LASTEXITCODE }
        Invoke-TestProject $Project "net11.0" @("-p:SalepVersion=$Version", "-p:EnforceCodeStyleInBuild=false", "-p:TreatWarningsAsErrors=false")
    }

    Write-Host "========================================="
    Write-Host "Salep build and dogfooding verification successful!"
    Write-Host "========================================="

}
finally {
    Pop-Location
}
