namespace GraphQLParser;

/// <summary>Reports a token mismatch encountered while parsing GraphQL source.</summary>
public sealed class GraphQLSyntaxException : Exception
{
    internal GraphQLSyntaxException(string message, int position, int length, string expected, string actual)
        : base(message)
    {
        Position = position;
        Length = length;
        Expected = expected;
        Actual = actual;
    }

    /// <summary>Gets the zero-based UTF-16 offset of the offending token.</summary>
    public int Position { get; }
    /// <summary>Gets the offending token length, or zero at end of input.</summary>
    public int Length { get; }

    /// <summary>Gets a short description of the parser's expectation at the failure point.</summary>
    public string Expected { get; }

    /// <summary>Gets the actual token kind and spelling encountered by the parser.</summary>
    public string Actual { get; }
}
