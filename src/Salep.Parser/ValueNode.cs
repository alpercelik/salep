using System.Globalization;
using System.Text;
using Salep.Parser.Buffers;

namespace Salep.Parser;

/// <summary>Base class for executable value nodes.</summary>
public abstract class ValueNode : AstNode, IValueNode
{
    /// <summary>Creates a value with a stable node kind and source location.</summary>
    protected ValueNode(AstNodeKind kind, SourceLocation location)
        : base(kind, location)
    {
    }

    private protected abstract object? UntypedValue { get; }
    object? IValueNode.Value => UntypedValue;
}

/// <summary>A variable reference value.</summary>
public sealed class VariableNode : ValueNode, IValueNode<string>
{
    /// <summary>Creates a variable reference with a default source location.</summary>
    public VariableNode(NameNode name) : this(name, default) { }

    /// <summary>Creates a variable reference by name with a default source location.</summary>
    public VariableNode(string name) : this(new NameNode(name)) { }

    /// <summary>Creates a variable reference by name.</summary>
    public VariableNode(Location location, NameNode name) : this(name, location) { }

    /// <summary>Creates an immutable variable reference.</summary>
    public VariableNode(NameNode name, SourceLocation location)
        : base(AstNodeKind.Variable, location)
    {
        ArgumentNullException.ThrowIfNull(name);
        Name = name;
    }

    /// <summary>Gets the variable name.</summary>
    public NameNode Name { get; }
    string IValueNode<string>.Value => Name.Value.ToString();
    private protected override object UntypedValue => Name.Value.ToString();

    /// <summary>Returns a copy with the specified name.</summary>
    public VariableNode WithName(NameNode name) => new(name, Location);
    /// <summary>Returns a copy with the specified location.</summary>
    public VariableNode WithLocation(Location location) => new(Name, location);
}

