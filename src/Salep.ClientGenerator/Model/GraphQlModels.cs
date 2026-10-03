namespace Salep.ClientGenerator.Model;

/// <summary>A language-neutral source span from a GraphQL document.</summary>
public sealed record GraphQlSourceSpan(int Start, int End, int Line, int Column, bool HasLocation);

/// <summary>Base type for all GraphQL type references.</summary>
public abstract record GraphQlTypeReference(GraphQlSourceSpan Source)
{
    /// <summary>Indicates whether this reference has a top-level non-null wrapper.</summary>
    public bool IsNonNull => this is GraphQlNonNullTypeReference;
}

/// <summary>A named GraphQL type reference.</summary>
public sealed record GraphQlNamedTypeReference(string Name, GraphQlSourceSpan Source) : GraphQlTypeReference(Source);

/// <summary>A GraphQL list type wrapper.</summary>
public sealed record GraphQlListTypeReference(GraphQlTypeReference ElementType, GraphQlSourceSpan Source) : GraphQlTypeReference(Source);

/// <summary>A GraphQL non-null type wrapper.</summary>
public sealed record GraphQlNonNullTypeReference(GraphQlTypeReference NullableType, GraphQlSourceSpan Source) : GraphQlTypeReference(Source);

/// <summary>A GraphQL value, retaining literal kind and nested contents.</summary>
public sealed record GraphQlValue(
    GraphQlValueKind Kind,
    string? Text = null,
    bool? Boolean = null,
    bool IsBlockString = false,
    IReadOnlyList<GraphQlValue>? Items = null,
    IReadOnlyList<GraphQlObjectField>? Fields = null,
    GraphQlSourceSpan? Source = null);

/// <summary>GraphQL value literal categories.</summary>
public enum GraphQlValueKind { Variable, IntegerLiteral, FloatLiteral, StringLiteral, BooleanLiteral, NullLiteral, EnumLiteral, ListLiteral, ObjectLiteral }

/// <summary>A named field in a GraphQL object literal.</summary>
public sealed record GraphQlObjectField(string Name, GraphQlValue Value, GraphQlSourceSpan Source);

/// <summary>A GraphQL argument or directive argument.</summary>
public sealed record GraphQlArgument(string Name, GraphQlValue Value, GraphQlSourceSpan Source);

/// <summary>A directive applied to a schema or executable node.</summary>
public sealed record GraphQlDirective(string Name, IReadOnlyList<GraphQlArgument> Arguments, GraphQlSourceSpan Source);

/// <summary>A field or input value definition.</summary>
public sealed record GraphQlInputValueDefinition(
    string Name,
    string? Description,
    GraphQlTypeReference Type,
    GraphQlValue? DefaultValue,
    IReadOnlyList<GraphQlDirective> Directives,
    GraphQlSourceSpan Source);

/// <summary>A field declared by an object or interface type.</summary>
public sealed record GraphQlFieldDefinition(
    string Name,
    string? Description,
    IReadOnlyList<GraphQlInputValueDefinition> Arguments,
    GraphQlTypeReference Type,
    IReadOnlyList<GraphQlDirective> Directives,
    GraphQlSourceSpan Source);

/// <summary>A named GraphQL type definition or extension.</summary>
public abstract record GraphQlSchemaType(
    string Name,
    string? Description,
    IReadOnlyList<GraphQlDirective> Directives,
    bool IsExtension,
    GraphQlSourceSpan Source);

/// <summary>A scalar definition or extension.</summary>
public sealed record GraphQlScalarType(
    string Name,
    string? Description,
    IReadOnlyList<GraphQlDirective> Directives,
    bool IsExtension,
    GraphQlSourceSpan Source) : GraphQlSchemaType(Name, Description, Directives, IsExtension, Source);

