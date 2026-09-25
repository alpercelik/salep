using Salep.Parser;
using Salep.Parser.Visitors;
using Salep.Parser.Utilities;
using Xunit;

namespace Salep.Parser.Tests;

public sealed class SyntaxNodeExtensionsTests
{
    [Fact]
    public void TypeHelpersUnwrapNestedListAndNonNullTypes()
    {
        var named = new NamedTypeNode(new NameNode("User"), default);
        ITypeNode nested = new NonNullTypeNode(new ListTypeNode(new NonNullTypeNode(named, default), default), default);

        Assert.True(SyntaxNodeExtensions.IsNonNullType(nested));
        Assert.IsType<ListTypeNode>(SyntaxNodeExtensions.NullableType(nested));
        Assert.IsType<NonNullTypeNode>(SyntaxNodeExtensions.ElementType(SyntaxNodeExtensions.NullableType(nested)));
        Assert.Same(named, SyntaxNodeExtensions.NamedType(nested));
        Assert.Same(named, SyntaxNodeExtensions.InnerType(nested));
        Assert.False(SyntaxNodeExtensions.IsListType(nested));
        Assert.True(SyntaxNodeExtensions.IsNull(new NullValueNode()));
    }

    [Fact]
    public void DirectiveExtensionSupportsConstructionChildrenCopyPrintAndRewrite()
    {
        var directive = new DirectiveNode(new NameNode("deprecated"), Array.Empty<ArgumentNode>(), default);
        var extension = new DirectiveExtensionNode(new Location(0, 30, 1, 1), new NameNode("custom"), new[] { directive });

        Assert.Equal("extend directive @custom @deprecated", extension.ToString());
        Assert.Equal(new[] { "custom", "deprecated" }, extension.GetNodes().Cast<AstNode>().Select(node => node switch
        {
            NameNode name => name.Value.ToString(),
            DirectiveNode value => value.Name.Value.ToString(),
            _ => node.ToString(),
        }));
        Assert.Equal("other", extension.WithLocation(new Location(2, 32, 2, 1)).WithName(new NameNode("other")).Name.Value.ToString());

        var rewritten = SyntaxRewriter.Create(static (node) => node is NameNode name && name.Value == "custom"
            ? name.WithValue("rewritten")
            : node).Rewrite(extension);
        Assert.Equal("extend directive @rewritten @deprecated", rewritten.ToString());
    }

    [Fact]
    public void SyntaxComparisonAndFormatterHelpersUseConfiguredOptions()
    {
        var first = GraphQLParser.Parse(new SourceText("{ user { name } }".AsMemory()));
        var second = GraphQLParser.Parse(new SourceText("{user{name}}".AsMemory()));

        Assert.True(SyntaxNodeExtensions.Equals(first, second, SyntaxComparison.Syntax));
        Assert.Equal(GraphQLPrinter.Print(first), SyntaxNodeExtensions.ToString(first, new SyntaxSerializerOptions()));
    }
}
