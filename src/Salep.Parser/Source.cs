namespace Salep.Parser;

/// <summary>Identifies GraphQL source text and its display offset within a containing file.</summary>
public sealed class Source
{
    /// <summary>Creates source metadata over an immutable string body.</summary>
    public Source(string body, string name = "GraphQL request")
        : this(body, name, new SourceLocationOffset(1, 1))
    {
    }

    /// <summary>Creates source metadata with an explicit display offset.</summary>
    public Source(string body, string name, SourceLocationOffset locationOffset)
    {
        ArgumentNullException.ThrowIfNull(body);
        ArgumentNullException.ThrowIfNull(name);
        if (locationOffset.Line <= 0) throw new ArgumentOutOfRangeException(nameof(locationOffset), "Line numbers are 1-indexed and must be positive.");
        if (locationOffset.Column <= 0) throw new ArgumentOutOfRangeException(nameof(locationOffset), "Column numbers are 1-indexed and must be positive.");
        Body = body;
        Name = name;
        LocationOffset = locationOffset;
    }

    /// <summary>Creates source metadata with an explicit display offset and the default name.</summary>
    public Source(string body, SourceLocationOffset locationOffset)
        : this(body, "GraphQL request", locationOffset)
    {
    }

    /// <summary>Gets the source body.</summary>
    public string Body { get; }
    /// <summary>Gets the display name for this source.</summary>
    public string Name { get; }
    /// <summary>Gets the 1-based display offset of the first source line.</summary>
    public SourceLocationOffset LocationOffset { get; }
}

/// <summary>A 1-based source line and column offset.</summary>
public readonly record struct SourceLocationOffset
{
    /// <summary>Creates a validated line and column offset.</summary>
    public SourceLocationOffset(int line, int column)
    {
        if (line <= 0) throw new ArgumentOutOfRangeException(nameof(line), "Line numbers are 1-indexed and must be positive.");
        if (column <= 0) throw new ArgumentOutOfRangeException(nameof(column), "Column numbers are 1-indexed and must be positive.");
        Line = line;
        Column = column;
    }

    /// <summary>Gets the 1-based line number.</summary>
    public int Line { get; }
    /// <summary>Gets the 1-based column number.</summary>
    public int Column { get; }
}

/// <summary>A 1-based source line and column.</summary>
public readonly record struct GraphQLSourceLocation
{
    /// <summary>Creates a validated source line and column.</summary>
    public GraphQLSourceLocation(int line, int column)
    {
        if (line <= 0) throw new ArgumentOutOfRangeException(nameof(line));
        if (column <= 0) throw new ArgumentOutOfRangeException(nameof(column));
        Line = line;
        Column = column;
    }

    /// <summary>Gets the 1-based line number.</summary>
    public int Line { get; }
    /// <summary>Gets the 1-based column number.</summary>
    public int Column { get; }
}