/// <summary>An integer literal with its original decimal spelling.</summary>
public sealed class IntValueNode : ValueNode, IValueNode<string>, IIntValueLiteral
{
    private string? _value;
    /// <summary>Creates an integer value backed by the supplied character memory.</summary>
    public IntValueNode(ReadOnlyMemorySegment value) : this(value.Memory) { }
    /// <summary>Creates an integer value with a default source location.</summary>
    public IntValueNode(ReadOnlyMemory<char> value) : this(value, default) { }
    /// <summary>Creates an integer value from its decimal spelling.</summary>
    public IntValueNode(string value) : this((value ?? throw new ArgumentNullException(nameof(value))).AsMemory()) { }
    /// <summary>Creates an integer value from a byte.</summary>
    public IntValueNode(byte value) : this(value.ToString(CultureInfo.InvariantCulture)) { }
    /// <summary>Creates an integer value from a signed 16-bit integer.</summary>
    public IntValueNode(short value) : this(value.ToString(CultureInfo.InvariantCulture)) { }
    /// <summary>Creates an integer value from a signed 32-bit integer.</summary>
    public IntValueNode(int value) : this(value.ToString(CultureInfo.InvariantCulture)) { }
    /// <summary>Creates an integer value from a signed 64-bit integer.</summary>
    public IntValueNode(long value) : this(value.ToString(CultureInfo.InvariantCulture)) { }
    /// <summary>Creates an integer value from a signed 8-bit integer.</summary>
    public IntValueNode(sbyte value) : this(value.ToString(CultureInfo.InvariantCulture)) { }
    /// <summary>Creates an integer value from an unsigned 16-bit integer.</summary>
    public IntValueNode(ushort value) : this(value.ToString(CultureInfo.InvariantCulture)) { }
    /// <summary>Creates an integer value from an unsigned 32-bit integer.</summary>
    public IntValueNode(uint value) : this(value.ToString(CultureInfo.InvariantCulture)) { }
    /// <summary>Creates an integer value from an unsigned 64-bit integer.</summary>
    public IntValueNode(ulong value) : this(value.ToString(CultureInfo.InvariantCulture)) { }
    /// <summary>Creates an integer value at a syntax location.</summary>
    public IntValueNode(Location location, ReadOnlyMemory<char> value) : this(value, (SourceLocation)location) { }
    /// <summary>Creates a source-backed integer value at a syntax location.</summary>
    public IntValueNode(Location location, ReadOnlyMemorySegment value) : this(value.Memory, (SourceLocation)location) { }
    /// <summary>Creates an integer value from a byte at a syntax location.</summary>
    public IntValueNode(Location location, byte value) : this(value.ToString(CultureInfo.InvariantCulture).AsMemory(), (SourceLocation)location) { }
    /// <summary>Creates an integer value from a signed 16-bit integer at a syntax location.</summary>
    public IntValueNode(Location location, short value) : this(value.ToString(CultureInfo.InvariantCulture).AsMemory(), (SourceLocation)location) { }
    /// <summary>Creates an integer value from a signed 32-bit integer at a syntax location.</summary>
    public IntValueNode(Location location, int value) : this(value.ToString(CultureInfo.InvariantCulture).AsMemory(), (SourceLocation)location) { }
    /// <summary>Creates an integer value from a signed 64-bit integer at a syntax location.</summary>
    public IntValueNode(Location location, long value) : this(value.ToString(CultureInfo.InvariantCulture).AsMemory(), (SourceLocation)location) { }
    /// <summary>Creates an integer value from a signed 8-bit integer at a syntax location.</summary>
    public IntValueNode(Location location, sbyte value) : this(value.ToString(CultureInfo.InvariantCulture).AsMemory(), (SourceLocation)location) { }
    /// <summary>Creates an integer value from an unsigned 16-bit integer at a syntax location.</summary>
    public IntValueNode(Location location, ushort value) : this(value.ToString(CultureInfo.InvariantCulture).AsMemory(), (SourceLocation)location) { }
    /// <summary>Creates an integer value from an unsigned 32-bit integer at a syntax location.</summary>
    public IntValueNode(Location location, uint value) : this(value.ToString(CultureInfo.InvariantCulture).AsMemory(), (SourceLocation)location) { }
    /// <summary>Creates an integer value from an unsigned 64-bit integer at a syntax location.</summary>
    public IntValueNode(Location location, ulong value) : this(value.ToString(CultureInfo.InvariantCulture).AsMemory(), (SourceLocation)location) { }
    /// <summary>Creates an integer value from a string at a syntax location.</summary>
    public IntValueNode(Location location, string value) : this((value ?? throw new ArgumentNullException(nameof(value))).AsMemory(), (SourceLocation)location) { }
    /// <summary>Creates an immutable integer value without converting or copying its source text.</summary>
    public IntValueNode(ReadOnlyMemory<char> value, SourceLocation location)
        : base(AstNodeKind.IntValue, location)
    {
        SourceValue = value;
    }

