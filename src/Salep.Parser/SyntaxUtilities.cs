using System.Collections.Concurrent;
using System.Text;

namespace Salep.Parser.Utilities;

/// <summary>Receives formatted GraphQL syntax output.</summary>
public interface ISyntaxWriter
{
    /// <summary>Gets the zero-based column of the next character.</summary>
    int Column { get; }
    /// <summary>Increases indentation.</summary>
    void Indent();
    /// <summary>Decreases indentation.</summary>
    void Unindent();
    /// <summary>Writes a character.</summary>
    void Write(char c);
    /// <summary>Writes text.</summary>
    void Write(string s);
    /// <summary>Writes indentation at the start of a line.</summary>
    void WriteIndent(bool condition = true);
    /// <summary>Writes a line feed.</summary>
    void WriteLine(bool condition = true);
    /// <summary>Writes a space.</summary>
    void WriteSpace(bool condition = true);
}

/// <summary>Writes syntax into a reusable string buffer.</summary>
public sealed class StringSyntaxWriter : ISyntaxWriter
{
    private static readonly ConcurrentBag<StringSyntaxWriter> Pool = new();
    private readonly StringBuilder _builder = new();
    private int _indent;
    private int _column;

    /// <summary>Gets the current column.</summary>
    public int Column => _column;
    /// <summary>Increases indentation for subsequent lines.</summary>
    public void Indent() => _indent++;
    /// <summary>Decreases indentation, clamped at zero.</summary>
    public void Unindent() => _indent = Math.Max(0, _indent - 1);
    /// <summary>Writes one character and updates the current column.</summary>
    public void Write(char c)
    {
        _builder.Append(c);
        _column = c == '\n' ? 0 : _column + 1;
    }
    /// <summary>Writes text and updates the current column.</summary>
    public void Write(string s)
    {
        ArgumentNullException.ThrowIfNull(s);
        _builder.Append(s);
        var newline = s.LastIndexOf('\n');
        _column = newline < 0 ? _column + s.Length : s.Length - newline - 1;
    }
    /// <summary>Writes indentation when enabled and at the start of a line.</summary>
    public void WriteIndent(bool condition = true)
    {
        if (condition && _column == 0 && _indent > 0)
            Write(new string(' ', _indent * 2));
    }
    /// <summary>Writes a line feed when enabled.</summary>
    public void WriteLine(bool condition = true)
    {
        if (condition) Write('\n');
    }
    /// <summary>Writes a space when enabled.</summary>
    public void WriteSpace(bool condition = true)
    {
        if (condition) Write(' ');
    }
    /// <summary>Clears the buffer and indentation state.</summary>
    public void Clear()
    {
        _builder.Clear();
        _indent = 0;
        _column = 0;
    }
    /// <summary>Rents an empty writer from the shared pool.</summary>
    public static StringSyntaxWriter Rent() => Pool.TryTake(out var writer) ? writer : new StringSyntaxWriter();
    /// <summary>Returns a writer to the shared pool after clearing it.</summary>
    public static void Return(StringSyntaxWriter writer)
    {
        ArgumentNullException.ThrowIfNull(writer);
        writer.Clear();
        Pool.Add(writer);
    }
    /// <summary>Returns the buffered text.</summary>
    public override string ToString() => _builder.ToString();
}

/// <summary>Controls syntax serialization formatting.</summary>
public struct SyntaxSerializerOptions
{
    /// <summary>Creates options with standard GraphQL formatting defaults.</summary>
    public SyntaxSerializerOptions()
    {
        Indented = true;
        MaxDirectivesPerLine = 5;
        PrintWidth = 80;
    }

    /// <summary>Gets or sets whether output uses multiline indentation.</summary>
    public bool Indented { get; set; }
    /// <summary>Gets or sets the maximum number of directives printed on one line.</summary>
    public int MaxDirectivesPerLine { get; set; }
    /// <summary>Gets or sets the preferred line width.</summary>
    public int PrintWidth { get; set; }
}

