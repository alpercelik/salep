namespace GraphQLParser.Visitors;

/// <summary>Controls traversal of a syntax tree.</summary>
public enum SyntaxVisitorActionKind
{
    /// <summary>Continues into child nodes.</summary>
    Continue = 0,
    /// <summary>Skips child nodes and does not run the leave callback.</summary>
    Skip = 1,
    /// <summary>Stops traversal immediately.</summary>
    Break = 2,
    /// <summary>Skips child nodes and runs the leave callback.</summary>
    SkipAndLeave = 3,
}

/// <summary>A syntax traversal action.</summary>
public interface ISyntaxVisitorAction
{
    /// <summary>Gets the action kind.</summary>
    SyntaxVisitorActionKind Kind { get; }
}

    /// <summary>Represents ContinueSyntaxVisitorAction.</summary>
public sealed class ContinueSyntaxVisitorAction : ISyntaxVisitorAction
{
    /// <summary>Creates a ContinueSyntaxVisitorAction value.</summary>
    public ContinueSyntaxVisitorAction() { }
    /// <summary>Represents a syntax visitor API member.</summary>
    public SyntaxVisitorActionKind Kind => SyntaxVisitorActionKind.Continue;
}

    /// <summary>Represents SkipSyntaxVisitorAction.</summary>
public sealed class SkipSyntaxVisitorAction : ISyntaxVisitorAction
{
    /// <summary>Creates a SkipSyntaxVisitorAction value.</summary>
    public SkipSyntaxVisitorAction() { }
    /// <summary>Represents a syntax visitor API member.</summary>
    public SyntaxVisitorActionKind Kind => SyntaxVisitorActionKind.Skip;
}

    /// <summary>Represents BreakSyntaxVisitorAction.</summary>
public sealed class BreakSyntaxVisitorAction : ISyntaxVisitorAction
{
    /// <summary>Creates a BreakSyntaxVisitorAction value.</summary>
    public BreakSyntaxVisitorAction() { }
    /// <summary>Represents a syntax visitor API member.</summary>
    public SyntaxVisitorActionKind Kind => SyntaxVisitorActionKind.Break;
}

    /// <summary>Represents SkipAndLeaveSyntaxVisitorAction.</summary>
public sealed class SkipAndLeaveSyntaxVisitorAction : ISyntaxVisitorAction
{
    /// <summary>Creates a SkipAndLeaveSyntaxVisitorAction value.</summary>
    public SkipAndLeaveSyntaxVisitorAction() { }
    /// <summary>Represents a syntax visitor API member.</summary>
    public SyntaxVisitorActionKind Kind => SyntaxVisitorActionKind.SkipAndLeave;
}

/// <summary>Generic syntax visitor contract.</summary>
public interface ISyntaxVisitor<in TContext>
{
    /// <summary>Visits one node with the supplied context.</summary>
    ISyntaxVisitorAction Visit(ISyntaxNode node, TContext context);
}

/// <summary>Callback used to visit a syntax node with context.</summary>
public delegate ISyntaxVisitorAction VisitSyntaxNode<TContext>(ISyntaxNode node, TContext context);

/// <summary>Controls which syntax node groups a visitor observes.</summary>
public struct SyntaxVisitorOptions
{
    /// <summary>Creates a SyntaxVisitorOptions value.</summary>
    public SyntaxVisitorOptions(bool visitArguments = true, bool visitDescriptions = true, bool visitDirectives = true, bool visitNames = true)
    {
        VisitArguments = visitArguments;
        VisitDescriptions = visitDescriptions;
        VisitDirectives = visitDirectives;
        VisitNames = visitNames;
    }
    /// <summary>Gets the visitArguments value.</summary>
    public bool VisitArguments { get; set; }
    /// <summary>Gets the visitDescriptions value.</summary>
    public bool VisitDescriptions { get; set; }
    /// <summary>Gets the visitDirectives value.</summary>
    public bool VisitDirectives { get; set; }
    /// <summary>Gets the visitNames value.</summary>
    public bool VisitNames { get; set; }
}

internal interface ISyntaxTreeWalker
{
    ISyntaxVisitorAction WalkTree(ISyntaxNode root, object? context);
}

