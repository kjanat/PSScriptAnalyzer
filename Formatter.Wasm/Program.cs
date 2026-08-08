using System.Runtime.InteropServices.JavaScript;
using System.Runtime.Versioning;
using System.Text.Json;
using Microsoft.PowerShell.ScriptAnalyzer.Formatter;

namespace Microsoft.PowerShell.ScriptAnalyzer.Formatter.Wasm;

[SupportedOSPlatform("browser")]
public partial class Program
{
    public static void Main()
    {
    }

    [JSExport]
    internal static string Format(string source, string optionsJson)
    {
        var options = string.IsNullOrWhiteSpace(optionsJson)
            ? new FormatterOptions()
            : JsonSerializer.Deserialize(optionsJson, FormatterJsonContext.Default.FormatterOptions)
                ?? new FormatterOptions();
        var result = PowerShellFormatter.Format(source, options);
        return JsonSerializer.Serialize(result, FormatterJsonContext.Default.FormatterResult);
    }
}
