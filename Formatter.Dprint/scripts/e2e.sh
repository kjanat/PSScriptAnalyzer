#!/usr/bin/env sh
set -eu

crate_dir=$(CDPATH= cd -- "$(dirname -- "$0")/.." && pwd)
artifact="$crate_dir/target/wasm32-unknown-unknown/wasm-release/dprint_plugin_powershell.wasm"
work_dir=$(mktemp -d)
trap 'rm -rf "$work_dir"' EXIT

cargo build --manifest-path "$crate_dir/Cargo.toml" --profile wasm-release --target wasm32-unknown-unknown
cp "$crate_dir/tests/fixtures/input.ps1" "$work_dir/input.ps1"

config_file="$work_dir/dprint.json"
printf '{"powerShell":{},"plugins":["%s"]}\n' "$artifact" >"$config_file"
dprint fmt --config "$config_file" "$work_dir/input.ps1"
diff -u "$crate_dir/tests/fixtures/expected.ps1" "$work_dir/input.ps1"
dprint check --config "$config_file" "$work_dir/input.ps1"
