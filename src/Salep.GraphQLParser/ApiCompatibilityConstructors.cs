namespace Salep.GraphQLParser;

// Location-first overloads mirror the public immutable AST construction contract.
public sealed partial class SchemaDefinitionNode
{
    /// <summary>Creates a syntax node using the public compatibility argument order.</summary>
    public SchemaDefinitionNode(Location location, StringValueNode? description, IReadOnlyList<DirectiveNode> directives, IReadOnlyList<OperationTypeDefinitionNode> operationTypes)
        : this(operationTypes, directives, (SourceLocation)(location ?? throw new ArgumentNullException(nameof(location))), description) { }
}

public sealed partial class SchemaExtensionNode
{
    /// <summary>Creates a syntax node using the public compatibility argument order.</summary>
    public SchemaExtensionNode(Location location, IReadOnlyList<DirectiveNode> directives, IReadOnlyList<OperationTypeDefinitionNode> operationTypes)
        : this(operationTypes, directives, (SourceLocation)(location ?? throw new ArgumentNullException(nameof(location)))) { }
}

public sealed partial class OperationTypeDefinitionNode
{
    /// <summary>Creates a syntax node using the public compatibility argument order.</summary>
    public OperationTypeDefinitionNode(Location location, OperationType operation, NamedTypeNode type)
        : this(operation, type, (SourceLocation)(location ?? throw new ArgumentNullException(nameof(location)))) { }
}

public sealed partial class EnumTypeDefinitionNode
{
    /// <summary>Creates a syntax node using the public compatibility argument order.</summary>
    public EnumTypeDefinitionNode(Location location, NameNode name, StringValueNode? description, IReadOnlyList<DirectiveNode> directives, IReadOnlyList<EnumValueDefinitionNode> values)
        : this(name, directives, values, (SourceLocation)(location ?? throw new ArgumentNullException(nameof(location))), description) { }
}

public sealed partial class EnumTypeExtensionNode
{
    /// <summary>Creates a syntax node using the public compatibility argument order.</summary>
    public EnumTypeExtensionNode(Location location, NameNode name, IReadOnlyList<DirectiveNode> directives, IReadOnlyList<EnumValueDefinitionNode> values)
        : this(name, directives, values, (SourceLocation)(location ?? throw new ArgumentNullException(nameof(location)))) { }
}

public sealed partial class InputObjectTypeDefinitionNode
{
    /// <summary>Creates a syntax node using the public compatibility argument order.</summary>
    public InputObjectTypeDefinitionNode(Location location, NameNode name, StringValueNode? description, IReadOnlyList<DirectiveNode> directives, IReadOnlyList<InputValueDefinitionNode> fields)
        : this(name, directives, fields, (SourceLocation)(location ?? throw new ArgumentNullException(nameof(location))), description) { }
}

public sealed partial class InputObjectTypeExtensionNode
{
    /// <summary>Creates a syntax node using the public compatibility argument order.</summary>
    public InputObjectTypeExtensionNode(Location location, NameNode name, IReadOnlyList<DirectiveNode> directives, IReadOnlyList<InputValueDefinitionNode> fields)
        : this(name, directives, fields, (SourceLocation)(location ?? throw new ArgumentNullException(nameof(location)))) { }
}

public sealed partial class InterfaceTypeDefinitionNode
{
    /// <summary>Creates a syntax node using the public compatibility argument order.</summary>
    public InterfaceTypeDefinitionNode(Location location, NameNode name, StringValueNode? description, IReadOnlyList<DirectiveNode> directives, IReadOnlyList<NamedTypeNode> interfaces, IReadOnlyList<FieldDefinitionNode> fields)
        : this(name, interfaces, directives, fields, (SourceLocation)(location ?? throw new ArgumentNullException(nameof(location))), description) { }
}

public sealed partial class InterfaceTypeExtensionNode
{
    /// <summary>Creates a syntax node using the public compatibility argument order.</summary>
    public InterfaceTypeExtensionNode(Location location, NameNode name, IReadOnlyList<DirectiveNode> directives, IReadOnlyList<NamedTypeNode> interfaces, IReadOnlyList<FieldDefinitionNode> fields)
        : this(name, interfaces, directives, fields, (SourceLocation)(location ?? throw new ArgumentNullException(nameof(location)))) { }
}

public sealed partial class ObjectTypeDefinitionNode
{
    /// <summary>Creates a syntax node using the public compatibility argument order.</summary>
    public ObjectTypeDefinitionNode(Location location, NameNode name, StringValueNode? description, IReadOnlyList<DirectiveNode> directives, IReadOnlyList<NamedTypeNode> interfaces, IReadOnlyList<FieldDefinitionNode> fields)
        : this(name, interfaces, directives, fields, (SourceLocation)(location ?? throw new ArgumentNullException(nameof(location))), description) { }
}

public sealed partial class ObjectTypeExtensionNode
{
    /// <summary>Creates a syntax node using the public compatibility argument order.</summary>
    public ObjectTypeExtensionNode(Location location, NameNode name, IReadOnlyList<DirectiveNode> directives, IReadOnlyList<NamedTypeNode> interfaces, IReadOnlyList<FieldDefinitionNode> fields)
        : this(name, interfaces, directives, fields, (SourceLocation)(location ?? throw new ArgumentNullException(nameof(location)))) { }
}

