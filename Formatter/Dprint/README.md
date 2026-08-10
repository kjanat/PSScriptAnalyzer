# dprint PowerShell formatter plugin

This project compiles the parser-backed C# formatter into one `plugin.wasm` implementing dprint's
schema-version-4 WebAssembly ABI. Dprint loads the module directly; this is not a process plugin and
does not start `pwsh`, `dotnet`, Node.js, or another formatter process.

## Build

Install the repository-pinned tools and the .NET 8 experimental WASI workload:

```sh
mise install
mise exec -- dotnet workload install wasi-experimental \
  --skip-manifest-update \
  --source https://api.nuget.org/v3/index.json
```

Publish the plugin:

```sh
mise exec -- dotnet publish Formatter/Dprint/Formatter.Dprint.csproj \
  -c Release \
  --source https://api.nuget.org/v3/index.json
```

The dprint artifact is:

```text
Formatter/Dprint/bin/Release/net8.0/wasi-wasm/AppBundle/plugin.wasm
```

It contains the Mono runtime, `Formatter.Core`, the required PowerShell parser assemblies, and the
dprint protocol bridge. Its only host import is dprint's supported `env.fd_write` function.

## Use with dprint

Install the latest released plugin from the dprint registry:

```sh
dprint add kjanat/PSScriptAnalyzer
```

Then configure it in `dprint.json`:

```json
{
  "powershell": {
    "indentSize": 4,
    "braceStyle": "sameLine"
  }
}
```

Then run normal dprint commands:

```sh
dprint fmt script.ps1
dprint check .
```

The plugin matches `.ps1`, `.psm1`, and `.psd1` files. Configuration is described by
[`schema.json`](schema.json); dprint also reports unknown keys and invalid values as configuration
diagnostics.

For local development, replace the registry-installed plugin URL with the built module path:

```json
{
  "plugins": [
    "./Formatter/Dprint/bin/Release/net8.0/wasi-wasm/AppBundle/plugin.wasm"
  ]
}
```

## How the C# module works

`WasmSingleFileBundle` embeds the managed assemblies into the WASI module. A small native bridge
exports dprint's memory and formatter protocol and invokes the managed `Plugin` methods through
Mono's embedding API. The .NET WASI runtime normally imports a broad
`wasi_snapshot_preview1` surface, but dprint intentionally provides only its own plugin imports.
The bridge redirects those runtime calls to deterministic in-module implementations, so the final
module has no WASI host dependency.

The formatting path is:

```text
dprint
  -> plugin.wasm protocol exports
    -> native Mono bridge
      -> Formatter.Dprint.Plugin.Format
        -> Formatter.Core.PowerShellFormatter
          -> System.Management.Automation.Language.Parser
```

The native layer handles only dprint byte transfer, UTF-8 validation, configuration lifetime, and
managed-runtime invocation. Formatting policy remains in the same C# `Formatter.Core` assembly used
by the browser/Node AppBundle.

## Validate

Run the complete plugin suite:

```sh
mise exec -- Formatter/Dprint/scripts/e2e.sh
```

It checks the module's imports and required exports, metadata URLs, generated schema drift, a real
dprint fixture, idempotence, unknown-key diagnostics, and invalid UTF-8 handling.

`schema.json` is generated from the configuration contract embedded in the managed plugin. After
changing that contract, publish the module and regenerate the schema with:

```sh
node Formatter/Dprint/scripts/generate-schema.mjs \
  Formatter/Dprint/bin/Release/net8.0/wasi-wasm/AppBundle/plugin.wasm \
  Formatter/Dprint/schema.json
```

## Release

The plugin version is the `Version` property in `Formatter/Dprint/Formatter.Dprint.csproj`. Its Git
tag uses the `<Version>-dprint` namespace, while the published dprint version remains `Version`.
The schema URL contains the full release tag so the dprint proxy resolves the namespaced GitHub
release without falling back to a bare version tag.

The repository also contains PSScriptAnalyzer's historical tags. The release workflow therefore
refuses to publish unless the pushed tag exactly matches `<Version>-dprint`.

To publish a new immutable release:

1. Update `Version`, build the module, and regenerate `schema.json`.
2. Run `mise exec -- Formatter/Dprint/scripts/e2e.sh` and commit the version, schema, and related
   changes.
3. Create and push a signed `<Version>-dprint` tag for that exact signed commit.
4. Follow the `Release dprint PowerShell formatter` workflow through completion.
5. Verify that the GitHub release contains `plugin.wasm`, `schema.json`, `LICENSE`, and
   `checksums.txt`, then
   verify `dprint add kjanat/PSScriptAnalyzer` against the published release.

Released assets are immutable through the dprint registry's cache. Never replace an asset on an
existing release; fix it by incrementing `Version` and publishing a new release.
