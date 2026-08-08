# PowerShell formatter for WebAssembly

This package runs a parser-backed PowerShell formatter in browsers and Node.js. It does not create a runspace or execute the input script.

```js
import { format } from '@psscriptanalyzer/formatter-wasm';

const result = await format("IF($x-EQ 1){'yes'}");
console.log(result.text);
```

Formatting is skipped when PowerShell reports a parse error; those errors are returned in `result.errors`.

Options use camel-case names. For example, `{ braceStyle: "nextLine", indentSize: 2 }` selects Allman-style braces and two-space indentation.

## Scope

The portable core formats script-block braces, indentation, operator and separator whitespace, and keyword/operator casing. It intentionally has no dependency on PSScriptAnalyzer's cmdlet host, rule discovery, session state, filesystem, or command metadata.

This is a small browser-safe formatter core, not yet a byte-for-byte port of every `Invoke-Formatter` rule. Assignment alignment and command/parameter casing are the main remaining parity gaps; command casing will need an injected command catalog rather than a live PowerShell session.

Build the package with:

```sh
dotnet publish Formatter.Wasm/Formatter.Wasm.csproj -c Release
```

The publishable package is written to `Formatter.Wasm/bin/Release/net8.0/browser-wasm/AppBundle`.

See [WebAssembly formatter development](../docs/FormatterWasm.md) for the architecture, complete
API reference, parity details, testing, and troubleshooting.
