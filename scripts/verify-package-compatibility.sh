#!/usr/bin/env bash
set -euo pipefail

repo_root="$(cd "$(dirname "${BASH_SOURCE[0]}")/.." && pwd)"
verify_root="$(mktemp -d "${TMPDIR:-/tmp}/graphql-parser-package-verify.XXXXXX")"
trap 'rm -rf "$verify_root"' EXIT
feed="$verify_root/feed"
mkdir -p "$feed"

dotnet pack "$repo_root/src/GraphQLParser/GraphQLParser.csproj" \
  --configuration Release \
  --no-restore \
  --output "$feed" \
  -p:PackageId=GraphQLParser \
  -p:PackageVersion=0.0.0-verify

package="$feed/GraphQLParser.0.0.0-verify.nupkg"
test -f "$package"
contents="$(unzip -Z1 "$package")"
grep -Fxq 'lib/net10.0/GraphQLParser.dll' <<<"$contents"
grep -Fxq 'lib/net10.0/GraphQLParser.xml' <<<"$contents"
grep -Fxq 'package-readme.md' <<<"$contents"
unexpected_assemblies="$(grep -E '^lib/[^/]+/[^/]+\.dll$' <<<"$contents" | grep -v '^lib/net10.0/GraphQLParser\.dll$' || true)"
if [[ -n "$unexpected_assemblies" ]]; then
  echo "Unexpected package assemblies:" >&2
  echo "$unexpected_assemblies" >&2
  exit 1
fi

consumer="$repo_root/tests/GraphQLParser.PackageConsumer/GraphQLParser.PackageConsumer.csproj"
dotnet restore "$consumer" --source "$feed" --packages "$verify_root/packages" --force --no-cache
dotnet build "$consumer" --configuration Release --no-restore
dotnet run --project "$consumer" --configuration Release --no-build --no-restore

echo "Package contents verified: net10.0/GraphQLParser.dll and XML documentation only."
