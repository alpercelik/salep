namespace GraphQLParser;

/// <summary>Base class for named syntax nodes with directives.</summary>
public abstract class NamedSyntaxNode : DefinitionNode, INamedSyntaxNode
{
    /// <summary>Creates a named syntax node.</summary>
    protected NamedSyntaxNode(AstNodeKind kind, NameNode name, SourceLocation location)
        : base(kind, location)
    {
        ArgumentNullException.ThrowIfNull(name);
        Name = name;
    }

    /// <summary>Gets the syntax node name.</summary>
    public NameNode Name { get; }
    /// <summary>Gets associated directives in source order.</summary>
    public abstract IReadOnlyList<DirectiveNode> Directives { get; }
    internal abstract IReadOnlyList<DirectiveNode> ContractDirectives { get; }
}

/// <summary>Base class for schema and named type definitions.</summary>
public abstract class TypeDefinitionNode : NamedSyntaxNode, ITypeDefinitionNode
{
    /// <summary>Creates a named type definition.</summary>
    protected TypeDefinitionNode(AstNodeKind kind, NameNode name, StringValueNode? description, SourceLocation location)
        : base(kind, name, location)
    {
        Description = description;
    }
    /// <summary>Gets the optional evaluated description.</summary>
    public StringValueNode? Description { get; }
}

/// <summary>Base class for object and interface type definitions.</summary>
public abstract class ComplexTypeDefinitionNodeBase : NamedSyntaxNode
{
    /// <summary>Creates a complex type definition.</summary>
    protected ComplexTypeDefinitionNodeBase(AstNodeKind kind, NameNode name, SourceLocation location)
        : base(kind, name, location)
    {
    }

    /// <summary>Gets implemented interfaces in source order.</summary>
    public abstract IReadOnlyList<NamedTypeNode> Interfaces { get; }
    /// <summary>Gets field definitions in source order.</summary>
    public abstract IReadOnlyList<FieldDefinitionNode> Fields { get; }
}

/// <summary>Base class for enum type definitions.</summary>
public abstract class EnumTypeDefinitionNodeBase : NamedSyntaxNode
{
    /// <summary>Creates an enum type definition.</summary>
    protected EnumTypeDefinitionNodeBase(AstNodeKind kind, NameNode name, SourceLocation location)
        : base(kind, name, location) { }

    /// <summary>Gets enum values in source order.</summary>
    public abstract IReadOnlyList<EnumValueDefinitionNode> Values { get; }
}

/// <summary>Base class for input-object type definitions.</summary>
public abstract class InputObjectTypeDefinitionNodeBase : NamedSyntaxNode
{
    /// <summary>Creates an input-object type definition.</summary>
    protected InputObjectTypeDefinitionNodeBase(AstNodeKind kind, NameNode name, SourceLocation location)
        : base(kind, name, location) { }

    /// <summary>Gets input fields in source order.</summary>
    public abstract IReadOnlyList<InputValueDefinitionNode> Fields { get; }
}

/// <summary>Base class for union type definitions.</summary>
public abstract class UnionTypeDefinitionNodeBase : NamedSyntaxNode
{
    /// <summary>Creates a union type definition.</summary>
    protected UnionTypeDefinitionNodeBase(AstNodeKind kind, NameNode name, SourceLocation location)
        : base(kind, name, location) { }

    /// <summary>Gets union member types in source order.</summary>
    public abstract IReadOnlyList<NamedTypeNode> Types { get; }
}

/// <summary>Base class for named type extensions.</summary>
public abstract class TypeExtensionNode : NamedSyntaxNode, ITypeExtensionNode
{
    /// <summary>Creates a named type extension.</summary>
    protected TypeExtensionNode(AstNodeKind kind, NameNode name, SourceLocation location)
        : base(kind, name, location)
    {
    }
}

/// <summary>Base class for schema definitions and extensions.</summary>
public abstract class SchemaDefinitionNodeBase : DefinitionNode, IHasDirectives
{
    /// <summary>Creates a schema syntax node.</summary>
    protected SchemaDefinitionNodeBase(AstNodeKind kind, SourceLocation location)
        : base(kind, location) { }

