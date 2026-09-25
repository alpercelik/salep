namespace Salep.Parser;

public sealed partial class DirectiveExtensionNode
{
    /// <summary>Returns an immutable copy with the specified directives.</summary>
    public DirectiveExtensionNode WithDirectives(IReadOnlyList<DirectiveNode> directives) => new(Name, directives, SourceRange);
    /// <summary>Returns an immutable copy with the specified location.</summary>
    public DirectiveExtensionNode WithLocation(Location location) => new(Name, Directives, (SourceLocation)(location ?? throw new ArgumentNullException(nameof(location))));
    /// <summary>Returns an immutable copy with the specified name.</summary>
    public DirectiveExtensionNode WithName(NameNode name) => new(name, Directives, SourceRange);
}

public sealed partial class DocumentNode
{
    /// <summary>Creates a document from definitions and derives its source range.</summary>
    public DocumentNode(IReadOnlyList<IDefinitionNode> definitions)
        : this(CreateSource(definitions, out var location), CastDefinitions(definitions), location, null)
    {
    }

    /// <summary>Creates a document with explicit location metadata.</summary>
    public DocumentNode(Location location, IReadOnlyList<IDefinitionNode> definitions)
        : this(CreateSource(definitions, out _), CastDefinitions(definitions), (SourceLocation)(location ?? throw new ArgumentNullException(nameof(location))), null)
    {
    }

    /// <summary>Gets the number of document definitions.</summary>
    public int Count => Definitions.Count;

    /// <summary>Gets a valid empty syntax document.</summary>
    public static DocumentNode Empty { get; } = new(new SourceText(ReadOnlyMemory<char>.Empty), [], new SourceLocation(0, 0, false), null, allowEmpty: true);

    /// <summary>Gets the number of executable field selections in the document.</summary>
    public int FieldsCount
    {
        get
        {
            var count = 0;
            GraphQLAstVisitor.Visit(this, node =>
            {
                if (node.AstKind == AstNodeKind.Field) count++;
                return GraphQLVisitControl.Continue;
            });
            return count;
        }
    }

    /// <summary>Returns a copy with different definitions.</summary>
    public DocumentNode WithDefinitions(IReadOnlyList<IDefinitionNode> definitions) =>
        new(Source, CastDefinitions(definitions), Location, SourceInfo);

    /// <summary>Returns a copy with a different source location.</summary>
    public DocumentNode WithLocation(Location location) =>
        new(Source, CastDefinitions(Definitions.ToArray()), (SourceLocation)(location ?? throw new ArgumentNullException(nameof(location))), SourceInfo);

    private static DefinitionNode[] CastDefinitions(IReadOnlyList<IDefinitionNode> definitions)
    {
        ArgumentNullException.ThrowIfNull(definitions);
        var result = new DefinitionNode[definitions.Count];
        for (var index = 0; index < result.Length; index++)
            result[index] = definitions[index] as DefinitionNode ?? throw new ArgumentException("Every definition must be a parser definition node.", nameof(definitions));
        return result;
    }

    private static SourceText CreateSource(IReadOnlyList<IDefinitionNode> definitions, out SourceLocation location)
    {
        ArgumentNullException.ThrowIfNull(definitions);
        var start = 0;
        var end = 0;
        if (definitions.Count > 0)
        {
            start = definitions.Min(item => item.Location.Start);
            end = definitions.Max(item => item.Location.End);
        }
        location = new SourceLocation(start, end, start == 0);
        return new SourceText(new string(' ', end).AsMemory());
    }
}

public sealed partial class OperationDefinitionNode
{
    /// <summary>Returns a copy with a different description.</summary>
    public OperationDefinitionNode WithDescription(StringValueNode? description) => new(Operation, Name, VariableDefinitions, Directives, SelectionSet, Location, description);
    /// <summary>Returns a copy with different directives.</summary>
    public OperationDefinitionNode WithDirectives(IReadOnlyList<DirectiveNode> directives) => new(Operation, Name, VariableDefinitions, directives, SelectionSet, Location, Description);
    /// <summary>Returns a copy with a different source location.</summary>
    public OperationDefinitionNode WithLocation(Location location) => new(Operation, Name, VariableDefinitions, Directives, SelectionSet, (SourceLocation)(location ?? throw new ArgumentNullException(nameof(location))), Description);
    /// <summary>Returns a copy with a different optional name.</summary>
    public OperationDefinitionNode WithName(NameNode? name) => new(Operation, name, VariableDefinitions, Directives, SelectionSet, Location, Description);
    /// <summary>Returns a copy with a different operation kind.</summary>
    public OperationDefinitionNode WithOperation(OperationType operation) => new(operation, Name, VariableDefinitions, Directives, SelectionSet, Location, Description);
    /// <summary>Returns a copy with a different selection set.</summary>
    public OperationDefinitionNode WithSelectionSet(SelectionSetNode selectionSet) => new(Operation, Name, VariableDefinitions, Directives, selectionSet, Location, Description);
    /// <summary>Returns a copy with different variable definitions.</summary>
    public OperationDefinitionNode WithVariableDefinitions(IReadOnlyList<VariableDefinitionNode> variableDefinitions) => new(Operation, Name, variableDefinitions, Directives, SelectionSet, Location, Description);
}

