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
}
