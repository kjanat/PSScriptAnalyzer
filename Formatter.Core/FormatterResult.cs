namespace Microsoft.PowerShell.ScriptAnalyzer.Formatter;

public sealed record FormatterResult(string Text, IReadOnlyList<FormatterParseError> Errors);

public sealed record FormatterParseError(
    string Message,
    string ErrorId,
    int StartOffset,
    int EndOffset,
    int StartLine,
    int StartColumn);
