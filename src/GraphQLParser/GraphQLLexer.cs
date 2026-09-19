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
        if (IsNameStart(current))
        {
            return ReadName();
        }

        if (current == '-' || IsDigit(current))
        {
            return ReadNumber();
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
