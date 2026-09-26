#!/usr/bin/env bash
set -euo pipefail

repo_root="$(cd "$(dirname "${BASH_SOURCE[0]}")/.." && pwd)"
cd "$repo_root"
package_dir="$repo_root/artifacts/packages"
mkdir -p "$package_dir"
package="$package_dir/Salep.GraphQLParser.0.0.0-verify.1.nupkg"
rm -f "$package"

dotnet pack "$repo_root/src/Salep.GraphQLParser/Salep.GraphQLParser.csproj" \
  --configuration Release \
  --output "$package_dir" \
  -p:PackageId=Salep.GraphQLParser \
  -p:PackageVersion=0.0.0-verify.1

test -f "$package"
contents="$(unzip -Z1 "$package")"
for framework in net10.0 net11.0; do
  grep -Fxq "lib/$framework/Salep.GraphQLParser.dll" <<<"$contents"
  grep -Fxq "lib/$framework/Salep.GraphQLParser.xml" <<<"$contents"
done
grep -Fxq 'package-readme.md' <<<"$contents"
unexpected_assemblies="$(grep -E '^lib/[^/]+/[^/]+\.dll$' <<<"$contents" | grep -Ev '^lib/net(10|11)\.0/Salep\.GraphQLParser\.dll$' || true)"
if [[ -n "$unexpected_assemblies" ]]; then
  echo "Unexpected package assemblies:" >&2
  echo "$unexpected_assemblies" >&2
  exit 1
fi

# Keep both restored packages and consumer outputs separate from normal builds.
# --no-cache alone does not bypass NuGet's installed global packages.
verification_dir="$(mktemp -d)"
trap 'rm -rf "$verification_dir"' EXIT
consumer="$repo_root/src/Salep.GraphQLParser.PublicApiConsumer/Salep.GraphQLParser.PublicApiConsumer.csproj"
consumer_options=("-p:NuGetPackageRoot=$verification_dir/packages" "-p:RestorePackagesPath=$verification_dir/packages" "-p:ArtifactsPath=$verification_dir/artifacts")
dotnet restore "$consumer" "${consumer_options[@]}" --configfile "$repo_root/NuGet.config" --force --no-cache
for framework in net10.0 net11.0; do
  unpacked="$verification_dir/Salep.GraphQLParser.$framework.dll"
  unzip -p "$package" "lib/$framework/Salep.GraphQLParser.dll" > "$unpacked"
  cmp "$unpacked" "$verification_dir/packages/salep.graphqlparser/0.0.0-verify.1/lib/$framework/Salep.GraphQLParser.dll"
done
dotnet build "$consumer" "${consumer_options[@]}" --configuration Release --no-restore
for framework in net10.0 net11.0; do
  dotnet run --project "$consumer" "${consumer_options[@]}" --configuration Release --framework "$framework" --no-build --no-restore
done

echo "Package contents verified: net10.0 and net11.0 assemblies with XML documentation only."
echo "Package saved to: $package"
