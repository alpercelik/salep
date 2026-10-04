#!/usr/bin/env bash
set -euo pipefail

SCRIPT_DIR="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"
cd "$SCRIPT_DIR"

# Generate a deterministic unique version for this build invocation
BUILD_ID="${GITHUB_RUN_NUMBER:-$(date +%s)}"
VERSION="0.1.0-dev.${BUILD_ID}"

echo "========================================="
echo "Building Salep (Version: $VERSION)"
echo "========================================="

run_test_project() {
    local project_path="$1"
    local framework="$2"
    shift 2

    dotnet build "$project_path" --framework "$framework" -m:1 /nodeReuse:false /p:UseSharedCompilation=false "$@"
    dotnet run --no-build --no-restore --project "$project_path" --framework "$framework" "$@"
}

# 1. Clean / create package output directory
mkdir -p artifacts/packages

# 2. Build and test Salep core and MSBuild integration
for framework in net10.0 net11.0; do

    echo "--> Testing Salep.ClientGenerator.Tests ($framework)..."
    run_test_project src/Salep.ClientGenerator.Tests/Salep.ClientGenerator.Tests.csproj "$framework" \
        -p:EnforceCodeStyleInBuild=false \
        -p:TreatWarningsAsErrors=false

    echo "--> Testing Salep.ClientGenerator.MSBuild.Tests ($framework)..."
    run_test_project src/Salep.ClientGenerator.MSBuild.Tests/Salep.ClientGenerator.MSBuild.Tests.csproj "$framework" \
        -p:EnforceCodeStyleInBuild=false \
        -p:TreatWarningsAsErrors=false
done

# 3. Pack the public Scriban generator package
echo "--> Packing Salep package (Version: $VERSION)..."
dotnet pack src/Salep.ClientGenerator.MSBuild/Salep.ClientGenerator.MSBuild.csproj \
    --no-restore \
    -m:1 \
    /nodeReuse:false \
    /p:UseSharedCompilation=false \
    -c Release \
    -p:Version="$VERSION" \
    -o artifacts/packages \
    -p:EnforceCodeStyleInBuild=false \
    -p:TreatWarningsAsErrors=false

# 4. Restore sample projects against local feed
echo "--> Restoring Sample projects..."
dotnet restore src/samples/Salep.Samples.GraphQLServer/Salep.Samples.GraphQLServer.csproj -m:1 /nodeReuse:false --source https://api.nuget.org/v3/index.json

echo "--> Building GraphQL server sample and exporting schema..."
dotnet build src/samples/Salep.Samples.GraphQLServer/Salep.Samples.GraphQLServer.csproj --framework net11.0 -m:1 /nodeReuse:false /p:UseSharedCompilation=false \
    -p:EnforceCodeStyleInBuild=false \
    -p:TreatWarningsAsErrors=false

# 5. Build and test the Scriban samples. MinimalDependencies uses native unions and therefore targets net11.0 only.
echo "--> Restoring Scriban sample projects..."
dotnet restore src/samples/Opinionated/Client.Tests/Salep.Samples.Opinionated.Client.Tests.csproj -m:1 /nodeReuse:false --source artifacts/packages --source https://api.nuget.org/v3/index.json -p:SalepVersion="$VERSION" -p:NuGetAudit=false
dotnet restore src/samples/Opinionated/Module.Tests/Salep.Samples.Opinionated.Module.Tests.csproj -m:1 /nodeReuse:false --source artifacts/packages --source https://api.nuget.org/v3/index.json -p:SalepVersion="$VERSION" -p:NuGetAudit=false
dotnet restore src/samples/MinimalDependencies/Client.Tests/Salep.Samples.MinimalDependencies.Client.Tests.csproj -m:1 /nodeReuse:false --source artifacts/packages --source https://api.nuget.org/v3/index.json -p:SalepVersion="$VERSION" -p:NuGetAudit=false
dotnet restore src/samples/MinimalDependencies/Module.Tests/Salep.Samples.MinimalDependencies.Module.Tests.csproj -m:1 /nodeReuse:false --source artifacts/packages --source https://api.nuget.org/v3/index.json -p:SalepVersion="$VERSION" -p:NuGetAudit=false

echo "--> Testing Scriban Opinionated sample projects..."
for framework in net10.0 net11.0; do
    run_test_project src/samples/Opinionated/Client.Tests/Salep.Samples.Opinionated.Client.Tests.csproj "$framework" \
        -p:SalepVersion="$VERSION" \
        -p:EnforceCodeStyleInBuild=false \
        -p:TreatWarningsAsErrors=false

    run_test_project src/samples/Opinionated/Module.Tests/Salep.Samples.Opinionated.Module.Tests.csproj "$framework" \
        -p:SalepVersion="$VERSION" \
        -p:EnforceCodeStyleInBuild=false \
        -p:TreatWarningsAsErrors=false
done

echo "--> Testing Scriban MinimalDependencies sample projects (net11.0)..."
run_test_project src/samples/MinimalDependencies/Client.Tests/Salep.Samples.MinimalDependencies.Client.Tests.csproj net11.0 \
    -p:SalepVersion="$VERSION" \
    -p:EnforceCodeStyleInBuild=false \
    -p:TreatWarningsAsErrors=false
run_test_project src/samples/MinimalDependencies/Module.Tests/Salep.Samples.MinimalDependencies.Module.Tests.csproj net11.0 \
    -p:SalepVersion="$VERSION" \
    -p:EnforceCodeStyleInBuild=false \
    -p:TreatWarningsAsErrors=false

# Run the same server acceptance fixture for both Scriban profiles.
for profile in Opinionated MinimalDependencies; do
    project="src/samples/$profile/IntegrationTests/Salep.Samples.$profile.IntegrationTests.csproj"
    dotnet restore "$project" -m:1 /nodeReuse:false --source artifacts/packages --source https://api.nuget.org/v3/index.json -p:SalepVersion="$VERSION"
    run_test_project "$project" net11.0 -p:SalepVersion="$VERSION" \
        -p:EnforceCodeStyleInBuild=false -p:TreatWarningsAsErrors=false
done

echo "========================================="
echo "Salep build and dogfooding verification successful!"
echo "========================================="