/// <summary>Performs ordered depth-first syntax visitation.</summary>
public class SyntaxVisitor<TContext> : ISyntaxVisitor<TContext>, ISyntaxTreeWalker
{
    private readonly VisitSyntaxNode<TContext> _enter;
    private readonly VisitSyntaxNode<TContext>? _leave;
    private readonly Func<TContext, ISyntaxNavigator?>? _navigator;

    /// <summary>Creates a visitor with enter and optional leave callbacks.</summary>
    protected SyntaxVisitor(
        VisitSyntaxNode<TContext> enter,
        VisitSyntaxNode<TContext>? leave,
        Func<TContext, ISyntaxNavigator?>? navigator = null)
    {
        _enter = enter;
        _leave = leave;
        _navigator = navigator;
    }

    /// <summary>Creates a ContinueSyntaxVisitorAction value.</summary>
    public ISyntaxVisitorAction Continue { get; } = new ContinueSyntaxVisitorAction();
    /// <summary>Creates a SkipSyntaxVisitorAction value.</summary>
    public ISyntaxVisitorAction Skip { get; } = new SkipSyntaxVisitorAction();
    /// <summary>Creates a BreakSyntaxVisitorAction value.</summary>
    public ISyntaxVisitorAction Break { get; } = new BreakSyntaxVisitorAction();
    /// <summary>Creates a SkipAndLeaveSyntaxVisitorAction value.</summary>
    public ISyntaxVisitorAction SkipAndLeave { get; } = new SkipAndLeaveSyntaxVisitorAction();

    /// <summary>Visits the supplied syntax node and walks its descendants.</summary>
    public ISyntaxVisitorAction Visit(ISyntaxNode node, TContext context) => WalkTree(node, context);

    ISyntaxVisitorAction ISyntaxTreeWalker.WalkTree(ISyntaxNode root, object? context) => WalkTree(root, (TContext)context!);

    private ISyntaxVisitorAction WalkTree(ISyntaxNode root, TContext context)
    {
        ArgumentNullException.ThrowIfNull(root);
        var pending = new Stack<(ISyntaxNode Node, bool Leaving)>();
        var navigator = _navigator?.Invoke(context);
        var initialNavigatorDepth = navigator?.Count ?? 0;
        ISyntaxVisitorAction? rootAction = null;
        pending.Push((root, false));
        try
        {
            while (pending.TryPop(out var item))
            {
                if (item.Leaving)
                {
                    var leaveAction = _leave?.Invoke(item.Node, context);
                    if (leaveAction?.Kind == SyntaxVisitorActionKind.Break) return new BreakSyntaxVisitorAction();
                    if (navigator is not null) navigator.Pop();
                    continue;
                }

                navigator?.Push(item.Node);
                var action = _enter(item.Node, context) ?? throw new InvalidOperationException("A syntax visitor returned a null action.");
                if (ReferenceEquals(item.Node, root)) rootAction = action;
                switch (action.Kind)
                {
                    case SyntaxVisitorActionKind.Break:
                        return action;
                    case SyntaxVisitorActionKind.Skip:
                        if (navigator is not null) navigator.Pop();
                        continue;
                    case SyntaxVisitorActionKind.SkipAndLeave:
                        if (_leave is not null) pending.Push((item.Node, true));
                        else if (navigator is not null) navigator.Pop();
                        continue;
                    case SyntaxVisitorActionKind.Continue:
                        if (_leave is not null || navigator is not null) pending.Push((item.Node, true));
                        var children = item.Node.GetNodes().ToArray();
                        for (var index = children.Length - 1; index >= 0; index--) pending.Push((children[index], false));
                        break;
                    default:
                        throw new ArgumentOutOfRangeException(nameof(action), "The visitor returned an unknown action kind.");
                }
            }

            return rootAction ?? new ContinueSyntaxVisitorAction();
        }
        finally
        {
            if (navigator is not null)
                while (navigator.Count > initialNavigatorDepth) navigator.Pop();
        }
    }

    /// <summary>Creates a syntax visitor.</summary>
    public static ISyntaxVisitor<TContext> Create(
        VisitSyntaxNode<TContext>? enter = null,
        VisitSyntaxNode<TContext>? leave = null,
        ISyntaxVisitorAction? defaultAction = null,
        SyntaxVisitorOptions options = default)
    {
        return new SyntaxVisitor<TContext>(enter ?? ((_, _) => defaultAction ?? new ContinueSyntaxVisitorAction()), leave);
    }
}

