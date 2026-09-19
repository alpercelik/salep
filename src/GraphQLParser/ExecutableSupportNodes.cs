namespace GraphQLParser;

/// <summary>An argument supplied to a field or directive.</summary>
public sealed class ArgumentNode : AstNode
{
    /// <summary>Creates an immutable argument.</summary>
    public ArgumentNode(NameNode name, ValueNode value, SourceLocation location)
        : base(AstNodeKind.Argument, location)
    {
        ArgumentNullException.ThrowIfNull(name);
        ArgumentNullException.ThrowIfNull(value);
        Name = name;
        Value = value;
    }

    /// <summary>Gets the argument name.</summary>
    public NameNode Name { get; }
    /// <summary>Gets the argument value.</summary>
    public ValueNode Value { get; }
}

/// <summary>A directive application.</summary>
public sealed class DirectiveNode : AstNode
{
    /// <summary>Creates an immutable directive application.</summary>
    public DirectiveNode(NameNode name, IEnumerable<ArgumentNode> arguments, SourceLocation location)
        : base(AstNodeKind.Directive, location)
    {
        ArgumentNullException.ThrowIfNull(name);
        ArgumentNullException.ThrowIfNull(arguments);
        Name = name;
        Arguments = new AstNodeList<ArgumentNode>(arguments);
    }

    /// <summary>Gets the directive name.</summary>
    public NameNode Name { get; }
    /// <summary>Gets directive arguments in source order.</summary>
    public AstNodeList<ArgumentNode> Arguments { get; }
}

/// <summary>A variable declaration within an operation.</summary>
public sealed class VariableDefinitionNode : AstNode
{
    /// <summary>Creates an immutable variable definition.</summary>
    public VariableDefinitionNode(
        VariableNode variable,
        TypeNode type,
        ValueNode? defaultValue,
        IEnumerable<DirectiveNode> directives,
        SourceLocation location,
        StringValueNode? description = null)
        : base(AstNodeKind.VariableDefinition, location)
    {
        ArgumentNullException.ThrowIfNull(variable);
        ArgumentNullException.ThrowIfNull(type);
        ArgumentNullException.ThrowIfNull(directives);
        Variable = variable;
        Type = type;
        DefaultValue = defaultValue;
        Directives = new AstNodeList<DirectiveNode>(directives);
        Description = description;
    }

    /// <summary>Gets the declared variable.</summary>
    public VariableNode Variable { get; }
    /// <summary>Gets the required variable type.</summary>
    public TypeNode Type { get; }
    /// <summary>Gets the optional constant default value.</summary>
    public ValueNode? DefaultValue { get; }
    /// <summary>Gets directives in source order.</summary>
    public AstNodeList<DirectiveNode> Directives { get; }
    /// <summary>Gets the optional variable definition description.</summary>
    public StringValueNode? Description { get; }
}
