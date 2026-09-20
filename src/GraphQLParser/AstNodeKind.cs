namespace GraphQLParser;

/// <summary>Stable classifications for GraphQL abstract syntax tree nodes.</summary>
public enum AstNodeKind : byte
{
    /// <summary>A GraphQL name node.</summary>
    Name,
    /// <summary>A document containing ordered definitions.</summary>
    Document,
    /// <summary>A query, mutation, or subscription operation.</summary>
    OperationDefinition,
    /// <summary>A fragment definition.</summary>
    FragmentDefinition,
    /// <summary>A selection set.</summary>
    SelectionSet,
    /// <summary>A field selection.</summary>
    Field,
    /// <summary>A named fragment spread.</summary>
    FragmentSpread,
    /// <summary>An inline fragment.</summary>
    InlineFragment,
    /// <summary>An argument.</summary>
    Argument,
    /// <summary>A directive application.</summary>
    Directive,
    /// <summary>A variable definition.</summary>
    VariableDefinition,
    /// <summary>A variable reference.</summary>
    Variable,
    /// <summary>An integer value.</summary>
    IntValue,
    /// <summary>A floating-point value.</summary>
    FloatValue,
    /// <summary>A quoted or block string value.</summary>
    StringValue,
    /// <summary>A boolean value.</summary>
    BooleanValue,
    /// <summary>A null value.</summary>
    NullValue,
    /// <summary>An enum value.</summary>
    EnumValue,
    /// <summary>A list value.</summary>
    ListValue,
    /// <summary>An object value.</summary>
    ObjectValue,
    /// <summary>An object field within an object value.</summary>
    ObjectField,
    /// <summary>A named type reference.</summary>
    NamedType,
    /// <summary>A list type reference.</summary>
    ListType,
    /// <summary>A non-null type reference.</summary>
    NonNullType,
    /// <summary>A schema definition.</summary>
    SchemaDefinition,
    /// <summary>A schema extension.</summary>
    SchemaExtension,
    /// <summary>A schema root operation mapping.</summary>
    OperationTypeDefinition,
    /// <summary>A scalar type definition.</summary>
    ScalarTypeDefinition,
    /// <summary>A scalar type extension.</summary>
    ScalarTypeExtension,
    /// <summary>An object type definition.</summary>
    ObjectTypeDefinition,
    /// <summary>An object type extension.</summary>
    ObjectTypeExtension,
    /// <summary>An interface type definition.</summary>
    InterfaceTypeDefinition,
    /// <summary>An interface type extension.</summary>
    InterfaceTypeExtension,
    /// <summary>A union type definition.</summary>
    UnionTypeDefinition,
    /// <summary>A union type extension.</summary>
    UnionTypeExtension,
    /// <summary>An enum type definition.</summary>
    EnumTypeDefinition,
    /// <summary>An enum type extension.</summary>
    EnumTypeExtension,
    /// <summary>An enum value definition.</summary>
    EnumValueDefinition,
    /// <summary>An input-object type definition.</summary>
    InputObjectTypeDefinition,
    /// <summary>An input-object type extension.</summary>
    InputObjectTypeExtension,
    /// <summary>A field definition within an object or interface.</summary>
    FieldDefinition,
    /// <summary>An input-value definition.</summary>
    InputValueDefinition,
    /// <summary>A directive definition.</summary>
    DirectiveDefinition,
    /// <summary>A type schema coordinate.</summary>
    TypeCoordinate,
    /// <summary>A field schema coordinate.</summary>
    MemberCoordinate,
    /// <summary>A field argument schema coordinate.</summary>
    ArgumentCoordinate,
    /// <summary>A directive schema coordinate.</summary>
    DirectiveCoordinate,
    /// <summary>A directive argument schema coordinate.</summary>
    DirectiveArgumentCoordinate,
}
