namespace GraphQLParser;

/// <summary>Base class for GraphQL type references.</summary>
public abstract class TypeNode : AstNode, ITypeNode, INullableTypeNode
{
    /// <summary>Creates a type reference with a stable node kind and source location.</summary>
    protected TypeNode(AstNodeKind kind, SourceLocation location)
        : base(kind, location)
    {
    }
}

/// <summary>A named GraphQL type reference.</summary>
public sealed partial class NamedTypeNode : TypeNode, INullableTypeNode
{
    /// <summary>Creates an immutable named type.</summary>
    public NamedTypeNode(NameNode name, SourceLocation location)
        : base(AstNodeKind.NamedType, location)
    {
        ArgumentNullException.ThrowIfNull(name);
        Name = name;
    }

    /// <summary>Gets the type name.</summary>
    public NameNode Name { get; }
}

/// <summary>A list-wrapped type reference.</summary>
public sealed partial class ListTypeNode : TypeNode, INullableTypeNode
{
    /// <summary>Creates an immutable list type.</summary>
    public ListTypeNode(TypeNode type, SourceLocation location)
        : base(AstNodeKind.ListType, location)
    {
        ArgumentNullException.ThrowIfNull(type);
        Type = type;
    }

    /// <summary>Gets the list item type.</summary>
    public ITypeNode Type { get; }
}

/// <summary>A non-null type wrapper.</summary>
public sealed partial class NonNullTypeNode : TypeNode
{
    /// <summary>Creates an immutable non-null type wrapper.</summary>
    public NonNullTypeNode(TypeNode type, SourceLocation location)
        : base(AstNodeKind.NonNullType, location)
    {
        ArgumentNullException.ThrowIfNull(type);
        if (type is NonNullTypeNode)
        {
            throw new ArgumentException("A non-null type cannot directly wrap another non-null type.", nameof(type));
        }

        Type = type;
    }

    /// <summary>Gets the wrapped type.</summary>
    public INullableTypeNode Type { get; }
}
