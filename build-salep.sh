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

# 1. Clean / create package output directory
mkdir -p artifacts/packages

# 2. Build and test Salep core and MSBuild integration
for framework in net10.0 net11.0; do
    echo "--> Testing Salep.ClientGenerator.Tests ($framework)..."
    dotnet run --project src/Salep.ClientGenerator.Tests/Salep.ClientGenerator.Tests.csproj --framework "$framework" \
        -p:EnforceCodeStyleInBuild=false \
        -p:TreatWarningsAsErrors=false

    echo "--> Testing Salep.ClientGenerator.MSBuild.Tests ($framework)..."
    dotnet run --project src/Salep.ClientGenerator.MSBuild.Tests/Salep.ClientGenerator.MSBuild.Tests.csproj --framework "$framework" \
        -p:EnforceCodeStyleInBuild=false \
        -p:TreatWarningsAsErrors=false
done

# 3. Pack Salep NuGet package
echo "--> Packing Salep package (Version: $VERSION)..."
dotnet pack src/Salep.ClientGenerator.MSBuild/Salep.ClientGenerator.MSBuild.csproj \
    --no-restore \
    -c Release \
    -p:Version="$VERSION" \
    -o artifacts/packages \
    -p:EnforceCodeStyleInBuild=false \
    -p:TreatWarningsAsErrors=false

# 4. Restore sample projects against local feed
echo "--> Restoring Sample projects..."
dotnet restore src/samples/Opinionated/Salep.Samples.Opinionated.Client/Salep.Samples.Opinionated.Client.csproj -p:SalepVersion="$VERSION"
dotnet restore src/samples/Opinionated/Salep.Samples.Opinionated.Module/Salep.Samples.Opinionated.Module.csproj -p:SalepVersion="$VERSION"
dotnet restore src/samples/Opinionated/Salep.Samples.Opinionated.Client.Tests/Salep.Samples.Opinionated.Client.Tests.csproj -p:SalepVersion="$VERSION"
dotnet restore src/samples/Opinionated/Salep.Samples.Opinionated.Module.Tests/Salep.Samples.Opinionated.Module.Tests.csproj -p:SalepVersion="$VERSION"
dotnet restore src/samples/Opinionated/Salep.Samples.Opinionated.IntegrationTests/Salep.Samples.Opinionated.IntegrationTests.csproj -p:SalepVersion="$VERSION"
dotnet restore src/samples/MinimalDependencies/Salep.Samples.MinimalDependencies.Client/Salep.Samples.MinimalDependencies.Client.csproj -p:SalepVersion="$VERSION"
dotnet restore src/samples/MinimalDependencies/Salep.Samples.MinimalDependencies.Module/Salep.Samples.MinimalDependencies.Module.csproj -p:SalepVersion="$VERSION"
dotnet restore src/samples/MinimalDependencies/Salep.Samples.MinimalDependencies.Client.Tests/Salep.Samples.MinimalDependencies.Client.Tests.csproj -p:SalepVersion="$VERSION"
dotnet restore src/samples/MinimalDependencies/Salep.Samples.MinimalDependencies.Module.Tests/Salep.Samples.MinimalDependencies.Module.Tests.csproj -p:SalepVersion="$VERSION"
dotnet restore src/samples/MinimalDependencies/Salep.Samples.MinimalDependencies.IntegrationTests/Salep.Samples.MinimalDependencies.IntegrationTests.csproj -p:SalepVersion="$VERSION"
dotnet restore src/samples/Salep.Samples.GraphQLServer/Salep.Samples.GraphQLServer.csproj

echo "--> Building GraphQL server sample and exporting schema..."
dotnet build src/samples/Salep.Samples.GraphQLServer/Salep.Samples.GraphQLServer.csproj --framework net11.0 \
    -p:EnforceCodeStyleInBuild=false \
    -p:TreatWarningsAsErrors=false

# 5. Build and test sample projects (code generation occurs automatically via Salep MSBuild targets during build)
echo "--> Testing Opinionated/Salep.Samples.Opinionated.Client.Tests..."
for framework in net10.0 net11.0; do
    echo "--> Testing Opinionated/Salep.Samples.Opinionated.Client.Tests ($framework)..."
    dotnet run --project src/samples/Opinionated/Salep.Samples.Opinionated.Client.Tests/Salep.Samples.Opinionated.Client.Tests.csproj --framework "$framework" \
        -p:SalepVersion="$VERSION" \
        -p:EnforceCodeStyleInBuild=false \
        -p:TreatWarningsAsErrors=false

    echo "--> Testing Opinionated/Salep.Samples.Opinionated.Module.Tests ($framework)..."
    dotnet run --project src/samples/Opinionated/Salep.Samples.Opinionated.Module.Tests/Salep.Samples.Opinionated.Module.Tests.csproj --framework "$framework" \
        -p:SalepVersion="$VERSION" \
        -p:EnforceCodeStyleInBuild=false \
        -p:TreatWarningsAsErrors=false
done

echo "--> Testing Opinionated GraphQL integration..."
dotnet run --project src/samples/Opinionated/Salep.Samples.Opinionated.IntegrationTests/Salep.Samples.Opinionated.IntegrationTests.csproj --framework net11.0 \
    -p:SalepVersion="$VERSION" \
    -p:EnforceCodeStyleInBuild=false \
    -p:TreatWarningsAsErrors=false

echo "--> Testing MinimalDependencies sample project set..."
dotnet run --project src/samples/MinimalDependencies/Salep.Samples.MinimalDependencies.Client.Tests/Salep.Samples.MinimalDependencies.Client.Tests.csproj --framework net11.0 \
    -p:SalepVersion="$VERSION" \
    -p:EnforceCodeStyleInBuild=false \
    -p:TreatWarningsAsErrors=false
dotnet run --project src/samples/MinimalDependencies/Salep.Samples.MinimalDependencies.Module.Tests/Salep.Samples.MinimalDependencies.Module.Tests.csproj --framework net11.0 \
    -p:SalepVersion="$VERSION" \
    -p:EnforceCodeStyleInBuild=false \
    -p:TreatWarningsAsErrors=false
dotnet run --project src/samples/MinimalDependencies/Salep.Samples.MinimalDependencies.IntegrationTests/Salep.Samples.MinimalDependencies.IntegrationTests.csproj --framework net11.0 \
    -p:SalepVersion="$VERSION" \
    -p:EnforceCodeStyleInBuild=false \
    -p:TreatWarningsAsErrors=false

echo "========================================="
echo "Salep build and dogfooding verification successful!"
echo "========================================="