    /// <summary>Gets the original integer spelling.</summary>
    public string Value => _value ??= SourceValue.ToString();
    /// <summary>Gets source-backed literal text without allocating the string value.</summary>
    public ReadOnlyMemory<char> SourceValue { get; }
    string IValueNode<string>.Value => Value;
    private protected override object UntypedValue => Value;
    /// <summary>Returns the source spelling encoded as UTF-8.</summary>
    public ReadOnlySpan<byte> AsSpan() => Encoding.UTF8.GetBytes(Value);
    /// <summary>Returns the original source-backed integer text.</summary>
    public ReadOnlyMemorySegment AsMemorySegment() => new(SourceValue);
    /// <summary>Converts the integer literal to a decimal value.</summary>
    public decimal ToDecimal() => decimal.Parse(SourceValue.Span, NumberStyles.Float, CultureInfo.InvariantCulture);
    /// <summary>Converts the integer literal to a double-precision value.</summary>
    public double ToDouble() => double.Parse(SourceValue.Span, NumberStyles.Float, CultureInfo.InvariantCulture);
    /// <summary>Converts the integer literal to a single-precision value.</summary>
    public float ToSingle() => float.Parse(SourceValue.Span, NumberStyles.Float, CultureInfo.InvariantCulture);
    /// <summary>Converts the integer literal to an unsigned 8-bit value.</summary>
    public byte ToByte() => byte.Parse(SourceValue.Span, NumberStyles.Integer, CultureInfo.InvariantCulture);
    /// <summary>Converts the integer literal to a signed 16-bit value.</summary>
    public short ToInt16() => short.Parse(SourceValue.Span, NumberStyles.Integer, CultureInfo.InvariantCulture);
    /// <summary>Converts the integer literal to a signed 32-bit value.</summary>
    public int ToInt32() => int.Parse(SourceValue.Span, NumberStyles.Integer, CultureInfo.InvariantCulture);
    /// <summary>Converts the integer literal to a signed 64-bit value.</summary>
    public long ToInt64() => long.Parse(SourceValue.Span, NumberStyles.Integer, CultureInfo.InvariantCulture);
    /// <summary>Converts the integer literal to a signed 8-bit value.</summary>
    public sbyte ToSByte() => sbyte.Parse(SourceValue.Span, NumberStyles.Integer, CultureInfo.InvariantCulture);
    /// <summary>Converts the integer literal to an unsigned 16-bit value.</summary>
    public ushort ToUInt16() => ushort.Parse(SourceValue.Span, NumberStyles.Integer, CultureInfo.InvariantCulture);
    /// <summary>Converts the integer literal to an unsigned 32-bit value.</summary>
    public uint ToUInt32() => uint.Parse(SourceValue.Span, NumberStyles.Integer, CultureInfo.InvariantCulture);
    /// <summary>Converts the integer literal to an unsigned 64-bit value.</summary>
    public ulong ToUInt64() => ulong.Parse(SourceValue.Span, NumberStyles.Integer, CultureInfo.InvariantCulture);

    /// <summary>Returns a copy with the specified source spelling.</summary>
    public IntValueNode WithValue(string value) => new((value ?? throw new ArgumentNullException(nameof(value))).AsMemory(), Location);
    /// <summary>Returns a copy with the specified byte value.</summary>
    public IntValueNode WithValue(byte value) => new(value.ToString(CultureInfo.InvariantCulture).AsMemory(), Location);
    /// <summary>Returns a copy with the specified signed 16-bit value.</summary>
    public IntValueNode WithValue(short value) => new(value.ToString(CultureInfo.InvariantCulture).AsMemory(), Location);
    /// <summary>Returns a copy with the specified signed 32-bit value.</summary>
    public IntValueNode WithValue(int value) => new(value.ToString(CultureInfo.InvariantCulture).AsMemory(), Location);
    /// <summary>Returns a copy with the specified signed 64-bit value.</summary>
    public IntValueNode WithValue(long value) => new(value.ToString(CultureInfo.InvariantCulture).AsMemory(), Location);
    /// <summary>Returns a copy with the specified signed 8-bit value.</summary>
    public IntValueNode WithValue(sbyte value) => new(value.ToString(CultureInfo.InvariantCulture).AsMemory(), Location);
    /// <summary>Returns a copy with the specified source-backed value.</summary>
    public IntValueNode WithValue(ReadOnlyMemory<char> value) => new(value, Location);
    /// <summary>Returns a copy with the specified source-backed value.</summary>
    public IntValueNode WithValue(ReadOnlyMemorySegment value) => new(value.Memory, SourceRange);
    /// <summary>Returns a copy with the specified location.</summary>
    public IntValueNode WithLocation(Location location) => new(SourceValue, (SourceLocation)location);
}

