namespace GraphQLParser;

/// <summary>Reads GraphQL punctuators while skipping ignored source characters.</summary>
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
    /// <exception cref="GraphQLLexicalException">A malformed spread or unexpected character was found.</exception>
    public Token NextToken()
    {
        SkipIgnoredInput();

        if (_position == _source.Length)
        {
            return new Token(TokenKind.EndOfFile, _position, _position, _source.Slice(_position, 0));
        }

        var start = _position;
        var current = _source.Content.Span[_position];
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
}
