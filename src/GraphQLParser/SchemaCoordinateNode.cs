namespace GraphQLParser;

/// <summary>Base class for schema-coordinate syntax.</summary>
public abstract class SchemaCoordinateNode : AstNode
{
    /// <summary>Creates a schema-coordinate node.</summary>
    protected SchemaCoordinateNode(AstNodeKind kind, SourceLocation location) : base(kind, location) { }
}

/// <summary>A named type coordinate.</summary>
public sealed class TypeCoordinateNode(NameNode name, SourceLocation location) : SchemaCoordinateNode(AstNodeKind.TypeCoordinate, location)
{
    /// <summary>Gets the type name.</summary>
    public NameNode Name { get; } = name ?? throw new ArgumentNullException(nameof(name));
}

/// <summary>A type field coordinate.</summary>
public sealed class MemberCoordinateNode(NameNode name, NameNode memberName, SourceLocation location) : SchemaCoordinateNode(AstNodeKind.MemberCoordinate, location)
{
    /// <summary>Gets the type name.</summary>
    public NameNode Name { get; } = name ?? throw new ArgumentNullException(nameof(name));
    /// <summary>Gets the field name.</summary>
    public NameNode MemberName { get; } = memberName ?? throw new ArgumentNullException(nameof(memberName));
}

/// <summary>A field argument coordinate.</summary>
public sealed class ArgumentCoordinateNode(NameNode name, NameNode fieldName, NameNode argumentName, SourceLocation location) : SchemaCoordinateNode(AstNodeKind.ArgumentCoordinate, location)
{
    /// <summary>Gets the type name.</summary>
    public NameNode Name { get; } = name ?? throw new ArgumentNullException(nameof(name));
    /// <summary>Gets the field name.</summary>
    public NameNode FieldName { get; } = fieldName ?? throw new ArgumentNullException(nameof(fieldName));
    /// <summary>Gets the argument name.</summary>
    public NameNode ArgumentName { get; } = argumentName ?? throw new ArgumentNullException(nameof(argumentName));
}

/// <summary>A directive coordinate.</summary>
public sealed class DirectiveCoordinateNode(NameNode name, SourceLocation location) : SchemaCoordinateNode(AstNodeKind.DirectiveCoordinate, location)
{
    /// <summary>Gets the directive name.</summary>
    public NameNode Name { get; } = name ?? throw new ArgumentNullException(nameof(name));
}

/// <summary>A directive argument coordinate.</summary>
public sealed class DirectiveArgumentCoordinateNode(NameNode name, NameNode argumentName, SourceLocation location) : SchemaCoordinateNode(AstNodeKind.DirectiveArgumentCoordinate, location)
{
    /// <summary>Gets the directive name.</summary>
    public NameNode Name { get; } = name ?? throw new ArgumentNullException(nameof(name));
    /// <summary>Gets the argument name.</summary>
    public NameNode ArgumentName { get; } = argumentName ?? throw new ArgumentNullException(nameof(argumentName));
}