/// <summary>An object definition or extension.</summary>
public sealed record GraphQlObjectType(
    string Name,
    string? Description,
    IReadOnlyList<GraphQlFieldDefinition> Fields,
    IReadOnlyList<string> Interfaces,
    IReadOnlyList<GraphQlDirective> Directives,
    bool IsExtension,
    GraphQlSourceSpan Source) : GraphQlSchemaType(Name, Description, Directives, IsExtension, Source);

/// <summary>An interface definition or extension.</summary>
public sealed record GraphQlInterfaceType(
    string Name,
    string? Description,
    IReadOnlyList<GraphQlFieldDefinition> Fields,
    IReadOnlyList<string> Interfaces,
    IReadOnlyList<GraphQlDirective> Directives,
    bool IsExtension,
    GraphQlSourceSpan Source) : GraphQlSchemaType(Name, Description, Directives, IsExtension, Source);

/// <summary>A union definition or extension.</summary>
public sealed record GraphQlUnionType(
    string Name,
    string? Description,
    IReadOnlyList<string> Members,
    IReadOnlyList<GraphQlDirective> Directives,
    bool IsExtension,
    GraphQlSourceSpan Source) : GraphQlSchemaType(Name, Description, Directives, IsExtension, Source);

/// <summary>An enum value definition.</summary>
public sealed record GraphQlEnumValueDefinition(string Name, string? Description, IReadOnlyList<GraphQlDirective> Directives, GraphQlSourceSpan Source);

/// <summary>An enum definition or extension.</summary>
public sealed record GraphQlEnumType(
    string Name,
    string? Description,
    IReadOnlyList<GraphQlEnumValueDefinition> Values,
    IReadOnlyList<GraphQlDirective> Directives,
    bool IsExtension,
    GraphQlSourceSpan Source) : GraphQlSchemaType(Name, Description, Directives, IsExtension, Source);

/// <summary>An input-object definition or extension.</summary>
public sealed record GraphQlInputObjectType(
    string Name,
    string? Description,
    IReadOnlyList<GraphQlInputValueDefinition> Fields,
    IReadOnlyList<GraphQlDirective> Directives,
    bool IsExtension,
    GraphQlSourceSpan Source) : GraphQlSchemaType(Name, Description, Directives, IsExtension, Source);

/// <summary>A schema root operation mapping.</summary>
public sealed record GraphQlRootOperation(string Operation, string TypeName, GraphQlSourceSpan Source);

/// <summary>A schema definition or extension.</summary>
public sealed record GraphQlSchemaDefinition(
    string? Description,
    IReadOnlyList<GraphQlRootOperation> RootOperations,
    IReadOnlyList<GraphQlDirective> Directives,
    bool IsExtension,
    GraphQlSourceSpan Source);

/// <summary>A custom directive definition, including arguments and valid locations.</summary>
public sealed record GraphQlDirectiveDefinition(
    string Name,
    string? Description,
    IReadOnlyList<GraphQlInputValueDefinition> Arguments,
    bool IsRepeatable,
    IReadOnlyList<string> Locations,
    IReadOnlyList<GraphQlDirective> Directives,
    GraphQlSourceSpan Source);

/// <summary>A complete target-independent view of GraphQL SDL definitions and extensions.</summary>
public sealed record GraphQlSchemaModel(
    IReadOnlyList<GraphQlSchemaType> Types,
    IReadOnlyList<GraphQlSchemaDefinition> SchemaDefinitions,
    IReadOnlyList<GraphQlDirectiveDefinition> DirectiveDefinitions)
{
    public IReadOnlyList<GraphQlObjectType> ObjectTypes => Types.OfType<GraphQlObjectType>().Where(type => !type.IsExtension).ToArray();
    public IReadOnlyList<GraphQlInterfaceType> InterfaceTypes => Types.OfType<GraphQlInterfaceType>().Where(type => !type.IsExtension).ToArray();
    public IReadOnlyList<GraphQlUnionType> UnionTypes => Types.OfType<GraphQlUnionType>().Where(type => !type.IsExtension).ToArray();
    public IReadOnlyList<GraphQlEnumType> EnumTypes => Types.OfType<GraphQlEnumType>().Where(type => !type.IsExtension).ToArray();
    public IReadOnlyList<GraphQlInputObjectType> InputTypes => Types.OfType<GraphQlInputObjectType>().Where(type => !type.IsExtension).ToArray();
    public IReadOnlyList<GraphQlScalarType> ScalarTypes => Types.OfType<GraphQlScalarType>().Where(type => !type.IsExtension).ToArray();
}

