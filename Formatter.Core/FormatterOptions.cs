namespace Microsoft.PowerShell.ScriptAnalyzer.Formatter;

public sealed class FormatterOptions
{
    public BraceStyle BraceStyle { get; set; } = BraceStyle.SameLine;

    public int IndentSize { get; set; } = 4;

    public bool UseTabs { get; set; }

    public bool CorrectKeywordCasing { get; set; } = true;

    public bool SpaceAroundOperators { get; set; } = true;

    public bool SpaceAroundPipe { get; set; } = true;

    public bool SpaceAfterSeparator { get; set; } = true;
}

public enum BraceStyle
{
    SameLine,
    NextLine,
}
