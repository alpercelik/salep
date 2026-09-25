namespace Salep.Parser;

/// <summary>Lexes the compact token vocabulary used by GraphQL schema coordinates.</summary>
/// <remarks>Unlike the document lexer, this lexer rejects ignored characters instead of skipping them.</remarks>
public sealed class GraphQLSchemaCoordinateLexer
{
    private readonly SourceText _source;
    private readonly ReadOnlySpanOwner _text;
    private int _position;

    /// <summary>Creates a schema-coordinate lexer over caller-owned source memory.</summary>
    public GraphQLSchemaCoordinateLexer(SourceText source)
    {
        _source = source;
        _text = new ReadOnlySpanOwner(source.Content);
    }

    /// <summary>Creates a schema-coordinate lexer over an immutable source string.</summary>
    public GraphQLSchemaCoordinateLexer(Source source)
        : this(new SourceText((source ?? throw new ArgumentNullException(nameof(source))).Body.AsMemory()))
    {
    }

    /// <summary>Gets the offset of the next unconsumed UTF-16 code unit.</summary>
    public int Position => _position;

    /// <summary>Reads the next name, punctuation, or end-of-file token.</summary>
    public Token Advance()
    {
        if (_position == _text.Length) return new Token(TokenKind.EndOfFile, _position, _position, ReadOnlyMemory<char>.Empty);

        var start = _position;
        var current = _text[_position];
        if (IsNameStart(current))
        {
            _position++;
            while (_position < _text.Length && IsNameContinue(_text[_position])) _position++;
            var name = _source.Slice(start, _position - start);
            return new Token(TokenKind.Name, start, _position, name);
        }

        var kind = current switch
        {
            '.' => TokenKind.Dot,
            '@' => TokenKind.At,
            '(' => TokenKind.ParenthesisLeft,
            ')' => TokenKind.ParenthesisRight,
            ':' => TokenKind.Colon,
            _ => ThrowInvalidCharacter(current, start),
        };
        _position++;
        var raw = _source.Slice(start, 1);
        return new Token(kind, start, _position, raw);
    }

    private static TokenKind ThrowInvalidCharacter(char value, int position)
    {
        var description = char.IsControl(value) ? $"U+{(int)value:X4}" : $"\"{value}\"";
        var message = $"Syntax Error: Invalid character: {description}.";
        throw new GraphQLLexicalException(message, position, 1);
    }

    private static bool IsNameStart(char value) => value is '_' or >= 'A' and <= 'Z' or >= 'a' and <= 'z';
    private static bool IsNameContinue(char value) => IsNameStart(value) || value is >= '0' and <= '9';

    // A small value wrapper keeps the ref-like span out of the public object layout.
    private readonly struct ReadOnlySpanOwner
    {
        private readonly ReadOnlyMemory<char> _memory;
        public ReadOnlySpanOwner(ReadOnlyMemory<char> memory) => _memory = memory;
        public int Length => _memory.Length;
        public char this[int index] => _memory.Span[index];
    }
}
