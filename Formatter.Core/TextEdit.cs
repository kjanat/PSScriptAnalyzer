namespace Microsoft.PowerShell.ScriptAnalyzer.Formatter;

internal readonly record struct TextEdit(int Start, int End, string Text);

internal static class TextEdits
{
    public static string Apply(string source, IEnumerable<TextEdit> edits)
    {
        var ordered = edits
            .Where(edit => edit.Start >= 0 && edit.End >= edit.Start && edit.End <= source.Length)
            .Distinct()
            .OrderByDescending(edit => edit.Start)
            .ThenByDescending(edit => edit.End)
            .ToArray();

        var previousStart = source.Length;
        foreach (var edit in ordered)
        {
            if (edit.End > previousStart)
            {
                continue;
            }

            source = string.Concat(source.AsSpan(0, edit.Start), edit.Text, source.AsSpan(edit.End));
            previousStart = edit.Start;
        }

        return source;
    }
}
