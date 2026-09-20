namespace GraphQLParser.Visitors;

/// <summary>Walks a syntax tree with the default object context.</summary>
public class SyntaxWalker : SyntaxVisitor
{
    /// <summary>Creates a syntax walker that continues through every child node.</summary>
    public SyntaxWalker() : base()
    {
    }

    /// <summary>Creates a syntax walker with a default visitor action.</summary>
    public SyntaxWalker(ISyntaxVisitorAction defaultAction, SyntaxVisitorOptions options = default)
        : base(defaultAction, options)
    {
    }
}

/// <summary>Walks a syntax tree with a typed context.</summary>
public class SyntaxWalker<TContext> : SyntaxVisitor<TContext>
{
    /// <summary>Creates a syntax walker that continues through every child node.</summary>
    public SyntaxWalker() : base((_, _) => new ContinueSyntaxVisitorAction(), null)
    {
    }
}
