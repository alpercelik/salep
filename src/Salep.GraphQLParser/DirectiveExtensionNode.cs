namespace Salep.GraphQLParser;

/// <summary>An extension adding directives to a directive definition.</summary>
public sealed partial class DirectiveExtensionNode : NamedSyntaxNode, ITypeSystemExtensionNode
{
    /// <summary>Creates a directive extension at a source location.</summary>
    public DirectiveExtensionNode(Location location, NameNode name, IReadOnlyList<DirectiveNode> directives)
        : this(name, directives, (SourceLocation)(location ?? throw new ArgumentNullException(nameof(location)))) { }

    internal DirectiveExtensionNode(NameNode name, IEnumerable<DirectiveNode> directives, SourceLocation location)
        : base(AstNodeKind.DirectiveExtension, name, location)
    {
        ArgumentNullException.ThrowIfNull(name);
        ArgumentNullException.ThrowIfNull(directives);
        Directives = new AstNodeList<DirectiveNode>(directives);
    }

    /// <summary>Gets the directive name.</summary>
    /// <summary>Gets directives in source order.</summary>
    public override AstNodeList<DirectiveNode> Directives { get; }
    internal override IReadOnlyList<DirectiveNode> ContractDirectives => Directives;
}
