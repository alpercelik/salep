using Salep.Parser.Visitors;
using Xunit;

namespace Salep.Parser.Tests;

public sealed class SyntaxVisitorCompatibilityTests
{
    [Fact]
    public void Visitor_options_can_be_updated_after_construction()
    {
        var options = new SyntaxVisitorOptions();
        options.VisitArguments = false;
        options.VisitDescriptions = false;
        options.VisitDirectives = false;
        options.VisitNames = false;

        Assert.False(options.VisitArguments);
        Assert.False(options.VisitDescriptions);
        Assert.False(options.VisitDirectives);
        Assert.False(options.VisitNames);
    }

    [Fact]
    public void Visitor_runs_ordered_depth_first_enter_and_leave_callbacks()
    {
        var document = GraphQLParser.Parse(new SourceText("query Q { a }".AsMemory()));
        var events = new List<string>();
        var visitor = SyntaxVisitor.Create(
            node => { events.Add("+" + node.Kind); return new ContinueSyntaxVisitorAction(); },
            node => { events.Add("-" + node.Kind); return new ContinueSyntaxVisitorAction(); });

        var rootAction = visitor.Visit(document);

        Assert.Equal(SyntaxVisitorActionKind.Continue, rootAction.Kind);
        Assert.Equal(new[]
        {
            "+Document", "+OperationDefinition", "+Name", "-Name", "+SelectionSet", "+Field", "+Name", "-Name", "-Field", "-SelectionSet", "-OperationDefinition", "-Document",
        }, events);
    }

    [Fact]
    public void Skip_and_skip_and_leave_have_distinct_leave_behavior()
    {
        var document = GraphQLParser.Parse(new SourceText("{ a }".AsMemory()));
        var visited = new List<SyntaxKind>();
        var left = new List<SyntaxKind>();
        var visitor = SyntaxVisitor.Create(
            node =>
            {
                visited.Add(node.Kind);
                return node.Kind == SyntaxKind.SelectionSet ? new SkipAndLeaveSyntaxVisitorAction() : new ContinueSyntaxVisitorAction();
            },
            node => { left.Add(node.Kind); return new ContinueSyntaxVisitorAction(); });

        visitor.Visit(document);

        Assert.Contains(SyntaxKind.SelectionSet, visited);
        Assert.DoesNotContain(SyntaxKind.Field, visited);
        Assert.Contains(SyntaxKind.SelectionSet, left);
        Assert.Contains(SyntaxKind.OperationDefinition, left);

        var skipVisited = new List<SyntaxKind>();
        var skipLeft = new List<SyntaxKind>();
        var skipVisitor = SyntaxVisitor.Create(
            node =>
            {
                skipVisited.Add(node.Kind);
                return node.Kind == SyntaxKind.OperationDefinition ? new SkipSyntaxVisitorAction() : new ContinueSyntaxVisitorAction();
            },
            node => { skipLeft.Add(node.Kind); return new ContinueSyntaxVisitorAction(); });
        skipVisitor.Visit(document);
        Assert.Equal(new[] { SyntaxKind.Document, SyntaxKind.OperationDefinition }, skipVisited);
        Assert.Equal(new[] { SyntaxKind.Document }, skipLeft);
    }

    [Fact]
    public void Break_stops_traversal_and_null_actions_are_rejected()
    {
        var document = GraphQLParser.Parse(new SourceText("{ a b }".AsMemory()));
        var visited = new List<SyntaxKind>();
        var visitor = SyntaxVisitor.Create(node =>
        {
            visited.Add(node.Kind);
            return node.Kind == SyntaxKind.Field ? new BreakSyntaxVisitorAction() : new ContinueSyntaxVisitorAction();
        });

        Assert.Equal(SyntaxVisitorActionKind.Break, visitor.Visit(document).Kind);
        Assert.Equal(1, visited.Count(kind => kind == SyntaxKind.Field));
        Assert.Throws<InvalidOperationException>(() => SyntaxVisitor.Create(_ => null!).Visit(document));
    }

    [Fact]
    public void Syntax_walkers_are_runnable_visitor_types()
    {
        var document = GraphQLParser.Parse(new SourceText("{ field }".AsMemory()));
        var walker = new SyntaxWalker();
        var contextualWalker = new SyntaxWalker<List<SyntaxKind>>();

        Assert.Equal(SyntaxVisitorActionKind.Continue, walker.Visit(document).Kind);
        Assert.Equal(SyntaxVisitorActionKind.Continue, contextualWalker.Visit(document, []).Kind);
    }
}
