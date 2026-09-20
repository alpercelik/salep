namespace GraphQLParser;

/// <summary>Common location-aware syntax-node contract.</summary>
public interface ISyntaxNode
{
    /// <summary>Gets the node syntax kind.</summary>
    SyntaxKind Kind { get; }
    /// <summary>Gets the node source location.</summary>
    Location Location { get; }
    /// <summary>Returns this node children in source order.</summary>
    IEnumerable<ISyntaxNode> GetNodes();
    /// <summary>Prints this node as GraphQL source.</summary>
    string ToString();
    /// <summary>Prints this node with the requested indentation mode.</summary>
    string ToString(bool indented);
}

/// <summary>Marker interface for a top-level executable or type-system definition.</summary>
public interface IDefinitionNode : ISyntaxNode { }

/// <summary>Marker interface for an executable operation or fragment definition.</summary>
public interface IExecutableDefinitionNode : IDefinitionNode { }

/// <summary>Marker interface for a type-system definition.</summary>
public interface ITypeSystemDefinitionNode : IDefinitionNode { }

/// <summary>Marker interface for a type-system extension.</summary>
public interface ITypeSystemExtensionNode : IDefinitionNode { }

/// <summary>Exposes a syntax node's name.</summary>
public interface IHasName
{
    /// <summary>Gets the associated name node.</summary>
    NameNode Name { get; }
}

/// <summary>Exposes a syntax node's directives.</summary>
public interface IHasDirectives
{
    /// <summary>Gets associated directives in source order.</summary>
    IReadOnlyList<DirectiveNode> Directives { get; }
}

/// <summary>Syntax nodes with a name and directives.</summary>
public interface INamedSyntaxNode : IHasDirectives, IHasName, ISyntaxNode { }

/// <summary>Exposes common type-definition properties.</summary>
public interface ITypeDefinitionNode : INamedSyntaxNode, ITypeSystemDefinitionNode
{
    /// <summary>Gets the optional description.</summary>
    StringValueNode? Description { get; }
}

/// <summary>Exposes common type-extension properties.</summary>
public interface ITypeExtensionNode : INamedSyntaxNode, ITypeSystemExtensionNode { }

/// <summary>Marker interface for an executable selection.</summary>
public interface ISelectionNode : IHasDirectives, ISyntaxNode { }

/// <summary>Marker interface for a GraphQL input value syntax node.</summary>
public interface IValueNode : ISyntaxNode
{
    /// <summary>Gets the literal value in its CLR representation.</summary>
    object? Value { get; }
}

/// <summary>Typed GraphQL literal value.</summary>
public interface IValueNode<out T> : IValueNode
{
    /// <summary>Gets the typed value.</summary>
    new T Value { get; }
}

/// <summary>Marker interface for a GraphQL type reference syntax node.</summary>
public interface ITypeNode : ISyntaxNode { }

/// <summary>Marker interface for nullable type references.</summary>
public interface INullableTypeNode : ITypeNode { }

/// <summary>Exposes a UTF-8 representation of source-backed literal text.</summary>
public interface IHasSpan
{
    /// <summary>Returns the literal text encoded as UTF-8.</summary>
    ReadOnlySpan<byte> AsSpan();
}

/// <summary>Numeric value literals that support floating-point conversions.</summary>
public interface IFloatValueLiteral : IHasSpan
{
    /// <summary>Converts the value to a decimal number.</summary>
    decimal ToDecimal();
    /// <summary>Converts the value to a double-precision number.</summary>
    double ToDouble();
    /// <summary>Converts the value to a single-precision number.</summary>
    float ToSingle();
}

