# WebAssembly formatter development

The WebAssembly formatter provides PowerShell-aware formatting in browsers, Node.js, and dprint
without starting a PowerShell runspace. It uses PowerShell's parser for token and syntax
information, but keeps formatting policy in a small host-independent assembly.

## Repository layout

- `Formatter/Core` contains the formatter, options, result types, and text-edit implementation. It
  depends on `System.Management.Automation` for the parser and has no dependency on the existing
  PSScriptAnalyzer Engine or Rules projects.
- `Formatter/Wasm` contains the browser-WASM host, JSON serialization boundary, JavaScript module,
  and npm package metadata.
- `Formatter/Dprint` contains the single-file .NET WASI module, dprint schema-version-4 ABI bridge,
  configuration schema, and end-to-end dprint checks.
- `Formatter/Core.Tests` is a dependency-free native test executable covering representative
  formatting and error cases.

The call path is:

```text
JavaScript format(source, options)
  -> JSExport string boundary
    -> PowerShellFormatter.Format
      -> System.Management.Automation.Language.Parser
```

The dprint call path reuses the same formatter:

```text
dprint -> plugin.wasm -> native Mono bridge -> PowerShellFormatter.Format
```

The WebAssembly boundary only passes strings. Options enter as JSON and results leave as JSON,
which avoids exposing managed objects or PowerShell runtime types to JavaScript.

## Build

Build the complete formatter family through the repository build entrypoint:

```sh
mise exec -- pwsh -File ./build.ps1 -Formatter -Configuration Release
```

The existing `./build.ps1 -All` path also builds these targets once after its PowerShell 5 and 7
module builds.

Use the .NET SDK selected by `global.json` and install the WebAssembly workload once:

```sh
dotnet workload install wasm-tools
dotnet publish Formatter/Wasm/Formatter.Wasm.csproj -c Release
```

The publishable npm package is written to:

```text
Formatter/Wasm/bin/Release/net8.0/browser-wasm/AppBundle
```

The dprint plugin is built separately as one directly loadable module. The tool versions are pinned
in `mise.toml`:

```sh
mise install
mise exec -- dotnet workload install wasi-experimental \
  --skip-manifest-update \
  --source https://api.nuget.org/v3/index.json
mise exec -- dotnet publish Formatter/Dprint/Formatter.Dprint.csproj \
  -c Release \
  --source https://api.nuget.org/v3/index.json
```

Its release artifact is
`Formatter/Dprint/bin/Release/net8.0/wasi-wasm/AppBundle/plugin.wasm`. Unlike the browser AppBundle,
it embeds the managed assemblies into the module and implements dprint's exported memory/protocol
ABI. Runtime WASI calls are resolved inside the module; its only host import is `env.fd_write`,
which dprint provides.

`System.Management.Automation` 7.4 does not provide a `browser-wasm` runtime asset. The WASM project
therefore references its Unix .NET 8 implementation explicitly. The implementation is compatible
with browser WASM for the parser-only surface used here. Publishing trims unused managed code and
uses invariant globalization to reduce the bundle.

PowerShell initializes its built-in CIM type accelerators through reflection while parsing typed
scripts. The direct dprint bundle therefore preserves the required
`Microsoft.Management.Infrastructure` types and explicitly embeds the Unix runtime assembly after
WASI dependency resolution; otherwise real scripts with attributes or type constraints fail before
formatting.

The .NET trimmer reports warnings from code elsewhere in `System.Management.Automation` and its
dependencies. These warnings are expected for the parser-only build; the formatter paths are
covered by native and WASM execution tests.

## JavaScript API

Import the module from the published package and await `format`:

```js
import { format } from "@psscriptanalyzer/formatter-wasm";

const result = await format("IF($value-EQ 1){'yes'}", {
  braceStyle: "sameLine",
  indentSize: 4,
});

if (result.errors.length === 0) {
  console.log(result.text);
}
```

Runtime initialization is lazy and cached. The first call loads .NET and the formatter assemblies;
later calls reuse that runtime.

### Options

| JavaScript property    | Type                         | Default      | Effect                                        |
| ---------------------- | ---------------------------- | ------------ | --------------------------------------------- |
| `braceStyle`           | `"sameLine"` or `"nextLine"` | `"sameLine"` | Places script-block opening braces.           |
| `indentSize`           | integer from 0 through 32    | `4`          | Sets spaces per indentation level.            |
| `useTabs`              | boolean                      | `false`      | Uses one tab per indentation level.           |
| `correctKeywordCasing` | boolean                      | `true`       | Lowercases PowerShell keywords and operators. |
| `spaceAroundOperators` | boolean                      | `true`       | Spaces binary and assignment operators.       |
| `spaceAroundPipe`      | boolean                      | `true`       | Spaces pipeline and pipeline-chain operators. |
| `spaceAfterSeparator`  | boolean                      | `true`       | Spaces after commas and semicolons.           |

