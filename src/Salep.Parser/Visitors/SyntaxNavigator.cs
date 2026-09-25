namespace Salep.Parser.Visitors;

/// <summary>Provides a navigator for syntax visitors and rewriters.</summary>
public interface INavigatorContext
{
    /// <summary>Gets the syntax navigator.</summary>
    ISyntaxNavigator Navigator { get; }
}

/// <summary>Context containing the default syntax navigator.</summary>
public sealed class NavigatorContext : INavigatorContext
{
    /// <summary>Creates a context with an empty navigator.</summary>
    public NavigatorContext() => Navigator = new DefaultSyntaxNavigator();

    /// <inheritdoc />
    public ISyntaxNavigator Navigator { get; }
}

/// <summary>Provides stack navigation and syntax ancestry queries.</summary>
public interface ISyntaxNavigator
{
    /// <summary>Gets the number of nodes in the current path.</summary>
    int Count { get; }
    /// <summary>Creates the schema coordinate represented by the current node and ancestry.</summary>
    SchemaCoordinateNode CreateCoordinate();
    /// <summary>Gets the nearest ancestor assignable to the requested type.</summary>
    TNode GetAncestor<TNode>() where TNode : class, ISyntaxNode;
    /// <summary>Enumerates matching ancestors from nearest to farthest.</summary>
    IEnumerable<TNode> GetAncestors<TNode>() where TNode : class, ISyntaxNode;
    /// <summary>Gets the current node.</summary>
    ISyntaxNode Peek();
    /// <summary>Gets the node at a zero-based depth from the top.</summary>
    ISyntaxNode Peek(int count);
    /// <summary>Removes and returns the current node.</summary>
    ISyntaxNode Pop();
    /// <summary>Adds a node to the current path.</summary>
    void Push(ISyntaxNode node);
    /// <summary>Attempts to get the current node.</summary>
    bool TryPeek(out ISyntaxNode? node);
    /// <summary>Attempts to get a node at a zero-based depth from the top.</summary>
    bool TryPeek(int count, out ISyntaxNode? node);
    /// <summary>Attempts to remove the current node.</summary>
    bool TryPop(out ISyntaxNode? node);
}

/// <summary>Maintains the current node and its ancestors in a last-in-first-out stack.</summary>
public sealed class DefaultSyntaxNavigator : ISyntaxNavigator
{
    private readonly List<ISyntaxNode> _nodes = [];

    /// <inheritdoc />
    public int Count => _nodes.Count;

    /// <inheritdoc />
    public void Push(ISyntaxNode node)
    {
        ArgumentNullException.ThrowIfNull(node);
        _nodes.Add(node);
    }

    /// <inheritdoc />
    public ISyntaxNode Peek() => Peek(0);

    /// <inheritdoc />
    public ISyntaxNode Peek(int count)
    {
        if (!TryPeek(count, out var node))
            throw new InvalidOperationException("The navigator does not contain a node at the requested depth.");
        return node!;
    }

    /// <inheritdoc />
    public bool TryPeek(out ISyntaxNode? node) => TryPeek(0, out node);

    /// <inheritdoc />
    public bool TryPeek(int count, out ISyntaxNode? node)
    {
        if (count < 0) throw new ArgumentOutOfRangeException(nameof(count));
        var index = _nodes.Count - count - 1;
        if ((uint)index >= (uint)_nodes.Count)
        {
            node = null;
            return false;
        }

        node = _nodes[index];
        return true;
    }

    /// <inheritdoc />
    public ISyntaxNode Pop()
    {
        if (!TryPop(out var node))
            throw new InvalidOperationException("The navigator is empty.");
        return node!;
    }

    /// <inheritdoc />
    public bool TryPop(out ISyntaxNode? node)
    {
        if (_nodes.Count == 0)
        {
            node = null;
            return false;
        }

        var index = _nodes.Count - 1;
        node = _nodes[index];
        _nodes.RemoveAt(index);
        return true;
    }

    /// <inheritdoc />
    public TNode GetAncestor<TNode>() where TNode : class, ISyntaxNode =>
        GetAncestors<TNode>().FirstOrDefault() ?? throw new InvalidOperationException($"No ancestor of type '{typeof(TNode).Name}' exists.");

    /// <inheritdoc />
    public IEnumerable<TNode> GetAncestors<TNode>() where TNode : class, ISyntaxNode
    {
        // The top entry is the node being visited; ancestors are returned nearest first.
        for (var index = _nodes.Count - 2; index >= 0; index--)
        {
            if (_nodes[index] is TNode node) yield return node;
        }
    }

    /// <inheritdoc />
    public SchemaCoordinateNode CreateCoordinate()
    {
        if (!TryPeek(out var current)) throw new InvalidOperationException("A coordinate cannot be created from an empty navigator.");
        var location = (SourceLocation)current!.Location;

        switch (current)
        {
            case ITypeDefinitionNode type:
                return new TypeCoordinateNode(type.Name, location);
            case ITypeExtensionNode type:
                return new TypeCoordinateNode(type.Name, location);
            case DirectiveDefinitionNode directive:
                return new DirectiveCoordinateNode(directive.Name, location);
            case FieldDefinitionNode field:
            {
                var ownerName = GetTypeOwnerName(GetAncestors<ISyntaxNode>());
                if (ownerName is null) throw MissingCoordinateAncestry(current);
                return new MemberCoordinateNode(ownerName, field.Name, location);
            }
            case InputValueDefinitionNode input:
            {
                var ancestors = GetAncestors<ISyntaxNode>().ToArray();
                var field = ancestors.OfType<FieldDefinitionNode>().FirstOrDefault();
                if (field is not null)
                {
                    var owner = GetTypeOwnerName(ancestors);
                    if (owner is null) throw MissingCoordinateAncestry(current);
                    return new ArgumentCoordinateNode(owner, field.Name, input.Name, location);
                }

                var directive = ancestors.OfType<DirectiveDefinitionNode>().FirstOrDefault();
                if (directive is not null)
                    return new DirectiveArgumentCoordinateNode(directive.Name, input.Name, location);
                throw MissingCoordinateAncestry(current);
            }
            case EnumValueDefinitionNode enumValue:
            {
                var ownerName = GetTypeOwnerName(GetAncestors<ISyntaxNode>());
                if (ownerName is null) throw MissingCoordinateAncestry(current);
                return new MemberCoordinateNode(ownerName, enumValue.Name, location);
            }
            default:
                throw new InvalidOperationException($"A schema coordinate cannot be created for syntax kind '{current.Kind}'.");
        }
    }

    private static InvalidOperationException MissingCoordinateAncestry(ISyntaxNode node) =>
        new($"A schema coordinate for syntax kind '{node.Kind}' requires its containing type or directive in the navigator.");

    private static NameNode? GetTypeOwnerName(IEnumerable<ISyntaxNode> ancestors) =>
        ancestors.FirstOrDefault(node => node is ITypeDefinitionNode or ITypeExtensionNode) is INamedSyntaxNode named
            ? named.Name
            : null;
}
