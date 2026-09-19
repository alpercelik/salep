namespace GraphQLParser;

/// <summary>Reports a token mismatch encountered while parsing GraphQL source.</summary>
public sealed class GraphQLSyntaxException : Exception
{
    internal GraphQLSyntaxException(string message, int position, int length)
        : base(message)
    {
        Position = position;
        Length = length;
    }

    /// <summary>Gets the zero-based UTF-16 offset of the offending token.</summary>
    public int Position { get; }
    /// <summary>Gets the offending token length, or zero at end of input.</summary>
    public int Length { get; }
}
