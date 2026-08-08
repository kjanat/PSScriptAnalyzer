import { dotnet } from "./_framework/dotnet.js";

let formatterPromise;

async function getFormatter() {
    formatterPromise ??= dotnet.create().then(async runtime => {
        const config = runtime.getConfig();
        const exports = await runtime.getAssemblyExports(config.mainAssemblyName);
        return exports.Microsoft.PowerShell.ScriptAnalyzer.Formatter.Wasm.Program;
    });
    return formatterPromise;
}

export async function format(source, options = {}) {
    if (typeof source !== "string") {
        throw new TypeError("source must be a string");
    }

    const formatter = await getFormatter();
    return JSON.parse(formatter.Format(source, JSON.stringify(options)));
}
