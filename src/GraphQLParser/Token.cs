namespace GraphQLParser;

/// <summary>A source-backed lexical token with a half-open UTF-16 source span.</summary>
public readonly struct Token
{
    internal Token(TokenKind kind, int start, int end, ReadOnlyMemory<char> rawValue)
        : this(kind, start, end, rawValue, rawValue)
    {
    }

    internal Token(TokenKind kind, int start, int end, ReadOnlyMemory<char> rawValue, ReadOnlyMemory<char> value)
    {
        Kind = kind;
        Start = start;
        End = end;
        RawValue = rawValue;
        Value = value;
    }

    /// <summary>Gets the token classification.</summary>
    public TokenKind Kind { get; }

    /// <summary>Gets the inclusive start offset in UTF-16 code units.</summary>
    public int Start { get; }

    /// <summary>Gets the exclusive end offset in UTF-16 code units.</summary>
    public int End { get; }

    /// <summary>Gets the complete source spelling of the token without copying its characters.</summary>
    public ReadOnlyMemory<char> RawValue { get; }

    /// <summary>
    /// Gets the token value. For quoted strings this is decoded content and references the source
    /// when no escapes require decoding; escaped strings use memory backed by the decoded string.
    /// For block strings this is the normalized value. For other tokens this is the same
    /// source-backed slice as <see cref="RawValue"/>.
    /// </summary>
    public ReadOnlyMemory<char> Value { get; }
}
