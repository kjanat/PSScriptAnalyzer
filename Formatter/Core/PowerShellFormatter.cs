using System.Management.Automation.Language;
using System.Text;

namespace Microsoft.PowerShell.ScriptAnalyzer.Formatter;

/// <summary>Formats PowerShell source text without creating or using a PowerShell runspace.</summary>
public static class PowerShellFormatter
{
    private const TokenFlags OperatorFlags =
        TokenFlags.AssignmentOperator | TokenFlags.BinaryOperator;

    /// <summary>Formats a complete PowerShell source string.</summary>
    /// <param name="source">The PowerShell source text to format.</param>
    /// <param name="options">Formatting options, or <see langword="null"/> for defaults.</param>
    /// <returns>
    /// The formatted text and parser errors. Input containing parser errors is returned unchanged.
    /// </returns>
    /// <exception cref="ArgumentNullException"><paramref name="source"/> is null.</exception>
    /// <exception cref="ArgumentOutOfRangeException">
    /// <see cref="FormatterOptions.IndentSize"/> is outside the range 0 through 32.
    /// </exception>
    public static FormatterResult Format(string source, FormatterOptions? options = null)
    {
        ArgumentNullException.ThrowIfNull(source);
        options ??= new FormatterOptions();
        if (options.IndentSize < 0 || options.IndentSize > 32)
        {
            throw new ArgumentOutOfRangeException(
                nameof(options),
                "IndentSize must be between 0 and 32."
            );
        }

        var (_, _, initialErrors) = Parse(source);
        if (initialErrors.Length > 0)
        {
            return new FormatterResult(source, ToErrors(initialErrors));
        }

        var text = FormatBraces(source, options);
        text = FormatWhitespace(text, options);
        text = FormatIndentation(text, options);
        if (options.CorrectKeywordCasing)
        {
            text = FormatCasing(text);
        }

        var (_, _, finalErrors) = Parse(text);
        return new FormatterResult(text, ToErrors(finalErrors));
    }

    private static string FormatCasing(string source)
    {
        var (_, tokens, _) = Parse(source);
        var edits = tokens
            .Where(token =>
                (token.TokenFlags & (TokenFlags.Keyword | OperatorFlags)) != 0
                && token.Text.Any(char.IsUpper)
            )
            .Select(token => new TextEdit(
                token.Extent.StartOffset,
                token.Extent.EndOffset,
                token.Text.ToLowerInvariant()
            ));
        return TextEdits.Apply(source, edits);
    }

    private static string FormatBraces(string source, FormatterOptions options)
    {
        var (ast, tokens, _) = Parse(source);
        var newLine = DetectNewLine(source);
        var hashtableBraces = ast.FindAll(
                node => node is HashtableAst,
                searchNestedScriptBlocks: true
            )
            .Cast<HashtableAst>()
            .SelectMany(table => new[] { table.Extent.StartOffset, table.Extent.EndOffset - 1 })
            .ToHashSet();
        var edits = new List<TextEdit>();

        for (var index = 0; index < tokens.Length; index++)
        {
            var token = tokens[index];
            if (
                token.Kind == TokenKind.LCurly
                && !hashtableBraces.Contains(token.Extent.StartOffset)
            )
            {
                var previous = PreviousSignificant(tokens, index);
                var next = NextSignificant(tokens, index);
                if (previous is not null && previous.Kind != TokenKind.NewLine)
                {
                    ReplaceWhitespaceBetween(
                        source,
                        previous,
                        token,
                        options.BraceStyle == BraceStyle.NextLine ? newLine : " ",
                        edits
                    );
                }

                if (
                    next is not null
                    && next.Kind != TokenKind.RCurly
                    && next.Kind != TokenKind.NewLine
                )
                {
                    ReplaceWhitespaceBetween(source, token, next, newLine, edits);
                }
            }
            else if (
                token.Kind == TokenKind.RCurly
                && !hashtableBraces.Contains(token.Extent.StartOffset)
            )
            {
                var previous = PreviousSignificant(tokens, index);
                var next = NextSignificant(tokens, index);
                if (
                    previous is not null
                    && previous.Kind is not (TokenKind.LCurly or TokenKind.NewLine)
                )
                {
                    ReplaceWhitespaceBetween(source, previous, token, newLine, edits);
                }

                if (
                    next is not null
                    && next.Kind != TokenKind.NewLine
                    && IsCuddledKeyword(next.Kind)
                )
                {
                    var separator = options.BraceStyle == BraceStyle.NextLine ? newLine : " ";
                    ReplaceWhitespaceBetween(source, token, next, separator, edits);
                }
            }
        }

        return TextEdits.Apply(source, edits);
    }