/// <summary>Serializes syntax nodes through an <see cref="ISyntaxWriter"/>.</summary>
public sealed class SyntaxSerializer
{
    /// <summary>Creates a serializer with optional formatting settings.</summary>
    public SyntaxSerializer(SyntaxSerializerOptions options = default) => Options = options.Equals(default(SyntaxSerializerOptions)) ? new SyntaxSerializerOptions() : options;
    /// <summary>Gets the formatting settings used by this serializer.</summary>
    public SyntaxSerializerOptions Options { get; }
    /// <summary>Writes a syntax node.</summary>
    public void Serialize(ISyntaxNode node, ISyntaxWriter writer)
    {
        ArgumentNullException.ThrowIfNull(node);
        ArgumentNullException.ThrowIfNull(writer);
        writer.Write(SyntaxPrinter.Print(node, Options.Indented));
    }
}

/// <summary>Formats syntax nodes as GraphQL text.</summary>
public static class SyntaxPrinter
{
    /// <summary>Prints a syntax node.</summary>
    public static string Print(ISyntaxNode node, bool indented = true)
    {
        ArgumentNullException.ThrowIfNull(node);
        if (node is not AstNode astNode)
            throw new ArgumentException("The syntax node is not supported by this printer.", nameof(node));
        return GraphQLPrinter.Print(astNode, indented);
    }

    /// <summary>Prints a syntax node to a stream asynchronously as UTF-8.</summary>
    public static async ValueTask PrintToAsync(ISyntaxNode node, Stream stream, bool indented = true, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(stream);
        var text = Print(node, indented);
        await stream.WriteAsync(Encoding.UTF8.GetBytes(text), cancellationToken).ConfigureAwait(false);
    }
}

