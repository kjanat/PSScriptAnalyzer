use anyhow::{Context, Result};
use tree_sitter::Parser;

use crate::{BraceStyle, Configuration};

#[derive(Clone, Copy, Debug, Eq, PartialEq)]
enum Kind {
    Word,
    Operator,
    Pipe,
    Separator,
    OpenBrace,
    HashOpen,
    CloseBrace,
    Comment,
    Literal,
    Other,
}

#[derive(Clone, Debug)]
struct Token {
    start: usize,
    end: usize,
    kind: Kind,
}

#[derive(Clone, Debug)]
struct Edit {
    start: usize,
    end: usize,
    text: String,
}

pub fn format(source: &str, config: &Configuration) -> Result<String> {
    if !is_parseable(source)? {
        return Ok(source.to_string());
    }

    let mut text = format_braces(source, config);
    text = format_whitespace(&text, config);
    text = format_indentation(&text, config);
    if config.correct_keyword_casing {
        text = format_casing(&text);
    }
    Ok(text)
}

fn is_parseable(source: &str) -> Result<bool> {
    let mut parser = Parser::new();
    parser
        .set_language(&tree_sitter_pwsh::LANGUAGE.into())
        .context("failed loading the PowerShell parser")?;
    let tree = parser
        .parse(source, None)
        .context("PowerShell parser returned no syntax tree")?;
    Ok(!tree.root_node().has_error())
}

fn format_braces(source: &str, config: &Configuration) -> String {
    let tokens = tokenize(source);
    let newline = detect_newline(source);
    let mut edits = Vec::new();
    let mut hash_depth = Vec::new();
    let mut hash_closes = std::collections::HashSet::new();

    for (index, token) in tokens.iter().enumerate() {
        match token.kind {
            Kind::HashOpen => hash_depth.push(true),
            Kind::OpenBrace => hash_depth.push(false),
            Kind::CloseBrace => {
                if hash_depth.pop() == Some(true) {
                    hash_closes.insert(index);
                }
            }
            _ => {}
        }
    }

    for (index, token) in tokens.iter().enumerate() {
        if token.kind == Kind::OpenBrace {
            if let Some(previous) = previous_token(&tokens, index) {
                replace_whitespace(
                    source,
                    previous,
                    token,
                    match config.brace_style {
                        BraceStyle::SameLine => " ",
                        BraceStyle::NextLine => newline,
                    },
                    &mut edits,
                );
            }
            if let Some(next) = next_token(&tokens, index)
                && next.kind != Kind::CloseBrace
            {
                replace_whitespace(source, token, next, newline, &mut edits);
            }
        } else if token.kind == Kind::CloseBrace && !hash_closes.contains(&index) {
            if let Some(previous) = previous_token(&tokens, index)
                && previous.kind != Kind::OpenBrace
            {
                replace_whitespace(source, previous, token, newline, &mut edits);
            }
            if let Some(next) = next_token(&tokens, index)
                && next.kind == Kind::Word
                && is_cuddled_keyword(&source[next.start..next.end])
            {
                replace_whitespace(source, token, next, " ", &mut edits);
            }
        }
    }

    apply_edits(source, edits)
}

fn format_whitespace(source: &str, config: &Configuration) -> String {
    let tokens = tokenize(source);
    let mut edits = Vec::new();
    for (index, token) in tokens.iter().enumerate() {
        let around = (token.kind == Kind::Operator && config.space_around_operators)
            || (token.kind == Kind::Pipe && config.space_around_pipe);
        if around {
            if let Some(previous) = previous_token(&tokens, index) {
                replace_whitespace(source, previous, token, " ", &mut edits);
            }
            if let Some(next) = next_token(&tokens, index) {
                replace_whitespace(source, token, next, " ", &mut edits);
            }
        } else if token.kind == Kind::Separator
            && config.space_after_separator
            && let Some(next) = next_token(&tokens, index)
        {
            replace_whitespace(source, token, next, " ", &mut edits);
        }
    }
    apply_edits(source, edits)
}

