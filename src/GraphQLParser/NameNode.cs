namespace GraphQLParser;

/// <summary>A source-backed GraphQL name.</summary>
/// <remarks>
/// The value references the original caller-owned source when created from a token. The source
/// must remain alive and unchanged while this node or any syntax node containing it is in use.
/// </remarks>
public sealed class NameNode : AstNode
{
    /// <summary>Creates a name node without copying its value.</summary>
    public NameNode(ReadOnlyMemory<char> value, SourceLocation location)
        : base(AstNodeKind.Name, location)
    {
        if (value.Length != location.Length)
        {
            throw new ArgumentException("A source-backed name must have the same length as its source location.", nameof(value));
        }

        Value = value;
    }

    /// <summary>Gets the source-backed name text.</summary>
    public ReadOnlyMemory<char> Value { get; }
}
