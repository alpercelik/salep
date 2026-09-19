namespace GraphQLParser;

/// <summary>A source-backed lexical token with a half-open UTF-16 source span.</summary>
public readonly struct Token
{
    internal Token(TokenKind kind, int start, int end, ReadOnlyMemory<char> value)
    {
        Kind = kind;
        Start = start;
        End = end;
        Value = value;
    }

    /// <summary>Gets the token classification.</summary>
    public TokenKind Kind { get; }

    /// <summary>Gets the inclusive start offset in UTF-16 code units.</summary>
    public int Start { get; }

    /// <summary>Gets the exclusive end offset in UTF-16 code units.</summary>
    public int End { get; }

    /// <summary>Gets the token's source slice without copying its characters.</summary>
    public ReadOnlyMemory<char> Value { get; }
}
