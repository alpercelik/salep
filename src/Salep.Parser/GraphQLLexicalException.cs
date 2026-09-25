namespace Salep.Parser;

/// <summary>Reports an invalid character sequence encountered by the lexer.</summary>
public sealed class GraphQLLexicalException : Exception
{
    internal GraphQLLexicalException(string message, int position, int length)
        : base(message)
    {
        Position = position;
        Length = length;
    }

    /// <summary>Gets the zero-based UTF-16 offset where the invalid sequence begins.</summary>
    public int Position { get; }

    /// <summary>Gets the number of UTF-16 code units in the invalid sequence.</summary>
    public int Length { get; }
}