    /// <summary>Gets root operation mappings in source order.</summary>
    public abstract IReadOnlyList<OperationTypeDefinitionNode> OperationTypes { get; }
    /// <summary>Gets associated directives in source order.</summary>
    public abstract IReadOnlyList<DirectiveNode> Directives { get; }
}

/// <summary>A schema definition with one or more root operation mappings.</summary>
public sealed partial class SchemaDefinitionNode : SchemaDefinitionNodeBase, ITypeSystemDefinitionNode
{
    /// <summary>Creates an immutable schema definition.</summary>
    public SchemaDefinitionNode(
        IEnumerable<OperationTypeDefinitionNode> operationTypes,
        IEnumerable<DirectiveNode> directives,
        SourceLocation location,
        StringValueNode? description = null)
        : base(AstNodeKind.SchemaDefinition, location)
    {
        ArgumentNullException.ThrowIfNull(operationTypes);
        ArgumentNullException.ThrowIfNull(directives);
        OperationTypes = new AstNodeList<OperationTypeDefinitionNode>(operationTypes);
        if (OperationTypes.Count == 0)
        {
            throw new ArgumentException("A schema definition requires at least one root operation mapping.", nameof(operationTypes));
        }

        Directives = new AstNodeList<DirectiveNode>(directives);
        Description = description;
    }

    /// <summary>Gets root operation mappings in source order.</summary>
    public override AstNodeList<OperationTypeDefinitionNode> OperationTypes { get; }
    /// <summary>Gets directives in source order.</summary>
    public override AstNodeList<DirectiveNode> Directives { get; }
    /// <summary>Gets the optional evaluated description.</summary>
    public StringValueNode? Description { get; }
}

/// <summary>A schema extension adding root mappings or directives.</summary>
public sealed partial class SchemaExtensionNode : SchemaDefinitionNodeBase, ITypeSystemExtensionNode
{
    /// <summary>Creates an immutable schema extension.</summary>
    public SchemaExtensionNode(
        IEnumerable<OperationTypeDefinitionNode> operationTypes,
        IEnumerable<DirectiveNode> directives,
        SourceLocation location)
        : base(AstNodeKind.SchemaExtension, location)
    {
        ArgumentNullException.ThrowIfNull(operationTypes);
        ArgumentNullException.ThrowIfNull(directives);
        OperationTypes = new AstNodeList<OperationTypeDefinitionNode>(operationTypes);
        Directives = new AstNodeList<DirectiveNode>(directives);
        if (OperationTypes.Count == 0 && Directives.Count == 0)
        {
            throw new ArgumentException("A schema extension must add a root operation mapping or directive.");
        }
    }

    /// <summary>Gets added root operation mappings in source order.</summary>
    public override AstNodeList<OperationTypeDefinitionNode> OperationTypes { get; }
    /// <summary>Gets added directives in source order.</summary>
    public override AstNodeList<DirectiveNode> Directives { get; }
}

/// <summary>A schema root operation mapping.</summary>
public sealed partial class OperationTypeDefinitionNode : AstNode
{
    /// <summary>Creates an immutable root operation mapping.</summary>
    public OperationTypeDefinitionNode(OperationType operation, NamedTypeNode type, SourceLocation location)
        : base(AstNodeKind.OperationTypeDefinition, location)
    {
        if (!Enum.IsDefined(operation))
        {
            throw new ArgumentOutOfRangeException(nameof(operation));
        }

        ArgumentNullException.ThrowIfNull(type);
        Operation = operation;
        Type = type;
    }

    /// <summary>Gets the query, mutation, or subscription operation.</summary>
    public OperationType Operation { get; }
    /// <summary>Gets the root named type.</summary>
    public NamedTypeNode Type { get; }
}

/// <summary>A scalar type definition.</summary>
public sealed partial class ScalarTypeDefinitionNode : TypeDefinitionNode
{
    /// <summary>Creates an immutable scalar definition.</summary>
    public ScalarTypeDefinitionNode(NameNode name, IEnumerable<DirectiveNode> directives, SourceLocation location, StringValueNode? description = null)
        : base(AstNodeKind.ScalarTypeDefinition, name, description, location)
    {
        ArgumentNullException.ThrowIfNull(directives);
        Directives = new AstNodeList<DirectiveNode>(directives);
    }

