namespace GraphQLParser;

/// <summary>A source-backed GraphQL name.</summary>
/// <remarks>
/// The value references its parser source when created from a token. Default parser entry points
/// use an immutable snapshot; explicitly borrowed entry points require the caller to keep source
/// memory alive and unchanged while this node or any containing syntax tree is in use.
/// </remarks>
public sealed partial class NameNode : AstNode, IEquatable<NameNode>
{
    private string? _value;

    /// <summary>Creates a name node without copying its value.</summary>
    public NameNode(ReadOnlyMemory<char> value, SourceLocation location)
        : base(AstNodeKind.Name, location)
    {
        if (value.Length != location.Length)
        {
            throw new ArgumentException("A source-backed name must have the same length as its source location.", nameof(value));
        }

        SourceValue = value;
    }

    /// <summary>Creates a name node backed by an immutable string.</summary>
    public NameNode(string value, SourceLocation location)
        : base(AstNodeKind.Name, location)
    {
        ArgumentNullException.ThrowIfNull(value);
        SourceValue = value.AsMemory();
        _value = value;
    }

    /// <summary>Creates a name node without explicit source metadata.</summary>
    public NameNode(string value)
        : this(value, new SourceLocation(0, (value ?? throw new ArgumentNullException(nameof(value))).Length, false))
    {
    }

    /// <summary>Creates a name node with public compatibility location metadata.</summary>
    public NameNode(Location location, string value)
        : this(value, (SourceLocation)(location ?? throw new ArgumentNullException(nameof(location))))
    {
    }

    /// <summary>Creates a name node with explicit source range metadata.</summary>
    public NameNode(SourceLocation location, string value)
        : this(value, location)
    {
    }

    /// <summary>Gets the name text as a string.</summary>
    public string Value => _value ??= SourceValue.ToString();

    /// <summary>Gets the source-backed name text without copying it.</summary>
    public ReadOnlyMemory<char> SourceValue { get; }

    /// <summary>Returns a copy with a different source location.</summary>
    public NameNode WithLocation(Location location) => new(location ?? throw new ArgumentNullException(nameof(location)), Value);

    /// <summary>Returns a copy with a different name value.</summary>
    public NameNode WithValue(string value) => new(value, SourceRange);

    /// <summary>Determines whether this name has the same kind, source range, and value as another name.</summary>
    public bool Equals(NameNode? other) => other is not null && Kind == other.Kind && SourceRange == other.SourceRange && Value == other.Value;
    /// <summary>Determines whether this name equals another object.</summary>
    public override bool Equals(object? obj) => obj is NameNode other && Equals(other);
    /// <summary>Returns the hash code for this name.</summary>
    public override int GetHashCode() => HashCode.Combine(Kind, SourceRange, Value);
}
