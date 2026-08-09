import { dotnet } from "./_framework/dotnet.js";

let formatterPromise;

/** Load and cache the .NET WebAssembly runtime and exported formatter. */
async function getFormatter() {
  formatterPromise ??= dotnet.create().then(async runtime => {
    const config = runtime.getConfig();
    const exports = await runtime.getAssemblyExports(config.mainAssemblyName);
    return exports.Microsoft.PowerShell.ScriptAnalyzer.Formatter.Wasm.Program;
  });
  return formatterPromise;
}

/**
 * Format a complete PowerShell source string.
 *
 * Input containing PowerShell parser errors is returned unchanged and the
 * errors are included in the result.
 *
 * @param {string} source PowerShell source text.
 * @param {object} [options={}] Camel-case formatter options.
 * @returns {Promise<{text: string, errors: Array<object>}>} The formatter result.
 * @throws {TypeError} If source is not a string.
 */
export async function format(source, options = {}) {
  if (typeof source !== "string") {
    throw new TypeError("source must be a string");
  }

  const formatter = await getFormatter();
  return JSON.parse(formatter.Format(source, JSON.stringify(options)));
}
