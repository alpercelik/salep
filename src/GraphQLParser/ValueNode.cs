namespace GraphQLParser;

/// <summary>Base class for executable value nodes.</summary>
public abstract class ValueNode : AstNode
{
    /// <summary>Creates a value with a stable node kind and source location.</summary>
    protected ValueNode(AstNodeKind kind, SourceLocation location)
        : base(kind, location)
    {
    }
}

/// <summary>A variable reference value.</summary>
public sealed class VariableNode : ValueNode
{
    /// <summary>Creates an immutable variable reference.</summary>
    public VariableNode(NameNode name, SourceLocation location)
        : base(AstNodeKind.Variable, location)
    {
        ArgumentNullException.ThrowIfNull(name);
        Name = name;
    }

    /// <summary>Gets the variable name.</summary>
    public NameNode Name { get; }
}

/// <summary>An integer literal with its original decimal spelling.</summary>
public sealed class IntValueNode : ValueNode
{
    /// <summary>Creates an immutable integer value without converting or copying its source text.</summary>
    public IntValueNode(ReadOnlyMemory<char> value, SourceLocation location)
        : base(AstNodeKind.IntValue, location)
    {
        Value = value;
    }

    /// <summary>Gets the original integer spelling.</summary>
    public ReadOnlyMemory<char> Value { get; }
}

/// <summary>A floating-point literal with its original decimal spelling.</summary>
public sealed class FloatValueNode : ValueNode
{
    /// <summary>Creates an immutable floating-point value without numeric conversion or copying.</summary>
    public FloatValueNode(ReadOnlyMemory<char> value, SourceLocation location)
        : base(AstNodeKind.FloatValue, location)
    {
        Value = value;
    }

    /// <summary>Gets the original floating-point spelling.</summary>
    public ReadOnlyMemory<char> Value { get; }
}

/// <summary>A decoded quoted or normalized block string literal.</summary>
public sealed class StringValueNode : ValueNode
{
    /// <summary>Creates an immutable evaluated string value.</summary>
    public StringValueNode(ReadOnlyMemory<char> value, bool isBlock, SourceLocation location)
        : base(AstNodeKind.StringValue, location)
    {
        Value = value;
        IsBlock = isBlock;
    }

    /// <summary>Gets the evaluated string content, backed by source or owned decoded storage.</summary>
    public ReadOnlyMemory<char> Value { get; }
    /// <summary>Gets whether the source used triple-quoted block-string syntax.</summary>
    public bool IsBlock { get; }
}

/// <summary>A boolean literal.</summary>
public sealed class BooleanValueNode : ValueNode
{
    /// <summary>Creates an immutable boolean value.</summary>
    public BooleanValueNode(bool value, SourceLocation location)
        : base(AstNodeKind.BooleanValue, location)
    {
        Value = value;
    }

    /// <summary>Gets the boolean value.</summary>
    public bool Value { get; }
}

/// <summary>A null literal.</summary>
public sealed class NullValueNode : ValueNode
{
    /// <summary>Creates an immutable null value.</summary>
    public NullValueNode(SourceLocation location)
        : base(AstNodeKind.NullValue, location)
    {
    }
}

/// <summary>An enum literal with its original name spelling.</summary>
public sealed class EnumValueNode : ValueNode
{
    /// <summary>Creates an immutable enum value without copying its source text.</summary>
    public EnumValueNode(ReadOnlyMemory<char> value, SourceLocation location)
        : base(AstNodeKind.EnumValue, location)
    {
        Value = value;
    }

    /// <summary>Gets the enum value text.</summary>
    public ReadOnlyMemory<char> Value { get; }
}

/// <summary>An ordered list literal, which may be empty.</summary>
public sealed class ListValueNode : ValueNode
{
    /// <summary>Creates an immutable list value.</summary>
    public ListValueNode(IEnumerable<ValueNode> values, SourceLocation location)
        : base(AstNodeKind.ListValue, location)
    {
        Values = new AstNodeList<ValueNode>(values);
    }

    /// <summary>Gets values in source order.</summary>
    public AstNodeList<ValueNode> Values { get; }
}

/// <summary>An ordered object literal, which may be empty.</summary>
public sealed class ObjectValueNode : ValueNode
{
    /// <summary>Creates an immutable object value.</summary>
    public ObjectValueNode(IEnumerable<ObjectFieldNode> fields, SourceLocation location)
        : base(AstNodeKind.ObjectValue, location)
    {
        Fields = new AstNodeList<ObjectFieldNode>(fields);
    }

    /// <summary>Gets object fields in source order.</summary>
    public AstNodeList<ObjectFieldNode> Fields { get; }
}

/// <summary>A named value within an input object literal.</summary>
public sealed class ObjectFieldNode : AstNode
{
    /// <summary>Creates an immutable object field.</summary>
    public ObjectFieldNode(NameNode name, ValueNode value, SourceLocation location)
        : base(AstNodeKind.ObjectField, location)
    {
        ArgumentNullException.ThrowIfNull(name);
        ArgumentNullException.ThrowIfNull(value);
        Name = name;
        Value = value;
    }

    /// <summary>Gets the object field name.</summary>
    public NameNode Name { get; }
    /// <summary>Gets the object field value.</summary>
    public ValueNode Value { get; }
}
