using Salep.GraphQLParser.Utilities;

namespace Salep.GraphQLParser;

/// <summary>Provides common operations on syntax nodes and type references.</summary>
public abstract class SyntaxNodeExtensions
{
    private SyntaxNodeExtensions() { }

    /// <summary>Returns the immediate element type of a list type.</summary>
    public static ITypeNode ElementType(ITypeNode type)
    {
        ArgumentNullException.ThrowIfNull(type);
        return type is ListTypeNode list ? list.Type : type;
    }

    /// <summary>Compares syntax nodes according to the selected mode.</summary>
    public static bool Equals(ISyntaxNode node, ISyntaxNode other, SyntaxComparison comparison)
    {
        ArgumentNullException.ThrowIfNull(node);
        ArgumentNullException.ThrowIfNull(other);
        var comparer = comparison switch
        {
            SyntaxComparison.Reference => SyntaxComparer.ByReference,
            SyntaxComparison.Syntax => SyntaxComparer.BySyntax,
            SyntaxComparison.SyntaxIgnoreDescriptions => SyntaxComparer.BySyntaxIgnoreDescriptions,
            _ => throw new ArgumentOutOfRangeException(nameof(comparison)),
        };
        return comparer.Equals(node, other);
    }

    /// <summary>Returns the innermost named type within list and non-null wrappers.</summary>
    public static ITypeNode InnerType(ITypeNode type)
    {
        ArgumentNullException.ThrowIfNull(type);
        while (type is ListTypeNode or NonNullTypeNode)
            type = type is ListTypeNode list ? list.Type : ((NonNullTypeNode)type).Type;
        return type;
    }

    /// <summary>Determines whether a type reference is a list.</summary>
    public static bool IsListType(ITypeNode type) => type is ListTypeNode;
    /// <summary>Determines whether a type reference is non-null.</summary>
    public static bool IsNonNullType(ITypeNode type) => type is NonNullTypeNode;
    /// <summary>Determines whether a value is the GraphQL null literal.</summary>
    public static bool IsNull(IValueNode value) => value is NullValueNode;

    /// <summary>Returns the named type inside list and non-null wrappers.</summary>
    public static NamedTypeNode NamedType(ITypeNode type) => InnerType(type) as NamedTypeNode
        ?? throw new ArgumentException("The type reference does not contain a named type.", nameof(type));

    /// <summary>Removes a non-null wrapper from a type, if present.</summary>
    public static ITypeNode NullableType(ITypeNode type)
    {
        ArgumentNullException.ThrowIfNull(type);
        return type is NonNullTypeNode nonNull ? nonNull.Type : type;
    }

    /// <summary>Formats a syntax node with the provided serializer options.</summary>
    public static string ToString(ISyntaxNode node, SyntaxSerializerOptions options)
    {
        ArgumentNullException.ThrowIfNull(node);
        return SyntaxPrinter.Print(node, options.Indented);
    }
}
