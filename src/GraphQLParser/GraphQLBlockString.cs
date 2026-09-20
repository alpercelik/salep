namespace GraphQLParser;

/// <summary>Utilities for formatting and normalizing GraphQL block-string values.</summary>
public static class GraphQLBlockString
{
    /// <summary>Removes common indentation and blank edge lines from block-string lines.</summary>
    public static string[] DedentBlockStringLines(IReadOnlyList<string> lines)
    {
        ArgumentNullException.ThrowIfNull(lines);
        var commonIndent = int.MaxValue;
        var firstNonEmptyLine = -1;
        var lastNonEmptyLine = -1;

        for (var i = 0; i < lines.Count; i++)
        {
            var line = lines[i] ?? throw new ArgumentException("Lines cannot contain null values.", nameof(lines));
            var indent = LeadingWhitespace(line);
            if (indent == line.Length) continue;

            if (firstNonEmptyLine < 0) firstNonEmptyLine = i;
            lastNonEmptyLine = i;
            if (i != 0 && indent < commonIndent) commonIndent = indent;
        }

        if (firstNonEmptyLine < 0) return [];

        var result = new string[lastNonEmptyLine - firstNonEmptyLine + 1];
        for (var i = firstNonEmptyLine; i <= lastNonEmptyLine; i++)
        {
            var line = lines[i];
            result[i - firstNonEmptyLine] = i == 0 ? line : line[Math.Min(commonIndent, line.Length)..];
        }

        return result;
    }

    /// <summary>Returns whether a value can be printed as a GraphQL block string without changing its value.</summary>
    public static bool IsPrintableAsBlockString(string value)
    {
        ArgumentNullException.ThrowIfNull(value);
        if (value.Length == 0) return true;

        var isEmptyLine = true;
        var hasIndent = false;
        var hasCommonIndent = true;
        var seenNonEmptyLine = false;

        for (var i = 0; i < value.Length; i++)
        {
            var c = value[i];
            if (c is '\0' or '\u0001' or '\u0002' or '\u0003' or '\u0004' or '\u0005' or '\u0006' or '\u0007'
                or '\u0008' or '\u000B' or '\u000C' or '\u000E' or '\u000F' or '\r') return false;

            if (c == '\n')
            {
                if (isEmptyLine && !seenNonEmptyLine) return false;
                seenNonEmptyLine = true;
                isEmptyLine = true;
                hasIndent = false;
            }
            else if (c is '\t' or ' ')
            {
                hasIndent |= isEmptyLine;
            }
            else
            {
                hasCommonIndent &= hasIndent;
                isEmptyLine = false;
            }
        }

        if (isEmptyLine) return false;
        return !(hasCommonIndent && seenNonEmptyLine);
    }

    /// <summary>Prints a GraphQL block string, optionally minimizing surrounding line breaks.</summary>
    public static string PrintBlockString(string value, BlockStringPrintOptions options = default)
    {
        ArgumentNullException.ThrowIfNull(value);
        var escapedValue = value.Replace("\"\"\"", "\\\"\"\"", StringComparison.Ordinal);
        var lines = SplitLines(value);
        var escapedLines = SplitLines(escapedValue);
        var isSingleLine = lines.Length == 1;
        var forceLeadingNewLine = lines.Length > 1 && escapedLines.Skip(1).All(line => line.Length == 0 || IsWhitespace(line[0]));
        var hasTrailingTripleQuotes = escapedValue.EndsWith("\\\"\"\"", StringComparison.Ordinal);
        var forceTrailingNewline = value.EndsWith('"') && !hasTrailingTripleQuotes || value.EndsWith('\\');
        var printAsMultipleLines = !options.Minimize
            && (!isSingleLine || value.Length > 70 || forceTrailingNewline || forceLeadingNewLine || hasTrailingTripleQuotes);

        var output = new System.Text.StringBuilder(escapedValue.Length + 8);
        output.Append("\"\"\"");
        var skipLeadingNewLine = isSingleLine && value.Length > 0 && IsWhitespace(value[0]);
        if ((printAsMultipleLines && !skipLeadingNewLine) || forceLeadingNewLine) output.Append('\n');
        output.Append(escapedValue);
        if (printAsMultipleLines || forceTrailingNewline) output.Append('\n');
        output.Append("\"\"\"");
        return output.ToString();
    }

    private static int LeadingWhitespace(string value)
    {
        var i = 0;
        while (i < value.Length && IsWhitespace(value[i])) i++;
        return i;
    }

    private static bool IsWhitespace(char value) => value is '\t' or ' ';

    private static string[] SplitLines(string value)
    {
        var lines = new List<string>();
        var start = 0;
        for (var i = 0; i < value.Length; i++)
        {
            if (value[i] is not ('\r' or '\n')) continue;
            lines.Add(value[start..i]);
            if (value[i] == '\r' && i + 1 < value.Length && value[i + 1] == '\n') i++;
            start = i + 1;
        }

        lines.Add(value[start..]);
        return [.. lines];
    }
}

/// <summary>Options controlling block-string formatting.</summary>
public readonly record struct BlockStringPrintOptions(bool Minimize = false);
