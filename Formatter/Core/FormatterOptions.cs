namespace Microsoft.PowerShell.ScriptAnalyzer.Formatter;

/// <summary>
/// Controls the formatting passes applied by <see cref="PowerShellFormatter"/>.
/// </summary>
public sealed class FormatterOptions
{
    /// <summary>Gets or sets where script-block opening braces are placed.</summary>
    public BraceStyle BraceStyle { get; set; } = BraceStyle.SameLine;

    /// <summary>
    /// Gets or sets the number of spaces in one indentation level. Valid values are 0 through 32.
    /// This value is ignored when <see cref="UseTabs"/> is <see langword="true"/>.
    /// </summary>
    public int IndentSize { get; set; } = 4;

    /// <summary>Gets or sets whether indentation levels use tabs instead of spaces.</summary>
    public bool UseTabs { get; set; }

    /// <summary>Gets or sets whether PowerShell keywords and operators are lowercased.</summary>
    public bool CorrectKeywordCasing { get; set; } = true;

    /// <summary>Gets or sets whether binary and assignment operators have surrounding spaces.</summary>
    public bool SpaceAroundOperators { get; set; } = true;

    /// <summary>Gets or sets whether pipeline and pipeline-chain operators have surrounding spaces.</summary>
    public bool SpaceAroundPipe { get; set; } = true;

    /// <summary>Gets or sets whether commas and semicolons are followed by a space.</summary>
    public bool SpaceAfterSeparator { get; set; } = true;
}

/// <summary>Specifies the placement of script-block opening braces.</summary>
public enum BraceStyle
{
    /// <summary>Place the opening brace on the same line as the preceding token.</summary>
    SameLine,

    /// <summary>Place the opening brace on the following line.</summary>
    NextLine,
}