public sealed partial class SelectionSetNode
{
    /// <summary>Returns a copy with different selections.</summary>
    public SelectionSetNode WithSelections(IReadOnlyList<ISelectionNode> selections)
    {
        ArgumentNullException.ThrowIfNull(selections);
        return new SelectionSetNode(selections.Select(selection => selection as SelectionNode
            ?? throw new ArgumentException("Every selection must be a parser selection node.", nameof(selections))), Location);
    }

    /// <summary>Returns a copy with a different source location.</summary>
    public SelectionSetNode WithLocation(Location location) => new(CastSelections(Selections.ToArray()), (SourceLocation)(location ?? throw new ArgumentNullException(nameof(location))));
}

public sealed partial class FieldNode
{
    /// <summary>Returns a copy with a different optional alias.</summary>
    public FieldNode WithAlias(NameNode? alias) => new(Name, alias, Arguments, Directives, SelectionSet, Location);
    /// <summary>Returns a copy with different arguments.</summary>
    public FieldNode WithArguments(IReadOnlyList<ArgumentNode> arguments) => new(Name, Alias, arguments, Directives, SelectionSet, Location);
    /// <summary>Returns a copy with different directives.</summary>
    public FieldNode WithDirectives(IReadOnlyList<DirectiveNode> directives) => new(Name, Alias, Arguments, directives, SelectionSet, Location);
    /// <summary>Returns a copy with a different source location.</summary>
    public FieldNode WithLocation(Location location) => new(Name, Alias, Arguments, Directives, SelectionSet, (SourceLocation)(location ?? throw new ArgumentNullException(nameof(location))));
    /// <summary>Returns a copy with a different name.</summary>
    public FieldNode WithName(NameNode name) => new(name, Alias, Arguments, Directives, SelectionSet, Location);
    /// <summary>Returns a copy with a different optional selection set.</summary>
    public FieldNode WithSelectionSet(SelectionSetNode? selectionSet) => new(Name, Alias, Arguments, Directives, selectionSet, Location);
}

public sealed partial class ArgumentNode
{
    /// <summary>Creates an argument at a public source location.</summary>
    public ArgumentNode(Location location, NameNode name, IValueNode value)
        : this(name, AsValueNode(value), (SourceLocation)(location ?? throw new ArgumentNullException(nameof(location)))) { }
    /// <summary>Creates an argument without explicit source metadata.</summary>
    public ArgumentNode(NameNode name, IValueNode value) : this(name, AsValueNode(value), default) { }
    /// <summary>Creates a boolean-valued argument from its name.</summary>
    public ArgumentNode(string name, bool value) : this(new NameNode(name), new BooleanValueNode(value), default) { }
    /// <summary>Creates an argument from a name and value node.</summary>
    public ArgumentNode(string name, IValueNode value) : this(new NameNode(name), AsValueNode(value), default) { }
    /// <summary>Creates an integer-valued argument from its name.</summary>
    public ArgumentNode(string name, int value) : this(new NameNode(name), new IntValueNode(value), default) { }
    /// <summary>Creates a string-valued argument from its name.</summary>
    public ArgumentNode(string name, string value) : this(new NameNode(name), new StringValueNode(value), default) { }

    /// <summary>Returns a copy with a different source location.</summary>
    public ArgumentNode WithLocation(Location location) => new(Name, AsValueNode(Value), (SourceLocation)(location ?? throw new ArgumentNullException(nameof(location))));
    /// <summary>Returns a copy with a different name.</summary>
    public ArgumentNode WithName(NameNode name) => new(name, AsValueNode(Value), Location);
    /// <summary>Returns a copy with a different value.</summary>
    public ArgumentNode WithValue(IValueNode value) => new(Name, AsValueNode(value), Location);

    private static ValueNode AsValueNode(IValueNode value) => value as ValueNode ?? throw new ArgumentException("Values must be parser value nodes.", nameof(value));
}

public sealed partial class DirectiveNode
{
    /// <summary>Creates a directive at a public source location.</summary>
    public DirectiveNode(Location location, NameNode name, IReadOnlyList<ArgumentNode> arguments)
        : this(name, arguments, (SourceLocation)(location ?? throw new ArgumentNullException(nameof(location)))) { }
    /// <summary>Creates a directive without explicit source metadata.</summary>
    public DirectiveNode(NameNode name, IReadOnlyList<ArgumentNode> arguments) : this(name, arguments, default) { }
    /// <summary>Creates a directive from a name and an argument array.</summary>
    public DirectiveNode(string name, ArgumentNode[] arguments) : this(new NameNode(name), arguments, default) { }
    /// <summary>Creates a directive from a name and arguments.</summary>
    public DirectiveNode(string name, IReadOnlyList<ArgumentNode> arguments) : this(new NameNode(name), arguments, default) { }

    /// <summary>Returns a copy with different arguments.</summary>
    public DirectiveNode WithArguments(IReadOnlyList<ArgumentNode> arguments) => new(Name, arguments, Location);
    /// <summary>Returns a copy with a different source location.</summary>
    public DirectiveNode WithLocation(Location location) => new(Name, Arguments, (SourceLocation)(location ?? throw new ArgumentNullException(nameof(location))));
    /// <summary>Returns a copy with a different name.</summary>
    public DirectiveNode WithName(NameNode name) => new(name, Arguments, Location);
}

