using GraphQLParser;
using Xunit;

namespace GraphQLParser.Tests;

public sealed class ParserHelperOptionTests
{
    [Fact]
    public void LocationSuppressionIsOptInAndPreservesInternalSpans()
    {
        const string source = "{ viewer { id } }";
        var normal = GraphQLParser.Parse(new SourceText(source.AsMemory()));
        Assert.True(normal.HasLocation);

        var suppressed = GraphQLParser.Parse(new SourceText(source.AsMemory()), new GraphQLParserOptions(noLocation: true));
        var operation = Assert.IsType<OperationDefinitionNode>(Assert.Single(suppressed.Definitions));
        Assert.False(suppressed.HasLocation);
        Assert.False(operation.HasLocation);
        Assert.Equal(new SourceLocation(0, source.Length, hasLocation: false), suppressed.Location);
        Assert.Equal(source.Length, suppressed.Location.End);
        var canonical = CanonicalAstJson.Project(suppressed).ToJsonString();
        Assert.DoesNotContain("\"loc\"", canonical, StringComparison.Ordinal);
    }

    [Fact]
    public void LegacyFragmentVariablesAreDisabledByDefaultAndCanBeEnabled()
    {
        const string source = "fragment F(\"identifier\" $id: ID! = 1) on User { id }";
        Assert.Throws<GraphQLSyntaxException>(() => GraphQLParser.Parse(new SourceText(source.AsMemory())));

        var document = GraphQLParser.Parse(new SourceText(source.AsMemory()), new GraphQLParserOptions(allowLegacyFragmentVariables: true));
        var fragment = Assert.IsType<FragmentDefinitionNode>(Assert.Single(document.Definitions));
        var variable = Assert.Single(fragment.VariableDefinitions);
        Assert.Equal("id", variable.Variable.Name.Value.ToString());
        Assert.IsType<NonNullTypeNode>(variable.Type);
    }
}