    /// <summary>Gets directives in source order.</summary>
    public override AstNodeList<DirectiveNode> Directives { get; }
    internal override IReadOnlyList<DirectiveNode> ContractDirectives => Directives;
}

/// <summary>A scalar type extension.</summary>
public sealed partial class ScalarTypeExtensionNode : TypeExtensionNode
{
    /// <summary>Creates an immutable scalar extension.</summary>
    public ScalarTypeExtensionNode(NameNode name, IEnumerable<DirectiveNode> directives, SourceLocation location)
        : base(AstNodeKind.ScalarTypeExtension, name, location)
    {
        ArgumentNullException.ThrowIfNull(directives);
        Directives = new AstNodeList<DirectiveNode>(directives);
        RequireAddedContent(Directives.Count > 0, nameof(directives));
    }

    /// <summary>Gets added directives in source order.</summary>
    public override AstNodeList<DirectiveNode> Directives { get; }
    internal override IReadOnlyList<DirectiveNode> ContractDirectives => Directives;

    private static void RequireAddedContent(bool hasContent, string parameterName)
    {
        if (!hasContent) throw new ArgumentException("A scalar extension must add at least one directive.", parameterName);
    }
}

/// <summary>An object type definition.</summary>
public sealed partial class ObjectTypeDefinitionNode : ComplexTypeDefinitionNodeBase, ITypeDefinitionNode
{
    /// <summary>Creates an immutable object type definition.</summary>
    public ObjectTypeDefinitionNode(
        NameNode name,
        IEnumerable<NamedTypeNode> interfaces,
        IEnumerable<DirectiveNode> directives,
        IEnumerable<FieldDefinitionNode> fields,
        SourceLocation location,
        StringValueNode? description = null)
        : base(AstNodeKind.ObjectTypeDefinition, name, location)
    {
        ArgumentNullException.ThrowIfNull(interfaces);
        ArgumentNullException.ThrowIfNull(directives);
        ArgumentNullException.ThrowIfNull(fields);
        Interfaces = new AstNodeList<NamedTypeNode>(interfaces);
        Directives = new AstNodeList<DirectiveNode>(directives);
        Fields = new AstNodeList<FieldDefinitionNode>(fields);
        Description = description;
    }

    /// <summary>Gets implemented interfaces in source order.</summary>
    public override AstNodeList<NamedTypeNode> Interfaces { get; }
    /// <summary>Gets directives in source order.</summary>
    public override AstNodeList<DirectiveNode> Directives { get; }
    internal override IReadOnlyList<DirectiveNode> ContractDirectives => Directives;
    /// <summary>Gets field definitions in source order.</summary>
    public override AstNodeList<FieldDefinitionNode> Fields { get; }
    /// <summary>Gets the optional evaluated description.</summary>
    public StringValueNode? Description { get; }
}

/// <summary>An object type extension.</summary>
public sealed partial class ObjectTypeExtensionNode : ComplexTypeDefinitionNodeBase, ITypeExtensionNode
{
    /// <summary>Creates an immutable object type extension.</summary>
    public ObjectTypeExtensionNode(
        NameNode name,
        IEnumerable<NamedTypeNode> interfaces,
        IEnumerable<DirectiveNode> directives,
        IEnumerable<FieldDefinitionNode> fields,
        SourceLocation location)
        : base(AstNodeKind.ObjectTypeExtension, name, location)
    {
        ArgumentNullException.ThrowIfNull(interfaces);
        ArgumentNullException.ThrowIfNull(directives);
        ArgumentNullException.ThrowIfNull(fields);
        Interfaces = new AstNodeList<NamedTypeNode>(interfaces);
        Directives = new AstNodeList<DirectiveNode>(directives);
        Fields = new AstNodeList<FieldDefinitionNode>(fields);
        if (Interfaces.Count == 0 && Directives.Count == 0 && Fields.Count == 0)
        {
            throw new ArgumentException("An object type extension must add an interface, directive, or field.");
        }
    }

    /// <summary>Gets added interfaces in source order.</summary>
    public override AstNodeList<NamedTypeNode> Interfaces { get; }
    /// <summary>Gets added directives in source order.</summary>
    public override AstNodeList<DirectiveNode> Directives { get; }
    internal override IReadOnlyList<DirectiveNode> ContractDirectives => Directives;
    /// <summary>Gets added field definitions in source order.</summary>
    public override AstNodeList<FieldDefinitionNode> Fields { get; }
}

