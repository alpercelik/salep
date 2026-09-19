namespace GraphQLParser;

/// <summary>Base class for executable selections.</summary>
public abstract class SelectionNode : AstNode
{
    /// <summary>Creates a selection with a stable node kind and source location.</summary>
    protected SelectionNode(AstNodeKind kind, SourceLocation location)
        : base(kind, location)
    {
    }
}

/// <summary>A non-empty ordered set of field and fragment selections.</summary>
public sealed class SelectionSetNode : AstNode
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
    public AstNodeList<SelectionNode> Selections { get; }
}

/// <summary>A field selection, optionally aliased and nested.</summary>
public sealed class FieldNode : SelectionNode
{
    /// <summary>Creates an immutable field selection.</summary>
    public FieldNode(
        NameNode name,
        NameNode? alias,
        IEnumerable<ArgumentNode> arguments,
        IEnumerable<DirectiveNode> directives,
        SelectionSetNode? selectionSet,
        SourceLocation location)
        : base(AstNodeKind.Field, location)
    {
        ArgumentNullException.ThrowIfNull(name);
        ArgumentNullException.ThrowIfNull(arguments);
        ArgumentNullException.ThrowIfNull(directives);
        Name = name;
        Alias = alias;
        Arguments = new AstNodeList<ArgumentNode>(arguments);
        Directives = new AstNodeList<DirectiveNode>(directives);
        SelectionSet = selectionSet;
    }

    /// <summary>Gets the field name.</summary>
    public NameNode Name { get; }
    /// <summary>Gets the optional alias.</summary>
    public NameNode? Alias { get; }
    /// <summary>Gets arguments in source order.</summary>
    public AstNodeList<ArgumentNode> Arguments { get; }
    /// <summary>Gets directives in source order.</summary>
    public AstNodeList<DirectiveNode> Directives { get; }
    /// <summary>Gets the optional nested selection set.</summary>
    public SelectionSetNode? SelectionSet { get; }
}

/// <summary>A spread of a named fragment.</summary>
public sealed class FragmentSpreadNode : SelectionNode
{
    /// <summary>Creates an immutable named fragment spread.</summary>
    public FragmentSpreadNode(NameNode name, IEnumerable<DirectiveNode> directives, SourceLocation location)
        : base(AstNodeKind.FragmentSpread, location)
    {
        ArgumentNullException.ThrowIfNull(name);
        ArgumentNullException.ThrowIfNull(directives);
        Name = name;
        Directives = new AstNodeList<DirectiveNode>(directives);
    }

    /// <summary>Gets the referenced fragment name.</summary>
    public NameNode Name { get; }
    /// <summary>Gets directives in source order.</summary>
    public AstNodeList<DirectiveNode> Directives { get; }
}

/// <summary>An inline fragment, with an optional type condition.</summary>
public sealed class InlineFragmentNode : SelectionNode
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
    public AstNodeList<DirectiveNode> Directives { get; }
    /// <summary>Gets the required nested selection set.</summary>
    public SelectionSetNode SelectionSet { get; }
}
