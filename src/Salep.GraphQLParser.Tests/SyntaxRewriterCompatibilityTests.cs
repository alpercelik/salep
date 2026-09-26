using Salep.GraphQLParser.Visitors;
using Xunit;

namespace Salep.GraphQLParser.Tests;

public sealed class SyntaxRewriterCompatibilityTests
{
    [Fact]
    public void RewriterHasAParameterlessPreserveEveryNodeConstructor()
    {
        var document = GraphQLParser.Parse(new SourceText("{ a }".AsMemory()));
        var rewriter = new SyntaxRewriter<object>();

        Assert.Same(document, rewriter.Rewrite(document, new object()));
    }

    [Fact]
    public void RewriterRebuildsOnlyChangedAncestorsAndLeavesOriginalTreeIntact()
    {
        var document = GraphQLParser.Parse(new SourceText("{ a b }".AsMemory()));
        var originalOperation = Assert.IsType<OperationDefinitionNode>(document.Definitions[0]);
        var originalFirst = Assert.IsType<FieldNode>(originalOperation.SelectionSet.Selections[0]);
        var originalSecond = Assert.IsType<FieldNode>(originalOperation.SelectionSet.Selections[1]);

        var unchanged = SyntaxRewriter.Create<object>((node, _) => node).Rewrite(document, new object());
        Assert.Same(document, unchanged);

        var rewriter = SyntaxRewriter.Create<object>((node, _) => node is NameNode name && name.Value == "a"
            ? new NameNode("x".AsMemory(), name.Location)
            : node);
        var rewritten = rewriter.Rewrite(document);
        var rewrittenOperation = Assert.IsType<OperationDefinitionNode>(rewritten.Definitions[0]);
        var rewrittenFirst = Assert.IsType<FieldNode>(rewrittenOperation.SelectionSet.Selections[0]);

        Assert.NotSame(document, rewritten);
        Assert.NotSame(originalOperation, rewrittenOperation);
        Assert.NotSame(originalFirst, rewrittenFirst);
        Assert.Same(originalSecond, rewrittenOperation.SelectionSet.Selections[1]);
        Assert.Equal("{\n  a\n  b\n}", document.ToString());
        Assert.Equal("{\n  x\n  b\n}", rewritten.ToString());
    }

    [Fact]
    public void RewriterRejectsNullReplacementForANode()
    {
        var document = GraphQLParser.Parse(new SourceText("{ a }".AsMemory()));
        var rewriter = SyntaxRewriter.Create<object>((_, _) => null!);
        Assert.Throws<SyntaxNodeCannotBeNullException>(() => rewriter.Rewrite(document, new object()));
    }

    [Fact]
    public void RewritingANoLocationTreePreservesLocationSuppression()
    {
        var source = new SourceText("{ a b }".AsMemory());
        var document = GraphQLParser.Parse(source, new GraphQLParserOptions(noLocation: true));
        var rewriter = SyntaxRewriter.Create<object>((node, _) => node is NameNode name && name.Value == "a"
            ? new NameNode("x".AsMemory(), (SourceLocation)name.Location)
            : node);

        var rewritten = rewriter.Rewrite(document);

        Assert.Equal("{\n  x\n  b\n}", rewritten.ToString());
        GraphQLAstVisitor.Visit(rewritten, node =>
        {
            Assert.False(((AstNode)node).HasLocation);
            return GraphQLVisitControl.Continue;
        });
    }

    [Fact]
    public void RewriterUpdatesSchemaTreeNamesWithoutMutatingTheOriginal()
    {
        var document = GraphQLParser.Parse(new SourceText("type A { f: Int }".AsMemory()));
        var rewriter = SyntaxRewriter.Create<object>((node, _) => node is NameNode name && name.Value == "A"
            ? new NameNode("B".AsMemory(), name.Location)
            : node);

        var rewritten = rewriter.Rewrite(document);

        Assert.NotSame(document, rewritten);
        Assert.Contains("type A", document.ToString(), StringComparison.Ordinal);
        Assert.Contains("type B", rewritten.ToString(), StringComparison.Ordinal);
    }

    [Fact]
    public void NavigatorRewriterExposesTheCurrentPathAndRestoresItAfterRewrite()
    {
        var context = new NavigatorContext();
        var document = GraphQLParser.Parse(new SourceText("type Query { value: String }".AsMemory()));
        var rewriter = SyntaxRewriter.CreateWithNavigator<NavigatorContext>((node, current) =>
        {
            Assert.Same(node, current.Navigator.Peek());
            if (node is FieldDefinitionNode)
            {
                var coordinate = Assert.IsType<MemberCoordinateNode>(current.Navigator.CreateCoordinate());
                Assert.Equal("Query", coordinate.Name.Value);
                Assert.Equal("value", coordinate.MemberName.Value);
            }
            return node;
        });

        Assert.Same(document, rewriter.Rewrite(document, context));
        Assert.Equal(0, context.Navigator.Count);
    }
}
