using GraphQLParser;
using Xunit;

namespace GraphQLParser.Tests;

public sealed class GraphQLParserTests
{
    [Fact]
    public void ParsesShorthandAndNamedOperationsWithPreciseSpans()
    {
        const string source = "  { viewer } query Find($id: [ID!]! = 1) { user }  ";
        var document = GraphQLParser.Parse(new SourceText(source.AsMemory()));

        Assert.Equal(new SourceLocation(0, source.Length), document.Location);
        Assert.Equal(2, document.Definitions.Count);
        var shorthand = Assert.IsType<OperationDefinitionNode>(document.Definitions[0]);
        Assert.Equal(OperationType.Query, shorthand.Operation);
        Assert.Null(shorthand.Name);
        Assert.Equal(new SourceLocation(2, 12), shorthand.Location);

        var named = Assert.IsType<OperationDefinitionNode>(document.Definitions[1]);
        Assert.Equal("Find", named.Name!.Value.ToString());
        Assert.Equal(new SourceLocation(13, source.Length - 2), named.Location);
        var variable = Assert.Single(named.VariableDefinitions);
        Assert.Equal("id", variable.Variable.Name.Value.ToString());
        Assert.IsType<NonNullTypeNode>(variable.Type);
        Assert.IsType<IntValueNode>(variable.DefaultValue);
    }

    [Theory]
    [InlineData("")]
    [InlineData("query Q() { field }")]
    [InlineData("query Q($x:) { field }")]
    [InlineData("query Q($x: Int) { }")]
    [InlineData("query Q { field")]
    [InlineData("{ field } garbage")]
    public void RejectsEmptyOrMalformedRequiredGrammar(string source)
    {
        Assert.Throws<GraphQLSyntaxException>(() => GraphQLParser.Parse(new SourceText(source.AsMemory())));
    }

    [Fact]
    public void UnexpectedTokenReportsItsExactSourceLocation()
    {
        const string source = "query Q($x: Int) { field } @";
        var error = Assert.Throws<GraphQLSyntaxException>(() => GraphQLParser.Parse(new SourceText(source.AsMemory())));

        Assert.Equal(source.Length - 1, error.Position);
        Assert.Equal(1, error.Length);
    }
}