public sealed partial class VariableDefinitionNode
{
    /// <summary>Returns a copy with a different default value.</summary>
    public VariableDefinitionNode WithDefaultValue(IValueNode? defaultValue) =>
        new(Variable, AsTypeNode(Type), defaultValue is null ? null : AsValueNode(defaultValue), Directives, Location, Description);
    /// <summary>Returns a copy with different directives.</summary>
    public VariableDefinitionNode WithDirectives(IReadOnlyList<DirectiveNode> directives) => new(Variable, AsTypeNode(Type), DefaultValue is null ? null : AsValueNode(DefaultValue), directives, Location, Description);
    /// <summary>Returns a copy with a different source location.</summary>
    public VariableDefinitionNode WithLocation(Location location) => new(Variable, AsTypeNode(Type), DefaultValue is null ? null : AsValueNode(DefaultValue), Directives, (SourceLocation)(location ?? throw new ArgumentNullException(nameof(location))), Description);
    /// <summary>Returns a copy with a different type.</summary>
    public VariableDefinitionNode WithType(ITypeNode type) => new(Variable, AsTypeNode(type), DefaultValue is null ? null : AsValueNode(DefaultValue), Directives, Location, Description);
    /// <summary>Returns a copy with a different variable.</summary>
    public VariableDefinitionNode WithVariable(VariableNode variable) => new(variable, AsTypeNode(Type), DefaultValue is null ? null : AsValueNode(DefaultValue), Directives, Location, Description);

    private static ValueNode AsValueNode(IValueNode value) => value as ValueNode ?? throw new ArgumentException("Values must be parser value nodes.", nameof(value));
    private static TypeNode AsTypeNode(ITypeNode type) => type as TypeNode ?? throw new ArgumentException("Types must be parser type nodes.", nameof(type));
}

public sealed partial class FragmentDefinitionNode
{
    /// <summary>Returns a copy with a different optional description.</summary>
    public FragmentDefinitionNode WithDescription(StringValueNode? description) => new(Name, TypeCondition, Directives, SelectionSet, Location, description, VariableDefinitions);
    /// <summary>Returns a copy with different directives.</summary>
    public FragmentDefinitionNode WithDirectives(IReadOnlyList<DirectiveNode> directives) => new(Name, TypeCondition, directives, SelectionSet, Location, Description, VariableDefinitions);
    /// <summary>Returns a copy with a different source location.</summary>
    public FragmentDefinitionNode WithLocation(Location location) => new(Name, TypeCondition, Directives, SelectionSet, (SourceLocation)(location ?? throw new ArgumentNullException(nameof(location))), Description, VariableDefinitions);
    /// <summary>Returns a copy with a different name.</summary>
    public FragmentDefinitionNode WithName(NameNode name) => new(name, TypeCondition, Directives, SelectionSet, Location, Description, VariableDefinitions);
    /// <summary>Returns a copy with a different selection set.</summary>
    public FragmentDefinitionNode WithSelectionSet(SelectionSetNode selectionSet) => new(Name, TypeCondition, Directives, selectionSet, Location, Description, VariableDefinitions);
    /// <summary>Returns a copy with a different type condition.</summary>
    public FragmentDefinitionNode WithTypeCondition(NamedTypeNode typeCondition) => new(Name, typeCondition, Directives, SelectionSet, Location, Description, VariableDefinitions);
    /// <summary>Returns a copy with different variable definitions.</summary>
    public FragmentDefinitionNode WithVariableDefinitions(IReadOnlyList<VariableDefinitionNode> variableDefinitions) => new(Name, TypeCondition, Directives, SelectionSet, Location, Description, variableDefinitions);
}

public sealed partial class FieldNode
{
    /// <summary>Creates a field selection at a public source location.</summary>
    public FieldNode(Location? location, NameNode name, NameNode? alias, IReadOnlyList<DirectiveNode> directives, IReadOnlyList<ArgumentNode> arguments, SelectionSetNode? selectionSet)
        : this(name, alias, arguments, directives, selectionSet, location is null ? default : (SourceLocation)location) { }
    /// <summary>Creates a field selection without explicit source metadata.</summary>
    public FieldNode(NameNode name, NameNode? alias, IReadOnlyList<DirectiveNode> directives, IReadOnlyList<ArgumentNode> arguments, SelectionSetNode? selectionSet)
        : this(name, alias, arguments, directives, selectionSet, default) { }
    /// <summary>Creates a named field selection with an optional nested selection set.</summary>
    public FieldNode(string name, SelectionSetNode? selectionSet) : this(new NameNode(name), null, [], [], selectionSet, default) { }
    /// <summary>Creates a named field selection.</summary>
    public FieldNode(string name) : this(name, null) { }
}

public sealed partial class NamedTypeNode
{
    /// <summary>Creates a named type without explicit source metadata.</summary>
    public NamedTypeNode(NameNode name) : this(name, default) { }
    /// <summary>Creates a named type at a public source location.</summary>
    public NamedTypeNode(Location location, NameNode name) : this(name, (SourceLocation)(location ?? throw new ArgumentNullException(nameof(location)))) { }
    /// <summary>Creates a named type from its name.</summary>
    public NamedTypeNode(string name) : this(new NameNode(name), default) { }
}

public sealed partial class ListTypeNode
{
    /// <summary>Creates a list type without explicit source metadata.</summary>
    public ListTypeNode(ITypeNode type) : this(type as TypeNode ?? throw new ArgumentException("Types must be parser type nodes.", nameof(type)), default) { }
    /// <summary>Creates a list type at a public source location.</summary>
    public ListTypeNode(Location location, ITypeNode type) : this(type as TypeNode ?? throw new ArgumentException("Types must be parser type nodes.", nameof(type)), (SourceLocation)(location ?? throw new ArgumentNullException(nameof(location)))) { }
}