/// <summary>A floating-point literal with its original decimal spelling.</summary>
public sealed class FloatValueNode : ValueNode, IValueNode<string>, IFloatValueLiteral
{
    private string? _value;
    private FloatFormat? _format;
    /// <summary>Creates a floating-point value with a default source location.</summary>
    public FloatValueNode(ReadOnlyMemory<char> value) : this(value, default) { }
    /// <summary>Creates a floating-point value backed by the supplied memory and format.</summary>
    public FloatValueNode(ReadOnlyMemorySegment value, FloatFormat format) : this(value.Memory, default, format) { }
    /// <summary>Creates a floating-point value from its spelling.</summary>
    public FloatValueNode(string value) : this((value ?? throw new ArgumentNullException(nameof(value))).AsMemory()) { }
    /// <summary>Creates a floating-point value from a double.</summary>
    public FloatValueNode(double value) : this(value.ToString("R", CultureInfo.InvariantCulture)) { }
    /// <summary>Creates a floating-point value from a decimal.</summary>
    public FloatValueNode(decimal value) : this(value.ToString(CultureInfo.InvariantCulture)) { }
    /// <summary>Creates a floating-point value at a syntax location.</summary>
    public FloatValueNode(Location location, ReadOnlyMemory<char> value) : this(value, (SourceLocation)location) { }
    /// <summary>Creates a source-backed floating-point value at a syntax location.</summary>
    public FloatValueNode(Location location, ReadOnlyMemorySegment value, FloatFormat format) : this(value.Memory, (SourceLocation)location, format) { }
    /// <summary>Creates a floating-point value from a double at a syntax location.</summary>
    public FloatValueNode(Location location, double value) : this(value.ToString("R", CultureInfo.InvariantCulture).AsMemory(), (SourceLocation)location) { }
    /// <summary>Creates a floating-point value from a decimal at a syntax location.</summary>
    public FloatValueNode(Location location, decimal value) : this(value.ToString(CultureInfo.InvariantCulture).AsMemory(), (SourceLocation)location) { }
    /// <summary>Creates a floating-point value from its spelling at a syntax location.</summary>
    public FloatValueNode(Location location, string value) : this((value ?? throw new ArgumentNullException(nameof(value))).AsMemory(), (SourceLocation)location) { }
    /// <summary>Creates an immutable floating-point value without numeric conversion or copying.</summary>
    public FloatValueNode(ReadOnlyMemory<char> value, SourceLocation location)
        : this(value, location, null)
    {
    }

    private FloatValueNode(ReadOnlyMemory<char> value, SourceLocation location, FloatFormat? format)
        : base(AstNodeKind.FloatValue, location)
    {
        SourceValue = value;
        _format = format;
    }

    /// <summary>Gets the original floating-point spelling.</summary>
    public string Value => _value ??= SourceValue.ToString();
    /// <summary>Gets source-backed literal text without allocating the string value.</summary>
    public ReadOnlyMemory<char> SourceValue { get; }
    /// <summary>Gets the preferred formatting mode for this literal.</summary>
    public FloatFormat Format => _format ?? (SourceValue.Span.IndexOfAny('e', 'E') >= 0 ? FloatFormat.Exponential : FloatFormat.FixedPoint);
    string IValueNode<string>.Value => Value;
    private protected override object UntypedValue => Value;
    /// <summary>Returns the source spelling encoded as UTF-8.</summary>
    public ReadOnlySpan<byte> AsSpan() => Encoding.UTF8.GetBytes(Value);
    /// <summary>Returns the original source-backed floating-point text.</summary>
    public ReadOnlyMemorySegment AsMemorySegment() => new(SourceValue);
    /// <summary>Converts the floating-point literal to a decimal value.</summary>
    public decimal ToDecimal() => decimal.Parse(SourceValue.Span, NumberStyles.Float, CultureInfo.InvariantCulture);
    /// <summary>Converts the floating-point literal to a double-precision value.</summary>
    public double ToDouble() => double.Parse(SourceValue.Span, NumberStyles.Float, CultureInfo.InvariantCulture);
    /// <summary>Converts the floating-point literal to a single-precision value.</summary>
    public float ToSingle() => float.Parse(SourceValue.Span, NumberStyles.Float, CultureInfo.InvariantCulture);
    /// <summary>Returns a copy with the specified floating-point spelling.</summary>
    public FloatValueNode WithValue(string value) => new((value ?? throw new ArgumentNullException(nameof(value))).AsMemory(), Location);
    /// <summary>Returns a copy with the specified double value.</summary>
    public FloatValueNode WithValue(double value) => new(value.ToString("R", CultureInfo.InvariantCulture).AsMemory(), Location);
    /// <summary>Returns a copy with the specified decimal value.</summary>
    public FloatValueNode WithValue(decimal value) => new(value.ToString(CultureInfo.InvariantCulture).AsMemory(), Location);
    /// <summary>Returns a copy with the specified source-backed value.</summary>
    public FloatValueNode WithValue(ReadOnlyMemory<char> value) => new(value, Location);
    /// <summary>Returns a copy with the specified source-backed value and format.</summary>
    public FloatValueNode WithValue(ReadOnlyMemorySegment value, FloatFormat format) => new(value.Memory, SourceRange, format);
    /// <summary>Returns a copy with the specified location.</summary>
    public FloatValueNode WithLocation(Location location) => new(SourceValue, (SourceLocation)location, _format);
}

