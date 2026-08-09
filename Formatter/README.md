# Portable PowerShell formatter

The formatter projects are grouped here so their portable runtime boundary is visible in the
repository layout:

- `Core` contains the parser-backed C# formatter and its public .NET API.
- `Core.Tests` runs the dependency-free native formatter checks.
- `Wasm` publishes the browser and Node.js AppBundle and npm package.
- `Dprint` publishes the directly loadable dprint `plugin.wasm`.

Both WebAssembly hosts call the same `Core` implementation. Neither creates a PowerShell runspace
or executes the source being formatted.

Build all formatter targets through the repository build module:

```sh
mise exec -- pwsh -File ./build.ps1 -Formatter -Configuration Release
```

`./build.ps1 -All` also includes the formatter targets after building the PowerShell 5 and 7 module
variants.

Format supported repository files from the root with the pinned toolchain:

```sh
mise exec -- dprint fmt
```

The root `.dprint.jsonc` loads the plugin produced by the formatter build above and applies the
repository's Allman, four-space PowerShell style. Native dprint plugins handle JSON, Markdown,
JavaScript/TypeScript, YAML, and shell files; `dprint-plugin-exec` delegates C# and MSBuild XML to
CSharpier, C/C++ to clang-format, and TOML to tombi.

See [WebAssembly formatter development](../docs/FormatterWasm.md) for architecture, build,
validation, and release details.
