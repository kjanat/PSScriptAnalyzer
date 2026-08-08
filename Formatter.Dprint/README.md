# dprint PowerShell plugin

This is the standalone dprint WebAssembly adapter for the portable PowerShell formatter. It is a
single sandboxed `plugin.wasm`; it does not use the .NET browser runtime or a process plugin.

Build and test it with:

```sh
cargo test --manifest-path Formatter.Dprint/Cargo.toml
cargo build --manifest-path Formatter.Dprint/Cargo.toml --profile wasm-release --target wasm32-unknown-unknown
Formatter.Dprint/scripts/e2e.sh
```

Use the local artifact in `dprint.json`:

```jsonc
{
	"powerShell": {
		"braceStyle": "sameLine",
		"indentWidth": 4
	},
	"plugins": [
		"./Formatter.Dprint/target/wasm32-unknown-unknown/wasm-release/dprint_plugin_powershell.wasm"
	]
}
```

The plugin formats `.ps1`, `.psm1`, and `.psd1` files. It inherits `indentWidth` and `useTabs` from
dprint's global configuration and returns no change for invalid PowerShell syntax or range-format
requests.
