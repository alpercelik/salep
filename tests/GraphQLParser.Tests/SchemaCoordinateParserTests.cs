using GraphQLParser;
using Xunit;

namespace GraphQLParser.Tests;

public sealed class SchemaCoordinateParserTests
{
    [Theory]
    [InlineData("Thing", AstNodeKind.TypeCoordinate)]
    [InlineData("Thing.field", AstNodeKind.MemberCoordinate)]
    [InlineData("Thing.field(arg:)", AstNodeKind.ArgumentCoordinate)]
    [InlineData("@directive", AstNodeKind.DirectiveCoordinate)]
    [InlineData("@directive(arg:)", AstNodeKind.DirectiveArgumentCoordinate)]
    [InlineData("__Type.__meta(arg:)", AstNodeKind.ArgumentCoordinate)]
    [InlineData("_T0._f9(_a2:)", AstNodeKind.ArgumentCoordinate)]
    [InlineData("Type.__metafield", AstNodeKind.MemberCoordinate)]
    [InlineData("Type.__metafield(arg:)", AstNodeKind.ArgumentCoordinate)]
    public void ParsesValidCoordinates(string source, AstNodeKind kind)
    {
        var node = GraphQLParser.ParseSchemaCoordinate(new SourceText(source.AsMemory()));
        Assert.Equal(kind, node.Kind);
        Assert.Equal(new SourceLocation(0, source.Length), node.Location);
    }

    [Theory]
    [InlineData("Thing.field.deep")]
    [InlineData("Thing.field(arg: value)")]
    [InlineData("@directive.field")]
    [InlineData("Thing .field")]
    [InlineData("Thing.field( arg:)")]
    [InlineData("Thing.field(arg:)trailing")]
    [InlineData("@directive(arg:)trailing")]
    [InlineData("Thing.field\n")]
    [InlineData("Thing.")]
    [InlineData("")]
    public void RejectsInvalidCoordinates(string source)
    {
        Assert.Throws<GraphQLSyntaxException>(() => GraphQLParser.ParseSchemaCoordinate(new SourceText(source.AsMemory())));
    }

    [Fact]
    public void AcceptsCallerBackedSourceTextAndRetainsExactNameSpans()
    {
        var source = "prefix: Thing.field(arg:)".AsMemory();
        var input = new SourceText(source[8..]);
        var node = Assert.IsType<ArgumentCoordinateNode>(GraphQLParser.ParseSchemaCoordinate(input));
        Assert.Equal("Thing", node.Name.Value.ToString());
        Assert.Equal(new SourceLocation(0, 5), node.Name.Location);
        Assert.Equal("field", node.FieldName.Value.ToString());
        Assert.Equal("arg", node.ArgumentName.Value.ToString());
    }

    [Fact]
    public void StringAndSourceTextOverloadsProduceEquivalentCoordinates()
    {
        var fromString = GraphQLParser.ParseSchemaCoordinate("MyType.field");
        var fromMemory = GraphQLParser.ParseSchemaCoordinate(new SourceText("MyType.field".AsMemory()));
        Assert.IsType<MemberCoordinateNode>(fromString);
        Assert.Equal(fromMemory.Kind, fromString.Kind);
        Assert.Equal(fromMemory.Location, fromString.Location);
    }
}
