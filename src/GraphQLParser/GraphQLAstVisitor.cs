namespace GraphQLParser;

/// <summary>Controls whether AST traversal continues into a node's children.</summary>
public enum GraphQLVisitControl : byte
{
    /// <summary>Continue with this node's children.</summary>
    Continue,
    /// <summary>Skip this node's children and continue with the next sibling.</summary>
    SkipChildren,
    /// <summary>Stop the complete traversal.</summary>
    Stop,
}

/// <summary>Performs ordered, read-only depth-first traversal of GraphQL syntax trees.</summary>
public static class GraphQLAstVisitor
{
    /// <summary>Visits nodes in depth-first order and optionally reports nodes after their children.</summary>
    /// <returns><see langword="true"/> when traversal completed, or <see langword="false"/> when stopped.</returns>
    public static bool Visit(AstNode root, Func<AstNode, GraphQLVisitControl> enter, Func<AstNode, GraphQLVisitControl>? leave = null)
    {
        ArgumentNullException.ThrowIfNull(root);
        ArgumentNullException.ThrowIfNull(enter);
        var pending = new Stack<(AstNode Node, bool IsLeaving)>();
        pending.Push((root, false));

        while (pending.TryPop(out var item))
        {
            if (item.IsLeaving)
            {
                if (leave?.Invoke(item.Node) == GraphQLVisitControl.Stop) return false;
                continue;
            }

            var decision = enter(item.Node);
            if (decision == GraphQLVisitControl.Stop) return false;
            if (decision == GraphQLVisitControl.SkipChildren)
            {
                if (leave is not null) pending.Push((item.Node, true));
                continue;
            }

            if (decision != GraphQLVisitControl.Continue) throw new ArgumentOutOfRangeException(nameof(enter), "The visitor returned an unknown traversal control.");
            if (leave is not null) pending.Push((item.Node, true));
            var children = GetChildren(item.Node);
            for (var index = children.Count - 1; index >= 0; index--) pending.Push((children[index], false));
        }

        return true;
    }