fn format_indentation(source: &str, config: &Configuration) -> String {
    let newline = detect_newline(source);
    let terminal_newline = source.ends_with('\n');
    let normalized = source.replace("\r\n", "\n");
    let tokens = tokenize(&normalized);
    let mut lines: Vec<String> = normalized.split('\n').map(str::to_string).collect();
    let mut depth = 0usize;

    for (line_index, line) in lines.iter_mut().enumerate() {
        let start = normalized
            .split_inclusive('\n')
            .take(line_index)
            .map(str::len)
            .sum::<usize>();
        let end = start + line.len();
        let line_tokens: Vec<_> = tokens
            .iter()
            .filter(|token| token.start >= start && token.start < end)
            .collect();
        if line_tokens.is_empty() {
            continue;
        }
        let first = line_tokens[0];
        let line_depth = if first.kind == Kind::CloseBrace {
            depth.saturating_sub(1)
        } else {
            depth
        };
        let content = line.trim_start_matches([' ', '\t']);
        if !content.is_empty() {
            let indent = if config.use_tabs {
                "\t".repeat(line_depth)
            } else {
                " ".repeat(line_depth * usize::from(config.indent_width))
            };
            *line = format!("{indent}{content}");
        }
        for token in line_tokens {
            match token.kind {
                Kind::OpenBrace | Kind::HashOpen => depth += 1,
                Kind::CloseBrace => depth = depth.saturating_sub(1),
                _ => {}
            }
        }
    }

    let mut result = lines.join(newline);
    if terminal_newline && !result.ends_with(newline) {
        result.push_str(newline);
    }
    result
}

fn format_casing(source: &str) -> String {
    let edits = tokenize(source)
        .into_iter()
        .filter(|token| token.kind == Kind::Word || token.kind == Kind::Operator)
        .filter_map(|token| {
            let text = &source[token.start..token.end];
            let lower = text.to_ascii_lowercase();
            (lower != text && (token.kind == Kind::Operator || is_keyword(text))).then_some(Edit {
                start: token.start,
                end: token.end,
                text: lower,
            })
        })
        .collect();
    apply_edits(source, edits)
}

fn tokenize(source: &str) -> Vec<Token> {
    let bytes = source.as_bytes();
    let mut tokens = Vec::new();
    let mut index = 0;
    while index < bytes.len() {
        if bytes[index].is_ascii_whitespace() {
            index += 1;
            continue;
        }
        let start = index;
        let (end, kind) = match bytes[index] {
            b'#' => (scan_until(bytes, index + 1, b'\n'), Kind::Comment),
            b'<' if bytes.get(index + 1) == Some(&b'#') => {
                (scan_pair(bytes, index + 2, b'#', b'>'), Kind::Comment)
            }
            b'\'' | b'"' => (scan_quoted(bytes, index, bytes[index]), Kind::Literal),
            b'@' if matches!(bytes.get(index + 1), Some(b'\'') | Some(b'"')) => (
                scan_here_string(bytes, index, bytes[index + 1]),
                Kind::Literal,
            ),
            b'@' if bytes.get(index + 1) == Some(&b'{') => (index + 2, Kind::HashOpen),
            b'{' => (index + 1, Kind::OpenBrace),
            b'}' => (index + 1, Kind::CloseBrace),
            b',' | b';' => (index + 1, Kind::Separator),
            b'|' => (
                index + usize::from(bytes.get(index + 1) == Some(&b'|')) + 1,
                Kind::Pipe,
            ),
            b'&' if bytes.get(index + 1) == Some(&b'&') => (index + 2, Kind::Pipe),
            b'=' | b'+' | b'*' | b'/' | b'%' | b'!' | b'?' => {
                (scan_operator(bytes, index), Kind::Operator)
            }
            b'-' if bytes.get(index + 1).is_some_and(u8::is_ascii_alphabetic) => {
                (scan_word(bytes, index), Kind::Operator)
            }
            byte if byte.is_ascii_alphabetic() || byte == b'_' => {
                (scan_word(bytes, index), Kind::Word)
            }
            _ => (scan_other(bytes, index), Kind::Other),
        };
        tokens.push(Token { start, end, kind });
        index = end.max(index + 1);
    }
    tokens
}

fn scan_until(bytes: &[u8], mut index: usize, end: u8) -> usize {
    while index < bytes.len() && bytes[index] != end {
        index += 1;
    }
    index
}

