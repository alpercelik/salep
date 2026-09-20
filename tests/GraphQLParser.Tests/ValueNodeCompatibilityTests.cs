using Xunit;

namespace GraphQLParser.Tests;

public sealed class ValueNodeCompatibilityTests
{
    private static Location Location(int start = 3, int end = 8) => new(start, end, 2, 4);

    [Fact]
    public void Scalar_value_nodes_support_immutable_copy_methods_and_preserve_source_memory()
    {
        var source = "_123456_".AsMemory(1, 6);
        var integer = new IntValueNode(source, new SourceLocation(3, 9));
        var movedInteger = integer.WithLocation(Location());
        Assert.Equal("123456", movedInteger.Value);
        Assert.Equal(source, movedInteger.SourceValue);
        Assert.Equal("-9", integer.WithValue(-9).Value);
        Assert.Equal("17", integer.WithValue(17L).Value);

        var floatNode = new FloatValueNode("12.50".AsMemory(), new SourceLocation(3, 8));
        Assert.Equal("125", floatNode.WithValue(125d).Value);
        Assert.Equal("2.75", floatNode.WithValue(2.75m).Value);
        Assert.Equal("12.50", floatNode.WithLocation(Location()).Value);

        var stringValue = new StringValueNode("hello".AsMemory(), true, new SourceLocation(3, 8));
        Assert.True(stringValue.WithValue("world").IsBlock);
        Assert.False(stringValue.WithValue("world", false).IsBlock);
        Assert.Equal("hello", stringValue.WithLocation(Location()).Value);

        var enumValue = new EnumValueNode("READY".AsMemory(), new SourceLocation(3, 8));
        Assert.Equal("DONE", enumValue.WithValue("DONE").Value);
        Assert.Equal("READY", enumValue.WithLocation(Location()).Value);
        Assert.Equal("READY"u8.ToArray(), enumValue.AsSpan().ToArray());

        var boolean = new BooleanValueNode(false, new SourceLocation(3, 8));
        Assert.True(boolean.WithValue(true).Value);
        Assert.True(boolean.WithLocation(Location()).HasLocation);
        Assert.IsType<NullValueNode>(new NullValueNode(new SourceLocation(3, 8)).WithLocation(Location()));
    }

    [Fact]
    public void Value_node_constructors_accept_the_compatibility_argument_shapes()
    {
        var location = Location();
        var variableName = new NameNode("item");
        Assert.Equal("next", new VariableNode(variableName).WithName(new NameNode("next")).Name.Value);
        Assert.Equal("item", new VariableNode(location, variableName).Name.Value);

        Assert.Equal("42", new IntValueNode(location, 42).Value);
        Assert.Equal("2.5", new FloatValueNode(location, 2.5m).Value);
        Assert.Equal("text", new StringValueNode(location, "text", false).Value);
        Assert.True(new BooleanValueNode(location, true).Value);
        Assert.Equal("READY", new EnumValueNode(location, "READY").Value);
        Assert.Equal("9", new IntValueNode(9).Value);
        Assert.Equal("1.5", new FloatValueNode(1.5m).Value);
        Assert.Equal("text", new StringValueNode("text").Value);
        Assert.Equal("READY", new EnumValueNode("READY").Value);
        Assert.Equal("7", new EnumValueNode(7).Value);
        Assert.True(BooleanValueNode.True.Value);
        Assert.False(BooleanValueNode.False.Value);
        Assert.Null(((IValueNode)NullValueNode.Default).Value);
        Assert.Null(NullValueNode.Default.Value);
        Assert.True(NullValueNode.Default.Equals((IValueNode)new NullValueNode()));
        Assert.False(NullValueNode.Default.Equals((IValueNode)new BooleanValueNode(false)));
    }

    [Fact]
    public void List_and_object_value_copy_methods_keep_order_and_child_identity()
    {
        var first = new IntValueNode(1);
        var second = new StringValueNode("two");
        IValueNode[] listItems = [first, second];
        var list = new ListValueNode(listItems);
        Assert.Same(first, list.WithItems(listItems).Values[0]);
        Assert.Same(second, list.WithLocation(Location()).Values[1]);
        Assert.Single(new ListValueNode(Location(), first).Values);

        var field = new ObjectFieldNode("answer", new IntValueNode(42));
        var fields = new[] { field };
        var objectValue = new ObjectValueNode(fields);
        Assert.Same(field, objectValue.WithFields(fields).Fields[0]);
        Assert.Same(field, objectValue.WithLocation(Location()).Fields[0]);

        var renamed = field.WithName(new NameNode("result"));
        Assert.Equal("result", renamed.Name.Value);
        Assert.Same(field.Value, renamed.Value);
        Assert.Equal("{result: 42}", GraphQLPrinter.Print(new ObjectValueNode([renamed])));
        Assert.Equal("\"text\"", GraphQLPrinter.Print(new ObjectFieldNode("text", "text").Value));
        Assert.Equal("true", GraphQLPrinter.Print(new ObjectFieldNode("flag", true).Value));
        Assert.Equal("2.5", GraphQLPrinter.Print(new ObjectFieldNode("decimal", 2.5d).Value));
    }

    [Fact]
    public void Object_field_equality_compares_name_and_syntax_value()
    {
        var first = new ObjectFieldNode(new NameNode("answer"), new IntValueNode("42"), new SourceLocation(0, 10));
        var sameSyntax = new ObjectFieldNode(new NameNode("answer"), new IntValueNode("42"), new SourceLocation(20, 30));
        var differentValue = new ObjectFieldNode(new NameNode("answer"), new IntValueNode("43"), new SourceLocation(0, 10));

        Assert.Equal(first, sameSyntax);
        Assert.Equal(first.GetHashCode(), sameSyntax.GetHashCode());
        Assert.NotEqual(first, differentValue);
    }
}