/// <summary>Integer value literals that support checked integral conversions.</summary>
public interface IIntValueLiteral : IFloatValueLiteral
{
    /// <summary>Converts the value to an unsigned 8-bit integer.</summary>
    byte ToByte();
    /// <summary>Converts the value to a signed 16-bit integer.</summary>
    short ToInt16();
    /// <summary>Converts the value to a signed 32-bit integer.</summary>
    int ToInt32();
    /// <summary>Converts the value to a signed 64-bit integer.</summary>
    long ToInt64();
    /// <summary>Converts the value to a signed 8-bit integer.</summary>
    sbyte ToSByte();
    /// <summary>Converts the value to an unsigned 16-bit integer.</summary>
    ushort ToUInt16();
    /// <summary>Converts the value to an unsigned 32-bit integer.</summary>
    uint ToUInt32();
    /// <summary>Converts the value to an unsigned 64-bit integer.</summary>
    ulong ToUInt64();
}

/// <summary>GraphQL syntax node classifications.</summary>
public enum SyntaxKind
{
    /// <summary>The Argument syntax kind.</summary>
    Argument = 7,
    /// <summary>The BooleanValue syntax kind.</summary>
    BooleanValue = 13,
    /// <summary>The Directive syntax kind.</summary>
    Directive = 19,
    /// <summary>The DirectiveDefinition syntax kind.</summary>
    DirectiveDefinition = 41,
    /// <summary>The DirectiveExtension syntax kind.</summary>
    DirectiveExtension = 44,
    /// <summary>The Document syntax kind.</summary>
    Document = 1,
    /// <summary>The EnumTypeDefinition syntax kind.</summary>
    EnumTypeDefinition = 31,
    /// <summary>The EnumTypeExtension syntax kind.</summary>
    EnumTypeExtension = 39,
    /// <summary>The EnumValue syntax kind.</summary>
    EnumValue = 15,
    /// <summary>The EnumValueDefinition syntax kind.</summary>
    EnumValueDefinition = 32,
    /// <summary>The Field syntax kind.</summary>
    Field = 6,
    /// <summary>The FieldDefinition syntax kind.</summary>
    FieldDefinition = 27,
    /// <summary>The FloatValue syntax kind.</summary>
    FloatValue = 42,
    /// <summary>The FragmentDefinition syntax kind.</summary>
    FragmentDefinition = 10,
    /// <summary>The FragmentSpread syntax kind.</summary>
    FragmentSpread = 8,
    /// <summary>The InlineFragment syntax kind.</summary>
    InlineFragment = 9,
    /// <summary>The InputObjectTypeDefinition syntax kind.</summary>
    InputObjectTypeDefinition = 33,
    /// <summary>The InputObjectTypeExtension syntax kind.</summary>
    InputObjectTypeExtension = 40,
    /// <summary>The InputValueDefinition syntax kind.</summary>
    InputValueDefinition = 28,
    /// <summary>The InterfaceTypeDefinition syntax kind.</summary>
    InterfaceTypeDefinition = 29,
    /// <summary>The InterfaceTypeExtension syntax kind.</summary>
    InterfaceTypeExtension = 37,
    /// <summary>The IntValue syntax kind.</summary>
    IntValue = 11,
    /// <summary>The ListType syntax kind.</summary>
    ListType = 21,
    /// <summary>The ListValue syntax kind.</summary>
    ListValue = 16,
    /// <summary>The Name syntax kind.</summary>
    Name = 0,
    /// <summary>The NamedType syntax kind.</summary>
    NamedType = 20,
    /// <summary>The NonNullType syntax kind.</summary>
    NonNullType = 22,
    /// <summary>The NullValue syntax kind.</summary>
    NullValue = 14,
    /// <summary>The ObjectField syntax kind.</summary>
    ObjectField = 18,
    /// <summary>The ObjectTypeDefinition syntax kind.</summary>
    ObjectTypeDefinition = 26,
    /// <summary>The ObjectTypeExtension syntax kind.</summary>
    ObjectTypeExtension = 36,
    /// <summary>The ObjectValue syntax kind.</summary>
    ObjectValue = 17,
    /// <summary>The OperationDefinition syntax kind.</summary>
    OperationDefinition = 2,
    /// <summary>The OperationTypeDefinition syntax kind.</summary>
    OperationTypeDefinition = 24,
    /// <summary>The ScalarTypeDefinition syntax kind.</summary>
    ScalarTypeDefinition = 25,
    /// <summary>The ScalarTypeExtension syntax kind.</summary>
    ScalarTypeExtension = 35,
    /// <summary>The SchemaCoordinate syntax kind.</summary>
    SchemaCoordinate = 43,
    /// <summary>The SchemaDefinition syntax kind.</summary>
    SchemaDefinition = 23,
    /// <summary>The SchemaExtension syntax kind.</summary>
    SchemaExtension = 34,
    /// <summary>The SelectionSet syntax kind.</summary>
    SelectionSet = 5,
    /// <summary>The StringValue syntax kind.</summary>
    StringValue = 12,
    /// <summary>The UnionTypeDefinition syntax kind.</summary>
    UnionTypeDefinition = 30,
    /// <summary>The UnionTypeExtension syntax kind.</summary>
    UnionTypeExtension = 38,
    /// <summary>The Variable syntax kind.</summary>
    Variable = 4,
    /// <summary>The VariableDefinition syntax kind.</summary>
    VariableDefinition = 3
}

