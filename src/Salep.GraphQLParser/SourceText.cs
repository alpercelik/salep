namespace Salep.GraphQLParser;

/// <summary>
/// Provides a read-only view over GraphQL source without copying its characters.
/// </summary>
/// <remarks>
/// <see cref="GraphQLParser.Parse(SourceText)"/> snapshots this view into immutable storage by default.
/// The explicitly borrowed parser entry points retain this memory; their callers must keep its
/// backing storage alive and unchanged while parsing and while the resulting syntax tree is in use.
/// </remarks>
public readonly struct SourceText
{
    private readonly ReadOnlyMemory<char> _content;

    /// <summary>
    /// Creates a source view over caller-owned memory without copying it.
    /// </summary>
    public SourceText(ReadOnlyMemory<char> content)
    {
        _content = content;
    }

    /// <summary>
    /// Gets the original caller-owned source memory.
    /// </summary>
    public ReadOnlyMemory<char> Content => _content;

    /// <summary>
    /// Gets the number of UTF-16 code units in the source.
    /// </summary>
    public int Length => _content.Length;

    /// <summary>
    /// Gets a source slice without copying its characters.
    /// </summary>
    /// <exception cref="ArgumentOutOfRangeException">The requested range is outside the source.</exception>
    public ReadOnlyMemory<char> Slice(int start, int length) => _content.Slice(start, length);

    internal SourceText ToOwned() => new(_content.ToString().AsMemory());
}
