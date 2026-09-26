using Salep.GraphQLParser;
using Salep.GraphQLParser.Visitors;
using Xunit;

namespace Salep.GraphQLParser.Tests;

public sealed class SyntaxNavigatorTests
{
    [Fact]
    public void StackOperationsUseZeroBasedDepthFromTheTopAndFailWithoutMutation()
    {
        var navigator = new DefaultSyntaxNavigator();
        var root = GraphQLParser.Parse(new SourceText("type Query { value: String }".AsMemory())).Definitions[0];
        var type = Assert.IsType<ObjectTypeDefinitionNode>(root);
        var field = type.Fields[0];

        Assert.Equal(0, navigator.Count);
        Assert.False(navigator.TryPeek(out var missing));
        Assert.Null(missing);
        Assert.False(navigator.TryPop(out missing));
        Assert.Null(missing);

        navigator.Push(type);
        navigator.Push(field);
        Assert.Equal(2, navigator.Count);
        Assert.Same(field, navigator.Peek());
        Assert.Same(field, navigator.Peek(0));
        Assert.Same(type, navigator.Peek(1));
        Assert.True(navigator.TryPeek(1, out var ancestor));
        Assert.Same(type, ancestor);
        Assert.Throws<ArgumentOutOfRangeException>(() => navigator.Peek(-1));
        Assert.Throws<InvalidOperationException>(() => navigator.Peek(2));
        Assert.Same(field, navigator.Pop());
        Assert.Same(type, navigator.Pop());
        Assert.Equal(0, navigator.Count);
    }

    [Fact]
    public void AncestorQueriesExcludeCurrentNodeAndReturnNearestFirst()
    {
        var navigator = new DefaultSyntaxNavigator();
        var type = Assert.IsType<ObjectTypeDefinitionNode>(GraphQLParser.Parse(new SourceText("type Query { value(arg: Int): String }".AsMemory())).Definitions[0]);
        var field = type.Fields[0];
        var input = field.Arguments[0];
        navigator.Push(type);
        navigator.Push(field);
        navigator.Push(input);

        Assert.Same(field, navigator.GetAncestor<FieldDefinitionNode>());
        Assert.Equal([field, type], navigator.GetAncestors<ISyntaxNode>());
        Assert.Throws<InvalidOperationException>(() => navigator.GetAncestor<ArgumentNode>());
    }

    [Fact]
    public void CreateCoordinateUsesTheCurrentSdlNodeAndItsContainingDefinition()
    {
        var type = Assert.IsType<ObjectTypeDefinitionNode>(GraphQLParser.Parse(new SourceText("type Query { value(arg: Int): String }".AsMemory())).Definitions[0]);
        var field = type.Fields[0];
        var input = field.Arguments[0];
        var navigator = new DefaultSyntaxNavigator();
        navigator.Push(type);
        navigator.Push(field);

        var member = Assert.IsType<MemberCoordinateNode>(navigator.CreateCoordinate());
        Assert.Equal("Query", member.Name.Value);
        Assert.Equal("value", member.MemberName.Value);
        Assert.Equal(field.Location.Start, member.Location.Start);

        navigator.Push(input);
        var argument = Assert.IsType<ArgumentCoordinateNode>(navigator.CreateCoordinate());
        Assert.Equal("Query", argument.Name.Value);
        Assert.Equal("value", argument.FieldName.Value);
        Assert.Equal("arg", argument.ArgumentName.Value);
    }

    [Fact]
    public void CreateCoordinateBuildsDirectiveCoordinatesFromDirectiveAncestry()
    {
        var directive = Assert.IsType<DirectiveDefinitionNode>(GraphQLParser.Parse(new SourceText("directive @tag(arg: Int) on FIELD_DEFINITION".AsMemory())).Definitions[0]);
        var navigator = new DefaultSyntaxNavigator();
        navigator.Push(directive);

        var coordinate = Assert.IsType<DirectiveCoordinateNode>(navigator.CreateCoordinate());
        Assert.Equal("tag", coordinate.Name.Value);

        navigator.Push(directive.Arguments[0]);
        var argument = Assert.IsType<DirectiveArgumentCoordinateNode>(navigator.CreateCoordinate());
        Assert.Equal("tag", argument.Name.Value);
        Assert.Equal("arg", argument.ArgumentName.Value);
    }

    [Fact]
    public void NavigatorContextCreatesAnEmptyDefaultNavigator()
    {
        var context = new NavigatorContext();
        Assert.IsType<DefaultSyntaxNavigator>(context.Navigator);
        Assert.Equal(0, context.Navigator.Count);
    }

    [Fact]
    public void CreateWithNavigatorTracksCurrentNodeAndAncestorsThenRestoresContext()
    {
        var context = new NavigatorContext();
        var visited = new List<SyntaxKind>();
        var visitor = SyntaxVisitor.CreateWithNavigator<NavigatorContext>(
            (node, current) =>
            {
                Assert.Same(node, current.Navigator.Peek());
                visited.Add(node.Kind);
                return new ContinueSyntaxVisitorAction();
            },
            (node, current) =>
            {
                Assert.Same(node, current.Navigator.Peek());
                return new ContinueSyntaxVisitorAction();
            });

        visitor.Visit(GraphQLParser.Parse(new SourceText("{ value }".AsMemory())), context);

        Assert.Contains(SyntaxKind.Field, visited);
        Assert.Equal(0, context.Navigator.Count);
    }

    [Fact]
    public void CreateWithNavigatorCleansThePathWhenTraversalBreaks()
    {
        var context = new NavigatorContext();
        var visitor = SyntaxVisitor.CreateWithNavigator<NavigatorContext>(
            (_, current) => current.Navigator.Peek().Kind == SyntaxKind.Field
                ? new BreakSyntaxVisitorAction()
                : new ContinueSyntaxVisitorAction());

        Assert.Equal(SyntaxVisitorActionKind.Break, visitor.Visit(
            GraphQLParser.Parse(new SourceText("{ value }".AsMemory())), context).Kind);
        Assert.Equal(0, context.Navigator.Count);
    }
}
