namespace Salep.GraphQLParser.Visitors;

/// <summary>Rewrites one syntax node with a traversal context.</summary>
public delegate ISyntaxNode RewriteSyntaxNode<TContext>(ISyntaxNode node, TContext context);

/// <summary>Contract for a syntax-tree rewriter.</summary>
public interface ISyntaxRewriter<in TContext>
{
    /// <summary>Rewrites a node and its supported descendants with the supplied context.</summary>
    ISyntaxNode Rewrite(ISyntaxNode node, TContext context);
}

/// <summary>Raised when a rewrite callback returns null for a required syntax node.</summary>
public sealed class SyntaxNodeCannotBeNullException : Exception
{
    /// <summary>Creates a SyntaxNodeCannotBeNullException value.</summary>
    public SyntaxNodeCannotBeNullException(ISyntaxNode node)
        : base($"A rewrite callback returned null for syntax node kind '{node.Kind}'.")
    {
        Kind = node.Kind;
        Location = node.Location;
    }

    /// <summary>Gets the kind value.</summary>
    public SyntaxKind Kind { get; }
    /// <summary>Gets the location value.</summary>
    public Location Location { get; }
}

/// <summary>Implements a recursive immutable syntax-tree rewriter.</summary>
public class SyntaxRewriter<TContext> : ISyntaxRewriter<TContext>
{
    private readonly RewriteSyntaxNode<TContext> _rewrite;
    private readonly Func<ISyntaxNode, TContext, TContext>? _enter;
    private readonly Action<ISyntaxNode, TContext>? _leave;
    private readonly Func<TContext, ISyntaxNavigator?>? _navigator;

    /// <summary>Creates a syntax rewriter that preserves every visited node.</summary>
    public SyntaxRewriter() : this(null, null, null, null) { }

    /// <summary>Creates a SyntaxRewriter value.</summary>
    public SyntaxRewriter(
        RewriteSyntaxNode<TContext>? rewrite,
        Func<ISyntaxNode, TContext, TContext>? enter = null,
        Action<ISyntaxNode, TContext>? leave = null,
        Func<TContext, ISyntaxNavigator?>? navigator = null)
    {
        _rewrite = rewrite ?? ((node, _) => node);
        _enter = enter;
        _leave = leave;
        _navigator = navigator;
    }

    /// <summary>Rewrites a syntax node or tree.</summary>
    public ISyntaxNode Rewrite(ISyntaxNode node, TContext context)
    {
        ArgumentNullException.ThrowIfNull(node);
        var navigator = _navigator?.Invoke(context);
        var initialNavigatorDepth = navigator?.Count ?? 0;
        navigator?.Push(node);
        try
        {
            var currentContext = _enter is null ? context : _enter(node, context);
            var rewritten = node is AstNode ast ? RewriteChildren(ast, currentContext) : node;
            rewritten = _rewrite(rewritten, currentContext) ?? throw new SyntaxNodeCannotBeNullException(rewritten);
            _leave?.Invoke(node, currentContext);
            return rewritten;
        }
        finally
        {
            if (navigator is not null)
                while (navigator.Count > initialNavigatorDepth) navigator.Pop();
        }
    }