/// <summary>Provides formatting operations for syntax writers.</summary>
public static class SyntaxWriterExtensions
{
    /// <summary>Writes an argument.</summary>
    public static void WriteArgument(this ISyntaxWriter writer, ArgumentNode node) => WriteNode(writer, node);
    /// <summary>Writes a boolean literal.</summary>
    public static void WriteBooleanValue(this ISyntaxWriter writer, BooleanValueNode node) => WriteNode(writer, node);
    /// <summary>Writes a directive.</summary>
    public static void WriteDirective(this ISyntaxWriter writer, DirectiveNode node) => WriteNode(writer, node);
    /// <summary>Writes an enum literal.</summary>
    public static void WriteEnumValue(this ISyntaxWriter writer, EnumValueNode node) => WriteNode(writer, node);
    /// <summary>Writes a field name and value.</summary>
    public static void WriteField(this ISyntaxWriter writer, NameNode name, IValueNode value, bool indented) => WriteFieldCore(writer, name, value, indented);
    /// <summary>Writes a field name and value.</summary>
    public static void WriteField(this ISyntaxWriter writer, NameNode name, IValueNode value) => WriteFieldCore(writer, name, value);
    /// <summary>Writes a floating-point literal.</summary>
    public static void WriteFloatValue(this ISyntaxWriter writer, FloatValueNode node) => WriteNode(writer, node);
    /// <summary>Writes an integer literal.</summary>
    public static void WriteIntValue(this ISyntaxWriter writer, IntValueNode node) => WriteNode(writer, node);
    /// <summary>Writes a list type.</summary>
    public static void WriteListType(this ISyntaxWriter writer, ListTypeNode node) => WriteNode(writer, node);
    /// <summary>Writes a list value.</summary>
    public static void WriteListValue(this ISyntaxWriter writer, ListValueNode node, bool indented) => WriteNode(writer, node, indented);
    /// <summary>Writes a list value.</summary>
    public static void WriteListValue(this ISyntaxWriter writer, ListValueNode node) => WriteNode(writer, node);
    /// <summary>Writes items separated by a writer action.</summary>
    public static void WriteMany<T>(this ISyntaxWriter writer, IReadOnlyList<T> items, Action<T, ISyntaxWriter> action, Action<ISyntaxWriter> separator)
    {
        ValidateMany(writer, items, action);
        ArgumentNullException.ThrowIfNull(separator);
        for (var i = 0; i < items.Count; i++)
        {
            if (i > 0) separator(writer);
            action(items[i], writer);
        }
    }
    /// <summary>Writes items separated by text.</summary>
    public static void WriteMany<T>(this ISyntaxWriter writer, IReadOnlyList<T> items, Action<T, ISyntaxWriter> action, string separator)
    {
        ValidateMany(writer, items, action);
        ArgumentNullException.ThrowIfNull(separator);
        for (var i = 0; i < items.Count; i++)
        {
            if (i > 0) writer.Write(separator);
            action(items[i], writer);
        }
    }
    /// <summary>Writes items without a separator.</summary>
    public static void WriteMany<T>(this ISyntaxWriter writer, IReadOnlyList<T> items, Action<T, ISyntaxWriter> action)
    {
        ValidateMany(writer, items, action);
        for (var i = 0; i < items.Count; i++) action(items[i], writer);
    }
    /// <summary>Writes a name.</summary>
    public static void WriteName(this ISyntaxWriter writer, NameNode nameNode) => WriteNode(writer, nameNode);
    /// <summary>Writes a named type.</summary>
    public static void WriteNamedType(this ISyntaxWriter writer, NamedTypeNode node) => WriteNode(writer, node);
    /// <summary>Writes a non-null type.</summary>
    public static void WriteNonNullType(this ISyntaxWriter writer, NonNullTypeNode node) => WriteNode(writer, node);
    /// <summary>Writes a null literal.</summary>
    public static void WriteNullValue(this ISyntaxWriter writer) { ArgumentNullException.ThrowIfNull(writer); writer.Write("null"); }
    /// <summary>Writes an object field.</summary>
    public static void WriteObjectField(this ISyntaxWriter writer, ObjectFieldNode node, bool indented) => WriteNode(writer, node, indented);
    /// <summary>Writes an object field.</summary>
    public static void WriteObjectField(this ISyntaxWriter writer, ObjectFieldNode node) => WriteNode(writer, node);
    /// <summary>Writes an object value.</summary>
    public static void WriteObjectValue(this ISyntaxWriter writer, ObjectValueNode node, bool indented) => WriteNode(writer, node, indented);
    /// <summary>Writes an object value.</summary>
    public static void WriteObjectValue(this ISyntaxWriter writer, ObjectValueNode node) => WriteNode(writer, node);
    /// <summary>Writes a string literal.</summary>
    public static void WriteStringValue(this ISyntaxWriter writer, StringValueNode node) => WriteNode(writer, node);
    /// <summary>Writes an escaped string value.</summary>
    public static void WriteStringValue(this ISyntaxWriter writer, string value)
    {
        ArgumentNullException.ThrowIfNull(writer);
        writer.Write(GraphQLString.PrintString(value ?? throw new ArgumentNullException(nameof(value))));
    }
    /// <summary>Writes a type reference.</summary>
    public static void WriteType(this ISyntaxWriter writer, ITypeNode node) => WriteNode(writer, node);
    /// <summary>Writes a value.</summary>
    public static void WriteValue(this ISyntaxWriter writer, IValueNode node, bool indented) => WriteNode(writer, node, indented);
    /// <summary>Writes a value.</summary>
    public static void WriteValue(this ISyntaxWriter writer, IValueNode node) => WriteNode(writer, node);
    /// <summary>Writes a variable.</summary>
    public static void WriteVariable(this ISyntaxWriter writer, VariableNode node) => WriteNode(writer, node);

    private static void WriteFieldCore(ISyntaxWriter writer, NameNode name, IValueNode value, bool indented = true)
    {
        ArgumentNullException.ThrowIfNull(writer);
        ArgumentNullException.ThrowIfNull(name);
        writer.Write(GraphQLPrinter.Print(name));
        writer.Write(": ");
        WriteNode(writer, value, indented);
    }
    private static void WriteNode(ISyntaxWriter writer, ISyntaxNode node, bool indented = true)
    {
        ArgumentNullException.ThrowIfNull(writer);
        ArgumentNullException.ThrowIfNull(node);
        writer.Write(SyntaxPrinter.Print(node, indented));
    }
    private static void ValidateMany<T>(ISyntaxWriter writer, IReadOnlyList<T> items, Action<T, ISyntaxWriter> action)
    {
        ArgumentNullException.ThrowIfNull(writer);
        ArgumentNullException.ThrowIfNull(items);
        ArgumentNullException.ThrowIfNull(action);
    }
}
