using System.Text;

namespace Salep.ClientGenerator.TypeScript.Generation;

/// <summary>Extracts static gql/graphql template literals and graphql(`...`) calls without executing JavaScript.</summary>
public static class TypeScriptDocumentExtractor
{
    public static IReadOnlyList<string> Extract(string source, IReadOnlyCollection<string>? tags = null)
        => ExtractWithLocations(source, tags).Select(document => document.Text).ToArray();

    public static IReadOnlyList<TypeScriptExtractedDocument> ExtractWithLocations(string source, IReadOnlyCollection<string>? tags = null)
    {
        tags ??= ["gql", "graphql"];
        var documents = new List<TypeScriptExtractedDocument>();
        for (var i = 0; i < source.Length;)
        {
            if (SkipTrivia(source, ref i)) continue;
            if (SkipRegularExpression(source, ref i)) continue;
            if (source[i] is '\'' or '"' or '`') { ReadLiteral(source, ref i, false); continue; }
            if (!IsIdentifierStart(source[i])) { i++; continue; }
            var start = i++;
            while (i < source.Length && IsIdentifierPart(source[i])) i++;
            var identifier = source[start..i];
            if (!tags.Contains(identifier, StringComparer.Ordinal)) continue;
            while (SkipTrivia(source, ref i)) { }
            var call = i < source.Length && source[i] == '(';
            if (call) { i++; while (SkipTrivia(source, ref i)) { } }
            if (i >= source.Length || source[i] != '`') continue;
            var offsets = new List<int>();
            var text = ReadLiteral(source, ref i, true, offsets);
            documents.Add(new(text, offsets));
            if (call)
            {
                while (SkipTrivia(source, ref i)) { }
                if (i >= source.Length || source[i] != ')') throw Failure("graphql() must contain one static template literal.", i);
                i++;
            }
        }
        return documents;
    }

    private static InvalidDataException Failure(string message, int position)
    {
        var error = new InvalidDataException(message);
        error.Data["SourcePosition"] = position;
        return error;
    }

    private static bool IsIdentifierStart(char value) => char.IsLetter(value) || value is '_' or '$';
    private static bool IsIdentifierPart(char value) => IsIdentifierStart(value) || char.IsDigit(value);

    private static bool SkipTrivia(string source, ref int index)
    {
        if (index >= source.Length) return false;
        if (char.IsWhiteSpace(source[index])) { index++; return true; }
        if (index + 1 >= source.Length || source[index] != '/') return false;
        if (source[index + 1] == '/')
        {
            index += 2;
            while (index < source.Length && source[index] is not ('\r' or '\n')) index++;
            return true;
        }
        if (source[index + 1] == '*')
        {
            var end = source.IndexOf("*/", index + 2, StringComparison.Ordinal);
            if (end < 0) throw Failure("Unterminated TypeScript comment.", index);
            index = end + 2;
            return true;
        }
        return false;
    }

    private static bool SkipRegularExpression(string source, ref int index)
    {
        if (source[index] != '/') return false;
        var previous = index - 1;
        while (previous >= 0 && char.IsWhiteSpace(source[previous])) previous--;
        var startsExpression = previous < 0 || "=(:,[!&|?;{".Contains(source[previous], StringComparison.Ordinal);
        if (!startsExpression)
        {
            var end = previous + 1;
            while (previous >= 0 && IsIdentifierPart(source[previous])) previous--;
            startsExpression = source[(previous + 1)..end] is "return" or "throw" or "yield";
        }
        if (!startsExpression) return false;
        index++;
        var characterClass = false;
        while (index < source.Length)
        {
            var value = source[index++];
            if (value == '\\') { if (index < source.Length) index++; continue; }
            if (value == '[') characterClass = true;
            if (value == ']') characterClass = false;
            if (value == '/' && !characterClass)
            {
                while (index < source.Length && char.IsLetter(source[index])) index++;
                return true;
            }
            if (value is '\r' or '\n') throw Failure("Unterminated TypeScript regular expression literal.", index);
        }
        throw Failure("Unterminated TypeScript regular expression literal.", index);
    }

    private static string ReadLiteral(string source, ref int index, bool document, List<int>? offsets = null)
    {
        var quote = source[index++];
        var result = new StringBuilder();
        void Append(char character, int position) { result.Append(character); offsets?.Add(position); }
        while (index < source.Length)
        {
            var position = index;
            var value = source[index++];
            if (value == quote) { offsets?.Add(position); return result.ToString(); }
            if (quote == '`' && value == '$' && index < source.Length && source[index] == '{')
            {
                if (document) throw Failure("GraphQL template interpolation is unsupported; use named GraphQL fragment spreads in static documents.", index);
                // Ignore unrelated dynamic templates, including nested strings/templates.
                index++;
                var depth = 1;
                while (index < source.Length && depth > 0)
                {
                    if (SkipTrivia(source, ref index)) continue;
                    if (source[index] is '\'' or '"' or '`') { ReadLiteral(source, ref index, false); continue; }
                    if (source[index] == '{') depth++;
                    if (source[index] == '}') depth--;
                    index++;
                }
                continue;
            }
            if (value == '\\')
            {
                if (index == source.Length) break;
                var escaped = source[index++];
                if (!document) continue;
                switch (escaped)
                {
                    case '\r': if (index < source.Length && source[index] == '\n') index++; break;
                    case '\n': break;
                    case 'n': Append('\n', position); break;
                    case 'r': Append('\r', position); break;
                    case 't': Append('\t', position); break;
                    case '\\': case '`': case '$': case '\'': case '"': Append(escaped, position); break;
                    default: throw Failure("Unsupported escape in GraphQL template literal; use a standalone .graphql file for this document.", index);
                }
            }
            else Append(value, position);
        }
        throw Failure("Unterminated TypeScript string/template literal.", index);
    }
}

/// <summary>Decoded GraphQL source with UTF-16 offsets back into its TypeScript literal.</summary>
public sealed record TypeScriptExtractedDocument(string Text, IReadOnlyList<int> Offsets);