    private ISyntaxNode RewriteChildren(AstNode node, TContext context)
    {
        switch (node)
        {
            case DocumentNode document:
            {
                var definitions = RewriteMany(document.Definitions.Select(definition => definition as DefinitionNode
                    ?? throw new InvalidOperationException("Document definitions must be parser definition nodes.")).ToArray(), context);
                return definitions.Changed ? new DocumentNode(document.Source, definitions.Items, document.SourceRange, document.SourceInfo) : node;
            }
            case OperationDefinitionNode operation:
            {
                var description = RewriteOptional(operation.Description, context);
                var name = RewriteOptional(operation.Name, context);
                var variables = RewriteMany(operation.VariableDefinitions, context);
                var directives = RewriteMany(operation.Directives, context);
                var selection = RewriteRequired(operation.SelectionSet, context);
                return Changed(operation.Description, description) || Changed(operation.Name, name) || variables.Changed || directives.Changed || Changed(operation.SelectionSet, selection)
                    ? new OperationDefinitionNode(operation.Operation, name, variables.Items, directives.Items, selection, operation.SourceRange, description)
                    : node;
            }
            case FragmentDefinitionNode fragment:
            {
                var description = RewriteOptional(fragment.Description, context);
                var name = RewriteRequired(fragment.Name, context);
                var variables = RewriteMany(fragment.VariableDefinitions, context);
                var condition = RewriteRequired(fragment.TypeCondition, context);
                var directives = RewriteMany(fragment.Directives, context);
                var selection = RewriteRequired(fragment.SelectionSet, context);
                return Changed(fragment.Description, description) || Changed(fragment.Name, name) || variables.Changed || Changed(fragment.TypeCondition, condition) || directives.Changed || Changed(fragment.SelectionSet, selection)
                    ? new FragmentDefinitionNode(name, condition, directives.Items, selection, fragment.SourceRange, description, variables.Items)
                    : node;
            }
            case SelectionSetNode selectionSet:
            {
                var selections = RewriteMany(selectionSet.Selections.Select(selection => selection as AstNode
                    ?? throw new InvalidOperationException("Selections must be parser syntax nodes.")).ToArray(), context);
                return selections.Changed ? new SelectionSetNode(selections.Items.Cast<SelectionNode>(), selectionSet.SourceRange) : node;
            }
            case FieldNode field:
            {
                var name = RewriteRequired(field.Name, context);
                var alias = RewriteOptional(field.Alias, context);
                var arguments = RewriteMany(field.Arguments, context);
                var directives = RewriteMany(field.Directives, context);
                var selection = RewriteOptional(field.SelectionSet, context);
                return Changed(field.Name, name) || Changed(field.Alias, alias) || arguments.Changed || directives.Changed || Changed(field.SelectionSet, selection)
                    ? new FieldNode(name, alias, arguments.Items, directives.Items, selection, field.SourceRange)
                    : node;
            }
            case FragmentSpreadNode spread:
            {
                var name = RewriteRequired(spread.Name, context);
                var arguments = RewriteMany(spread.Arguments, context);
                var directives = RewriteMany(spread.Directives, context);
                return Changed(spread.Name, name) || arguments.Changed || directives.Changed
                    ? new FragmentSpreadNode(spread.SourceRange, name, arguments.Items, directives.Items)
                    : node;
            }
            case InlineFragmentNode inline:
            {
                var condition = RewriteOptional(inline.TypeCondition, context);
                var directives = RewriteMany(inline.Directives, context);
                var selection = RewriteRequired(inline.SelectionSet, context);
                return Changed(inline.TypeCondition, condition) || directives.Changed || Changed(inline.SelectionSet, selection)
                    ? new InlineFragmentNode(condition, directives.Items, selection, inline.SourceRange)
                    : node;
            }
            case ArgumentNode argument:
            {
                var name = RewriteRequired(argument.Name, context);
                var originalValue = argument.Value as ValueNode ?? throw new InvalidOperationException("Arguments must contain parser value nodes.");
                var value = RewriteRequired(originalValue, context);
                return Changed(argument.Name, name) || Changed(argument.Value, value) ? new ArgumentNode(name, value, argument.SourceRange) : node;
            }
            case DirectiveNode directive:
            {
                var name = RewriteRequired(directive.Name, context);
                var arguments = RewriteMany(directive.Arguments, context);
                return Changed(directive.Name, name) || arguments.Changed ? new DirectiveNode(name, arguments.Items, directive.SourceRange) : node;
            }
            case VariableDefinitionNode variable:
            {
                var description = RewriteOptional(variable.Description, context);
                var variableNode = RewriteRequired(variable.Variable, context);
                var originalType = variable.Type as TypeNode ?? throw new InvalidOperationException("Variable definitions must use parser type nodes.");
                var type = RewriteRequired(originalType, context);
                var originalDefault = variable.DefaultValue as ValueNode;
                var defaultValue = RewriteOptional(originalDefault, context);
                var directives = RewriteMany(variable.Directives, context);
                return Changed(variable.Description, description) || Changed(variable.Variable, variableNode) || Changed(variable.Type, type) || Changed(variable.DefaultValue, defaultValue) || directives.Changed
                    ? new VariableDefinitionNode(variableNode, type, defaultValue, directives.Items, variable.SourceRange, description)
                    : node;
            }
            case VariableNode variable:
            {
                var name = RewriteRequired(variable.Name, context);
                return Changed(variable.Name, name) ? new VariableNode(name, variable.SourceRange) : node;
            }
            case ListValueNode list:
            {
                var values = RewriteMany(list.Values, context);
                return values.Changed ? new ListValueNode(values.Items, list.SourceRange) : node;
            }
            case ObjectValueNode obj:
            {
                var fields = RewriteMany(obj.Fields, context);
                return fields.Changed ? new ObjectValueNode(fields.Items, obj.SourceRange) : node;
            }
            case ObjectFieldNode field:
            {
                var name = RewriteRequired(field.Name, context);
                var originalValue = field.Value as ValueNode ?? throw new InvalidOperationException("Object fields must contain parser value nodes.");
                var value = RewriteRequired(originalValue, context);
                return Changed(field.Name, name) || Changed(field.Value, value) ? new ObjectFieldNode(name, value, field.SourceRange) : node;
            }
            case NamedTypeNode type:
            {
                var name = RewriteRequired(type.Name, context);
                return Changed(type.Name, name) ? new NamedTypeNode(name, type.SourceRange) : node;
            }
            case ListTypeNode type:
            {
                var originalInner = type.Type as TypeNode ?? throw new InvalidOperationException("List types must contain parser type nodes.");
                var inner = RewriteRequired(originalInner, context);
                return Changed(type.Type, inner) ? new ListTypeNode(inner, type.SourceRange) : node;
            }
            case NonNullTypeNode type:
            {
                var originalInner = type.Type as TypeNode ?? throw new InvalidOperationException("Non-null types must contain parser type nodes.");
                var inner = RewriteRequired(originalInner, context);
                return Changed<TypeNode>((TypeNode)type.Type, inner) ? new NonNullTypeNode(inner, type.SourceRange) : node;
            }
            case SchemaDefinitionNode schema:
            {
                var description = RewriteOptional(schema.Description, context);
                var directives = RewriteMany(schema.Directives, context);
                var operations = RewriteMany(schema.OperationTypes, context);
                return Changed(schema.Description, description) || directives.Changed || operations.Changed
                    ? new SchemaDefinitionNode(operations.Items, directives.Items, schema.SourceRange, description)
                    : node;
            }
            case SchemaExtensionNode schema:
            {
                var directives = RewriteMany(schema.Directives, context);
                var operations = RewriteMany(schema.OperationTypes, context);
                return directives.Changed || operations.Changed
                    ? new SchemaExtensionNode(operations.Items, directives.Items, schema.SourceRange)
                    : node;
            }
            case OperationTypeDefinitionNode operation:
            {
                var type = RewriteRequired(operation.Type, context);
                return Changed(operation.Type, type) ? new OperationTypeDefinitionNode(operation.Operation, type, operation.SourceRange) : node;
            }
            case ScalarTypeDefinitionNode scalar:
            {
                var name = RewriteRequired(scalar.Name, context);
                var description = RewriteOptional(scalar.Description, context);
                var directives = RewriteMany(scalar.Directives, context);
                return Changed(scalar.Name, name) || Changed(scalar.Description, description) || directives.Changed
                    ? new ScalarTypeDefinitionNode(name, directives.Items, scalar.SourceRange, description)
                    : node;
            }
            case ScalarTypeExtensionNode scalar:
            {
                var name = RewriteRequired(scalar.Name, context);
                var directives = RewriteMany(scalar.Directives, context);
                return Changed(scalar.Name, name) || directives.Changed ? new ScalarTypeExtensionNode(name, directives.Items, scalar.SourceRange) : node;
            }
            case ObjectTypeDefinitionNode type:
            {
                var name = RewriteRequired(type.Name, context);
                var description = RewriteOptional(type.Description, context);
                var interfaces = RewriteMany(type.Interfaces, context);
                var directives = RewriteMany(type.Directives, context);
                var fields = RewriteMany(type.Fields, context);
                return Changed(type.Name, name) || Changed(type.Description, description) || interfaces.Changed || directives.Changed || fields.Changed
                    ? new ObjectTypeDefinitionNode(name, interfaces.Items, directives.Items, fields.Items, type.SourceRange, description)
                    : node;
            }
            case ObjectTypeExtensionNode type:
            {
                var name = RewriteRequired(type.Name, context);
                var interfaces = RewriteMany(type.Interfaces, context);
                var directives = RewriteMany(type.Directives, context);
                var fields = RewriteMany(type.Fields, context);
                return Changed(type.Name, name) || interfaces.Changed || directives.Changed || fields.Changed
                    ? new ObjectTypeExtensionNode(name, interfaces.Items, directives.Items, fields.Items, type.SourceRange)
                    : node;
            }
            case InterfaceTypeDefinitionNode type:
            {
                var name = RewriteRequired(type.Name, context);
                var description = RewriteOptional(type.Description, context);
                var interfaces = RewriteMany(type.Interfaces, context);
                var directives = RewriteMany(type.Directives, context);
                var fields = RewriteMany(type.Fields, context);
                return Changed(type.Name, name) || Changed(type.Description, description) || interfaces.Changed || directives.Changed || fields.Changed
                    ? new InterfaceTypeDefinitionNode(name, interfaces.Items, directives.Items, fields.Items, type.SourceRange, description)
                    : node;
            }
            case InterfaceTypeExtensionNode type:
            {
                var name = RewriteRequired(type.Name, context);
                var interfaces = RewriteMany(type.Interfaces, context);
                var directives = RewriteMany(type.Directives, context);
                var fields = RewriteMany(type.Fields, context);
                return Changed(type.Name, name) || interfaces.Changed || directives.Changed || fields.Changed
                    ? new InterfaceTypeExtensionNode(name, interfaces.Items, directives.Items, fields.Items, type.SourceRange)
                    : node;
            }
            case UnionTypeDefinitionNode type:
            {
                var name = RewriteRequired(type.Name, context);
                var description = RewriteOptional(type.Description, context);
                var directives = RewriteMany(type.Directives, context);
                var types = RewriteMany(type.Types, context);
                return Changed(type.Name, name) || Changed(type.Description, description) || directives.Changed || types.Changed
                    ? new UnionTypeDefinitionNode(name, directives.Items, types.Items, type.SourceRange, description)
                    : node;
            }
            case UnionTypeExtensionNode type:
            {
                var name = RewriteRequired(type.Name, context);
                var directives = RewriteMany(type.Directives, context);
                var types = RewriteMany(type.Types, context);
                return Changed(type.Name, name) || directives.Changed || types.Changed
                    ? new UnionTypeExtensionNode(name, directives.Items, types.Items, type.SourceRange)
                    : node;
            }
            case EnumTypeDefinitionNode type:
            {
                var name = RewriteRequired(type.Name, context);
                var description = RewriteOptional(type.Description, context);
                var directives = RewriteMany(type.Directives, context);
                var values = RewriteMany(type.Values, context);
                return Changed(type.Name, name) || Changed(type.Description, description) || directives.Changed || values.Changed
                    ? new EnumTypeDefinitionNode(name, directives.Items, values.Items, type.SourceRange, description)
                    : node;
            }
            case EnumTypeExtensionNode type:
            {
                var name = RewriteRequired(type.Name, context);
                var directives = RewriteMany(type.Directives, context);
                var values = RewriteMany(type.Values, context);
                return Changed(type.Name, name) || directives.Changed || values.Changed
                    ? new EnumTypeExtensionNode(name, directives.Items, values.Items, type.SourceRange)
                    : node;
            }
            case InputObjectTypeDefinitionNode type:
            {
                var name = RewriteRequired(type.Name, context);
                var description = RewriteOptional(type.Description, context);
                var directives = RewriteMany(type.Directives, context);
                var fields = RewriteMany(type.Fields, context);
                return Changed(type.Name, name) || Changed(type.Description, description) || directives.Changed || fields.Changed
                    ? new InputObjectTypeDefinitionNode(name, directives.Items, fields.Items, type.SourceRange, description)
                    : node;
            }
            case InputObjectTypeExtensionNode type:
            {
                var name = RewriteRequired(type.Name, context);
                var directives = RewriteMany(type.Directives, context);
                var fields = RewriteMany(type.Fields, context);
                return Changed(type.Name, name) || directives.Changed || fields.Changed
                    ? new InputObjectTypeExtensionNode(name, directives.Items, fields.Items, type.SourceRange)
                    : node;
            }
            case FieldDefinitionNode field:
            {
                var description = RewriteOptional(field.Description, context);
                var name = RewriteRequired(field.Name, context);
                var arguments = RewriteMany(field.Arguments, context);
                var originalType = field.Type as TypeNode ?? throw new InvalidOperationException("Field definitions must use parser type nodes.");
                var type = RewriteRequired(originalType, context);
                var directives = RewriteMany(field.Directives, context);
                return Changed(field.Description, description) || Changed(field.Name, name) || arguments.Changed || Changed(field.Type, type) || directives.Changed
                    ? new FieldDefinitionNode(name, arguments.Items, type, directives.Items, field.SourceRange, description)
                    : node;
            }
            case InputValueDefinitionNode input:
            {
                var description = RewriteOptional(input.Description, context);
                var name = RewriteRequired(input.Name, context);
                var originalType = input.Type as TypeNode ?? throw new InvalidOperationException("Input-value definitions must use parser type nodes.");
                var type = RewriteRequired(originalType, context);
                var originalValue = input.DefaultValue as ValueNode;
                var value = RewriteOptional(originalValue, context);
                var directives = RewriteMany(input.Directives, context);
                return Changed(input.Description, description) || Changed(input.Name, name) || Changed(input.Type, type) || Changed(input.DefaultValue, value) || directives.Changed
                    ? new InputValueDefinitionNode(name, type, value, directives.Items, input.SourceRange, description)
                    : node;
            }
            case EnumValueDefinitionNode value:
            {
                var description = RewriteOptional(value.Description, context);
                var name = RewriteRequired(value.Name, context);
                var directives = RewriteMany(value.Directives, context);
                return Changed(value.Description, description) || Changed(value.Name, name) || directives.Changed
                    ? new EnumValueDefinitionNode(name, directives.Items, value.SourceRange, description)
                    : node;
            }
            case DirectiveDefinitionNode directive:
            {
                var description = RewriteOptional(directive.Description, context);
                var name = RewriteRequired(directive.Name, context);
                var arguments = RewriteMany(directive.Arguments, context);
                var locations = RewriteMany(directive.Locations, context);
                return Changed(directive.Description, description) || Changed(directive.Name, name) || arguments.Changed || locations.Changed
                    ? new DirectiveDefinitionNode(name, arguments.Items, directive.Repeatable, locations.Items, directive.SourceRange, description)
                    : node;
            }
            case DirectiveExtensionNode directiveExtension:
            {
                var name = RewriteRequired(directiveExtension.Name, context);
                var directives = RewriteMany(directiveExtension.Directives, context);
                return Changed(directiveExtension.Name, name) || directives.Changed
                    ? new DirectiveExtensionNode(name, directives.Items, directiveExtension.SourceRange)
                    : node;
            }
            case TypeCoordinateNode coordinate:
            {
                var name = RewriteRequired(coordinate.Name, context);
                return Changed(coordinate.Name, name) ? new TypeCoordinateNode(name, coordinate.SourceRange) : node;
            }
            case MemberCoordinateNode coordinate:
            {
                var name = RewriteRequired(coordinate.Name, context);
                var member = RewriteRequired(coordinate.MemberName, context);
                return Changed(coordinate.Name, name) || Changed(coordinate.MemberName, member)
                    ? new MemberCoordinateNode(name, member, coordinate.SourceRange)
                    : node;
            }
            case ArgumentCoordinateNode coordinate:
            {
                var name = RewriteRequired(coordinate.Name, context);
                var field = RewriteRequired(coordinate.FieldName, context);
                var argument = RewriteRequired(coordinate.ArgumentName, context);
                return Changed(coordinate.Name, name) || Changed(coordinate.FieldName, field) || Changed(coordinate.ArgumentName, argument)
                    ? new ArgumentCoordinateNode(name, field, argument, coordinate.SourceRange)
                    : node;
            }
            case DirectiveCoordinateNode coordinate:
            {
                var name = RewriteRequired(coordinate.Name, context);
                return Changed(coordinate.Name, name) ? new DirectiveCoordinateNode(name, coordinate.SourceRange) : node;
            }
            case DirectiveArgumentCoordinateNode coordinate:
            {
                var name = RewriteRequired(coordinate.Name, context);
                var argument = RewriteRequired(coordinate.ArgumentName, context);
                return Changed(coordinate.Name, name) || Changed(coordinate.ArgumentName, argument)
                    ? new DirectiveArgumentCoordinateNode(name, argument, coordinate.SourceRange)
                    : node;
            }
            default:
                return node;
        }
    }