/// <summary>An interface type definition.</summary>
public sealed partial class InterfaceTypeDefinitionNode : ComplexTypeDefinitionNodeBase, ITypeDefinitionNode
{
    /// <summary>Creates an immutable interface type definition.</summary>
    public InterfaceTypeDefinitionNode(
        NameNode name,
        IEnumerable<NamedTypeNode> interfaces,
        IEnumerable<DirectiveNode> directives,
        IEnumerable<FieldDefinitionNode> fields,
        SourceLocation location,
        StringValueNode? description = null)
        : base(AstNodeKind.InterfaceTypeDefinition, name, location)
    {
        ArgumentNullException.ThrowIfNull(interfaces);
        ArgumentNullException.ThrowIfNull(directives);
        ArgumentNullException.ThrowIfNull(fields);
        Interfaces = new AstNodeList<NamedTypeNode>(interfaces);
        Directives = new AstNodeList<DirectiveNode>(directives);
        Fields = new AstNodeList<FieldDefinitionNode>(fields);
        Description = description;
    }

    /// <summary>Gets implemented interfaces in source order.</summary>
    public override AstNodeList<NamedTypeNode> Interfaces { get; }
    /// <summary>Gets directives in source order.</summary>
    public override AstNodeList<DirectiveNode> Directives { get; }
    internal override IReadOnlyList<DirectiveNode> ContractDirectives => Directives;
    /// <summary>Gets field definitions in source order.</summary>
    public override AstNodeList<FieldDefinitionNode> Fields { get; }
    /// <summary>Gets the optional evaluated description.</summary>
    public StringValueNode? Description { get; }
}

/// <summary>An interface type extension.</summary>
public sealed partial class InterfaceTypeExtensionNode : ComplexTypeDefinitionNodeBase, ITypeExtensionNode
{
    /// <summary>Creates an immutable interface type extension.</summary>
    public InterfaceTypeExtensionNode(
        NameNode name,
        IEnumerable<NamedTypeNode> interfaces,
        IEnumerable<DirectiveNode> directives,
        IEnumerable<FieldDefinitionNode> fields,
        SourceLocation location)
        : base(AstNodeKind.InterfaceTypeExtension, name, location)
    {
        ArgumentNullException.ThrowIfNull(interfaces);
        ArgumentNullException.ThrowIfNull(directives);
        ArgumentNullException.ThrowIfNull(fields);
        Interfaces = new AstNodeList<NamedTypeNode>(interfaces);
        Directives = new AstNodeList<DirectiveNode>(directives);
        Fields = new AstNodeList<FieldDefinitionNode>(fields);
        if (Interfaces.Count == 0 && Directives.Count == 0 && Fields.Count == 0)
        {
            throw new ArgumentException("An interface extension must add an interface, directive, or field.");
        }
    }

    /// <summary>Gets added interfaces in source order.</summary>
    public override AstNodeList<NamedTypeNode> Interfaces { get; }
    /// <summary>Gets added directives in source order.</summary>
    public override AstNodeList<DirectiveNode> Directives { get; }
    internal override IReadOnlyList<DirectiveNode> ContractDirectives => Directives;
    /// <summary>Gets added field definitions in source order.</summary>
    public override AstNodeList<FieldDefinitionNode> Fields { get; }
}

/// <summary>A union type definition.</summary>
public sealed partial class UnionTypeDefinitionNode : UnionTypeDefinitionNodeBase, ITypeDefinitionNode
{
    /// <summary>Creates an immutable union type definition.</summary>
    public UnionTypeDefinitionNode(
        NameNode name,
        IEnumerable<DirectiveNode> directives,
        IEnumerable<NamedTypeNode> types,
        SourceLocation location,
        StringValueNode? description = null)
        : base(AstNodeKind.UnionTypeDefinition, name, location)
    {
        ArgumentNullException.ThrowIfNull(directives);
        ArgumentNullException.ThrowIfNull(types);
        Directives = new AstNodeList<DirectiveNode>(directives);
        Types = new AstNodeList<NamedTypeNode>(types);
        Description = description;
    }