/// <summary>A set of GraphQL selections.</summary>
public sealed record GraphQlSelectionSet(IReadOnlyList<GraphQlSelection> Selections, GraphQlSourceSpan Source);

/// <summary>Base type for GraphQL executable selections.</summary>
public abstract record GraphQlSelection(IReadOnlyList<GraphQlDirective> Directives, GraphQlSourceSpan Source);

/// <summary>A field selection, including alias, arguments, and nested selection.</summary>
public sealed record GraphQlFieldSelection(
    string Name,
    string? Alias,
    IReadOnlyList<GraphQlArgument> Arguments,
    IReadOnlyList<GraphQlDirective> Directives,
    GraphQlSelectionSet? SelectionSet,
    GraphQlSourceSpan Source) : GraphQlSelection(Directives, Source)
{
    public string ResponseName => Alias ?? Name;
}

/// <summary>A named fragment spread.</summary>
public sealed record GraphQlFragmentSpreadSelection(
    string Name,
    IReadOnlyList<GraphQlArgument> Arguments,
    IReadOnlyList<GraphQlDirective> Directives,
    GraphQlSourceSpan Source) : GraphQlSelection(Directives, Source);

/// <summary>An inline fragment with optional type condition.</summary>
public sealed record GraphQlInlineFragmentSelection(
    string? TypeCondition,
    IReadOnlyList<GraphQlDirective> Directives,
    GraphQlSelectionSet SelectionSet,
    GraphQlSourceSpan Source) : GraphQlSelection(Directives, Source);

/// <summary>A variable declaration in an operation or fragment.</summary>
public sealed record GraphQlVariableDefinition(
    string Name,
    GraphQlTypeReference Type,
    GraphQlValue? DefaultValue,
    IReadOnlyList<GraphQlDirective> Directives,
    string? Description,
    GraphQlSourceSpan Source);

/// <summary>A query, mutation, or subscription operation.</summary>
public sealed record GraphQlOperationDefinition(
    string OperationType,
    string? Name,
    IReadOnlyList<GraphQlVariableDefinition> Variables,
    IReadOnlyList<GraphQlDirective> Directives,
    GraphQlSelectionSet SelectionSet,
    GraphQlSourceSpan Source,
    GraphQlResponseProjection? ResponseProjection = null);

/// <summary>A concrete GraphQL object shape selected in an operation response.</summary>
public sealed record GraphQlResponseProjection(string GraphQlTypeName, IReadOnlyList<GraphQlResponseFieldProjection> Fields);

/// <summary>A response field with aliases resolved and nested abstract variants expanded.</summary>
public sealed record GraphQlResponseFieldProjection(
    string ResponseName,
    string GraphQlFieldName,
    GraphQlTypeReference? Type,
    IReadOnlyList<GraphQlResponseProjection> Variants,
    GraphQlSourceSpan Source);

/// <summary>A reusable fragment definition.</summary>
public sealed record GraphQlFragmentDefinition(
    string Name,
    string TypeCondition,
    IReadOnlyList<GraphQlVariableDefinition> Variables,
    IReadOnlyList<GraphQlDirective> Directives,
    GraphQlSelectionSet SelectionSet,
    string? Description,
    GraphQlSourceSpan Source);

/// <summary>A complete target-independent view of executable GraphQL documents.</summary>
public sealed record GraphQlExecutableDocument(
    IReadOnlyList<GraphQlOperationDefinition> Operations,
    IReadOnlyList<GraphQlFragmentDefinition> Fragments);
