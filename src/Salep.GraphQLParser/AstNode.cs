namespace Salep.GraphQLParser;

/// <summary>Base class for immutable GraphQL syntax nodes.</summary>
public abstract class AstNode : ISyntaxNode
{
    private readonly SourceLocation _sourceRange;

    /// <summary>Creates a node with its stable kind and source location.</summary>
    protected AstNode(AstNodeKind kind, SourceLocation location)
    {
        AstKind = kind;
        _sourceRange = location;
        Location = new Location(location.Start, location.End, location.Line, location.Column, location.HasLocation);
    }

    /// <summary>Gets the stable syntax classification of this node.</summary>
    /// <summary>Gets the parser's internal node category.</summary>
    public AstNodeKind AstKind { get; }

    /// <summary>Gets the normalized syntax kind.</summary>
    public SyntaxKind Kind => SyntaxCompatibility.ToSyntaxKind(AstKind);

    /// <summary>Gets this node's half-open source range.</summary>
    public Location Location { get; }

    /// <summary>Gets the parser's half-open source range and location-availability flag.</summary>
    internal SourceLocation SourceRange => _sourceRange;

    /// <summary>Gets whether this node exposes source location metadata.</summary>
    public bool HasLocation => _sourceRange.HasLocation;

    Location ISyntaxNode.Location => Location;

    /// <summary>Returns child syntax nodes in source order.</summary>
    public IEnumerable<ISyntaxNode> GetNodes() => GraphQLAstVisitor.GetSyntaxChildren(this);

    /// <summary>Prints this node as GraphQL source.</summary>
    public override string ToString() => GraphQLPrinter.Print(this);

    /// <summary>Prints this node as GraphQL source.</summary>
    public string ToString(bool indented) => GraphQLPrinter.Print(this, indented);
}