    /// <summary>Gets directives in source order.</summary>
    public override AstNodeList<DirectiveNode> Directives { get; }
    internal override IReadOnlyList<DirectiveNode> ContractDirectives => Directives;
    /// <summary>Gets union member types in source order.</summary>
    public override AstNodeList<NamedTypeNode> Types { get; }
    /// <summary>Gets the optional evaluated description.</summary>
    public StringValueNode? Description { get; }
}

/// <summary>A union type extension.</summary>
public sealed partial class UnionTypeExtensionNode : UnionTypeDefinitionNodeBase, ITypeExtensionNode
{
    /// <summary>Creates an immutable union type extension.</summary>
    public UnionTypeExtensionNode(NameNode name, IEnumerable<DirectiveNode> directives, IEnumerable<NamedTypeNode> types, SourceLocation location)
        : base(AstNodeKind.UnionTypeExtension, name, location)
    {
        ArgumentNullException.ThrowIfNull(directives);
        ArgumentNullException.ThrowIfNull(types);
        Directives = new AstNodeList<DirectiveNode>(directives);
        Types = new AstNodeList<NamedTypeNode>(types);
        if (Directives.Count == 0 && Types.Count == 0)
        {
            throw new ArgumentException("A union extension must add a directive or member type.");
        }
    }

    /// <summary>Gets added directives in source order.</summary>
    public override AstNodeList<DirectiveNode> Directives { get; }
    internal override IReadOnlyList<DirectiveNode> ContractDirectives => Directives;
    /// <summary>Gets added member types in source order.</summary>
    public override AstNodeList<NamedTypeNode> Types { get; }
}

/// <summary>An enum type definition.</summary>
public sealed partial class EnumTypeDefinitionNode : EnumTypeDefinitionNodeBase, ITypeDefinitionNode
{
    /// <summary>Creates an immutable enum type definition.</summary>
    public EnumTypeDefinitionNode(
        NameNode name,
        IEnumerable<DirectiveNode> directives,
        IEnumerable<EnumValueDefinitionNode> values,
        SourceLocation location,
        StringValueNode? description = null)
        : base(AstNodeKind.EnumTypeDefinition, name, location)
    {
        ArgumentNullException.ThrowIfNull(directives);
        ArgumentNullException.ThrowIfNull(values);
        Directives = new AstNodeList<DirectiveNode>(directives);
        Values = new AstNodeList<EnumValueDefinitionNode>(values);
        Description = description;
    }

    /// <summary>Gets directives in source order.</summary>
    public override AstNodeList<DirectiveNode> Directives { get; }
    internal override IReadOnlyList<DirectiveNode> ContractDirectives => Directives;
    /// <summary>Gets enum values in source order.</summary>
    public override AstNodeList<EnumValueDefinitionNode> Values { get; }
    /// <summary>Gets the optional evaluated description.</summary>
    public StringValueNode? Description { get; }
}

/// <summary>An enum type extension.</summary>
public sealed partial class EnumTypeExtensionNode : EnumTypeDefinitionNodeBase, ITypeExtensionNode
{
    /// <summary>Creates an immutable enum type extension.</summary>
    public EnumTypeExtensionNode(NameNode name, IEnumerable<DirectiveNode> directives, IEnumerable<EnumValueDefinitionNode> values, SourceLocation location)
        : base(AstNodeKind.EnumTypeExtension, name, location)
    {
        ArgumentNullException.ThrowIfNull(directives);
        ArgumentNullException.ThrowIfNull(values);
        Directives = new AstNodeList<DirectiveNode>(directives);
        Values = new AstNodeList<EnumValueDefinitionNode>(values);
        if (Directives.Count == 0 && Values.Count == 0)
        {
            throw new ArgumentException("An enum extension must add a directive or enum value.");
        }
    }

    /// <summary>Gets added directives in source order.</summary>
    public override AstNodeList<DirectiveNode> Directives { get; }
    internal override IReadOnlyList<DirectiveNode> ContractDirectives => Directives;
    /// <summary>Gets added enum values in source order.</summary>
    public override AstNodeList<EnumValueDefinitionNode> Values { get; }
}