/// <summary>One-based line and column plus a half-open UTF-16 source range.</summary>
public sealed class Location : IComparable<Location>, IEquatable<Location>
{
    /// <summary>Creates a source location with one-based line and column metadata.</summary>
    public Location(int start, int end, int line, int column)
    {
        if (start < 0) throw new ArgumentOutOfRangeException(nameof(start));
        if (end < start) throw new ArgumentOutOfRangeException(nameof(end));
        if (line < 1) throw new ArgumentOutOfRangeException(nameof(line));
        if (column < 1) throw new ArgumentOutOfRangeException(nameof(column));
        Start = start; End = end; Line = line; Column = column; HasLocation = true;
    }

    internal Location(int start, int end, int line, int column, bool hasLocation)
    {
        if (start < 0) throw new ArgumentOutOfRangeException(nameof(start));
        if (end < start) throw new ArgumentOutOfRangeException(nameof(end));
        if (line < (hasLocation ? 1 : 0)) throw new ArgumentOutOfRangeException(nameof(line));
        if (column < (hasLocation ? 1 : 0)) throw new ArgumentOutOfRangeException(nameof(column));
        Start = start; End = end; Line = line; Column = column; HasLocation = hasLocation;
    }

    /// <summary>Gets the inclusive start offset.</summary>
    public int Start { get; }
    /// <summary>Gets the exclusive end offset.</summary>
    public int End { get; }
    /// <summary>Gets the one-based line.</summary>
    public int Line { get; }
    /// <summary>Gets the one-based column.</summary>
    public int Column { get; }
    internal bool HasLocation { get; private set; }
    /// <summary>Compares source start offsets.</summary>
    public int CompareTo(Location? other) => other is null ? 1 : Start.CompareTo(other.Start);
    /// <summary>Determines whether two locations have the same range and position.</summary>
    public bool Equals(Location? other) => other is not null && Start == other.Start && End == other.End && Line == other.Line && Column == other.Column;
    /// <summary>Determines whether the object is an equal location.</summary>
    public override bool Equals(object? obj) => obj is Location other && Equals(other);
    /// <summary>Returns a hash code for this location.</summary>
    public override int GetHashCode() => HashCode.Combine(Start, End, Line, Column);

    /// <summary>Converts a parser range into the public syntax location contract.</summary>
    public static implicit operator Location(SourceLocation location) =>
        new(location.Start, location.End, location.Line, location.Column);

    /// <summary>Converts a public syntax location into a parser source range.</summary>
    public static implicit operator SourceLocation(Location location)
    {
        ArgumentNullException.ThrowIfNull(location);
        return new SourceLocation(location.Start, location.End, location.Line, location.Column, location.HasLocation);
    }
}

