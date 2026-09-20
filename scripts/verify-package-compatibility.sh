#!/usr/bin/env bash
set -euo pipefail

repo_root="$(cd "$(dirname "${BASH_SOURCE[0]}")/.." && pwd)"
package_dir="$repo_root/artifacts/packages"
mkdir -p "$package_dir"
package="$package_dir/GraphQLParser.0.0.0-verify.1.nupkg"
rm -f "$package"

dotnet pack "$repo_root/src/GraphQLParser/GraphQLParser.csproj" \
  --configuration Release \
  --no-restore \
  --output "$package_dir" \
  -p:PackageId=GraphQLParser \
  -p:PackageVersion=0.0.0-verify.1

test -f "$package"
contents="$(unzip -Z1 "$package")"
for framework in net10.0 net11.0; do
  grep -Fxq "lib/$framework/GraphQLParser.dll" <<<"$contents"
  grep -Fxq "lib/$framework/GraphQLParser.xml" <<<"$contents"
done
grep -Fxq 'package-readme.md' <<<"$contents"
unexpected_assemblies="$(grep -E '^lib/[^/]+/[^/]+\.dll$' <<<"$contents" | grep -Ev '^lib/net(10|11)\.0/GraphQLParser\.dll$' || true)"
if [[ -n "$unexpected_assemblies" ]]; then
  echo "Unexpected package assemblies:" >&2
  echo "$unexpected_assemblies" >&2
  exit 1
fi

consumer="$repo_root/tests/GraphQLParser.PublicApiConsumer/GraphQLParser.PublicApiConsumer.csproj"
dotnet restore "$consumer" --source "$package_dir" --force --no-cache
dotnet build "$consumer" --configuration Release --no-restore
for framework in net10.0 net11.0; do
  dotnet run --project "$consumer" --configuration Release --framework "$framework" --no-build --no-restore
done

echo "Package contents verified: net10.0 and net11.0 assemblies with XML documentation only."
echo "Package saved to: $package"