public sealed partial class UnionTypeDefinitionNode
{
    /// <summary>Creates a syntax node using the public compatibility argument order.</summary>
    public UnionTypeDefinitionNode(Location location, NameNode name, StringValueNode? description, IReadOnlyList<DirectiveNode> directives, IReadOnlyList<NamedTypeNode> types)
        : this(name, directives, types, (SourceLocation)(location ?? throw new ArgumentNullException(nameof(location))), description) { }
}

public sealed partial class UnionTypeExtensionNode
{
    /// <summary>Creates a syntax node using the public compatibility argument order.</summary>
    public UnionTypeExtensionNode(Location location, NameNode name, IReadOnlyList<DirectiveNode> directives, IReadOnlyList<NamedTypeNode> types)
        : this(name, directives, types, (SourceLocation)(location ?? throw new ArgumentNullException(nameof(location)))) { }
}

public sealed partial class ScalarTypeExtensionNode
{
    /// <summary>Creates a syntax node using the public compatibility argument order.</summary>
    public ScalarTypeExtensionNode(Location location, NameNode name, IReadOnlyList<DirectiveNode> directives)
        : this(name, directives, (SourceLocation)(location ?? throw new ArgumentNullException(nameof(location)))) { }
}

public sealed partial class FieldDefinitionNode
{
    /// <summary>Creates a syntax node using the public compatibility argument order.</summary>
    public FieldDefinitionNode(Location location, NameNode name, StringValueNode? description, IReadOnlyList<InputValueDefinitionNode> arguments, ITypeNode type, IReadOnlyList<DirectiveNode> directives)
        : this(name, arguments, ApiCompatibilityCast.Type(type), directives, (SourceLocation)(location ?? throw new ArgumentNullException(nameof(location))), description) { }
}

public sealed partial class InputValueDefinitionNode
{
    /// <summary>Creates a syntax node using the public compatibility argument order.</summary>
    public InputValueDefinitionNode(Location location, NameNode name, StringValueNode? description, ITypeNode type, IValueNode? defaultValue, IReadOnlyList<DirectiveNode> directives)
        : this(name, ApiCompatibilityCast.Type(type), ApiCompatibilityCast.NullableValue(defaultValue), directives, (SourceLocation)(location ?? throw new ArgumentNullException(nameof(location))), description) { }
}

public sealed partial class EnumValueDefinitionNode
{
    /// <summary>Creates a syntax node using the public compatibility argument order.</summary>
    public EnumValueDefinitionNode(Location location, NameNode name, StringValueNode? description, IReadOnlyList<DirectiveNode> directives)
        : this(name, directives, (SourceLocation)(location ?? throw new ArgumentNullException(nameof(location))), description) { }
}

public sealed partial class OperationDefinitionNode
{
    /// <summary>Creates a syntax node using the public compatibility argument order.</summary>
    public OperationDefinitionNode(Location location, NameNode? name, StringValueNode? description, OperationType operation, IReadOnlyList<VariableDefinitionNode> variableDefinitions, IReadOnlyList<DirectiveNode> directives, SelectionSetNode selectionSet)
        : this(operation, name, variableDefinitions, directives, selectionSet, (SourceLocation)(location ?? throw new ArgumentNullException(nameof(location))), description) { }
}

public sealed partial class FragmentDefinitionNode
{
    /// <summary>Creates a syntax node using the public compatibility argument order.</summary>
    public FragmentDefinitionNode(Location location, NameNode name, StringValueNode? description, IReadOnlyList<VariableDefinitionNode> variableDefinitions, NamedTypeNode typeCondition, IReadOnlyList<DirectiveNode> directives, SelectionSetNode selectionSet)
        : this(name, typeCondition, directives, selectionSet, (SourceLocation)(location ?? throw new ArgumentNullException(nameof(location))), description, variableDefinitions) { }
}

public sealed partial class FragmentSpreadNode
{
    /// <summary>Creates a syntax node using the public compatibility argument order.</summary>
    public FragmentSpreadNode(Location location, NameNode name, IReadOnlyList<DirectiveNode> directives)
        : this(name, directives, (SourceLocation)(location ?? throw new ArgumentNullException(nameof(location)))) { }
}

public sealed partial class InlineFragmentNode
{
    /// <summary>Creates a syntax node using the public compatibility argument order.</summary>
    public InlineFragmentNode(Location location, NamedTypeNode typeCondition, IReadOnlyList<DirectiveNode> directives, SelectionSetNode selectionSet)
        : this(typeCondition, directives, selectionSet, (SourceLocation)(location ?? throw new ArgumentNullException(nameof(location)))) { }
}

public sealed partial class VariableDefinitionNode
{
    /// <summary>Creates a syntax node using the public compatibility argument order.</summary>
    public VariableDefinitionNode(Location location, VariableNode variable, StringValueNode? description, ITypeNode type, IValueNode? defaultValue, IReadOnlyList<DirectiveNode> directives)
        : this(variable, ApiCompatibilityCast.Type(type), ApiCompatibilityCast.NullableValue(defaultValue), directives, (SourceLocation)(location ?? throw new ArgumentNullException(nameof(location))), description) { }
}

internal static class ApiCompatibilityCast
{
    internal static TypeNode Type(ITypeNode type) => type as TypeNode ?? throw new ArgumentException("Types must be parser type nodes.", nameof(type));
    internal static ValueNode? NullableValue(IValueNode? value) => value is null ? null : value as ValueNode ?? throw new ArgumentException("Values must be parser value nodes.", nameof(value));
}