/// <summary>An input-object type definition.</summary>
public sealed partial class InputObjectTypeDefinitionNode : InputObjectTypeDefinitionNodeBase, ITypeDefinitionNode
{
    /// <summary>Creates an immutable input-object definition.</summary>
    public InputObjectTypeDefinitionNode(
        NameNode name,
        IEnumerable<DirectiveNode> directives,
        IEnumerable<InputValueDefinitionNode> fields,
        SourceLocation location,
        StringValueNode? description = null)
        : base(AstNodeKind.InputObjectTypeDefinition, name, location)
    {
        ArgumentNullException.ThrowIfNull(directives);
        ArgumentNullException.ThrowIfNull(fields);
        Directives = new AstNodeList<DirectiveNode>(directives);
        Fields = new AstNodeList<InputValueDefinitionNode>(fields);
        Description = description;
    }

    /// <summary>Gets directives in source order.</summary>
    public override AstNodeList<DirectiveNode> Directives { get; }
    internal override IReadOnlyList<DirectiveNode> ContractDirectives => Directives;
    /// <summary>Gets input fields in source order.</summary>
    public override AstNodeList<InputValueDefinitionNode> Fields { get; }
    /// <summary>Gets the optional evaluated description.</summary>
    public StringValueNode? Description { get; }
}

/// <summary>An input-object type extension.</summary>
public sealed partial class InputObjectTypeExtensionNode : InputObjectTypeDefinitionNodeBase, ITypeExtensionNode
{
    /// <summary>Creates an immutable input-object type extension.</summary>
    public InputObjectTypeExtensionNode(NameNode name, IEnumerable<DirectiveNode> directives, IEnumerable<InputValueDefinitionNode> fields, SourceLocation location)
        : base(AstNodeKind.InputObjectTypeExtension, name, location)
    {
        ArgumentNullException.ThrowIfNull(directives);
        ArgumentNullException.ThrowIfNull(fields);
        Directives = new AstNodeList<DirectiveNode>(directives);
        Fields = new AstNodeList<InputValueDefinitionNode>(fields);
        if (Directives.Count == 0 && Fields.Count == 0)
        {
            throw new ArgumentException("An input-object extension must add a directive or input field.");
        }
    }

    /// <summary>Gets added directives in source order.</summary>
    public override AstNodeList<DirectiveNode> Directives { get; }
    internal override IReadOnlyList<DirectiveNode> ContractDirectives => Directives;
    /// <summary>Gets added input fields in source order.</summary>
    public override AstNodeList<InputValueDefinitionNode> Fields { get; }
}

/// <summary>A field definition within an object or interface.</summary>
public sealed partial class FieldDefinitionNode : NamedSyntaxNode
{
    /// <summary>Creates an immutable field definition.</summary>
    public FieldDefinitionNode(
        NameNode name,
        IEnumerable<InputValueDefinitionNode> arguments,
        TypeNode type,
        IEnumerable<DirectiveNode> directives,
        SourceLocation location,
        StringValueNode? description = null)
        : base(AstNodeKind.FieldDefinition, name, location)
    {
        ArgumentNullException.ThrowIfNull(name);
        ArgumentNullException.ThrowIfNull(arguments);
        ArgumentNullException.ThrowIfNull(type);
        ArgumentNullException.ThrowIfNull(directives);
        Arguments = new AstNodeList<InputValueDefinitionNode>(arguments);
        Type = type;
        Directives = new AstNodeList<DirectiveNode>(directives);
        Description = description;
    }

    /// <summary>Gets the field name.</summary>
    /// <summary>Gets argument definitions in source order.</summary>
    public IReadOnlyList<InputValueDefinitionNode> Arguments { get; }
    /// <summary>Gets the required field type.</summary>
    public ITypeNode Type { get; }
    /// <summary>Gets directives in source order.</summary>
    public override AstNodeList<DirectiveNode> Directives { get; }
    internal override IReadOnlyList<DirectiveNode> ContractDirectives => Directives;
    /// <summary>Gets the optional evaluated description.</summary>
    public StringValueNode? Description { get; }
}

/// <summary>An argument or input-object field definition.</summary>
public sealed partial class InputValueDefinitionNode : NamedSyntaxNode
{
    /// <summary>Creates an immutable input-value definition.</summary>
    public InputValueDefinitionNode(
        NameNode name,
        TypeNode type,
        ValueNode? defaultValue,
        IEnumerable<DirectiveNode> directives,
        SourceLocation location,
        StringValueNode? description = null)
        : base(AstNodeKind.InputValueDefinition, name, location)
    {
        ArgumentNullException.ThrowIfNull(name);
        ArgumentNullException.ThrowIfNull(type);
        ArgumentNullException.ThrowIfNull(directives);
        Type = type;
        DefaultValue = defaultValue;
        Directives = new AstNodeList<DirectiveNode>(directives);
        Description = description;
    }