public sealed partial class NonNullTypeNode
{
    /// <summary>Creates a non-null type without explicit source metadata.</summary>
    public NonNullTypeNode(INullableTypeNode type) : this(type as TypeNode ?? throw new ArgumentException("Types must be parser type nodes.", nameof(type)), default) { }
    /// <summary>Creates a non-null type at a public source location.</summary>
    public NonNullTypeNode(Location location, INullableTypeNode type) : this(type as TypeNode ?? throw new ArgumentException("Types must be parser type nodes.", nameof(type)), (SourceLocation)(location ?? throw new ArgumentNullException(nameof(location)))) { }
}

public sealed partial class SelectionSetNode
{
    /// <summary>Creates a selection set from the public selection interface.</summary>
    public SelectionSetNode(IReadOnlyList<ISelectionNode> selections) : this(CastSelections(selections), default) { }
    /// <summary>Creates a selection set at a public source location.</summary>
    public SelectionSetNode(Location location, IReadOnlyList<ISelectionNode> selections) : this(CastSelections(selections), (SourceLocation)(location ?? throw new ArgumentNullException(nameof(location)))) { }

    private static IEnumerable<SelectionNode> CastSelections(IReadOnlyList<ISelectionNode> selections)
    {
        ArgumentNullException.ThrowIfNull(selections);
        return selections.Select(selection => selection as SelectionNode ?? throw new ArgumentException("Selections must be parser selection nodes.", nameof(selections)));
    }
}

public sealed partial class ScalarTypeDefinitionNode
{
    /// <summary>Creates a scalar definition without explicit source metadata.</summary>
    public ScalarTypeDefinitionNode(NameNode name, StringValueNode? description, IReadOnlyList<DirectiveNode> directives)
        : this(name, directives, default, description) { }
    /// <summary>Creates a scalar definition at a public source location.</summary>
    public ScalarTypeDefinitionNode(Location location, NameNode name, StringValueNode? description, IReadOnlyList<DirectiveNode> directives)
        : this(name, directives, (SourceLocation)(location ?? throw new ArgumentNullException(nameof(location))), description) { }
}

public sealed partial class FragmentSpreadNode
{
    /// <summary>Returns a copy with different arguments.</summary>
    public FragmentSpreadNode WithArguments(IReadOnlyList<ArgumentNode> arguments) => new(Name, arguments, Directives, Location);
    /// <summary>Returns a copy with different directives.</summary>
    public FragmentSpreadNode WithDirectives(IReadOnlyList<DirectiveNode> directives) => new(Name, Arguments, directives, Location);
    /// <summary>Returns a copy with a different source location.</summary>
    public FragmentSpreadNode WithLocation(Location location) => new(Name, Arguments, Directives, (SourceLocation)(location ?? throw new ArgumentNullException(nameof(location))));
    /// <summary>Returns a copy with a different name.</summary>
    public FragmentSpreadNode WithName(NameNode name) => new(name, Arguments, Directives, Location);
}

public sealed partial class InlineFragmentNode
{
    /// <summary>Returns a copy with different directives.</summary>
    public InlineFragmentNode WithDirectives(IReadOnlyList<DirectiveNode> directives) => new(TypeCondition, directives, SelectionSet, Location);
    /// <summary>Returns a copy with a different source location.</summary>
    public InlineFragmentNode WithLocation(Location location) => new(TypeCondition, Directives, SelectionSet, (SourceLocation)(location ?? throw new ArgumentNullException(nameof(location))));
    /// <summary>Returns a copy with a different selection set.</summary>
    public InlineFragmentNode WithSelectionSet(SelectionSetNode selectionSet) => new(TypeCondition, Directives, selectionSet, Location);
    /// <summary>Returns a copy with a different optional type condition.</summary>
    public InlineFragmentNode WithTypeCondition(NamedTypeNode? typeCondition) => new(typeCondition, Directives, SelectionSet, Location);
}

public sealed partial class NamedTypeNode
{
    /// <summary>Returns a copy with a different source location.</summary>
    public NamedTypeNode WithLocation(Location location) => new(Name, (SourceLocation)(location ?? throw new ArgumentNullException(nameof(location))));
    /// <summary>Returns a copy with a different name.</summary>
    public NamedTypeNode WithName(NameNode name) => new(name, Location);
}

public sealed partial class ListTypeNode
{
    /// <summary>Returns a copy with a different source location.</summary>
    public ListTypeNode WithLocation(Location location) => new((TypeNode)Type, (SourceLocation)(location ?? throw new ArgumentNullException(nameof(location))));
    /// <summary>Returns a copy with a different item type.</summary>
    public ListTypeNode WithType(ITypeNode type) => new(type as TypeNode ?? throw new ArgumentException("Types must be parser type nodes.", nameof(type)), Location);
}

public sealed partial class NonNullTypeNode
{
    /// <summary>Returns a copy with a different source location.</summary>
    public NonNullTypeNode WithLocation(Location location) => new((TypeNode)Type, (SourceLocation)(location ?? throw new ArgumentNullException(nameof(location))));
    /// <summary>Returns a copy with a different wrapped nullable type.</summary>
    public NonNullTypeNode WithType(INullableTypeNode type) =>
        new(type as TypeNode ?? throw new ArgumentException("Types must be parser type nodes.", nameof(type)), Location);
}

