using System.Collections;

namespace GraphQLParser;

/// <summary>An immutable snapshot of an ordered AST child sequence.</summary>
public sealed class AstNodeList<TNode> : IReadOnlyList<TNode>
{
    private readonly TNode[] _items;

    /// <summary>Copies the supplied nodes into an immutable ordered collection.</summary>
    public AstNodeList(IEnumerable<TNode> items)
    {
        ArgumentNullException.ThrowIfNull(items);
        _items = items.ToArray();
        for (var index = 0; index < _items.Length; index++)
        {
            ArgumentNullException.ThrowIfNull(_items[index], nameof(items));
        }
    }

    /// <summary>Gets a shared empty child list.</summary>
    public static AstNodeList<TNode> Empty { get; } = new(Array.Empty<TNode>());

    /// <summary>Gets the number of nodes in this collection.</summary>
    public int Count => _items.Length;

    /// <summary>Gets the node at the specified zero-based index.</summary>
    public TNode this[int index] => _items[index];

    /// <inheritdoc />
    public IEnumerator<TNode> GetEnumerator() => ((IEnumerable<TNode>)_items).GetEnumerator();

    IEnumerator IEnumerable.GetEnumerator() => _items.GetEnumerator();
}