/// <summary>Controls the notation used to format floating-point values.</summary>
public enum FloatFormat
{
    /// <summary>Formats the number using fixed-point notation.</summary>
    FixedPoint = 0,
    /// <summary>Formats the number using exponential notation.</summary>
    Exponential = 1
}

/// <summary>A decoded quoted or normalized block string literal.</summary>
public sealed class StringValueNode : ValueNode, IValueNode<string>, IHasSpan
{
    private string? _value;
    /// <summary>Creates an ordinary string value with a default source location.</summary>
    public StringValueNode(string value) : this((value ?? throw new ArgumentNullException(nameof(value))).AsMemory(), false, default) { }
    /// <summary>Creates a string value at a syntax location.</summary>
    public StringValueNode(Location location, string value, bool block) : this((value ?? throw new ArgumentNullException(nameof(value))).AsMemory(), block, (SourceLocation)location) { }
    /// <summary>Creates a string value at a syntax location.</summary>
    public StringValueNode(Location location, ReadOnlyMemory<char> value, bool block) : this(value, block, (SourceLocation)location) { }
    /// <summary>Creates a source-backed string value at a syntax location.</summary>
    public StringValueNode(Location location, ReadOnlyMemorySegment value, bool block) : this(value.Memory, block, (SourceLocation)location) { }
    /// <summary>Creates an immutable evaluated string value.</summary>
    public StringValueNode(ReadOnlyMemory<char> value, bool isBlock, SourceLocation location)
        : base(AstNodeKind.StringValue, location)
    {
        SourceValue = value;
        IsBlock = isBlock;
    }

    /// <summary>Gets the evaluated string content, backed by source or owned decoded storage.</summary>
    public string Value => _value ??= SourceValue.ToString();
    /// <summary>Gets the backing memory for the evaluated literal text.</summary>
    public ReadOnlyMemory<char> SourceValue { get; }
    /// <summary>Gets whether the source used triple-quoted block-string syntax.</summary>
    public bool IsBlock { get; }
    /// <summary>Gets whether the literal uses block-string syntax.</summary>
    public bool Block => IsBlock;
    string IValueNode<string>.Value => Value;
    private protected override object UntypedValue => Value;
    /// <summary>Returns the value text encoded as UTF-8.</summary>
    public ReadOnlySpan<byte> AsSpan() => Encoding.UTF8.GetBytes(Value);
    /// <summary>Returns the backing segment for the evaluated string content.</summary>
    public ReadOnlyMemorySegment AsMemorySegment() => new(SourceValue);
    /// <summary>Returns a copy with the specified value and block-string style.</summary>
    public StringValueNode WithValue(string value, bool block) => new((value ?? throw new ArgumentNullException(nameof(value))).AsMemory(), block, Location);
    /// <summary>Returns a copy with the specified value and existing block-string style.</summary>
    public StringValueNode WithValue(string value) => new((value ?? throw new ArgumentNullException(nameof(value))).AsMemory(), IsBlock, Location);
    /// <summary>Returns a copy with the specified location.</summary>
    public StringValueNode WithLocation(Location location) => new(SourceValue, IsBlock, (SourceLocation)location);
}