public sealed partial class SchemaDefinitionNode
{
    /// <summary>Returns a copy with a different optional description.</summary>
    public SchemaDefinitionNode WithDescription(StringValueNode? description) => new(OperationTypes, Directives, Location, description);
    /// <summary>Returns a copy with different directives.</summary>
    public SchemaDefinitionNode WithDirectives(IReadOnlyList<DirectiveNode> directives) => new(OperationTypes, directives, Location, Description);
    /// <summary>Returns a copy with a different source location.</summary>
    public SchemaDefinitionNode WithLocation(Location location) => new(OperationTypes, Directives, (SourceLocation)(location ?? throw new ArgumentNullException(nameof(location))), Description);
    /// <summary>Returns a copy with different root operation mappings.</summary>
    public SchemaDefinitionNode WithOperationTypes(IReadOnlyList<OperationTypeDefinitionNode> operationTypes) => new(operationTypes, Directives, Location, Description);
}

public sealed partial class SchemaExtensionNode
{
    /// <summary>Returns a copy with different directives.</summary>
    public SchemaExtensionNode WithDirectives(IReadOnlyList<DirectiveNode> directives) => new(OperationTypes, directives, Location);
    /// <summary>Returns a copy with a different source location.</summary>
    public SchemaExtensionNode WithLocation(Location location) => new(OperationTypes, Directives, (SourceLocation)(location ?? throw new ArgumentNullException(nameof(location))));
    /// <summary>Returns a copy with different root operation mappings.</summary>
    public SchemaExtensionNode WithOperationTypes(IReadOnlyList<OperationTypeDefinitionNode> operationTypes) => new(operationTypes, Directives, Location);
}

public sealed partial class OperationTypeDefinitionNode
{
    /// <summary>Returns a copy with a different source location.</summary>
    public OperationTypeDefinitionNode WithLocation(Location location) => new(Operation, Type, (SourceLocation)(location ?? throw new ArgumentNullException(nameof(location))));
    /// <summary>Returns a copy with a different operation kind.</summary>
    public OperationTypeDefinitionNode WithOperation(OperationType operation) => new(operation, Type, Location);
    /// <summary>Returns a copy with a different root type.</summary>
    public OperationTypeDefinitionNode WithType(NamedTypeNode type) => new(Operation, type, Location);
}

public sealed partial class ScalarTypeDefinitionNode
{
    /// <summary>Returns a copy with a different optional description.</summary>
    public ScalarTypeDefinitionNode WithDescription(StringValueNode? description) => new(Name, Directives, Location, description);
    /// <summary>Returns a copy with different directives.</summary>
    public ScalarTypeDefinitionNode WithDirectives(IReadOnlyList<DirectiveNode> directives) => new(Name, directives, Location, Description);
    /// <summary>Returns a copy with a different source location.</summary>
    public ScalarTypeDefinitionNode WithLocation(Location location) => new(Name, Directives, (SourceLocation)(location ?? throw new ArgumentNullException(nameof(location))), Description);
    /// <summary>Returns a copy with a different name.</summary>
    public ScalarTypeDefinitionNode WithName(NameNode name) => new(name, Directives, Location, Description);
}

public sealed partial class ScalarTypeExtensionNode
{
    /// <summary>Returns a copy with different directives.</summary>
    public ScalarTypeExtensionNode WithDirectives(IReadOnlyList<DirectiveNode> directives) => new(Name, directives, Location);
    /// <summary>Returns a copy with a different source location.</summary>
    public ScalarTypeExtensionNode WithLocation(Location location) => new(Name, Directives, (SourceLocation)(location ?? throw new ArgumentNullException(nameof(location))));
    /// <summary>Returns a copy with a different name.</summary>
    public ScalarTypeExtensionNode WithName(NameNode name) => new(name, Directives, Location);
}

public sealed partial class ObjectTypeDefinitionNode
{
    /// <summary>Returns a copy with a different optional description.</summary>
    public ObjectTypeDefinitionNode WithDescription(StringValueNode? description) => new(Name, Interfaces, Directives, Fields, Location, description);
    /// <summary>Returns a copy with different directives.</summary>
    public ObjectTypeDefinitionNode WithDirectives(IReadOnlyList<DirectiveNode> directives) => new(Name, Interfaces, directives, Fields, Location, Description);
    /// <summary>Returns a copy with different field definitions.</summary>
    public ObjectTypeDefinitionNode WithFields(IReadOnlyList<FieldDefinitionNode> fields) => new(Name, Interfaces, Directives, fields, Location, Description);
    /// <summary>Returns a copy with different implemented interfaces.</summary>
    public ObjectTypeDefinitionNode WithInterfaces(IReadOnlyList<NamedTypeNode> interfaces) => new(Name, interfaces, Directives, Fields, Location, Description);
    /// <summary>Returns a copy with a different source location.</summary>
    public ObjectTypeDefinitionNode WithLocation(Location location) => new(Name, Interfaces, Directives, Fields, (SourceLocation)(location ?? throw new ArgumentNullException(nameof(location))), Description);
    /// <summary>Returns a copy with a different name.</summary>
    public ObjectTypeDefinitionNode WithName(NameNode name) => new(name, Interfaces, Directives, Fields, Location, Description);
}

