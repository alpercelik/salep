namespace Salep.GraphQLParser;

/// <summary>Base exception for GraphQL language processing errors.</summary>
public class LanguageException : Exception
{
    /// <summary>Creates a language exception.</summary>
    public LanguageException() { }
    /// <summary>Creates a language exception with a message.</summary>
    public LanguageException(string? message) : base(message) { }
    /// <summary>Creates a language exception with a message and inner exception.</summary>
    public LanguageException(string? message, Exception? inner) : base(message, inner) { }
}

/// <summary>Reports a value that does not have the required GraphQL language format.</summary>
public class InvalidFormatException : LanguageException
{
    /// <summary>Creates an invalid-format exception.</summary>
    public InvalidFormatException() { }
    /// <summary>Creates an invalid-format exception with a message.</summary>
    public InvalidFormatException(string? message) : base(message) { }
    /// <summary>Creates an invalid-format exception with a message and inner exception.</summary>
    public InvalidFormatException(string? message, Exception? innerException) : base(message, innerException) { }
}

/// <summary>Reports a syntax error with its source position.</summary>
public class SyntaxException : Exception
{
    /// <summary>Creates a syntax exception.</summary>
    public SyntaxException() { }
    /// <summary>Creates a syntax exception with a message.</summary>
    public SyntaxException(string? message) : base(message) { }
    /// <summary>Creates a syntax exception with a message and inner exception.</summary>
    public SyntaxException(string? message, Exception? innerException) : base(message, innerException) { }
    /// <summary>Creates a syntax exception with source coordinates.</summary>
    public SyntaxException(string? message, int position, int line, int column) : base(message)
    {
        Position = position;
        Line = line;
        Column = column;
    }

    /// <summary>Gets the zero-based UTF-16 source position.</summary>
    public int Position { get; }
    /// <summary>Gets the one-based source line.</summary>
    public int Line { get; }
    /// <summary>Gets the one-based source column.</summary>
    public int Column { get; }
}