/// <summary>A boolean literal.</summary>
public sealed class BooleanValueNode : ValueNode, IValueNode<bool>
{
    /// <summary>Gets the canonical true literal.</summary>
    public static BooleanValueNode True { get; } = new(true);
    /// <summary>Gets the canonical false literal.</summary>
    public static BooleanValueNode False { get; } = new(false);
    /// <summary>Creates a boolean value with a default source location.</summary>
    public BooleanValueNode(bool value) : this(value, default) { }
    /// <summary>Creates a boolean value at a syntax location.</summary>
    public BooleanValueNode(Location location, bool value) : this(value, (SourceLocation)location) { }
    /// <summary>Creates an immutable boolean value.</summary>
    public BooleanValueNode(bool value, SourceLocation location)
        : base(AstNodeKind.BooleanValue, location)
    {
        Value = value;
    }

    /// <summary>Gets the boolean value.</summary>
    public bool Value { get; }
    private protected override object UntypedValue => Value;
    /// <summary>Returns a copy with the specified value.</summary>
    public BooleanValueNode WithValue(bool value) => new(value, Location);
    /// <summary>Returns a copy with the specified location.</summary>
    public BooleanValueNode WithLocation(Location location) => new(Value, (SourceLocation)location);
}

/// <summary>A null literal.</summary>
public sealed class NullValueNode : ValueNode, IValueNode<object>, IEquatable<NullValueNode>
{
    /// <summary>Gets the canonical null literal.</summary>
    public static NullValueNode Default { get; } = new();
    /// <summary>Creates a null value with a default source location.</summary>
    public NullValueNode() : this(default(SourceLocation)) { }
    /// <summary>Creates a null value at a syntax location.</summary>
    public NullValueNode(Location location) : this((SourceLocation)location) { }
    /// <summary>Creates an immutable null value.</summary>
    public NullValueNode(SourceLocation location)
        : base(AstNodeKind.NullValue, location)
    {
    }

    /// <summary>Gets the null literal value.</summary>
    public object? Value => null;
    object IValueNode<object>.Value => null!;
    private protected override object? UntypedValue => null;
    /// <summary>Returns a copy with the specified location.</summary>
    public NullValueNode WithLocation(Location location) => new((SourceLocation)location);
    /// <summary>Determines whether this null literal equals another value node.</summary>
    public bool Equals(IValueNode? other) => other is NullValueNode;
    /// <summary>Determines whether this null literal equals another null literal.</summary>
    public bool Equals(NullValueNode? other) => other is not null;
    /// <summary>Determines whether this null literal equals another object.</summary>
    public override bool Equals(object? obj) => obj is NullValueNode;
    /// <summary>Returns the hash code for the null literal.</summary>
    public override int GetHashCode() => (int)AstNodeKind.NullValue;
}

/// <summary>An enum literal with its original name spelling.</summary>
public sealed class EnumValueNode : ValueNode, IValueNode<string>
{
    private string? _value;
    /// <summary>Creates an enum value with a default source location.</summary>
    public EnumValueNode(ReadOnlyMemory<char> value) : this(value, default) { }
    /// <summary>Creates an enum value backed by the supplied character memory.</summary>
    public EnumValueNode(ReadOnlyMemorySegment value) : this(value.Memory) { }
    /// <summary>Creates an enum value from text.</summary>
    public EnumValueNode(string value) : this((value ?? throw new ArgumentNullException(nameof(value))).AsMemory()) { }
    /// <summary>Creates an enum value at a syntax location.</summary>
    public EnumValueNode(Location location, ReadOnlyMemory<char> value) : this(value, (SourceLocation)location) { }
    /// <summary>Creates a source-backed enum value at a syntax location.</summary>
    public EnumValueNode(Location location, ReadOnlyMemorySegment value) : this(value.Memory, (SourceLocation)location) { }
    /// <summary>Creates an enum value from text at a syntax location.</summary>
    public EnumValueNode(Location location, string value) : this((value ?? throw new ArgumentNullException(nameof(value))).AsMemory(), (SourceLocation)location) { }
    /// <summary>Creates an enum value from a boxed value's invariant string representation.</summary>
    public EnumValueNode(object value) : this(Convert.ToString(value, CultureInfo.InvariantCulture) ?? throw new ArgumentNullException(nameof(value))) { }
    /// <summary>Creates an immutable enum value without copying its source text.</summary>
    public EnumValueNode(ReadOnlyMemory<char> value, SourceLocation location)
        : base(AstNodeKind.EnumValue, location)
    {
        SourceValue = value;
    }