    private static string FormatWhitespace(string source, FormatterOptions options)
    {
        var (_, tokens, _) = Parse(source);
        var edits = new List<TextEdit>();
        for (var index = 0; index < tokens.Length; index++)
        {
            var token = tokens[index];
            if (options.SpaceAroundOperators && IsBinaryOrAssignmentOperator(token))
            {
                var previous = PreviousSignificant(tokens, index);
                var next = NextSignificant(tokens, index);
                if (previous is not null && previous.Kind != TokenKind.NewLine)
                {
                    ReplaceWhitespaceBetween(source, previous, token, " ", edits);
                }
                if (next is not null && next.Kind != TokenKind.NewLine)
                {
                    ReplaceWhitespaceBetween(source, token, next, " ", edits);
                }
            }
            else if (
                options.SpaceAroundPipe
                && token.Kind is TokenKind.Pipe or TokenKind.AndAnd or TokenKind.OrOr
            )
            {
                var previous = PreviousSignificant(tokens, index);
                var next = NextSignificant(tokens, index);
                if (previous is not null && previous.Kind != TokenKind.NewLine)
                {
                    ReplaceWhitespaceBetween(source, previous, token, " ", edits);
                }
                if (next is not null && next.Kind != TokenKind.NewLine)
                {
                    ReplaceWhitespaceBetween(source, token, next, " ", edits);
                }
            }
            else if (options.SpaceAfterSeparator && token.Kind is TokenKind.Comma or TokenKind.Semi)
            {
                var next = NextSignificant(tokens, index);
                if (next is not null && next.Kind != TokenKind.NewLine)
                {
                    ReplaceWhitespaceBetween(source, token, next, " ", edits);
                }
            }
        }

        return TextEdits.Apply(source, edits);
    }

    private static string FormatIndentation(string source, FormatterOptions options)
    {
        var (_, tokens, _) = Parse(source);
        var protectedLines = new HashSet<int>();
        foreach (
            var token in tokens.Where(token =>
                token.Kind != TokenKind.NewLine
                && token.Extent.EndLineNumber > token.Extent.StartLineNumber
            )
        )
        {
            for (
                var line = token.Extent.StartLineNumber + 1;
                line <= token.Extent.EndLineNumber;
                line++
            )
            {
                protectedLines.Add(line);
            }
        }

        var newLine = DetectNewLine(source);
        var hasTerminalNewLine = source.EndsWith("\n", StringComparison.Ordinal);
        var lines = source.Replace("\r\n", "\n", StringComparison.Ordinal).Split('\n');
        var tokensByLine = tokens
            .Where(token => token.Kind is not (TokenKind.NewLine or TokenKind.EndOfInput))
            .GroupBy(token => token.Extent.StartLineNumber)
            .ToDictionary(
                group => group.Key,
                group => group.OrderBy(token => token.Extent.StartOffset).ToArray()
            );
        var depth = 0;

        for (var lineIndex = 0; lineIndex < lines.Length; lineIndex++)
        {
            var lineNumber = lineIndex + 1;
            if (
                !tokensByLine.TryGetValue(lineNumber, out var lineTokens)
                || protectedLines.Contains(lineNumber)
            )
            {
                continue;
            }

            var first = lineTokens[0];
            var lineDepth = IsClosingDelimiter(first.Kind) ? Math.Max(0, depth - 1) : depth;
            var content = lines[lineIndex].TrimStart(' ', '\t');
            if (content.Length > 0)
            {
                lines[lineIndex] = MakeIndent(lineDepth, options) + content;
            }

            foreach (var token in lineTokens)
            {
                if (IsOpeningDelimiter(token.Kind))
                {
                    depth++;
                }
                else if (IsClosingDelimiter(token.Kind))
                {
                    depth = Math.Max(0, depth - 1);
                }
            }
        }

        var result = string.Join(newLine, lines);
        if (hasTerminalNewLine && !result.EndsWith(newLine, StringComparison.Ordinal))
        {
            result += newLine;
        }
        return result;
    }

