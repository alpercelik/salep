using System.Text;

namespace Salep.ClientGenerator.TypeScript.Generation;

/// <summary>Extracts static gql/graphql template literals and graphql(`...`) calls without executing JavaScript.</summary>
public static class TypeScriptDocumentExtractor
{
    public static IReadOnlyList<string> Extract(string source, IReadOnlyCollection<string>? tags = null, bool jsx = true)
        => ExtractWithLocations(source, tags, jsx).Select(document => document.Text).ToArray();

    public static IReadOnlyList<TypeScriptExtractedDocument> ExtractWithLocations(string source, IReadOnlyCollection<string>? tags = null, bool jsx = true)
    {
        tags ??= ["gql", "graphql"];
        var documents = new List<TypeScriptExtractedDocument>();
        var i = 0;
        ScanJavaScript(source, ref i, tags, documents, false, jsx);
        return documents;
    }

    private static void ScanJavaScript(string source, ref int i, IReadOnlyCollection<string> tags, List<TypeScriptExtractedDocument> documents, bool expression, bool jsx = true)
    {
        var depth = 0;
        var scanStart = i;
        while (i < source.Length)
        {
            if (expression && source[i] == '}')
            {
                if (depth-- == 0) { i++; return; }
            }
            else if (expression && source[i] == '{') depth++;
            if (jsx && TryReadJsx(source, ref i, tags, documents)) continue;
            if (SkipTrivia(source, ref i)) continue;
            if (SkipRegularExpression(source, ref i, scanStart: scanStart)) continue;
            if (source[i] is '\'' or '"' or '`') { ReadLiteral(source, ref i, false, tags: tags, documents: documents, jsx: jsx); continue; }
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
                if (i < source.Length && source[i] == ',')
                {
                    i++;
                    while (SkipTrivia(source, ref i)) { }
                }
                if (i >= source.Length || source[i] != ')') throw Failure("graphql() must contain one static template literal.", i);
                i++;
            }
        }
        if (expression) throw Failure("Unterminated JSX expression.", i);
    }

    private static bool TryReadJsx(string source, ref int index, IReadOnlyCollection<string> tags, List<TypeScriptExtractedDocument> documents, bool markup = false)
    {
        if (source[index] != '<' || index + 1 >= source.Length) return false;
        if (!markup)
        {
            var previous = index - 1;
            while (previous >= 0 && char.IsWhiteSpace(source[previous])) previous--;
            if (previous >= 0 && !"=(:,[!&|?;{>".Contains(source[previous], StringComparison.Ordinal))
            {
                var end = previous + 1;
                while (previous >= 0 && IsIdentifierPart(source[previous])) previous--;
                if (source[(previous + 1)..end] is not ("return" or "yield")) return false;
            }
        }
        var cursor = index + 1;
        var fragment = source[cursor] == '>';
        if (!fragment && !IsIdentifierStart(source[cursor])) return false;
        var nameStart = cursor;
        if (!fragment)
            while (cursor < source.Length && (IsIdentifierPart(source[cursor]) || source[cursor] is '.' or ':' or '-')) cursor++;
        var name = source[nameStart..cursor];
        // A generic parameter/type assertion has no matching JSX closing tag. Do not
        // consume it as markup; self-closing tags remain unambiguous.
        if (!fragment && source.IndexOf("</" + name, cursor, StringComparison.Ordinal) < 0)
        {
            var end = JsxTagEnd(source, cursor);
            if (end < 0 || source[end - 1] != '/') return false;
        }
        if (!fragment && cursor < source.Length && source[cursor] == ',') return false;
        index = cursor;
        // Generic JSX components may carry TypeScript type arguments before attributes.
        if (index < source.Length && source[index] == '<')
        {
            var depth = 0;
            do
            {
                if (source[index] == '<') depth++;
                if (source[index] == '>') depth--;
                index++;
            } while (index < source.Length && depth > 0);
        }
        while (index < source.Length)
        {
            if (source[index] == '{') { index++; ScanJavaScript(source, ref index, tags, documents, true); continue; }
            if (source[index] is '\'' or '"')
            {
                var quote = source[index++];
                while (index < source.Length && source[index] != quote) index++;
                if (index == source.Length) throw Failure("Unterminated JSX attribute.", index);
                index++;
                continue;
            }
            if (source[index] == '>') { index++; break; }
            if (source[index] == '/' && index + 1 < source.Length && source[index + 1] == '>') { index += 2; return true; }
            index++;
        }
        while (index < source.Length)
        {
            if (source[index] == '{') { index++; ScanJavaScript(source, ref index, tags, documents, true); continue; }
            if (source[index] == '<' && index + 1 < source.Length && source[index + 1] == '/')
            {
                var end = source.IndexOf('>', index + 2);
                if (end < 0) throw Failure("Unterminated JSX closing tag.", index);
                index = end + 1;
                return true;
            }
            if (TryReadJsx(source, ref index, tags, documents, true)) continue;
            index++;
        }
        throw Failure("Unterminated JSX element.", index);
    }

    private static int JsxTagEnd(string source, int cursor)
    {
        var braces = 0;
        var typeArguments = 0;
        while (cursor < source.Length)
        {
            var value = source[cursor++];
            if (value is '\'' or '"' or '`')
            {
                while (cursor < source.Length)
                {
                    if (source[cursor] == value) { cursor++; break; }
                    if (source[cursor++] == '\\' && cursor < source.Length) cursor++;
                }
                continue;
            }
            if (value == '{') braces++;
            if (value == '}') braces--;
            if (braces != 0) continue;
            if (value == '<') typeArguments++;
            if (value == '>')
            {
                if (typeArguments > 0) typeArguments--;
                else return cursor - 1;
            }
        }
        return -1;
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

    private static bool SkipRegularExpression(string source, ref int index, bool? expressionStart = null, int scanStart = 0)
    {
        if (source[index] != '/') return false;
        if (!(expressionStart ?? RegexStartsAt(source, index, scanStart))) return false;
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

    private static bool RegexStartsAfter(string token, bool closedControlHeader) =>
        closedControlHeader || token.Length == 0 ||
        token is "statement-block" or "else" or "do" or "return" or "throw" or "yield" or "await" or "case" or "delete" or "void" or "typeof" or "instanceof" or "in" or "of" ||
        token.Length == 1 && "=(:,[!&|?;{+-*%^~<>".Contains(token[0], StringComparison.Ordinal);

    private static bool RegexStartsAt(string source, int end, int scanStart)
    {
        // Significant tokens, rather than adjacent characters, determine slash context.
        // Comments preserve the preceding token; strings and regex contents never
        // contribute parentheses or expression-start keywords.
        var parentheses = new Stack<(bool Control, bool? FunctionDeclaration)>();
        var braces = new Stack<(bool ClosesStatement, bool ContainsStatements)>();
        var token = string.Empty;
        var closedControlHeader = false;
        bool? pendingFunction = null;
        bool? functionBody = null;
        bool? pendingClass = null;
        var asyncDeclaration = false;
        bool StatementStart() => closedControlHeader || token.Length == 0 && scanStart == 0 || token is ";" or "statement-block" or "export" or "default" ||
            token == "{" && braces.TryPeek(out var scope) && scope.ContainsStatements;
        var cursor = scanStart;
        while (cursor < end)
        {
            if (SkipTrivia(source, ref cursor)) continue;
            if (SkipRegularExpression(source, ref cursor, RegexStartsAfter(token, closedControlHeader)))
            {
                token = "value";
                closedControlHeader = false;
                continue;
            }
            if (source[cursor] is '\'' or '"' or '`')
            {
                ReadLiteral(source, ref cursor, false);
                token = "value";
                closedControlHeader = false;
                continue;
            }
            if (IsIdentifierStart(source[cursor]))
            {
                var memberAccess = token == ".";
                var start = cursor++;
                while (cursor < end && IsIdentifierPart(source[cursor])) cursor++;
                var identifier = source[start..cursor];
                if (!memberAccess && identifier == "async") asyncDeclaration = StatementStart();
                if (!memberAccess && identifier == "function") pendingFunction = token == "async" ? asyncDeclaration : StatementStart();
                if (!memberAccess && identifier == "class") pendingClass = StatementStart();
                token = memberAccess ? "member" : identifier;
                closedControlHeader = false;
                continue;
            }
            var value = source[cursor++];
            if (value is '+' or '-' && cursor < end && source[cursor] == value)
            {
                cursor++;
                token = new string(value, 2);
                closedControlHeader = false;
                continue;
            }
            if (value == '=' && cursor < end && source[cursor] == '>')
            {
                cursor++;
                token = "=>";
                closedControlHeader = false;
                continue;
            }
            if (value == '{')
            {
                var declaration = functionBody ?? pendingClass;
                var statement = declaration ?? (StatementStart() || token is "else" or "do" or "try" or "finally");
                braces.Push((statement, statement || declaration.HasValue || token is "=>" or ")"));
                functionBody = null;
                pendingClass = null;
            }
            if (value == '}')
            {
                token = braces.TryPop(out var scope) && scope.ClosesStatement ? "statement-block" : "value";
                closedControlHeader = false;
                continue;
            }
            closedControlHeader = false;
            if (value == '(')
            {
                parentheses.Push((token is "if" or "while" or "for" or "with" or "switch" or "catch", pendingFunction));
                pendingFunction = null;
            }
            if (value == ')' && parentheses.TryPop(out var header))
            {
                closedControlHeader = header.Control;
                functionBody = header.FunctionDeclaration;
            }
            token = value.ToString();
        }
        return RegexStartsAfter(token, closedControlHeader);
    }

    private static string ReadLiteral(string source, ref int index, bool document, List<int>? offsets = null,
        IReadOnlyCollection<string>? tags = null, List<TypeScriptExtractedDocument>? documents = null, bool jsx = true)
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
                index++;
                if (tags is not null && documents is not null)
                {
                    ScanJavaScript(source, ref index, tags, documents, true, jsx);
                    continue;
                }
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
            else if (value == '\r')
            {
                // JavaScript normalizes raw template line endings in cooked values.
                // Keep the CR source offset while consuming a following LF.
                if (index < source.Length && source[index] == '\n') index++;
                Append('\n', position);
            }
            else Append(value, position);
        }
        throw Failure("Unterminated TypeScript string/template literal.", index);
    }
}

/// <summary>Decoded GraphQL source with UTF-16 offsets back into its TypeScript literal.</summary>
public sealed record TypeScriptExtractedDocument(string Text, IReadOnlyList<int> Offsets);
