namespace Microsoft.PowerShell.ScriptAnalyzer.Formatter;

/// <summary>
/// Contains the formatted script and any PowerShell parser errors. When <see cref="Errors"/> is
/// nonempty, <see cref="Text"/> is the unchanged input.
/// </summary>
/// <param name="Text">The formatted script, or the original script when parsing failed.</param>
/// <param name="Errors">PowerShell parser errors reported for the input or formatted output.</param>
public sealed record FormatterResult(string Text, IReadOnlyList<FormatterParseError> Errors);

/// <summary>Describes a PowerShell parser error using offsets and one-based source coordinates.</summary>
/// <param name="Message">The human-readable parser message.</param>
/// <param name="ErrorId">The stable PowerShell parser error identifier.</param>
/// <param name="StartOffset">The zero-based start offset in the source string.</param>
/// <param name="EndOffset">The exclusive zero-based end offset in the source string.</param>
/// <param name="StartLine">The one-based source line.</param>
/// <param name="StartColumn">The one-based source column.</param>
public sealed record FormatterParseError(
    string Message,
    string ErrorId,
    int StartOffset,
    int EndOffset,
    int StartLine,
    int StartColumn
);
