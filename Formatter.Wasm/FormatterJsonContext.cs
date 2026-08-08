using System.Text.Json;
using System.Text.Json.Serialization;
using Microsoft.PowerShell.ScriptAnalyzer.Formatter;

namespace Microsoft.PowerShell.ScriptAnalyzer.Formatter.Wasm;

[JsonSourceGenerationOptions(
    JsonSerializerDefaults.Web,
    Converters = new[] { typeof(JsonStringEnumConverter<BraceStyle>) })]
[JsonSerializable(typeof(FormatterOptions))]
[JsonSerializable(typeof(FormatterResult))]
internal partial class FormatterJsonContext : JsonSerializerContext
{
}
