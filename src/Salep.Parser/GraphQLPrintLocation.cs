using System.Text;

namespace Salep.Parser;

/// <summary>Formats a short source excerpt around a GraphQL source location.</summary>
public static class GraphQLPrintLocation
{
    /// <summary>Renders a source excerpt with a caret at a 1-based source location.</summary>
    public static string PrintSourceLocation(Source source, GraphQLSourceLocation sourceLocation)
    {
        ArgumentNullException.ThrowIfNull(source);
        var firstLineColumnOffset = source.LocationOffset.Column - 1;
        var body = new string(' ', firstLineColumnOffset) + source.Body;
        var lines = SplitLines(body);
        var lineIndex = sourceLocation.Line - 1;
        if ((uint)lineIndex >= (uint)lines.Length) throw new ArgumentOutOfRangeException(nameof(sourceLocation), "The location line is outside the source body.");

        var lineNumber = checked(sourceLocation.Line + source.LocationOffset.Line - 1);
        var columnOffset = sourceLocation.Line == 1 ? firstLineColumnOffset : 0;
        var columnNumber = checked(sourceLocation.Column + columnOffset);
        var header = $"{source.Name}:{lineNumber}:{columnNumber}\n";
        var locationLine = lines[lineIndex];

        if (locationLine.Length > 120)
        {
            var subLineIndex = Math.Min(columnNumber / 80, (locationLine.Length - 1) / 80);
            var subLineColumn = columnNumber % 80;
            var snippets = new List<(string Prefix, string? Text)> { ($"{lineNumber} |", locationLine[..Math.Min(80, locationLine.Length)]) };
            var chunks = new List<string>();
            for (var i = 0; i < locationLine.Length; i += 80) chunks.Add(locationLine.Substring(i, Math.Min(80, locationLine.Length - i)));
            for (var i = 1; i <= subLineIndex && i < chunks.Count; i++) snippets.Add(("|", chunks[i]));
            snippets.Add(("|", new string(' ', Math.Max(0, subLineColumn - 1)) + "^"));
            if (subLineIndex + 1 < chunks.Count) snippets.Add(("|", chunks[subLineIndex + 1]));
            return header + PrintPrefixedLines(snippets);
        }

        return header + PrintPrefixedLines(
        [
            ($"{lineNumber - 1} |", lineIndex > 0 ? lines[lineIndex - 1] : null),
            ($"{lineNumber} |", locationLine),
            ("|", new string(' ', Math.Max(0, columnNumber - 1)) + "^"),
            ($"{lineNumber + 1} |", lineIndex + 1 < lines.Length ? lines[lineIndex + 1] : null),
        ]);
    }

    private static string PrintPrefixedLines(IReadOnlyList<(string Prefix, string? Text)> lines)
    {
        var existing = lines.Where(line => line.Text is not null).ToArray();
        var padding = existing.Length == 0 ? 0 : existing.Max(line => line.Prefix.Length);
        var output = new StringBuilder();
        for (var i = 0; i < existing.Length; i++)
        {
            if (i > 0) output.Append('\n');
            output.Append(existing[i].Prefix.PadLeft(padding));
            if (existing[i].Text!.Length > 0) output.Append(' ').Append(existing[i].Text);
        }

        return output.ToString();
    }

    private static string[] SplitLines(string text)
    {
        var lines = new List<string>();
        var start = 0;
        for (var i = 0; i < text.Length; i++)
        {
            if (text[i] is not ('\r' or '\n')) continue;
            lines.Add(text[start..i]);
            if (text[i] == '\r' && i + 1 < text.Length && text[i + 1] == '\n') i++;
            start = i + 1;
        }

        lines.Add(text[start..]);
        return [.. lines];
    }
}