    /// <summary>Gets the input-value name.</summary>
    /// <summary>Gets the required input-value type.</summary>
    public ITypeNode Type { get; }
    /// <summary>Gets the optional constant default value.</summary>
    public IValueNode? DefaultValue { get; }
    /// <summary>Gets directives in source order.</summary>
    public override AstNodeList<DirectiveNode> Directives { get; }
    internal override IReadOnlyList<DirectiveNode> ContractDirectives => Directives;
    /// <summary>Gets the optional evaluated description.</summary>
    public StringValueNode? Description { get; }
}

/// <summary>An enum value definition.</summary>
public sealed partial class EnumValueDefinitionNode : NamedSyntaxNode
{
    /// <summary>Creates an immutable enum value definition.</summary>
    public EnumValueDefinitionNode(NameNode name, IEnumerable<DirectiveNode> directives, SourceLocation location, StringValueNode? description = null)
        : base(AstNodeKind.EnumValueDefinition, name, location)
    {
        ArgumentNullException.ThrowIfNull(name);
        ArgumentNullException.ThrowIfNull(directives);
        Directives = new AstNodeList<DirectiveNode>(directives);
        Description = description;
    }

    /// <summary>Gets the enum value name.</summary>
    /// <summary>Gets directives in source order.</summary>
    public override AstNodeList<DirectiveNode> Directives { get; }
    internal override IReadOnlyList<DirectiveNode> ContractDirectives => Directives;
    /// <summary>Gets the optional evaluated description.</summary>
    public StringValueNode? Description { get; }
}

/// <summary>A directive definition, including its application locations.</summary>
public sealed partial class DirectiveDefinitionNode : DefinitionNode, ITypeSystemDefinitionNode, IHasName, IHasDirectives
{
    /// <summary>Creates an immutable directive definition.</summary>
    public DirectiveDefinitionNode(
        NameNode name,
        IEnumerable<InputValueDefinitionNode> arguments,
        bool repeatable,
        IEnumerable<NameNode> locations,
        SourceLocation location,
        StringValueNode? description = null)
        : this(name, arguments, repeatable, locations, location, description, [])
    {
    }

    private DirectiveDefinitionNode(
        NameNode name,
        IEnumerable<InputValueDefinitionNode> arguments,
        bool repeatable,
        IEnumerable<NameNode> locations,
        SourceLocation location,
        StringValueNode? description,
        IEnumerable<DirectiveNode> directives)
        : base(AstNodeKind.DirectiveDefinition, location)
    {
        ArgumentNullException.ThrowIfNull(name);
        ArgumentNullException.ThrowIfNull(arguments);
        ArgumentNullException.ThrowIfNull(locations);
        ArgumentNullException.ThrowIfNull(directives);
        Name = name;
        Arguments = new AstNodeList<InputValueDefinitionNode>(arguments);
        Repeatable = repeatable;
        Locations = new AstNodeList<NameNode>(locations);
        Directives = new AstNodeList<DirectiveNode>(directives);
        if (Locations.Count == 0)
        {
            throw new ArgumentException("A directive definition requires at least one location.", nameof(locations));
        }

        Description = description;
    }

    /// <summary>Gets the directive name.</summary>
    public NameNode Name { get; }
    /// <summary>Gets argument definitions in source order.</summary>
    public IReadOnlyList<InputValueDefinitionNode> Arguments { get; }
    /// <summary>Gets whether applications may repeat.</summary>
    public bool Repeatable { get; }
    /// <summary>Gets allowed directive locations in source order.</summary>
    public IReadOnlyList<NameNode> Locations { get; }
    /// <summary>Gets directives associated with this definition.</summary>
    public IReadOnlyList<DirectiveNode> Directives { get; }
    IReadOnlyList<DirectiveNode> IHasDirectives.Directives => Directives;
    /// <summary>Gets the optional evaluated description.</summary>
    public StringValueNode? Description { get; }
}
