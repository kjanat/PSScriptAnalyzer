using System.Diagnostics.CodeAnalysis;
using System.Text;
using System.Text.Encodings.Web;
using System.Text.Json;
using Microsoft.PowerShell.ScriptAnalyzer.Formatter;

namespace Microsoft.PowerShell.ScriptAnalyzer.Formatter.Dprint;

public static class Program
{
    [DynamicDependency(DynamicallyAccessedMemberTypes.PublicMethods, typeof(Plugin))]
    public static void Main()
    {
    }
}

public static class Plugin
{
    private static readonly HashSet<string> KnownProperties =
    [
        "braceStyle",
        "indentSize",
        "useTabs",
        "correctKeywordCasing",
        "spaceAroundOperators",
        "spaceAroundPipe",
        "spaceAfterSeparator",
    ];

    public static string Format(string source, string configJson, string overrideConfigJson)
    {
        var result = PowerShellFormatter.Format(source, ParseOptions(configJson, overrideConfigJson));
        return result.Text;
    }

    public static string GetConfigDiagnostics(string configJson)
    {
        var diagnostics = new List<(string PropertyName, string Message)>();
        try
        {
            using var document = JsonDocument.Parse(configJson);
            if (document.RootElement.TryGetProperty("plugin", out var plugin) && plugin.ValueKind == JsonValueKind.Object)
            {
                foreach (var property in plugin.EnumerateObject())
                {
                    if (!KnownProperties.Contains(property.Name))
                    {
                        diagnostics.Add((property.Name, "Unknown property."));
                    }
                }

                ValidateStringChoice(plugin, "braceStyle", ["sameLine", "nextLine"], diagnostics);
                ValidateInteger(plugin, "indentSize", 0, 32, diagnostics);
                ValidateBoolean(plugin, "useTabs", diagnostics);
                ValidateBoolean(plugin, "correctKeywordCasing", diagnostics);
                ValidateBoolean(plugin, "spaceAroundOperators", diagnostics);
                ValidateBoolean(plugin, "spaceAroundPipe", diagnostics);
                ValidateBoolean(plugin, "spaceAfterSeparator", diagnostics);
            }
        }
        catch (JsonException exception)
        {
            diagnostics.Add(("", exception.Message));
        }

        return SerializeDiagnostics(diagnostics);
    }

    public static string GetResolvedConfig(string configJson)
    {
        var options = ParseOptions(configJson, "");
        var braceStyle = options.BraceStyle == BraceStyle.NextLine ? "nextLine" : "sameLine";
        return $$"""
            {"braceStyle":"{{braceStyle}}","indentSize":{{options.IndentSize}},"useTabs":{{Boolean(options.UseTabs)}},"correctKeywordCasing":{{Boolean(options.CorrectKeywordCasing)}},"spaceAroundOperators":{{Boolean(options.SpaceAroundOperators)}},"spaceAroundPipe":{{Boolean(options.SpaceAroundPipe)}},"spaceAfterSeparator":{{Boolean(options.SpaceAfterSeparator)}}}
            """;
    }

    public static string GetConfigSchema() => """
        {
          "$schema": "http://json-schema.org/draft-07/schema#",
          "title": "dprint PowerShell formatter configuration",
          "type": "object",
          "additionalProperties": false,
          "properties": {
            "braceStyle": {
              "description": "Placement of script-block opening braces.",
              "type": "string",
              "enum": ["sameLine", "nextLine"],
              "default": "sameLine"
            },
            "indentSize": {
              "description": "Spaces in one indentation level when tabs are disabled.",
              "type": "integer",
              "minimum": 0,
              "maximum": 32,
              "default": 4
            },
            "useTabs": {
              "description": "Use one tab per indentation level.",
              "type": "boolean",
              "default": false
            },
            "correctKeywordCasing": {
              "description": "Lowercase PowerShell keywords and operators.",
              "type": "boolean",
              "default": true
            },
            "spaceAroundOperators": {
              "description": "Add spaces around binary and assignment operators.",
              "type": "boolean",
              "default": true
            },
            "spaceAroundPipe": {
              "description": "Add spaces around pipeline and pipeline-chain operators.",
              "type": "boolean",
              "default": true
            },
            "spaceAfterSeparator": {
              "description": "Add a space after commas and semicolons.",
              "type": "boolean",
              "default": true
            }
          }
        }
        """;

