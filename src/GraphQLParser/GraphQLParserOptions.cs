namespace GraphQLParser;

/// <summary>Immutable input and work limits for GraphQL parsing.</summary>
public sealed class GraphQLParserOptions
{
    /// <summary>Creates parsing limits. Defaults allow documents up to 1 Mi UTF-16 units, 250,000 tokens, nesting 128, and 100 diagnostics.</summary>
    public GraphQLParserOptions(int maximumSourceLength = 1_048_576, int maximumTokenCount = 250_000, int maximumNestingDepth = 128, int maximumDiagnosticCount = GraphQLParser.MaximumDiagnosticCount)
    {
        if (maximumSourceLength <= 0) throw new ArgumentOutOfRangeException(nameof(maximumSourceLength));
        if (maximumTokenCount <= 0) throw new ArgumentOutOfRangeException(nameof(maximumTokenCount));
        if (maximumNestingDepth <= 0) throw new ArgumentOutOfRangeException(nameof(maximumNestingDepth));
        if (maximumDiagnosticCount is <= 0 or > 10_000) throw new ArgumentOutOfRangeException(nameof(maximumDiagnosticCount), "Diagnostic limits must be between 1 and 10,000.");

        MaximumSourceLength = maximumSourceLength;
        MaximumTokenCount = maximumTokenCount;
        MaximumNestingDepth = maximumNestingDepth;
        MaximumDiagnosticCount = maximumDiagnosticCount;
    }

    /// <summary>Gets the shared default parser limits.</summary>
    public static GraphQLParserOptions Default { get; } = new();
    /// <summary>Gets the maximum source length in UTF-16 code units.</summary>
    public int MaximumSourceLength { get; }
    /// <summary>Gets the maximum number of non-EOF tokens.</summary>
    public int MaximumTokenCount { get; }
    /// <summary>Gets the maximum combined nesting depth of braces, parentheses, and brackets.</summary>
    public int MaximumNestingDepth { get; }
    /// <summary>Gets the maximum number of collected diagnostics.</summary>
    public int MaximumDiagnosticCount { get; }
}
