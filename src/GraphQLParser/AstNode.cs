namespace GraphQLParser;

/// <summary>Base class for immutable GraphQL syntax nodes.</summary>
public abstract class AstNode
{
    /// <summary>Creates a node with its stable kind and source location.</summary>
    protected AstNode(AstNodeKind kind, SourceLocation location)
    {
        Kind = kind;
        Location = location;
    }

    /// <summary>Gets the stable syntax classification of this node.</summary>
    public AstNodeKind Kind { get; }

    /// <summary>Gets this node's half-open source range.</summary>
    public SourceLocation Location { get; }
}