internal static class SyntaxCompatibility
{
    public static SyntaxKind ToSyntaxKind(AstNodeKind kind) => kind switch
    {
        AstNodeKind.Name => SyntaxKind.Name,
        AstNodeKind.Document => SyntaxKind.Document,
        AstNodeKind.OperationDefinition => SyntaxKind.OperationDefinition,
        AstNodeKind.FragmentDefinition => SyntaxKind.FragmentDefinition,
        AstNodeKind.SelectionSet => SyntaxKind.SelectionSet,
        AstNodeKind.Field => SyntaxKind.Field,
        AstNodeKind.FragmentSpread => SyntaxKind.FragmentSpread,
        AstNodeKind.InlineFragment => SyntaxKind.InlineFragment,
        AstNodeKind.Argument => SyntaxKind.Argument,
        AstNodeKind.Directive => SyntaxKind.Directive,
        AstNodeKind.VariableDefinition => SyntaxKind.VariableDefinition,
        AstNodeKind.Variable => SyntaxKind.Variable,
        AstNodeKind.IntValue => SyntaxKind.IntValue,
        AstNodeKind.FloatValue => SyntaxKind.FloatValue,
        AstNodeKind.StringValue => SyntaxKind.StringValue,
        AstNodeKind.BooleanValue => SyntaxKind.BooleanValue,
        AstNodeKind.NullValue => SyntaxKind.NullValue,
        AstNodeKind.EnumValue => SyntaxKind.EnumValue,
        AstNodeKind.ListValue => SyntaxKind.ListValue,
        AstNodeKind.ObjectValue => SyntaxKind.ObjectValue,
        AstNodeKind.ObjectField => SyntaxKind.ObjectField,
        AstNodeKind.NamedType => SyntaxKind.NamedType,
        AstNodeKind.ListType => SyntaxKind.ListType,
        AstNodeKind.NonNullType => SyntaxKind.NonNullType,
        AstNodeKind.SchemaDefinition => SyntaxKind.SchemaDefinition,
        AstNodeKind.SchemaExtension => SyntaxKind.SchemaExtension,
        AstNodeKind.OperationTypeDefinition => SyntaxKind.OperationTypeDefinition,
        AstNodeKind.ScalarTypeDefinition => SyntaxKind.ScalarTypeDefinition,
        AstNodeKind.ScalarTypeExtension => SyntaxKind.ScalarTypeExtension,
        AstNodeKind.ObjectTypeDefinition => SyntaxKind.ObjectTypeDefinition,
        AstNodeKind.ObjectTypeExtension => SyntaxKind.ObjectTypeExtension,
        AstNodeKind.InterfaceTypeDefinition => SyntaxKind.InterfaceTypeDefinition,
        AstNodeKind.InterfaceTypeExtension => SyntaxKind.InterfaceTypeExtension,
        AstNodeKind.UnionTypeDefinition => SyntaxKind.UnionTypeDefinition,
        AstNodeKind.UnionTypeExtension => SyntaxKind.UnionTypeExtension,
        AstNodeKind.EnumTypeDefinition => SyntaxKind.EnumTypeDefinition,
        AstNodeKind.EnumTypeExtension => SyntaxKind.EnumTypeExtension,
        AstNodeKind.EnumValueDefinition => SyntaxKind.EnumValueDefinition,
        AstNodeKind.InputObjectTypeDefinition => SyntaxKind.InputObjectTypeDefinition,
        AstNodeKind.InputObjectTypeExtension => SyntaxKind.InputObjectTypeExtension,
        AstNodeKind.FieldDefinition => SyntaxKind.FieldDefinition,
        AstNodeKind.InputValueDefinition => SyntaxKind.InputValueDefinition,
        AstNodeKind.DirectiveDefinition => SyntaxKind.DirectiveDefinition,
        AstNodeKind.DirectiveExtension => SyntaxKind.DirectiveExtension,
        AstNodeKind.TypeCoordinate or AstNodeKind.MemberCoordinate or AstNodeKind.ArgumentCoordinate or AstNodeKind.DirectiveCoordinate or AstNodeKind.DirectiveArgumentCoordinate => SyntaxKind.SchemaCoordinate,
        _ => throw new ArgumentOutOfRangeException(nameof(kind)),
    };
}