public sealed partial class ObjectTypeExtensionNode
{
    /// <summary>Returns a copy with different directives.</summary>
    public ObjectTypeExtensionNode WithDirectives(IReadOnlyList<DirectiveNode> directives) => new(Name, Interfaces, directives, Fields, Location);
    /// <summary>Returns a copy with different field definitions.</summary>
    public ObjectTypeExtensionNode WithFields(IReadOnlyList<FieldDefinitionNode> fields) => new(Name, Interfaces, Directives, fields, Location);
    /// <summary>Returns a copy with different implemented interfaces.</summary>
    public ObjectTypeExtensionNode WithInterfaces(IReadOnlyList<NamedTypeNode> interfaces) => new(Name, interfaces, Directives, Fields, Location);
    /// <summary>Returns a copy with a different source location.</summary>
    public ObjectTypeExtensionNode WithLocation(Location location) => new(Name, Interfaces, Directives, Fields, (SourceLocation)(location ?? throw new ArgumentNullException(nameof(location))));
    /// <summary>Returns a copy with a different name.</summary>
    public ObjectTypeExtensionNode WithName(NameNode name) => new(name, Interfaces, Directives, Fields, Location);
}

public sealed partial class InterfaceTypeDefinitionNode
{
    /// <summary>Returns a copy with a different optional description.</summary>
    public InterfaceTypeDefinitionNode WithDescription(StringValueNode? description) => new(Name, Interfaces, Directives, Fields, Location, description);
    /// <summary>Returns a copy with different directives.</summary>
    public InterfaceTypeDefinitionNode WithDirectives(IReadOnlyList<DirectiveNode> directives) => new(Name, Interfaces, directives, Fields, Location, Description);
    /// <summary>Returns a copy with different field definitions.</summary>
    public InterfaceTypeDefinitionNode WithFields(IReadOnlyList<FieldDefinitionNode> fields) => new(Name, Interfaces, Directives, fields, Location, Description);
    /// <summary>Returns a copy with different implemented interfaces.</summary>
    public InterfaceTypeDefinitionNode WithInterfaces(IReadOnlyList<NamedTypeNode> interfaces) => new(Name, interfaces, Directives, Fields, Location, Description);
    /// <summary>Returns a copy with a different source location.</summary>
    public InterfaceTypeDefinitionNode WithLocation(Location location) => new(Name, Interfaces, Directives, Fields, (SourceLocation)(location ?? throw new ArgumentNullException(nameof(location))), Description);
    /// <summary>Returns a copy with a different name.</summary>
    public InterfaceTypeDefinitionNode WithName(NameNode name) => new(name, Interfaces, Directives, Fields, Location, Description);
}

public sealed partial class InterfaceTypeExtensionNode
{
    /// <summary>Returns a copy with different directives.</summary>
    public InterfaceTypeExtensionNode WithDirectives(IReadOnlyList<DirectiveNode> directives) => new(Name, Interfaces, directives, Fields, Location);
    /// <summary>Returns a copy with different field definitions.</summary>
    public InterfaceTypeExtensionNode WithFields(IReadOnlyList<FieldDefinitionNode> fields) => new(Name, Interfaces, Directives, fields, Location);
    /// <summary>Returns a copy with different implemented interfaces.</summary>
    public InterfaceTypeExtensionNode WithInterfaces(IReadOnlyList<NamedTypeNode> interfaces) => new(Name, interfaces, Directives, Fields, Location);
    /// <summary>Returns a copy with a different source location.</summary>
    public InterfaceTypeExtensionNode WithLocation(Location location) => new(Name, Interfaces, Directives, Fields, (SourceLocation)(location ?? throw new ArgumentNullException(nameof(location))));
    /// <summary>Returns a copy with a different name.</summary>
    public InterfaceTypeExtensionNode WithName(NameNode name) => new(name, Interfaces, Directives, Fields, Location);
}

public sealed partial class UnionTypeDefinitionNode
{
    /// <summary>Returns a copy with a different optional description.</summary>
    public UnionTypeDefinitionNode WithDescription(StringValueNode? description) => new(Name, Directives, Types, Location, description);
    /// <summary>Returns a copy with different directives.</summary>
    public UnionTypeDefinitionNode WithDirectives(IReadOnlyList<DirectiveNode> directives) => new(Name, directives, Types, Location, Description);
    /// <summary>Returns a copy with a different source location.</summary>
    public UnionTypeDefinitionNode WithLocation(Location location) => new(Name, Directives, Types, (SourceLocation)(location ?? throw new ArgumentNullException(nameof(location))), Description);
    /// <summary>Returns a copy with a different name.</summary>
    public UnionTypeDefinitionNode WithName(NameNode name) => new(name, Directives, Types, Location, Description);
    /// <summary>Returns a copy with different member types.</summary>
    public UnionTypeDefinitionNode WithTypes(IReadOnlyList<NamedTypeNode> types) => new(Name, Directives, types, Location, Description);
}

public sealed partial class UnionTypeExtensionNode
{
    /// <summary>Returns a copy with different directives.</summary>
    public UnionTypeExtensionNode WithDirectives(IReadOnlyList<DirectiveNode> directives) => new(Name, directives, Types, Location);
    /// <summary>Returns a copy with a different source location.</summary>
    public UnionTypeExtensionNode WithLocation(Location location) => new(Name, Directives, Types, (SourceLocation)(location ?? throw new ArgumentNullException(nameof(location))));
    /// <summary>Returns a copy with a different name.</summary>
    public UnionTypeExtensionNode WithName(NameNode name) => new(name, Directives, Types, Location);
    /// <summary>Returns a copy with different member types.</summary>
    public UnionTypeExtensionNode WithTypes(IReadOnlyList<NamedTypeNode> types) => new(Name, Directives, types, Location);
}

