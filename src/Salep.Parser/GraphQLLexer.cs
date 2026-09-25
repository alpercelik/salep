using System.Text;

namespace Salep.Parser;

/// <summary>Tokenizes GraphQL names, numeric literals, quoted strings, and punctuators.</summary>
/// <remarks>
/// The source memory must remain alive and unchanged for the lifetime of this lexer and its tokens.
/// Offsets are measured in UTF-16 code units.
/// </remarks>
public ref struct GraphQLLexer
{
    private readonly SourceText _source;
    private int _position;

    /// <summary>Creates a lexer over caller-owned source memory.</summary>
    public GraphQLLexer(SourceText source)
    {
        _source = source;
        _position = 0;
    }

    /// <summary>Reads the next token, returning the same end-of-file token after the source ends.</summary>
    /// <exception cref="GraphQLLexicalException">An invalid character sequence or literal was found.</exception>
    public Token NextToken()
    {
        SkipIgnoredInput();

        if (_position == _source.Length)
        {
            return new Token(TokenKind.EndOfFile, _position, _position, _source.Slice(_position, 0));
        }

        var start = _position;
        var current = _source.Content.Span[_position];
        if (IsNameStart(current))
        {
            return ReadName();
        }

        if (current == '-' || IsDigit(current))
        {
            return ReadNumber();
        }

        if (current == '"')
        {
            var remaining = _source.Content.Span[_position..];
            if (remaining.Length >= 3 && remaining[1] == '"' && remaining[2] == '"')
            {
                return ReadBlockString();
            }

            return ReadQuotedString();
        }

        var kind = current switch
        {
            '!' => TokenKind.Bang,
            '$' => TokenKind.Dollar,
            '&' => TokenKind.Ampersand,
            '(' => TokenKind.ParenthesisLeft,
            ')' => TokenKind.ParenthesisRight,
            ':' => TokenKind.Colon,
            '=' => TokenKind.Equals,
            '@' => TokenKind.At,
            '[' => TokenKind.BracketLeft,
            ']' => TokenKind.BracketRight,
            '{' => TokenKind.BraceLeft,
            '|' => TokenKind.Pipe,
            '}' => TokenKind.BraceRight,
            '.' => ReadSpread(),
            _ => throw UnexpectedCharacter(current, start),
        };

        if (kind != TokenKind.Spread)
        {
            _position++;
        }

        return new Token(kind, start, _position, _source.Slice(start, _position - start));
    }

    private Token ReadName()
    {
        var start = _position++;
        var source = _source.Content.Span;
        while (_position < source.Length && IsNameContinue(source[_position]))
        {
            _position++;
        }

        return new Token(TokenKind.Name, start, _position, _source.Slice(start, _position - start));
    }

    private Token ReadNumber()
    {
        var start = _position;
        var source = _source.Content.Span;
        if (source[_position] == '-')
        {
            _position++;
        }

        if (_position == source.Length || !IsDigit(source[_position]))
        {
            throw ExpectedDigit(_position);
        }

        if (source[_position] == '0')
        {
            _position++;
            if (_position < source.Length && IsDigit(source[_position]))
            {
                throw new GraphQLLexicalException("An integer literal cannot contain a leading zero.", _position, 1);
            }
        }
        else
        {
            while (_position < source.Length && IsDigit(source[_position]))
            {
                _position++;
            }
        }

        var kind = TokenKind.Integer;
        if (_position < source.Length && source[_position] == '.')
        {
            kind = TokenKind.Float;
            _position++;
            if (_position == source.Length || !IsDigit(source[_position]))
            {
                throw ExpectedDigit(_position);
            }

            while (_position < source.Length && IsDigit(source[_position]))
            {
                _position++;
            }
        }

        if (_position < source.Length && source[_position] is 'e' or 'E')
        {
            kind = TokenKind.Float;
            _position++;
            if (_position < source.Length && source[_position] is '+' or '-')
            {
                _position++;
            }

            if (_position == source.Length || !IsDigit(source[_position]))
            {
                throw ExpectedDigit(_position);
            }

            while (_position < source.Length && IsDigit(source[_position]))
            {
                _position++;
            }
        }

        if (_position < source.Length && IsNameStart(source[_position]))
        {
            throw ExpectedDigit(_position);
        }

        if (_position < source.Length && source[_position] == '.')
        {
            throw ExpectedDigit(_position);
        }

        return new Token(kind, start, _position, _source.Slice(start, _position - start));
    }

    private Token ReadQuotedString()
    {
        var start = _position++;
        var contentStart = _position;
        var chunkStart = contentStart;
        StringBuilder? decoded = null;
        var source = _source.Content.Span;

        while (_position < source.Length)
        {
            var current = source[_position];
            if (current == '"')
            {
                ReadOnlyMemory<char> value;
                if (decoded is null)
                {
                    value = _source.Slice(contentStart, _position - contentStart);
                }
                else
                {
                    decoded.Append(source[chunkStart.._position]);
                    value = decoded.ToString().AsMemory();
                }

                var end = ++_position;
                return new Token(
                    TokenKind.String,
                    start,
                    end,
                    _source.Slice(start, end - start),
                    value);
            }

            if (current == '\\')
            {
                decoded ??= new StringBuilder();
                decoded.Append(source[chunkStart.._position]);
                var escapeStart = _position;
                AppendEscape(decoded, escapeStart);
                chunkStart = _position;
                continue;
            }

            if (current < 0x20)
            {
                throw new GraphQLLexicalException("Quoted strings cannot contain raw control characters or line terminators.", _position, 1);
            }

            if (char.IsHighSurrogate(current))
            {
                if (_position + 1 >= source.Length || !char.IsLowSurrogate(source[_position + 1]))
                {
                    throw new GraphQLLexicalException("Quoted strings must contain valid Unicode scalar values.", _position, 1);
                }

                _position += 2;
                continue;
            }

            if (char.IsLowSurrogate(current))
            {
                throw new GraphQLLexicalException("Quoted strings must contain valid Unicode scalar values.", _position, 1);
            }

            _position++;
        }

        throw new GraphQLLexicalException("Unterminated quoted string.", _position, 0);
    }

    private Token ReadBlockString()
    {
        var start = _position;
        _position += 3;
        var contentStart = _position;
        var chunkStart = contentStart;
        StringBuilder? rawBuilder = null;
        var source = _source.Content.Span;

        while (_position < source.Length)
        {
            if (source[_position] == '\\' && HasTripleQuoteAt(_position + 1))
            {
                rawBuilder ??= new StringBuilder();
                rawBuilder.Append(source[chunkStart.._position]);
                rawBuilder.Append("\"\"\"");
                _position += 4;
                chunkStart = _position;
                continue;
            }

            if (HasTripleQuoteAt(_position))
            {
                var contentEnd = _position;
                string normalized;
                if (rawBuilder is null)
                {
                    normalized = NormalizeBlockString(source[contentStart..contentEnd]);
                }
                else
                {
                    rawBuilder.Append(source[chunkStart..contentEnd]);
                    normalized = NormalizeBlockString(rawBuilder.ToString());
                }

                _position += 3;
                return new Token(
                    TokenKind.BlockString,
                    start,
                    _position,
                    _source.Slice(start, _position - start),
                    normalized.AsMemory());
            }

            var current = source[_position];
            if (current < 0x20 && current is not '\t' and not '\r' and not '\n')
            {
                throw new GraphQLLexicalException("Block strings cannot contain raw control characters.", _position, 1);
            }

            if (char.IsHighSurrogate(current))
            {
                if (_position + 1 >= source.Length || !char.IsLowSurrogate(source[_position + 1]))
                {
                    throw new GraphQLLexicalException("Block strings must contain valid Unicode scalar values.", _position, 1);
                }

                _position += 2;
                continue;
            }

            if (char.IsLowSurrogate(current))
            {
                throw new GraphQLLexicalException("Block strings must contain valid Unicode scalar values.", _position, 1);
            }

            _position++;
        }

        throw new GraphQLLexicalException("Unterminated block string.", _position, 0);
    }

    private bool HasTripleQuoteAt(int position)
    {
        var source = _source.Content.Span;
        return position + 2 < source.Length
            && source[position] == '"'
            && source[position + 1] == '"'
            && source[position + 2] == '"';
    }

    private static string NormalizeBlockString(ReadOnlySpan<char> raw)
    {
        var lines = new List<(int Start, int Length)>();
        var lineStart = 0;
        for (var index = 0; index < raw.Length; index++)
        {
            if (raw[index] is not '\r' and not '\n')
            {
                continue;
            }

            lines.Add((lineStart, index - lineStart));
            if (raw[index] == '\r' && index + 1 < raw.Length && raw[index + 1] == '\n')
            {
                index++;
            }

            lineStart = index + 1;
        }

        lines.Add((lineStart, raw.Length - lineStart));

        int? commonIndent = null;
        for (var index = 1; index < lines.Count; index++)
        {
            var line = lines[index];
            var content = raw.Slice(line.Start, line.Length);
            var indent = CountLeadingWhitespace(content);
            if (indent < line.Length && (commonIndent is null || indent < commonIndent.Value))
            {
                commonIndent = indent;
            }
        }

        if (commonIndent is { } indentation)
        {
            for (var index = 1; index < lines.Count; index++)
            {
                var line = lines[index];
                var removed = Math.Min(indentation, line.Length);
                lines[index] = (line.Start + removed, line.Length - removed);
            }
        }

        while (lines.Count > 0 && IsBlankLine(raw.Slice(lines[0].Start, lines[0].Length)))
        {
            lines.RemoveAt(0);
        }

        while (lines.Count > 0 && IsBlankLine(raw.Slice(lines[^1].Start, lines[^1].Length)))
        {
            lines.RemoveAt(lines.Count - 1);
        }

        if (lines.Count == 0)
        {
            return string.Empty;
        }

        var normalized = new StringBuilder();
        for (var index = 0; index < lines.Count; index++)
        {
            if (index > 0)
            {
                normalized.Append('\n');
            }

            var line = lines[index];
            normalized.Append(raw.Slice(line.Start, line.Length));
        }

        return normalized.ToString();
    }

    private static int CountLeadingWhitespace(ReadOnlySpan<char> line)
    {
        var count = 0;
        while (count < line.Length && IsWhitespace(line[count]))
        {
            count++;
        }

        return count;
    }

    private static bool IsBlankLine(ReadOnlySpan<char> line)
    {
        foreach (var character in line)
        {
            if (!IsWhitespace(character))
            {
                return false;
            }
        }

        return true;
    }

    private static bool IsWhitespace(char character) => character is ' ' or '\t';

    private void AppendEscape(StringBuilder decoded, int escapeStart)
    {
        var source = _source.Content.Span;
        _position++;
        if (_position == source.Length)
        {
            throw InvalidEscape(escapeStart, 1);
        }

        var escape = source[_position++];
        switch (escape)
        {
            case '"': decoded.Append('"'); return;
            case '/': decoded.Append('/'); return;
            case '\\': decoded.Append('\\'); return;
            case 'b': decoded.Append('\b'); return;
            case 'f': decoded.Append('\f'); return;
            case 'n': decoded.Append('\n'); return;
            case 'r': decoded.Append('\r'); return;
            case 't': decoded.Append('\t'); return;
            case 'u': AppendUnicodeEscape(decoded, escapeStart); return;
            default: throw InvalidEscape(escapeStart, _position - escapeStart);
        }
    }

    private void AppendUnicodeEscape(StringBuilder decoded, int escapeStart)
    {
        var source = _source.Content.Span;
        if (_position < source.Length && source[_position] == '{')
        {
            _position++;
            var scalar = 0;
            var digits = 0;
            while (_position < source.Length && TryHexValue(source[_position], out var digit))
            {
                if (digits == 8) throw InvalidEscape(escapeStart, _position - escapeStart + 1);
                scalar = scalar > 0x10FFFF / 16 ? 0x110000 : (scalar * 16) + digit;
                if (scalar > 0x10FFFF) scalar = 0x110000;
                digits++;
                _position++;
            }

            if (digits == 0 || _position == source.Length || source[_position] != '}')
            {
                throw InvalidEscape(escapeStart, Math.Max(1, _position - escapeStart));
            }

            _position++;
            if (!Rune.IsValid(scalar))
            {
                throw InvalidEscape(escapeStart, _position - escapeStart);
            }

            AppendScalar(decoded, scalar);
            return;
        }

        var first = ReadFixedUnicodeCodeUnit(escapeStart);
        if (char.IsLowSurrogate(first))
        {
            throw InvalidEscape(escapeStart, _position - escapeStart);
        }

        if (!char.IsHighSurrogate(first))
        {
            decoded.Append(first);
            return;
        }

        if (_position + 2 > source.Length || source[_position] != '\\' || source[_position + 1] != 'u')
        {
            throw InvalidEscape(escapeStart, _position - escapeStart);
        }

        _position += 2;
        var second = ReadFixedUnicodeCodeUnit(escapeStart);
        if (!char.IsLowSurrogate(second))
        {
            throw InvalidEscape(escapeStart, _position - escapeStart);
        }

        AppendScalar(decoded, char.ConvertToUtf32(first, second));
    }

    private char ReadFixedUnicodeCodeUnit(int escapeStart)
    {
        var source = _source.Content.Span;
        if (_position + 4 > source.Length)
        {
            throw InvalidEscape(escapeStart, source.Length - escapeStart);
        }

        var codeUnit = 0;
        for (var digitIndex = 0; digitIndex < 4; digitIndex++)
        {
            if (!TryHexValue(source[_position], out var digit))
            {
                throw InvalidEscape(escapeStart, _position - escapeStart + 1);
            }

            codeUnit = (codeUnit * 16) + digit;
            _position++;
        }

        return (char)codeUnit;
    }

    private static void AppendScalar(StringBuilder decoded, int scalar)
    {
        Span<char> utf16 = stackalloc char[2];
        var count = new Rune(scalar).EncodeToUtf16(utf16);
        decoded.Append(utf16[..count]);
    }

    private GraphQLLexicalException InvalidEscape(int start, int length) =>
        new("Invalid string escape sequence or Unicode scalar.", start, length);

    private static bool TryHexValue(char character, out int value)
    {
        if (character is >= '0' and <= '9')
        {
            value = character - '0';
            return true;
        }

        if (character is >= 'a' and <= 'f')
        {
            value = character - 'a' + 10;
            return true;
        }

        if (character is >= 'A' and <= 'F')
        {
            value = character - 'A' + 10;
            return true;
        }

        value = 0;
        return false;
    }

    private TokenKind ReadSpread()
    {
        var remaining = _source.Content.Span[_position..];
        if (remaining.Length < 3 || remaining[1] != '.' || remaining[2] != '.')
        {
            var availablePeriods = 1;
            if (remaining.Length > 1 && remaining[1] == '.') availablePeriods++;
            throw new GraphQLLexicalException(
                "A spread token must contain exactly three consecutive periods.",
                _position,
                availablePeriods);
        }

        _position += 3;
        return TokenKind.Spread;
    }

    private void SkipIgnoredInput()
    {
        var source = _source.Content.Span;
        while (_position < source.Length)
        {
            switch (source[_position])
            {
                case '\uFEFF':
                case '\u0009':
                case '\u000A':
                case '\u000D':
                case '\u0020':
                case ',':
                    _position++;
                    continue;
                case '#':
                    _position++;
                    while (_position < source.Length && source[_position] is not '\u000A' and not '\u000D')
                    {
                        if (char.IsHighSurrogate(source[_position]))
                        {
                            if (_position + 1 >= source.Length || !char.IsLowSurrogate(source[_position + 1]))
                            {
                                throw new GraphQLLexicalException("Ignored input must contain valid Unicode scalar values.", _position, 1);
                            }

                            _position++;
                        }
                        else if (char.IsLowSurrogate(source[_position]))
                        {
                            throw new GraphQLLexicalException("Ignored input must contain valid Unicode scalar values.", _position, 1);
                        }

                        _position++;
                    }
                    continue;
                default:
                    return;
            }
        }
    }

    private static GraphQLLexicalException UnexpectedCharacter(char character, int position) =>
        new($"Unexpected character U+{(int)character:X4}.", position, 1);

    private GraphQLLexicalException ExpectedDigit(int position)
    {
        if (position == _source.Length)
        {
            return new GraphQLLexicalException("Invalid number: expected a digit at end of input.", position, 0);
        }

        var character = _source.Content.Span[position];
        return new GraphQLLexicalException(
            $"Invalid number: expected a digit but found U+{(int)character:X4}.",
            position,
            1);
    }

    private static bool IsNameStart(char character) =>
        character is '_' or >= 'A' and <= 'Z' or >= 'a' and <= 'z';

    private static bool IsNameContinue(char character) => IsNameStart(character) || IsDigit(character);

    private static bool IsDigit(char character) => character is >= '0' and <= '9';
}
