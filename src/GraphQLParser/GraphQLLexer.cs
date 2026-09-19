using System.Text;

namespace GraphQLParser;

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
                if (digits == 6)
                {
                    throw InvalidEscape(escapeStart, _position - escapeStart + 1);
                }

                scalar = (scalar * 16) + digit;
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