public sealed partial class EnumTypeDefinitionNode
{
    /// <summary>Returns a copy with a different optional description.</summary>
    public EnumTypeDefinitionNode WithDescription(StringValueNode? description) => new(Name, Directives, Values, Location, description);
    /// <summary>Returns a copy with different directives.</summary>
    public EnumTypeDefinitionNode WithDirectives(IReadOnlyList<DirectiveNode> directives) => new(Name, directives, Values, Location, Description);
    /// <summary>Returns a copy with a different source location.</summary>
    public EnumTypeDefinitionNode WithLocation(Location location) => new(Name, Directives, Values, (SourceLocation)(location ?? throw new ArgumentNullException(nameof(location))), Description);
    /// <summary>Returns a copy with a different name.</summary>
    public EnumTypeDefinitionNode WithName(NameNode name) => new(name, Directives, Values, Location, Description);
    /// <summary>Returns a copy with different enum values.</summary>
    public EnumTypeDefinitionNode WithValues(IReadOnlyList<EnumValueDefinitionNode> values) => new(Name, Directives, values, Location, Description);
}

public sealed partial class EnumTypeExtensionNode
{
    /// <summary>Returns a copy with different directives.</summary>
    public EnumTypeExtensionNode WithDirectives(IReadOnlyList<DirectiveNode> directives) => new(Name, directives, Values, Location);
    /// <summary>Returns a copy with a different source location.</summary>
    public EnumTypeExtensionNode WithLocation(Location location) => new(Name, Directives, Values, (SourceLocation)(location ?? throw new ArgumentNullException(nameof(location))));
    /// <summary>Returns a copy with a different name.</summary>
    public EnumTypeExtensionNode WithName(NameNode name) => new(name, Directives, Values, Location);
    /// <summary>Returns a copy with different enum values.</summary>
    public EnumTypeExtensionNode WithValues(IReadOnlyList<EnumValueDefinitionNode> values) => new(Name, Directives, values, Location);
}

public sealed partial class InputObjectTypeDefinitionNode
{
    /// <summary>Returns a copy with a different optional description.</summary>
    public InputObjectTypeDefinitionNode WithDescription(StringValueNode? description) => new(Name, Directives, Fields, Location, description);
    /// <summary>Returns a copy with different directives.</summary>
    public InputObjectTypeDefinitionNode WithDirectives(IReadOnlyList<DirectiveNode> directives) => new(Name, directives, Fields, Location, Description);
    /// <summary>Returns a copy with different input fields.</summary>
    public InputObjectTypeDefinitionNode WithFields(IReadOnlyList<InputValueDefinitionNode> fields) => new(Name, Directives, fields, Location, Description);
    /// <summary>Returns a copy with a different source location.</summary>
    public InputObjectTypeDefinitionNode WithLocation(Location location) => new(Name, Directives, Fields, (SourceLocation)(location ?? throw new ArgumentNullException(nameof(location))), Description);
    /// <summary>Returns a copy with a different name.</summary>
    public InputObjectTypeDefinitionNode WithName(NameNode name) => new(name, Directives, Fields, Location, Description);
}

public sealed partial class InputObjectTypeExtensionNode
{
    /// <summary>Returns a copy with different directives.</summary>
    public InputObjectTypeExtensionNode WithDirectives(IReadOnlyList<DirectiveNode> directives) => new(Name, directives, Fields, Location);
    /// <summary>Returns a copy with different input fields.</summary>
    public InputObjectTypeExtensionNode WithFields(IReadOnlyList<InputValueDefinitionNode> fields) => new(Name, Directives, fields, Location);
    /// <summary>Returns a copy with a different source location.</summary>
    public InputObjectTypeExtensionNode WithLocation(Location location) => new(Name, Directives, Fields, (SourceLocation)(location ?? throw new ArgumentNullException(nameof(location))));
    /// <summary>Returns a copy with a different name.</summary>
    public InputObjectTypeExtensionNode WithName(NameNode name) => new(name, Directives, Fields, Location);
}

public sealed partial class FieldDefinitionNode
{
    /// <summary>Returns a copy with different arguments.</summary>
    public FieldDefinitionNode WithArguments(IReadOnlyList<InputValueDefinitionNode> arguments) => new(Name, arguments, (TypeNode)Type, Directives, Location, Description);
    /// <summary>Returns a copy with a different optional description.</summary>
    public FieldDefinitionNode WithDescription(StringValueNode? description) => new(Name, Arguments, (TypeNode)Type, Directives, Location, description);
    /// <summary>Returns a copy with different directives.</summary>
    public FieldDefinitionNode WithDirectives(IReadOnlyList<DirectiveNode> directives) => new(Name, Arguments, (TypeNode)Type, directives, Location, Description);
    /// <summary>Returns a copy with a different source location.</summary>
    public FieldDefinitionNode WithLocation(Location location) => new(Name, Arguments, (TypeNode)Type, Directives, (SourceLocation)(location ?? throw new ArgumentNullException(nameof(location))), Description);
    /// <summary>Returns a copy with a different name.</summary>
    public FieldDefinitionNode WithName(NameNode name) => new(name, Arguments, (TypeNode)Type, Directives, Location, Description);
    /// <summary>Returns a copy with a different type.</summary>
    public FieldDefinitionNode WithType(ITypeNode type) => new(Name, Arguments, type as TypeNode ?? throw new ArgumentException("Types must be parser type nodes.", nameof(type)), Directives, Location, Description);
}