    /// <summary>Gets the enum value text.</summary>
    public string Value => _value ??= SourceValue.ToString();
    /// <summary>Gets source-backed literal text without allocating the string value.</summary>
    public ReadOnlyMemory<char> SourceValue { get; }
    string IValueNode<string>.Value => Value;
    private protected override object UntypedValue => Value;
    /// <summary>Returns the UTF-8 encoded enum literal bytes.</summary>
    public ReadOnlySpan<byte> AsSpan() => Encoding.UTF8.GetBytes(Value);
    /// <summary>Returns the original source-backed enum text.</summary>
    public ReadOnlyMemorySegment AsMemorySegment() => new(SourceValue);
    /// <summary>Returns a copy with the specified text.</summary>
    public EnumValueNode WithValue(string value) => new((value ?? throw new ArgumentNullException(nameof(value))).AsMemory(), Location);
    /// <summary>Returns a copy with the specified location.</summary>
    public EnumValueNode WithLocation(Location location) => new(SourceValue, (SourceLocation)location);
}

/// <summary>An ordered list literal, which may be empty.</summary>
public sealed class ListValueNode : ValueNode, IValueNode<IReadOnlyList<IValueNode>>
{
    /// <summary>Creates a single-item list value with a default location.</summary>
    public ListValueNode(IValueNode item) : this(new[] { AsValueNode(item) }, default(SourceLocation)) { }
    /// <summary>Creates a list value from items with a default location.</summary>
    public ListValueNode(IValueNode[] items) : this(ToValueNodes(items), default(SourceLocation)) { }
    /// <summary>Creates a list value with a default location.</summary>
    public ListValueNode(IReadOnlyList<IValueNode> items) : this(ToValueNodes(items), default(SourceLocation)) { }
    /// <summary>Creates a single-item list value at a syntax location.</summary>
    public ListValueNode(Location location, IValueNode item) : this(new[] { AsValueNode(item) }, (SourceLocation)location) { }
    /// <summary>Creates a list value at a syntax location.</summary>
    public ListValueNode(Location location, IReadOnlyList<IValueNode> items) : this(ToValueNodes(items), (SourceLocation)location) { }
    /// <summary>Creates a list value from interface values.</summary>
    /// <summary>Creates an immutable list value.</summary>
    public ListValueNode(IEnumerable<ValueNode> values, SourceLocation location)
        : base(AstNodeKind.ListValue, location)
    {
        Values = new AstNodeList<ValueNode>(values);
    }

    /// <summary>Gets values in source order.</summary>
    public AstNodeList<ValueNode> Values { get; }
    /// <summary>Gets list items through the public value-node interface.</summary>
    public IReadOnlyList<IValueNode> Items => Values;
    IReadOnlyList<IValueNode> IValueNode<IReadOnlyList<IValueNode>>.Value => Values;
    private protected override object UntypedValue => Values;
    /// <summary>Returns a copy with the specified list contents.</summary>
    public ListValueNode WithItems(IReadOnlyList<IValueNode> items) => new(ToValueNodes(items), Location);
    /// <summary>Returns a copy with the specified location.</summary>
    public ListValueNode WithLocation(Location location) => new((IEnumerable<ValueNode>)Values, (SourceLocation)location);

    private static ValueNode AsValueNode(IValueNode value)
    {
        ArgumentNullException.ThrowIfNull(value);
        return value as ValueNode ?? throw new ArgumentException("Value nodes must be AST value nodes.", nameof(value));
    }

    private static ValueNode[] ToValueNodes(IReadOnlyList<IValueNode> values)
    {
        ArgumentNullException.ThrowIfNull(values);
        var converted = new ValueNode[values.Count];
        for (var index = 0; index < converted.Length; index++)
        {
            converted[index] = AsValueNode(values[index]);
        }
        return converted;
    }
}