    private TNode RewriteRequired<TNode>(TNode node, TContext context) where TNode : AstNode =>
        (TNode)Rewrite(node, context);

    private TNode? RewriteOptional<TNode>(TNode? node, TContext context) where TNode : AstNode =>
        node is null ? null : (TNode)Rewrite(node, context);

    private RewriteList<TNode> RewriteMany<TNode>(IReadOnlyList<TNode> nodes, TContext context) where TNode : AstNode
    {
        TNode[]? rewritten = null;
        for (var index = 0; index < nodes.Count; index++)
        {
            var item = (TNode)Rewrite(nodes[index], context);
            if (!ReferenceEquals(item, nodes[index]))
            {
                rewritten ??= nodes.ToArray();
                rewritten[index] = item;
            }
            else if (rewritten is not null) rewritten[index] = item;
        }
        return rewritten is null ? new RewriteList<TNode>(nodes, false) : new RewriteList<TNode>(rewritten, true);
    }

    private static bool Changed<TNode>(TNode? original, TNode? rewritten) where TNode : class => !ReferenceEquals(original, rewritten);

    private readonly record struct RewriteList<TNode>(IReadOnlyList<TNode> Items, bool Changed);
}

/// <summary>Factories for syntax rewriters.</summary>
public static class SyntaxRewriter
{
    /// <summary>Creates a syntax rewriter.</summary>
    public static ISyntaxRewriter<object> Create(Func<ISyntaxNode, ISyntaxNode> rewrite) =>
        new SyntaxRewriter<object>((node, _) => rewrite(node));

