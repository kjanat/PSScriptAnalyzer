#!/usr/bin/env bash
set -euo pipefail

project_dir=$(cd "$(dirname "${BASH_SOURCE[0]}")/.." && pwd)
repo_dir=$(cd "$project_dir/.." && pwd)
plugin="$project_dir/bin/Release/net8.0/wasi-wasm/AppBundle/plugin.wasm"

cd "$repo_dir"
mise exec -- dotnet publish Formatter.Dprint/Formatter.Dprint.csproj \
  -c Release \
  --source https://api.nuget.org/v3/index.json

version=$(mise exec -- dotnet msbuild Formatter.Dprint/Formatter.Dprint.csproj \
  -nologo \
  -getProperty:Version)
node Formatter.Dprint/scripts/check-plugin.mjs "$plugin" "$version" LICENSE
node Formatter.Dprint/scripts/generate-schema.mjs \
  "$plugin" \
  Formatter.Dprint/schema.json \
  --check

cd Formatter.Dprint/tests
input=$(< fixtures/input.ps1)
expected=$(< fixtures/expected.ps1)
formatted=$(printf '%s' "$input" | dprint fmt --stdin input.ps1 --config dprint.json)
second=$(printf '%s' "$formatted" | dprint fmt --stdin input.ps1 --config dprint.json)

if [[ "$formatted" != "$expected" ]]; then
  echo "dprint output did not match the fixture" >&2
  exit 1
fi
if [[ "$second" != "$formatted" ]]; then
  echo "dprint formatting was not idempotent" >&2
  exit 1
fi

set +e
unknown_output=$(dprint check --config unknown-config.json fixtures/input.ps1 2>&1)
unknown_status=$?
invalid_output=$(printf '\377' | dprint fmt --stdin invalid.ps1 --config dprint.json 2>&1)
invalid_status=$?
set -e

if [[ $unknown_status -eq 0 || "$unknown_output" != *"Unknown property. (unknownProperty)"* ]]; then
  echo "unknown configuration key was not diagnosed" >&2
  exit 1
fi
if [[ $invalid_status -eq 0 || "$invalid_output" != *"valid UTF-8"* ]]; then
  echo "invalid UTF-8 was not rejected" >&2
  exit 1
fi

echo "dprint plugin checks passed"