    private static FormatterOptions ParseOptions(string configJson, string overrideConfigJson)
    {
        var options = new FormatterOptions();
        if (!string.IsNullOrWhiteSpace(configJson))
        {
            using var document = JsonDocument.Parse(configJson);
            var root = document.RootElement;
            if (root.TryGetProperty("global", out var global))
            {
                ApplyBoolean(global, "useTabs", value => options.UseTabs = value);
                ApplyInteger(global, "indentWidth", value => options.IndentSize = value);
            }
            if (root.TryGetProperty("plugin", out var plugin))
            {
                ApplyPluginOptions(plugin, options);
            }
        }

        if (!string.IsNullOrWhiteSpace(overrideConfigJson))
        {
            using var overrideDocument = JsonDocument.Parse(overrideConfigJson);
            ApplyPluginOptions(overrideDocument.RootElement, options);
        }

        return options;
    }

    private static void ApplyPluginOptions(JsonElement plugin, FormatterOptions options)
    {
        ApplyInteger(plugin, "indentSize", value => options.IndentSize = value);
        ApplyBoolean(plugin, "useTabs", value => options.UseTabs = value);
        ApplyBoolean(plugin, "correctKeywordCasing", value => options.CorrectKeywordCasing = value);
        ApplyBoolean(plugin, "spaceAroundOperators", value => options.SpaceAroundOperators = value);
        ApplyBoolean(plugin, "spaceAroundPipe", value => options.SpaceAroundPipe = value);
        ApplyBoolean(plugin, "spaceAfterSeparator", value => options.SpaceAfterSeparator = value);

        if (plugin.TryGetProperty("braceStyle", out var braceStyle) && braceStyle.ValueKind == JsonValueKind.String)
        {
            options.BraceStyle = braceStyle.GetString() switch
            {
                "nextLine" => BraceStyle.NextLine,
                _ => BraceStyle.SameLine,
            };
        }

    }

    private static void ValidateBoolean(
        JsonElement config,
        string name,
        ICollection<(string PropertyName, string Message)> diagnostics)
    {
        if (config.TryGetProperty(name, out var value) &&
            value.ValueKind is not (JsonValueKind.True or JsonValueKind.False))
        {
            diagnostics.Add((name, "Expected a boolean value."));
        }
    }

    private static void ValidateInteger(
        JsonElement config,
        string name,
        int minimum,
        int maximum,
        ICollection<(string PropertyName, string Message)> diagnostics)
    {
        if (!config.TryGetProperty(name, out var value))
        {
            return;
        }
        if (value.ValueKind != JsonValueKind.Number || !value.TryGetInt32(out var integer))
        {
            diagnostics.Add((name, "Expected an integer value."));
        }
        else if (integer < minimum || integer > maximum)
        {
            diagnostics.Add((name, $"Expected a value from {minimum} through {maximum}."));
        }
    }

    private static void ValidateStringChoice(
        JsonElement config,
        string name,
        IReadOnlyCollection<string> choices,
        ICollection<(string PropertyName, string Message)> diagnostics)
    {
        if (!config.TryGetProperty(name, out var value))
        {
            return;
        }
        if (value.ValueKind != JsonValueKind.String || !choices.Contains(value.GetString()))
        {
            diagnostics.Add((name, $"Expected one of: {string.Join(", ", choices)}."));
        }
    }

    private static string SerializeDiagnostics(IEnumerable<(string PropertyName, string Message)> diagnostics)
    {
        var json = new StringBuilder("[");
        var first = true;
        foreach (var diagnostic in diagnostics)
        {
            if (!first)
            {
                json.Append(',');
            }
            first = false;
            json.Append("{\"propertyName\":\"")
                .Append(JavaScriptEncoder.Default.Encode(diagnostic.PropertyName))
                .Append("\",\"message\":\"")
                .Append(JavaScriptEncoder.Default.Encode(diagnostic.Message))
                .Append("\"}");
        }
        return json.Append(']').ToString();
    }

    private static string Boolean(bool value) => value ? "true" : "false";

    private static void ApplyBoolean(JsonElement config, string name, Action<bool> apply)
    {
        if (config.TryGetProperty(name, out var value) && value.ValueKind is JsonValueKind.True or JsonValueKind.False)
        {
            apply(value.GetBoolean());
        }
    }

    private static void ApplyInteger(JsonElement config, string name, Action<int> apply)
    {
        if (config.TryGetProperty(name, out var value) &&
            value.ValueKind == JsonValueKind.Number &&
            value.TryGetInt32(out var integer))
        {
            apply(integer);
        }
    }
}
