namespace GraphQLParser;

/// <summary>
/// Provides a read-only view over GraphQL source without copying its characters.
/// </summary>
/// <remarks>
/// The owner of the memory must keep it alive and must not mutate its backing storage
/// while parsing is in progress or while source-backed syntax nodes are in use.
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
}