    private static List<AstNode> GetChildren(AstNode node)
    {
        var result = new List<AstNode>();
        void Add(AstNode? child) { if (child is not null) result.Add(child); }
        void AddRange<TNode>(IEnumerable<TNode> children) where TNode : AstNode { foreach (var child in children) result.Add(child); }

        switch (node)
        {
            case DocumentNode document: AddRange(document.Definitions); break;
            case OperationDefinitionNode operation:
                Add(operation.Description); Add(operation.Name); AddRange(operation.VariableDefinitions); AddRange(operation.Directives); Add(operation.SelectionSet); break;
            case FragmentDefinitionNode fragment:
                Add(fragment.Description); Add(fragment.Name); AddRange(fragment.VariableDefinitions); Add(fragment.TypeCondition); AddRange(fragment.Directives); Add(fragment.SelectionSet); break;
            case SelectionSetNode selectionSet: AddRange(selectionSet.Selections); break;
            case FieldNode field: Add(field.Alias); Add(field.Name); AddRange(field.Arguments); AddRange(field.Directives); Add(field.SelectionSet); break;
            case FragmentSpreadNode spread: Add(spread.Name); AddRange(spread.Directives); break;
            case InlineFragmentNode inline: Add(inline.TypeCondition); AddRange(inline.Directives); Add(inline.SelectionSet); break;
            case ArgumentNode argument: Add(argument.Name); Add(argument.Value); break;
            case DirectiveNode directive: Add(directive.Name); AddRange(directive.Arguments); break;
            case VariableDefinitionNode variableDefinition:
                Add(variableDefinition.Description); Add(variableDefinition.Variable); Add(variableDefinition.Type); Add(variableDefinition.DefaultValue); AddRange(variableDefinition.Directives); break;
            case VariableNode variable: Add(variable.Name); break;
            case ListValueNode list: AddRange(list.Values); break;
            case ObjectValueNode obj: AddRange(obj.Fields); break;
            case ObjectFieldNode field: Add(field.Name); Add(field.Value); break;
            case NamedTypeNode type: Add(type.Name); break;
            case ListTypeNode type: Add(type.Type); break;
            case NonNullTypeNode type: Add(type.Type); break;
            case SchemaDefinitionNode schema: Add(schema.Description); AddRange(schema.Directives); AddRange(schema.OperationTypes); break;
            case SchemaExtensionNode schema: AddRange(schema.Directives); AddRange(schema.OperationTypes); break;
            case OperationTypeDefinitionNode operationType: Add(operationType.Type); break;
            case TypeDefinitionNode typeDefinition:
                Add(typeDefinition.Description); Add(typeDefinition.Name);
                switch (typeDefinition)
                {
                    case ScalarTypeDefinitionNode scalar: AddRange(scalar.Directives); break;
                    case ObjectTypeDefinitionNode obj: AddRange(obj.Interfaces); AddRange(obj.Directives); AddRange(obj.Fields); break;
                    case InterfaceTypeDefinitionNode iface: AddRange(iface.Interfaces); AddRange(iface.Directives); AddRange(iface.Fields); break;
                    case UnionTypeDefinitionNode union: AddRange(union.Directives); AddRange(union.Types); break;
                    case EnumTypeDefinitionNode en: AddRange(en.Directives); AddRange(en.Values); break;
                    case InputObjectTypeDefinitionNode input: AddRange(input.Directives); AddRange(input.Fields); break;
                }
                break;
            case TypeExtensionNode typeExtension:
                Add(typeExtension.Name);
                switch (typeExtension)
                {
                    case ScalarTypeExtensionNode scalar: AddRange(scalar.Directives); break;
                    case ObjectTypeExtensionNode obj: AddRange(obj.Interfaces); AddRange(obj.Directives); AddRange(obj.Fields); break;
                    case InterfaceTypeExtensionNode iface: AddRange(iface.Interfaces); AddRange(iface.Directives); AddRange(iface.Fields); break;
                    case UnionTypeExtensionNode union: AddRange(union.Directives); AddRange(union.Types); break;
                    case EnumTypeExtensionNode en: AddRange(en.Directives); AddRange(en.Values); break;
                    case InputObjectTypeExtensionNode input: AddRange(input.Directives); AddRange(input.Fields); break;
                }
                break;
            case FieldDefinitionNode fieldDefinition:
                Add(fieldDefinition.Description); Add(fieldDefinition.Name); AddRange(fieldDefinition.Arguments); Add(fieldDefinition.Type); AddRange(fieldDefinition.Directives); break;
            case InputValueDefinitionNode inputValue:
                Add(inputValue.Description); Add(inputValue.Name); Add(inputValue.Type); Add(inputValue.DefaultValue); AddRange(inputValue.Directives); break;
            case EnumValueDefinitionNode enumValue: Add(enumValue.Description); Add(enumValue.Name); AddRange(enumValue.Directives); break;
            case DirectiveDefinitionNode directiveDefinition:
                Add(directiveDefinition.Description); Add(directiveDefinition.Name); AddRange(directiveDefinition.Arguments); AddRange(directiveDefinition.Locations); break;
            case TypeCoordinateNode typeCoordinate: Add(typeCoordinate.Name); break;
            case MemberCoordinateNode memberCoordinate: Add(memberCoordinate.Name); Add(memberCoordinate.MemberName); break;
            case ArgumentCoordinateNode argumentCoordinate: Add(argumentCoordinate.Name); Add(argumentCoordinate.FieldName); Add(argumentCoordinate.ArgumentName); break;
            case DirectiveCoordinateNode directiveCoordinate: Add(directiveCoordinate.Name); break;
            case DirectiveArgumentCoordinateNode directiveArgument: Add(directiveArgument.Name); Add(directiveArgument.ArgumentName); break;
        }

        return result;
    }
}
