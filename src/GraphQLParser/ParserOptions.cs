namespace GraphQLParser;

/// <summary>Enables parser features that are not part of the stable grammar surface.</summary>
public sealed class ParserOptionsExperimental
{
    /// <summary>Creates a ParserOptionsExperimental value.</summary>
    public ParserOptionsExperimental(bool allowFragmentVariables = false, bool allowFragmentArguments = false)
    {
        AllowFragmentVariables = allowFragmentVariables;
        AllowFragmentArguments = allowFragmentArguments;
    }

    /// <summary>Gets the allowFragmentArguments value.</summary>
    public bool AllowFragmentArguments { get; }
    /// <summary>Gets the allowFragmentVariables value.</summary>
    public bool AllowFragmentVariables { get; }
}

/// <summary>Controls parser compatibility features and syntax resource limits.</summary>
public sealed class ParserOptions
{
    /// <summary>Creates a ParserOptions value.</summary>
    public ParserOptions(
        bool noLocations = false,
        bool allowFragmentVariables = false,
        int maxAllowedNodes = int.MaxValue,
        int maxAllowedTokens = int.MaxValue,
        int maxAllowedFields = 2048,
        int maxAllowedDirectives = 4,
        int maxAllowedRecursionDepth = 200)
        : this(new ParserOptionsExperimental(allowFragmentVariables), noLocations, maxAllowedNodes, maxAllowedTokens, maxAllowedFields, maxAllowedDirectives, maxAllowedRecursionDepth)
    {
    }

    /// <summary>Creates a ParserOptions value.</summary>
    public ParserOptions(
        ParserOptionsExperimental experimental,
        bool noLocations = false,
        int maxAllowedNodes = int.MaxValue,
        int maxAllowedTokens = int.MaxValue,
        int maxAllowedFields = 2048,
        int maxAllowedDirectives = 4,
        int maxAllowedRecursionDepth = 200)
    {
        ArgumentNullException.ThrowIfNull(experimental);
        ValidateLimit(maxAllowedNodes, nameof(maxAllowedNodes));
        ValidateLimit(maxAllowedTokens, nameof(maxAllowedTokens));
        ValidateLimit(maxAllowedFields, nameof(maxAllowedFields));
        ValidateLimit(maxAllowedDirectives, nameof(maxAllowedDirectives));
        ValidateLimit(maxAllowedRecursionDepth, nameof(maxAllowedRecursionDepth));
        Experimental = experimental;
        NoLocations = noLocations;
        MaxAllowedNodes = maxAllowedNodes;
        MaxAllowedTokens = maxAllowedTokens;
        MaxAllowedFields = maxAllowedFields;
        MaxAllowedDirectives = maxAllowedDirectives;
        MaxAllowedRecursionDepth = maxAllowedRecursionDepth;
    }

    /// <summary>Creates a new value.</summary>
    public static ParserOptions Default { get; } = new();
    /// <summary>Creates a new value.</summary>
    public static ParserOptions Trusted { get; } = new(new ParserOptionsExperimental(true, true), maxAllowedNodes: int.MaxValue, maxAllowedTokens: int.MaxValue,
        maxAllowedFields: int.MaxValue, maxAllowedDirectives: int.MaxValue, maxAllowedRecursionDepth: int.MaxValue);
    /// <summary>Gets the experimental value.</summary>
    public ParserOptionsExperimental Experimental { get; }
    /// <summary>Gets the maxAllowedNodes value.</summary>
    public int MaxAllowedNodes { get; }
    /// <summary>Gets the maxAllowedTokens value.</summary>
    public int MaxAllowedTokens { get; }
    /// <summary>Gets the maxAllowedFields value.</summary>
    public int MaxAllowedFields { get; }
    /// <summary>Gets the maxAllowedDirectives value.</summary>
    public int MaxAllowedDirectives { get; }
    /// <summary>Gets the maxAllowedRecursionDepth value.</summary>
    public int MaxAllowedRecursionDepth { get; }
    /// <summary>Gets the noLocations value.</summary>
    public bool NoLocations { get; }
    /// <summary>Creates a Copy value.</summary>
    public ParserOptions NoLocation => NoLocations ? this : Copy(noLocations: true);

    internal GraphQLParserOptions ToGraphQLParserOptions() => new(
        maximumSourceLength: GraphQLParserOptions.Default.MaximumSourceLength,
        maximumTokenCount: Math.Min(MaxAllowedTokens, GraphQLParserOptions.Default.MaximumTokenCount),
        maximumNestingDepth: MaxAllowedRecursionDepth,
        maximumDiagnosticCount: GraphQLParserOptions.Default.MaximumDiagnosticCount,
        noLocation: NoLocations,
        allowLegacyFragmentVariables: Experimental.AllowFragmentVariables);

    internal void ValidateTree(DocumentNode document)
    {
        var nodes = 0;
        var fields = 0;
        var directives = 0;
        GraphQLResourceLimitException? exceeded = null;
        GraphQLAstVisitor.Visit(document, node =>
        {
            if (++nodes > MaxAllowedNodes)
            {
                exceeded = new GraphQLResourceLimitException("syntax nodes", MaxAllowedNodes, nodes, node.Location);
                return GraphQLVisitControl.Stop;
            }

            if (node.AstKind == AstNodeKind.Field && ++fields > MaxAllowedFields)
            {
                exceeded = new GraphQLResourceLimitException("fields", MaxAllowedFields, fields, node.Location);
                return GraphQLVisitControl.Stop;
            }

            if (node.AstKind == AstNodeKind.Directive && ++directives > MaxAllowedDirectives)
            {
                exceeded = new GraphQLResourceLimitException("directives", MaxAllowedDirectives, directives, node.Location);
                return GraphQLVisitControl.Stop;
            }

            return GraphQLVisitControl.Continue;
        });
        if (exceeded is not null) throw exceeded;
    }

    private ParserOptions Copy(bool noLocations) => new(Experimental, noLocations, MaxAllowedNodes, MaxAllowedTokens, MaxAllowedFields, MaxAllowedDirectives, MaxAllowedRecursionDepth);

    private static void ValidateLimit(int value, string parameterName)
    {
        if (value <= 0) throw new ArgumentOutOfRangeException(parameterName, "Parser limits must be greater than zero.");
    }
}