public sealed partial class InputValueDefinitionNode
{
    /// <summary>Returns a copy with a different default value.</summary>
    public InputValueDefinitionNode WithDefaultValue(IValueNode? defaultValue) => new(Name, (TypeNode)Type, defaultValue is null ? null : defaultValue as ValueNode ?? throw new ArgumentException("Values must be parser value nodes.", nameof(defaultValue)), Directives, Location, Description);
    /// <summary>Returns a copy with a different optional description.</summary>
    public InputValueDefinitionNode WithDescription(StringValueNode? description) => new(Name, (TypeNode)Type, DefaultValue is null ? null : (ValueNode)DefaultValue, Directives, Location, description);
    /// <summary>Returns a copy with different directives.</summary>
    public InputValueDefinitionNode WithDirectives(IReadOnlyList<DirectiveNode> directives) => new(Name, (TypeNode)Type, DefaultValue is null ? null : (ValueNode)DefaultValue, directives, Location, Description);
    /// <summary>Returns a copy with a different source location.</summary>
    public InputValueDefinitionNode WithLocation(Location location) => new(Name, (TypeNode)Type, DefaultValue is null ? null : (ValueNode)DefaultValue, Directives, (SourceLocation)(location ?? throw new ArgumentNullException(nameof(location))), Description);
    /// <summary>Returns a copy with a different name.</summary>
    public InputValueDefinitionNode WithName(NameNode name) => new(name, (TypeNode)Type, DefaultValue is null ? null : (ValueNode)DefaultValue, Directives, Location, Description);
    /// <summary>Returns a copy with a different type.</summary>
    public InputValueDefinitionNode WithType(ITypeNode type) => new(Name, type as TypeNode ?? throw new ArgumentException("Types must be parser type nodes.", nameof(type)), DefaultValue is null ? null : (ValueNode)DefaultValue, Directives, Location, Description);
}

public sealed partial class EnumValueDefinitionNode
{
    /// <summary>Returns a copy with a different optional description.</summary>
    public EnumValueDefinitionNode WithDescription(StringValueNode? description) => new(Name, Directives, Location, description);
    /// <summary>Returns a copy with different directives.</summary>
    public EnumValueDefinitionNode WithDirectives(IReadOnlyList<DirectiveNode> directives) => new(Name, directives, Location, Description);
    /// <summary>Returns a copy with a different source location.</summary>
    public EnumValueDefinitionNode WithLocation(Location location) => new(Name, Directives, (SourceLocation)(location ?? throw new ArgumentNullException(nameof(location))), Description);
    /// <summary>Returns a copy with a different name.</summary>
    public EnumValueDefinitionNode WithName(NameNode name) => new(name, Directives, Location, Description);
}

public sealed partial class DirectiveDefinitionNode
{
    /// <summary>Creates a directive definition without applied directives.</summary>
    public DirectiveDefinitionNode(Location location, NameNode name, StringValueNode? description, bool isRepeatable, IReadOnlyList<InputValueDefinitionNode> arguments, IReadOnlyList<NameNode> locations)
        : this(name, arguments, isRepeatable, locations, (SourceLocation)(location ?? throw new ArgumentNullException(nameof(location))), description) { }
    /// <summary>Creates a directive definition with applied directives.</summary>
    public DirectiveDefinitionNode(Location location, NameNode name, StringValueNode? description, bool isRepeatable, IReadOnlyList<InputValueDefinitionNode> arguments, IReadOnlyList<DirectiveNode> directives, IReadOnlyList<NameNode> locations)
        : this(name, arguments, isRepeatable, locations, (SourceLocation)(location ?? throw new ArgumentNullException(nameof(location))), description, directives) { }

    /// <summary>Gets whether the directive is repeatable.</summary>
    public bool IsRepeatable => Repeatable;
    /// <summary>Returns a copy with repetition enabled.</summary>
    /// <summary>Returns a copy with the repeatable state set to the supplied value.</summary>
    public DirectiveDefinitionNode AsRepeatable(bool repeatable = true) => Repeatable == repeatable
        ? this
        : new(Name, Arguments, repeatable, Locations, SourceRange, Description, Directives);
    /// <summary>Returns a copy with different argument definitions.</summary>
    public DirectiveDefinitionNode WithArguments(IReadOnlyList<InputValueDefinitionNode> arguments) => new(Name, arguments, Repeatable, Locations, Location, Description);
    /// <summary>Returns a copy with a different optional description.</summary>
    public DirectiveDefinitionNode WithDescription(StringValueNode? description) => new(Name, Arguments, Repeatable, Locations, Location, description);
    /// <summary>Returns a copy with different directives.</summary>
    public DirectiveDefinitionNode WithDirectives(IReadOnlyList<DirectiveNode> directives) =>
        new(Name, Arguments, Repeatable, Locations, Location, Description, directives);
    /// <summary>Returns a copy with a different source location.</summary>
    public DirectiveDefinitionNode WithLocation(Location location) => new(Name, Arguments, Repeatable, Locations, (SourceLocation)(location ?? throw new ArgumentNullException(nameof(location))), Description);
    /// <summary>Returns a copy with different allowed locations.</summary>
    public DirectiveDefinitionNode WithLocations(IReadOnlyList<NameNode> locations) => new(Name, Arguments, Repeatable, locations, Location, Description);
    /// <summary>Returns a copy with a different name.</summary>
    public DirectiveDefinitionNode WithName(NameNode name) => new(name, Arguments, Repeatable, Locations, Location, Description);
}