fn scan_pair(bytes: &[u8], mut index: usize, first: u8, second: u8) -> usize {
    while index + 1 < bytes.len() {
        if bytes[index] == first && bytes[index + 1] == second {
            return index + 2;
        }
        index += 1;
    }
    bytes.len()
}

fn scan_quoted(bytes: &[u8], mut index: usize, quote: u8) -> usize {
    index += 1;
    while index < bytes.len() {
        if bytes[index] == b'`' {
            index += 2;
        } else if bytes[index] == quote {
            if bytes.get(index + 1) == Some(&quote) {
                index += 2;
            } else {
                return index + 1;
            }
        } else {
            index += 1;
        }
    }
    bytes.len()
}

fn scan_here_string(bytes: &[u8], index: usize, quote: u8) -> usize {
    let closing = [quote, b'@'];
    let mut cursor = index + 2;
    while cursor + 1 < bytes.len() {
        if bytes[cursor..].starts_with(&closing)
            && (cursor == 0 || bytes[cursor - 1] == b'\n' || bytes[cursor - 1] == b'\r')
        {
            return cursor + 2;
        }
        cursor += 1;
    }
    bytes.len()
}

fn scan_operator(bytes: &[u8], index: usize) -> usize {
    let mut end = index + 1;
    while end < bytes.len() && b"=+*/%!?.".contains(&bytes[end]) {
        end += 1;
    }
    end
}

fn scan_word(bytes: &[u8], mut index: usize) -> usize {
    index += 1;
    while index < bytes.len()
        && (bytes[index].is_ascii_alphanumeric() || matches!(bytes[index], b'_' | b'-'))
    {
        index += 1;
    }
    index
}

fn scan_other(bytes: &[u8], mut index: usize) -> usize {
    index += 1;
    while index < bytes.len()
        && !bytes[index].is_ascii_whitespace()
        && !b"{}@,;|&=+*/%!?\"'".contains(&bytes[index])
    {
        index += 1;
    }
    index
}

fn previous_token(tokens: &[Token], index: usize) -> Option<&Token> {
    tokens[..index]
        .iter()
        .rev()
        .find(|token| token.kind != Kind::Comment)
}

fn next_token(tokens: &[Token], index: usize) -> Option<&Token> {
    tokens[index + 1..]
        .iter()
        .find(|token| token.kind != Kind::Comment)
}

fn replace_whitespace(
    source: &str,
    left: &Token,
    right: &Token,
    replacement: &str,
    edits: &mut Vec<Edit>,
) {
    if right.start < left.end {
        return;
    }
    let current = &source[left.end..right.start];
    if current.chars().all(char::is_whitespace) && current != replacement {
        edits.push(Edit {
            start: left.end,
            end: right.start,
            text: replacement.to_string(),
        });
    }
}

fn apply_edits(source: &str, mut edits: Vec<Edit>) -> String {
    edits.sort_by(|left, right| right.start.cmp(&left.start).then(right.end.cmp(&left.end)));
    let mut result = source.to_string();
    let mut previous_start = source.len();
    for edit in edits {
        if edit.end <= previous_start {
            result.replace_range(edit.start..edit.end, &edit.text);
            previous_start = edit.start;
        }
    }
    result
}

fn detect_newline(source: &str) -> &'static str {
    if source.contains("\r\n") {
        "\r\n"
    } else {
        "\n"
    }
}

fn is_cuddled_keyword(text: &str) -> bool {
    matches_ignore_ascii_case(text, &["else", "elseif", "catch", "finally"])
}

fn is_keyword(text: &str) -> bool {
    matches_ignore_ascii_case(
        text,
        &[
            "begin",
            "break",
            "catch",
            "class",
            "clean",
            "continue",
            "data",
            "do",
            "dynamicparam",
            "else",
            "elseif",
            "end",
            "enum",
            "exit",
            "filter",
            "finally",
            "for",
            "foreach",
            "from",
            "function",
            "hidden",
            "if",
            "in",
            "param",
            "process",
            "return",
            "static",
            "switch",
            "throw",
            "trap",
            "try",
            "until",
            "using",
            "var",
            "while",
            "workflow",
        ],
    )
}

fn matches_ignore_ascii_case(text: &str, values: &[&str]) -> bool {
    values.iter().any(|value| text.eq_ignore_ascii_case(value))
}