When `useTabs` is true, `indentSize` does not affect indentation.

### Result

`format` resolves to an object with these properties:

- `text`: formatted PowerShell source, or unchanged input when parsing fails.
- `errors`: PowerShell parser diagnostics. An empty array means parsing succeeded.

Each parser error contains `message`, `errorId`, `startOffset`, `endOffset`, `startLine`, and
`startColumn`. Offsets are zero-based; lines and columns are one-based.

Passing a non-string source throws `TypeError`. An `indentSize` outside 0 through 32 rejects the
format operation with a managed argument error.

## .NET API

Projects that can host .NET directly may reference `Formatter/Core` without using WebAssembly:

```csharp
using Microsoft.PowerShell.ScriptAnalyzer.Formatter;

FormatterResult result = PowerShellFormatter.Format(
    "function Test { 'ok' }",
    new FormatterOptions { BraceStyle = BraceStyle.NextLine, IndentSize = 2 }
);
```

`PowerShellFormatter.Format` never executes the source. If the initial parser pass reports an
error, it returns the input unchanged with those diagnostics. It also reparses the formatted output
and returns any resulting errors.

## Formatting scope and PSScriptAnalyzer parity

The formatter deliberately does not load the current `Formatter`, `ScriptAnalyzer`, or Rules
assemblies. Those components assume a cmdlet host, a live session state, reflection-based rule
discovery, and filesystem-backed settings. Keeping those dependencies outside the portable core is
the main isolation boundary.

| Existing default rule        | Portable support                                            |
| ---------------------------- | ----------------------------------------------------------- |
| `PSPlaceOpenBrace`           | Script-block brace placement; one-line blocks are expanded. |
| `PSPlaceCloseBrace`          | Closing-brace placement and cuddled branch keywords.        |
| `PSUseConsistentWhitespace`  | Operators, pipelines, commas, and semicolons.               |
| `PSUseConsistentIndentation` | Brace-depth indentation with tabs or spaces.                |
| `PSAlignAssignmentStatement` | Not implemented.                                            |
| `PSUseCorrectCasing`         | Keywords and operators only.                                |

Command and parameter casing is not available because the existing rule obtains canonical names
from a live PowerShell session. Portable support should use an injected command catalog rather than
reintroducing a runspace. Range formatting and PSScriptAnalyzer settings files are also not yet
supported.

Multiline token contents, including here-strings, are protected from indentation rewriting.
Hashtable braces remain inline while whitespace inside hashtables can still be normalized.

## Security boundary

The formatter parses text and applies offset-based text edits. It does not invoke commands, evaluate
expressions, import modules, inspect the filesystem, or query command metadata. Consumers should
still treat formatted text as untrusted source code: formatting does not validate that a script is
safe to execute.

## Validate changes

Run the native checks:

```sh
dotnet run --project Formatter/Core.Tests/Formatter.Core.Tests.csproj
```

Publish the package, then test the actual WebAssembly entry point from the `AppBundle` directory:

```sh
node --input-type=module -e '
import("./index.mjs").then(async ({ format }) => {
    const first = await format("IF($x-EQ 1){\u0027yes\u0027}");
    const second = await format(first.text);
    if (first.errors.length || second.text !== first.text) process.exitCode = 1;
});'
```

The idempotence check catches edit ordering and reparsing regressions that a compile-only WASM test
would miss.

Run the direct dprint-module checks separately:

```sh
mise exec -- Formatter/Dprint/scripts/e2e.sh
```

That suite validates the actual `plugin.wasm` import/export surface and metadata, generated schema,
real dprint formatting, idempotence, configuration diagnostics, and invalid UTF-8 handling.

## Release namespaces

Dprint releases use `<version>-dprint` tags such as `0.1.1-dprint`. Browser and Node.js package
releases use the separate `<version>-npm` namespace.

The dprint proxy selects the newest non-draft, non-prerelease GitHub release when producing
`latest.json`. The npm workflow therefore publishes its GitHub release as a prerelease. This keeps
the npm tarball fully downloadable while preventing it from being mistaken for a dprint plugin
release. The dprint workflow explicitly marks its prefixed release as GitHub's latest release.
