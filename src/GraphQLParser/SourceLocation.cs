namespace GraphQLParser;

/// <summary>A zero-based, half-open source range measured in UTF-16 code units.</summary>
public readonly record struct SourceLocation
{
    /// <summary>Creates a validated source range.</summary>
    /// <exception cref="ArgumentOutOfRangeException">The range starts below zero or ends before it starts.</exception>
    public SourceLocation(int start, int end, bool hasLocation = true)
        : this(start, end, 1, start + 1, hasLocation)
    {
    }

    internal SourceLocation(int start, int end, int line, int column, bool hasLocation = true)
    {
        if (start < 0)
        {
            throw new ArgumentOutOfRangeException(nameof(start));
        }

        if (end < start)
        {
            throw new ArgumentOutOfRangeException(nameof(end));
        }

        Start = start;
        End = end;
        HasLocation = hasLocation;
        Line = line;
        Column = column;
    }

    /// <summary>Gets the inclusive start offset.</summary>
    public int Start { get; }

    /// <summary>Gets the exclusive end offset.</summary>
    public int End { get; }

    /// <summary>Gets whether this range should be exposed as an AST location.</summary>
    public bool HasLocation { get; }

    /// <summary>Gets the one-based source line.</summary>
    public int Line { get; }

    /// <summary>Gets the one-based source column.</summary>
    public int Column { get; }

    /// <summary>Gets the number of UTF-16 code units in the range.</summary>
    public int Length => End - Start;

    /// <summary>Determines whether source ranges and location visibility are equal.</summary>
    public bool Equals(SourceLocation other) => Start == other.Start && End == other.End && HasLocation == other.HasLocation;

    /// <summary>Returns a hash code for this source range.</summary>
    public override int GetHashCode() => HashCode.Combine(Start, End, HasLocation);
}