    /// <summary>Creates a member value.</summary>
    public static ISyntaxRewriter<TContext> Create<TContext>(
        RewriteSyntaxNode<TContext>? rewrite = null,
        Func<ISyntaxNode, TContext, TContext>? enter = null,
        Action<ISyntaxNode, TContext>? leave = null) => new SyntaxRewriter<TContext>(rewrite, enter, leave);

    /// <summary>Creates a syntax rewriter that tracks ancestry in navigator contexts.</summary>
    public static ISyntaxRewriter<NavigatorContext> CreateWithNavigator(
        RewriteSyntaxNode<NavigatorContext>? rewrite = null,
        Func<ISyntaxNode, NavigatorContext, NavigatorContext>? enter = null,
        Action<ISyntaxNode, NavigatorContext>? leave = null) =>
        new SyntaxRewriter<NavigatorContext>(rewrite, enter, leave, context => context.Navigator);

    /// <summary>Creates a context-aware syntax rewriter that tracks ancestry in navigator contexts.</summary>
    public static ISyntaxRewriter<TContext> CreateWithNavigator<TContext>(
        RewriteSyntaxNode<TContext>? rewrite = null,
        Func<ISyntaxNode, TContext, TContext>? enter = null,
        Action<ISyntaxNode, TContext>? leave = null) =>
        new SyntaxRewriter<TContext>(rewrite, enter, leave,
            context => context is INavigatorContext navigatorContext ? navigatorContext.Navigator : null);
}

/// <summary>Convenience extensions for syntax rewriting.</summary>
public static class SyntaxRewriterExtensions
{
    /// <summary>Rewrites a syntax node or tree.</summary>
    public static ISyntaxNode Rewrite(this ISyntaxRewriter<object> rewriter, ISyntaxNode node)
    {
        ArgumentNullException.ThrowIfNull(rewriter);
        ArgumentNullException.ThrowIfNull(node);
        return rewriter.Rewrite(node, new object());
    }

    /// <summary>Creates a member value.</summary>
    public static T Rewrite<T>(this ISyntaxRewriter<object> rewriter, T node) where T : class, ISyntaxNode
    {
        var rewritten = rewriter.Rewrite((ISyntaxNode)node);
        return rewritten is T typed ? typed : throw new InvalidCastException($"The rewrite returned '{rewritten.GetType().Name}', not '{typeof(T).Name}'.");
    }
}
