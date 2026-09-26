namespace Salep.GraphQLParser;

/// <summary>Base class for executable selections.</summary>
#pragma warning disable CA1715 // SelectionNode is the established public GraphQL AST name; renaming it would break API compatibility.
public interface SelectionNode : ISelectionNode { }
#pragma warning restore CA1715

/// <summary>A non-empty ordered set of field and fragment selections.</summary>
public sealed partial class SelectionSetNode : AstNode
{
    /// <summary>Creates a non-empty immutable selection set.</summary>
    public SelectionSetNode(IEnumerable<SelectionNode> selections, SourceLocation location)
        : base(AstNodeKind.SelectionSet, location)
    {
        Selections = new AstNodeList<SelectionNode>(selections);
        if (Selections.Count == 0)
        {
            throw new ArgumentException("A selection set must contain at least one selection.", nameof(selections));
        }

        for (var index = 0; index < Selections.Count; index++)
        {
            var selectionLocation = Selections[index].Location;
            if (selectionLocation.Start < location.Start || selectionLocation.End > location.End)
            {
                throw new ArgumentException("Every selection must be contained by its selection set.", nameof(selections));
            }

            if (index > 0 && selectionLocation.Start < Selections[index - 1].Location.Start)
            {
                throw new ArgumentException("Selections must be in source order.", nameof(selections));
            }
        }
    }

    /// <summary>Gets selections in source order.</summary>
    public IReadOnlyList<ISelectionNode> Selections { get; }
}

/// <summary>A field selection, optionally aliased and nested.</summary>
public sealed partial class FieldNode : NamedSyntaxNode, SelectionNode
{
    /// <summary>Creates an immutable field selection.</summary>
    public FieldNode(
        NameNode name,
        NameNode? alias,
        IEnumerable<ArgumentNode> arguments,
        IEnumerable<DirectiveNode> directives,
        SelectionSetNode? selectionSet,
        SourceLocation location)
        : base(AstNodeKind.Field, name, location)
    {
        ArgumentNullException.ThrowIfNull(name);
        ArgumentNullException.ThrowIfNull(arguments);
        ArgumentNullException.ThrowIfNull(directives);
        Alias = alias;
        Arguments = new AstNodeList<ArgumentNode>(arguments);
        Directives = new AstNodeList<DirectiveNode>(directives);
        SelectionSet = selectionSet;
    }

    /// <summary>Gets the field name.</summary>
    /// <summary>Gets the optional alias.</summary>
    public NameNode? Alias { get; }
    /// <summary>Gets arguments in source order.</summary>
    public IReadOnlyList<ArgumentNode> Arguments { get; }
    /// <summary>Gets directives in source order.</summary>
    public override AstNodeList<DirectiveNode> Directives { get; }
    internal override IReadOnlyList<DirectiveNode> ContractDirectives => Directives;
    IReadOnlyList<DirectiveNode> IHasDirectives.Directives => Directives;
    /// <summary>Gets the optional nested selection set.</summary>
    public SelectionSetNode? SelectionSet { get; }
}

/// <summary>A spread of a named fragment.</summary>
public sealed partial class FragmentSpreadNode : NamedSyntaxNode, SelectionNode
{
    /// <summary>Creates an immutable named fragment spread.</summary>
    public FragmentSpreadNode(NameNode name, IEnumerable<DirectiveNode> directives, SourceLocation location)
        : this(name, [], directives, location)
    {
    }

    /// <summary>Creates an immutable named fragment spread with arguments.</summary>
    public FragmentSpreadNode(Location location, NameNode name, IReadOnlyList<ArgumentNode> arguments, IReadOnlyList<DirectiveNode> directives)
        : this(name, arguments, directives, (SourceLocation)(location ?? throw new ArgumentNullException(nameof(location))))
    {
    }

    private FragmentSpreadNode(NameNode name, IEnumerable<ArgumentNode> arguments, IEnumerable<DirectiveNode> directives, SourceLocation location)
        : base(AstNodeKind.FragmentSpread, name, location)
    {
        ArgumentNullException.ThrowIfNull(name);
        ArgumentNullException.ThrowIfNull(arguments);
        ArgumentNullException.ThrowIfNull(directives);
        Arguments = new AstNodeList<ArgumentNode>(arguments);
        Directives = new AstNodeList<DirectiveNode>(directives);
    }

    /// <summary>Gets the referenced fragment name.</summary>
    /// <summary>Gets arguments supplied to the referenced fragment.</summary>
    public IReadOnlyList<ArgumentNode> Arguments { get; }
    /// <summary>Gets directives in source order.</summary>
    public override AstNodeList<DirectiveNode> Directives { get; }
    internal override IReadOnlyList<DirectiveNode> ContractDirectives => Directives;
    IReadOnlyList<DirectiveNode> IHasDirectives.Directives => Directives;
}

/// <summary>An inline fragment, with an optional type condition.</summary>
public sealed partial class InlineFragmentNode : AstNode, SelectionNode
{
    /// <summary>Creates an immutable inline fragment.</summary>
    public InlineFragmentNode(
        NamedTypeNode? typeCondition,
        IEnumerable<DirectiveNode> directives,
        SelectionSetNode selectionSet,
        SourceLocation location)
        : base(AstNodeKind.InlineFragment, location)
    {
        ArgumentNullException.ThrowIfNull(directives);
        ArgumentNullException.ThrowIfNull(selectionSet);
        TypeCondition = typeCondition;
        Directives = new AstNodeList<DirectiveNode>(directives);
        SelectionSet = selectionSet;
    }

    /// <summary>Gets the optional type condition.</summary>
    public NamedTypeNode? TypeCondition { get; }
    /// <summary>Gets directives in source order.</summary>
    public IReadOnlyList<DirectiveNode> Directives { get; }
    /// <summary>Gets the required nested selection set.</summary>
    public SelectionSetNode SelectionSet { get; }
}
