namespace GraphQLParser;

/// <summary>A zero-based, half-open source range measured in UTF-16 code units.</summary>
public readonly record struct SourceLocation
{
    /// <summary>Creates a validated source range.</summary>
    /// <exception cref="ArgumentOutOfRangeException">The range starts below zero or ends before it starts.</exception>
    public SourceLocation(int start, int end)
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
    }

    /// <summary>Gets the inclusive start offset.</summary>
    public int Start { get; }

    /// <summary>Gets the exclusive end offset.</summary>
    public int End { get; }

    /// <summary>Gets the number of UTF-16 code units in the range.</summary>
    public int Length => End - Start;
}
