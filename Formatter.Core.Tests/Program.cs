using Microsoft.PowerShell.ScriptAnalyzer.Formatter;

var failures = new List<string>();

Check(
    "default formatting",
    "IF($x-EQ 1){'yes'}ELSE{'no'}",
    "if($x -eq 1) {\n    'yes'\n} else {\n    'no'\n}");

Check(
    "nested indentation",
    "function Test {\nif ($true) {\nWrite-Output 'yes'\n}\n}",
    "function Test {\n    if ($true) {\n        Write-Output 'yes'\n    }\n}");

Check(
    "hashtable remains inline",
    "$x=@{one=1;two=2}",
    "$x = @{one = 1; two = 2}");

Check(
    "next-line braces",
    "function Test { 'ok' }",
    "function Test\n{\n    'ok'\n}",
    new FormatterOptions { BraceStyle = BraceStyle.NextLine });

Check(
    "unary operators",
    "$x=-1\n$y=!$false",
    "$x = -1\n$y = !$false");

Check(
    "multiline strings",
    "if($true){\n$x=@'\n  untouched\n'@\n}",
    "if($true) {\n    $x = @'\n  untouched\n'@\n}");

var invalid = "if (";
var invalidResult = PowerShellFormatter.Format(invalid);
if (invalidResult.Text != invalid || invalidResult.Errors.Count == 0)
{
    failures.Add("parse errors must preserve input and return diagnostics");
}

if (failures.Count > 0)
{
    Console.Error.WriteLine(string.Join(Environment.NewLine, failures));
    return 1;
}

Console.WriteLine("7 formatter checks passed");
return 0;

void Check(string name, string input, string expected, FormatterOptions? options = null)
{
    var result = PowerShellFormatter.Format(input, options);
    if (result.Errors.Count > 0 || result.Text != expected)
    {
        failures.Add($"{name}: expected [{Escape(expected)}], got [{Escape(result.Text)}]");
    }
}

static string Escape(string value) => value.Replace("\r", "\\r").Replace("\n", "\\n");
