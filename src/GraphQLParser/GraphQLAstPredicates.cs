namespace GraphQLParser;

/// <summary>Classifies GraphQL syntax nodes by their language role.</summary>
public static class GraphQLAstPredicates
{
    /// <summary>Returns whether a node is a document definition.</summary>
    public static bool IsDefinitionNode(AstNode node) => node is DefinitionNode;
    /// <summary>Returns whether a node defines an executable operation or fragment.</summary>
    public static bool IsExecutableDefinitionNode(AstNode node) => node is OperationDefinitionNode or FragmentDefinitionNode;
    /// <summary>Returns whether a node is a field or fragment selection.</summary>
    public static bool IsSelectionNode(AstNode node) => node is SelectionNode;
    /// <summary>Returns whether a node represents an executable or constant value.</summary>
    public static bool IsValueNode(AstNode node) => node is ValueNode;
    /// <summary>Returns whether a value contains no variable references, including nested values.</summary>
    public static bool IsConstValueNode(AstNode node) => node is ValueNode value && IsConstant(value);
    /// <summary>Returns whether a node represents a named, list, or non-null type reference.</summary>
    public static bool IsTypeNode(AstNode node) => node is TypeNode;
    /// <summary>Returns whether a node defines the schema or a named type or directive.</summary>
    public static bool IsTypeSystemDefinitionNode(AstNode node) => node is SchemaDefinitionNode or TypeDefinitionNode or DirectiveDefinitionNode;
    /// <summary>Returns whether a node defines a named schema type.</summary>
    public static bool IsTypeDefinitionNode(AstNode node) => node is TypeDefinitionNode;
    /// <summary>Returns whether a node extends the schema or a named schema type.</summary>
    public static bool IsTypeSystemExtensionNode(AstNode node) => node is SchemaExtensionNode or TypeExtensionNode;
    /// <summary>Returns whether a node extends a named schema type.</summary>
    public static bool IsTypeExtensionNode(AstNode node) => node is TypeExtensionNode;
    /// <summary>Returns whether a node represents a schema coordinate.</summary>
    public static bool IsSchemaCoordinateNode(AstNode node) => node is SchemaCoordinateNode;

    private static bool IsConstant(ValueNode value) => value switch
    {
        VariableNode => false,
        ListValueNode list => list.Values.All(IsConstant),
        ObjectValueNode obj => obj.Fields.All(field => IsConstant(field.Value)),
        _ => true,
    };
}