/// <summary>An ordered object literal, which may be empty.</summary>
public sealed class ObjectValueNode : ValueNode, IValueNode<IReadOnlyList<ObjectFieldNode>>
{
    /// <summary>Creates an object value from fields with a default source location.</summary>
    public ObjectValueNode(ObjectFieldNode[] fields) : this((IReadOnlyList<ObjectFieldNode>)fields) { }
    /// <summary>Creates an object value from fields with a default source location.</summary>
    public ObjectValueNode(IReadOnlyList<ObjectFieldNode> fields) : this(fields, default) { }
    /// <summary>Creates an object value at a syntax location.</summary>
    public ObjectValueNode(Location location, IReadOnlyList<ObjectFieldNode> fields) : this(fields, (SourceLocation)location) { }
    /// <summary>Creates an immutable object value.</summary>
    public ObjectValueNode(IEnumerable<ObjectFieldNode> fields, SourceLocation location)
        : base(AstNodeKind.ObjectValue, location)
    {
        Fields = new AstNodeList<ObjectFieldNode>(fields);
    }

    /// <summary>Gets object fields in source order.</summary>
    public IReadOnlyList<ObjectFieldNode> Fields { get; }
    IReadOnlyList<ObjectFieldNode> IValueNode<IReadOnlyList<ObjectFieldNode>>.Value => Fields;
    private protected override object UntypedValue => Fields;
    /// <summary>Returns a copy with the specified fields.</summary>
    public ObjectValueNode WithFields(IReadOnlyList<ObjectFieldNode> fields) => new(fields, Location);
    /// <summary>Returns a copy with the specified location.</summary>
    public ObjectValueNode WithLocation(Location location) => new((IEnumerable<ObjectFieldNode>)Fields, (SourceLocation)location);
}

/// <summary>A named value within an input object literal.</summary>
public sealed class ObjectFieldNode : AstNode, IEquatable<ObjectFieldNode>
{
    /// <summary>Creates an object field from a name and value interface.</summary>
    public ObjectFieldNode(Location location, NameNode name, IValueNode value) : this(name, AsValueNode(value), (SourceLocation)location) { }
    /// <summary>Creates an object field from a name and value interface.</summary>
    public ObjectFieldNode(string name, IValueNode value) : this(new NameNode(name), AsValueNode(value), default) { }
    /// <summary>Creates an object field with a string value.</summary>
    public ObjectFieldNode(string name, string value) : this(new NameNode(name), new StringValueNode(value), default) { }
    /// <summary>Creates an object field with an integer value.</summary>
    public ObjectFieldNode(string name, int value) : this(new NameNode(name), new IntValueNode(value), default) { }
    /// <summary>Creates an object field with a floating-point value.</summary>
    public ObjectFieldNode(string name, double value) : this(new NameNode(name), new FloatValueNode(value), default) { }
    /// <summary>Creates an object field with a boolean value.</summary>
    public ObjectFieldNode(string name, bool value) : this(new NameNode(name), new BooleanValueNode(value), default) { }
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
    public IValueNode Value { get; }
    /// <summary>Returns a copy with the specified name.</summary>
    public ObjectFieldNode WithName(NameNode name) => new(name, AsValueNode(Value), Location);
    /// <summary>Returns a copy with the specified value.</summary>
    public ObjectFieldNode WithValue(IValueNode value) => new(Name, AsValueNode(value), Location);
    /// <summary>Returns a copy with the specified location.</summary>
    public ObjectFieldNode WithLocation(Location location) => new(Name, AsValueNode(Value), (SourceLocation)location);

    /// <summary>Compares the field name and syntax value while ignoring source locations.</summary>
    public bool Equals(ObjectFieldNode? other) => other is not null
        && Name.Value == other.Name.Value
        && SyntaxComparer.BySyntax.Equals(Value, other.Value);
    /// <inheritdoc />
    public override bool Equals(object? obj) => obj is ObjectFieldNode other && Equals(other);
    /// <inheritdoc />
    public override int GetHashCode() => HashCode.Combine(Name.Value, SyntaxComparer.BySyntax.GetHashCode(Value));

    private static ValueNode AsValueNode(IValueNode value)
    {
        ArgumentNullException.ThrowIfNull(value);
        return value as ValueNode ?? throw new ArgumentException("Value nodes must be AST value nodes.", nameof(value));
    }
}