    private static bool IsBinaryOrAssignmentOperator(Token token) =>
        (token.TokenFlags & OperatorFlags) != 0
        && token.Kind is not (TokenKind.DotDot or TokenKind.PlusPlus or TokenKind.MinusMinus);

    private static bool IsCuddledKeyword(TokenKind kind) =>
        kind is TokenKind.Else or TokenKind.ElseIf or TokenKind.Catch or TokenKind.Finally;

    private static bool IsOpeningDelimiter(TokenKind kind) =>
        kind
            is TokenKind.LCurly
                or TokenKind.AtCurly
                or TokenKind.LParen
                or TokenKind.AtParen
                or TokenKind.DollarParen
                or TokenKind.LBracket;

    private static bool IsClosingDelimiter(TokenKind kind) =>
        kind is TokenKind.RCurly or TokenKind.RParen or TokenKind.RBracket;

    private static Token? PreviousSignificant(Token[] tokens, int index)
    {
        for (var cursor = index - 1; cursor >= 0; cursor--)
        {
            if (tokens[cursor].Kind != TokenKind.Comment)
            {
                return tokens[cursor];
            }
        }
        return null;
    }

    private static Token? NextSignificant(Token[] tokens, int index)
    {
        for (var cursor = index + 1; cursor < tokens.Length; cursor++)
        {
            if (
                tokens[cursor].Kind != TokenKind.Comment
                && tokens[cursor].Kind != TokenKind.EndOfInput
            )
            {
                return tokens[cursor];
            }
        }
        return null;
    }

    private static void ReplaceWhitespaceBetween(
        string source,
        Token left,
        Token right,
        string replacement,
        ICollection<TextEdit> edits
    )
    {
        var start = left.Extent.EndOffset;
        var end = right.Extent.StartOffset;
        if (end < start)
        {
            return;
        }

        var current = source[start..end];
        if (current.Any(character => !char.IsWhiteSpace(character)) || current == replacement)
        {
            return;
        }
        edits.Add(new TextEdit(start, end, replacement));
    }

    private static string MakeIndent(int depth, FormatterOptions options) =>
        options.UseTabs ? new string('\t', depth) : new string(' ', depth * options.IndentSize);

    private static string DetectNewLine(string source) =>
        source.Contains("\r\n", StringComparison.Ordinal) ? "\r\n" : "\n";

    private static (ScriptBlockAst Ast, Token[] Tokens, ParseError[] Errors) Parse(string source)
    {
        var ast = Parser.ParseInput(source, out var tokens, out var errors);
        return (ast, tokens, errors);
    }

    private static IReadOnlyList<FormatterParseError> ToErrors(ParseError[] errors) =>
        errors
            .Select(error => new FormatterParseError(
                error.Message,
                error.ErrorId,
                error.Extent.StartOffset,
                error.Extent.EndOffset,
                error.Extent.StartLineNumber,
                error.Extent.StartColumnNumber
            ))
            .ToArray();
}