/// <summary>Creates and walks a syntax visitor without a custom context.</summary>
public class SyntaxVisitor : SyntaxVisitor<object>
{
    private SyntaxVisitor(Func<ISyntaxNode, ISyntaxVisitorAction>? enter, Func<ISyntaxNode, ISyntaxVisitorAction>? leave)
        : base((node, _) => enter is null ? new ContinueSyntaxVisitorAction() : enter(node), leave is null ? null : (node, _) => leave(node))
    {
    }

    /// <summary>Creates a SyntaxVisitor value.</summary>
    public SyntaxVisitor(ISyntaxVisitorAction defaultResult, SyntaxVisitorOptions options = default)
        : base((_, _) => defaultResult, null) { ArgumentNullException.ThrowIfNull(defaultResult); }

    /// <summary>Creates a SyntaxVisitor value.</summary>
    public SyntaxVisitor(SyntaxVisitorOptions options = default)
        : base((_, _) => new ContinueSyntaxVisitorAction(), null) { }

    /// <summary>Creates a syntax visitor.</summary>
    public static ISyntaxVisitor<object> Create(
        Func<ISyntaxNode, ISyntaxVisitorAction>? enter = null,
        Func<ISyntaxNode, ISyntaxVisitorAction>? leave = null,
        ISyntaxVisitorAction? defaultAction = null,
        SyntaxVisitorOptions options = default) =>
        new SyntaxVisitor(enter ?? (_ => defaultAction ?? new ContinueSyntaxVisitorAction()), leave);

    /// <summary>Creates a context-aware syntax visitor.</summary>
    public static ISyntaxVisitor<TContext> Create<TContext>(
        VisitSyntaxNode<TContext>? enter = null,
        VisitSyntaxNode<TContext>? leave = null,
        ISyntaxVisitorAction? defaultAction = null,
        SyntaxVisitorOptions options = default) =>
        SyntaxVisitor<TContext>.Create(enter, leave, defaultAction, options);

    /// <summary>Creates a context-aware syntax visitor that tracks node ancestry in navigator contexts.</summary>
    public static ISyntaxVisitor<TContext> CreateWithNavigator<TContext>(
        VisitSyntaxNode<TContext>? enter = null,
        VisitSyntaxNode<TContext>? leave = null,
        ISyntaxVisitorAction? defaultAction = null,
        SyntaxVisitorOptions options = default) =>
        new NavigatorSyntaxVisitor<TContext>(enter, leave, defaultAction);
}

internal sealed class NavigatorSyntaxVisitor<TContext> : SyntaxVisitor<TContext>
{
    public NavigatorSyntaxVisitor(
        VisitSyntaxNode<TContext>? enter,
        VisitSyntaxNode<TContext>? leave,
        ISyntaxVisitorAction? defaultAction)
        : base(enter ?? ((_, _) => defaultAction ?? new ContinueSyntaxVisitorAction()), leave,
            context => context is INavigatorContext navigatorContext ? navigatorContext.Navigator : null)
    {
    }
}

/// <summary>Runs a visitor over a syntax tree.</summary>
public static class SyntaxVisitorExtensions
{
    /// <summary>Visits or walks syntax nodes.</summary>
    public static ISyntaxVisitorAction Visit(this ISyntaxVisitor<object> visitor, ISyntaxNode node)
    {
        ArgumentNullException.ThrowIfNull(visitor);
        ArgumentNullException.ThrowIfNull(node);
        if (visitor is ISyntaxTreeWalker walker) return walker.WalkTree(node, null);
        return visitor.Visit(node, new object());
    }
}

/// <summary>Helpers for checking syntax traversal actions.</summary>
public static class SyntaxVisitorActionExtension
{
    /// <summary>Checks whether an action stops traversal.</summary>
    public static bool IsBreak(this ISyntaxVisitorAction action) => action.Kind == SyntaxVisitorActionKind.Break;
    /// <summary>Checks whether an action continues traversal.</summary>
    public static bool IsContinue(this ISyntaxVisitorAction action) => action.Kind == SyntaxVisitorActionKind.Continue;
    /// <summary>Checks whether an action skips child nodes.</summary>
    public static bool IsSkip(this ISyntaxVisitorAction action) => action.Kind is SyntaxVisitorActionKind.Skip or SyntaxVisitorActionKind.SkipAndLeave;
}
