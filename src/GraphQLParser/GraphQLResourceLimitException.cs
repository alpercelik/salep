namespace GraphQLParser;

/// <summary>Reports that parsing exceeded a configured input or work limit.</summary>
public sealed class GraphQLResourceLimitException : Exception
{
    internal GraphQLResourceLimitException(string resource, int limit, int observed, SourceLocation location)
        : base($"GraphQL {resource} limit exceeded: limit {limit}, observed at least {observed}.")
    {
        Resource = resource;
        Limit = limit;
        Observed = observed;
        Location = location;
    }

    /// <summary>Gets the resource whose configured limit was exceeded.</summary>
    public string Resource { get; }
    /// <summary>Gets the configured maximum.</summary>
    public int Limit { get; }
    /// <summary>Gets the observed value, or a lower bound when scanning stops at the limit.</summary>
    public int Observed { get; }
    /// <summary>Gets the half-open UTF-16 source location where the limit was crossed.</summary>
    public SourceLocation Location { get; }
}
