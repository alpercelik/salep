namespace GraphQLParser;

/// <summary>Prints GraphQL syntax trees as stable, readable GraphQL source.</summary>
public static class GraphQLPrinter
{
    /// <summary>Prints a supported GraphQL syntax node.</summary>
    public static string Print(AstNode node)
    {
        ArgumentNullException.ThrowIfNull(node);
        return Render(node, 0);
    }

    private static string Render(AstNode node, int indent)
    {
        switch (node)
        {
            case NameNode name: return name.Value.ToString();
            case DocumentNode document: return string.Join("\n\n", document.Definitions.Select(definition => Render(definition, indent)));
            case OperationDefinitionNode operation: return RenderOperation(operation, indent);
            case FragmentDefinitionNode fragment: return RenderFragment(fragment, indent);
            case SelectionSetNode selectionSet: return RenderSelectionSet(selectionSet, indent);
            case FieldNode field: return RenderField(field, indent);
            case FragmentSpreadNode spread: return "..." + Render(spread.Name, indent) + RenderDirectives(spread.Directives, indent);
            case InlineFragmentNode inline:
                return "..." + (inline.TypeCondition is null ? string.Empty : " on " + Render(inline.TypeCondition, indent))
                    + RenderDirectives(inline.Directives, indent) + " " + Render(inline.SelectionSet, indent);
            case ArgumentNode argument: return Render(argument.Name, indent) + ": " + Render(argument.Value, indent);
            case DirectiveNode directive: return "@" + Render(directive.Name, indent) + RenderArguments(directive.Arguments, indent, 1 + directive.Name.Value.Length);
            case VariableDefinitionNode variable: return RenderDescription(variable.Description, indent) + "$" + Render(variable.Variable.Name, indent)
                + ": " + Render(variable.Type, indent) + (variable.DefaultValue is null ? string.Empty : " = " + Render(variable.DefaultValue, indent))
                + RenderDirectives(variable.Directives, indent);
            case VariableNode variable: return "$" + Render(variable.Name, indent);
            case IntValueNode value: return value.Value.ToString();
            case FloatValueNode value: return value.Value.ToString();
            case StringValueNode value: return value.IsBlock
                ? GraphQLBlockString.PrintBlockString(value.Value.ToString())
                : GraphQLString.PrintString(value.Value.ToString());
            case BooleanValueNode value: return value.Value ? "true" : "false";
            case NullValueNode: return "null";
            case EnumValueNode value: return value.Value.ToString();
            case ListValueNode list: return "[" + string.Join(", ", list.Values.Select(value => Render(value, indent))) + "]";
            case ObjectValueNode obj: return "{" + string.Join(", ", obj.Fields.Select(value => Render(value, indent))) + "}";
            case ObjectFieldNode field: return Render(field.Name, indent) + ": " + Render(field.Value, indent);
            case NamedTypeNode type: return Render(type.Name, indent);
            case ListTypeNode type: return "[" + Render(type.Type, indent) + "]";
            case NonNullTypeNode type: return Render(type.Type, indent) + "!";
            case SchemaDefinitionNode schema: return RenderSchema(schema.Description, schema.Directives, schema.OperationTypes, indent);
            case SchemaExtensionNode schema: return "extend " + RenderSchema(null, schema.Directives, schema.OperationTypes, indent);
            case OperationTypeDefinitionNode operationType: return OperationName(operationType.Operation) + ": " + Render(operationType.Type, indent);
            case ScalarTypeDefinitionNode scalar: return RenderDescription(scalar.Description, indent) + "scalar " + Render(scalar.Name, indent) + RenderDirectives(scalar.Directives, indent);
            case ScalarTypeExtensionNode scalar: return "extend scalar " + Render(scalar.Name, indent) + RenderDirectives(scalar.Directives, indent);
            case ObjectTypeDefinitionNode obj: return RenderDescription(obj.Description, indent) + "type " + Render(obj.Name, indent) + RenderImplements(obj.Interfaces, indent) + RenderDirectives(obj.Directives, indent) + RenderDefinitions(obj.Fields, indent);
            case ObjectTypeExtensionNode obj: return "extend type " + Render(obj.Name, indent) + RenderImplements(obj.Interfaces, indent) + RenderDirectives(obj.Directives, indent) + RenderDefinitions(obj.Fields, indent);
            case InterfaceTypeDefinitionNode iface: return RenderDescription(iface.Description, indent) + "interface " + Render(iface.Name, indent) + RenderImplements(iface.Interfaces, indent) + RenderDirectives(iface.Directives, indent) + RenderDefinitions(iface.Fields, indent);
            case InterfaceTypeExtensionNode iface: return "extend interface " + Render(iface.Name, indent) + RenderImplements(iface.Interfaces, indent) + RenderDirectives(iface.Directives, indent) + RenderDefinitions(iface.Fields, indent);
            case UnionTypeDefinitionNode union: return RenderDescription(union.Description, indent) + "union " + Render(union.Name, indent) + RenderDirectives(union.Directives, indent) + RenderUnionTypes(union.Types, indent);
            case UnionTypeExtensionNode union: return "extend union " + Render(union.Name, indent) + RenderDirectives(union.Directives, indent) + RenderUnionTypes(union.Types, indent);
            case EnumTypeDefinitionNode en: return RenderDescription(en.Description, indent) + "enum " + Render(en.Name, indent) + RenderDirectives(en.Directives, indent) + RenderDefinitions(en.Values, indent);
            case EnumTypeExtensionNode en: return "extend enum " + Render(en.Name, indent) + RenderDirectives(en.Directives, indent) + RenderDefinitions(en.Values, indent);
            case InputObjectTypeDefinitionNode input: return RenderDescription(input.Description, indent) + "input " + Render(input.Name, indent) + RenderDirectives(input.Directives, indent) + RenderDefinitions(input.Fields, indent);
            case InputObjectTypeExtensionNode input: return "extend input " + Render(input.Name, indent) + RenderDirectives(input.Directives, indent) + RenderDefinitions(input.Fields, indent);
            case FieldDefinitionNode field: return RenderDescription(field.Description, indent) + Render(field.Name, indent) + RenderInputArguments(field.Arguments, indent) + ": " + Render(field.Type, indent) + RenderDirectives(field.Directives, indent);
            case InputValueDefinitionNode input: return RenderDescription(input.Description, indent) + Render(input.Name, indent) + ": " + Render(input.Type, indent)
                + (input.DefaultValue is null ? string.Empty : " = " + Render(input.DefaultValue, indent)) + RenderDirectives(input.Directives, indent);
            case EnumValueDefinitionNode value: return RenderDescription(value.Description, indent) + Render(value.Name, indent) + RenderDirectives(value.Directives, indent);
            case DirectiveDefinitionNode directive: return RenderDescription(directive.Description, indent) + "directive @" + Render(directive.Name, indent)
                + RenderInputArguments(directive.Arguments, indent) + (directive.Repeatable ? " repeatable" : string.Empty)
                + " on " + string.Join(" | ", directive.Locations.Select(location => Render(location, indent)));
            case TypeCoordinateNode coordinate: return Render(coordinate.Name, indent);
            case MemberCoordinateNode coordinate: return Render(coordinate.Name, indent) + "." + Render(coordinate.MemberName, indent);
            case ArgumentCoordinateNode coordinate: return Render(coordinate.Name, indent) + "." + Render(coordinate.FieldName, indent) + "(" + Render(coordinate.ArgumentName, indent) + ":)";
            case DirectiveCoordinateNode coordinate: return "@" + Render(coordinate.Name, indent);
            case DirectiveArgumentCoordinateNode coordinate: return "@" + Render(coordinate.Name, indent) + "(" + Render(coordinate.ArgumentName, indent) + ":)";
            default: throw new ArgumentException($"Unsupported GraphQL syntax node type '{node.GetType().Name}'.", nameof(node));
        }
    }

    private static string RenderOperation(OperationDefinitionNode operation, int indent)
    {
        var description = RenderDescription(operation.Description, indent);
        var shorthand = operation.Operation == OperationType.Query && operation.Name is null
            && operation.VariableDefinitions.Count == 0 && operation.Directives.Count == 0 && operation.Description is null;
        if (shorthand) return Render(operation.SelectionSet, indent);
        var head = OperationName(operation.Operation);
        if (operation.Name is not null) head += " " + Render(operation.Name, indent);
        else if (operation.VariableDefinitions.Count > 0) head += " ";
        head += RenderVariableDefinitions(operation.VariableDefinitions, indent);
        head += RenderDirectives(operation.Directives, indent);
        return description + head + " " + Render(operation.SelectionSet, indent);
    }

    private static string RenderFragment(FragmentDefinitionNode fragment, int indent) => RenderDescription(fragment.Description, indent)
        + "fragment " + Render(fragment.Name, indent) + RenderVariableDefinitions(fragment.VariableDefinitions, indent)
        + " on " + Render(fragment.TypeCondition, indent) + RenderDirectives(fragment.Directives, indent)
        + " " + Render(fragment.SelectionSet, indent);

    private static string RenderSelectionSet(SelectionSetNode selectionSet, int indent)
    {
        var pad = new string(' ', (indent + 1) * 2);
        return "{\n" + string.Join("\n", selectionSet.Selections.Select(selection => pad + Render(selection, indent + 1)))
            + "\n" + new string(' ', indent * 2) + "}";
    }

    private static string RenderField(FieldNode field, int indent)
    {
        var head = (field.Alias is null ? string.Empty : Render(field.Alias, indent) + ": ") + Render(field.Name, indent);
        head += RenderArguments(field.Arguments, indent, head.Length);
        head += RenderDirectives(field.Directives, indent);
        if (field.SelectionSet is not null) head += " " + Render(field.SelectionSet, indent);
        return head;
    }

    private static string RenderSchema(StringValueNode? description, IEnumerable<DirectiveNode> directives, IEnumerable<OperationTypeDefinitionNode> operations, int indent)
    {
        var body = RenderDefinitions(operations.ToArray(), indent);
        return RenderDescription(description, indent) + "schema" + RenderDirectives(directives, indent) + body;
    }

    private static string RenderDefinitions<TNode>(IReadOnlyList<TNode> definitions, int indent) where TNode : AstNode
    {
        if (definitions.Count == 0) return string.Empty;
        var pad = new string(' ', (indent + 1) * 2);
        return " {\n" + string.Join("\n", definitions.Select(definition => pad + Render(definition, indent + 1)))
            + "\n" + new string(' ', indent * 2) + "}";
    }

    private static string RenderArguments(IReadOnlyList<ArgumentNode> arguments, int indent, int prefixLength)
    {
        if (arguments.Count == 0) return string.Empty;
        var values = arguments.Select(argument => Render(argument, indent)).ToArray();
        var inline = "(" + string.Join(", ", values) + ")";
        if (inline.Length + (indent * 2) + prefixLength <= 80) return inline;
        var pad = new string(' ', (indent + 1) * 2);
        return "(\n" + string.Join("\n", values.Select(value => pad + value)) + "\n" + new string(' ', indent * 2) + ")";
    }

    private static string RenderInputArguments(IReadOnlyList<InputValueDefinitionNode> arguments, int indent)
    {
        if (arguments.Count == 0) return string.Empty;
        return "(" + string.Join(", ", arguments.Select(argument => Render(argument, indent))) + ")";
    }

    private static string RenderVariableDefinitions(IReadOnlyList<VariableDefinitionNode> definitions, int indent)
    {
        if (definitions.Count == 0) return string.Empty;
        var values = definitions.Select(definition => Render(definition, indent)).ToArray();
        var inline = "(" + string.Join(", ", values) + ")";
        if (inline.Length + (indent * 2) <= 80) return inline;
        var pad = new string(' ', (indent + 1) * 2);
        return "(\n" + string.Join("\n", values.Select(value => pad + value)) + "\n" + new string(' ', indent * 2) + ")";
    }

    private static string RenderDirectives(IEnumerable<DirectiveNode> directives, int indent)
    {
        var items = directives.Select(directive => Render(directive, indent));
        var text = string.Join(" ", items);
        return text.Length == 0 ? string.Empty : " " + text;
    }

    private static string RenderImplements(IReadOnlyList<NamedTypeNode> interfaces, int indent) => interfaces.Count == 0
        ? string.Empty
        : " implements " + string.Join(" & ", interfaces.Select(type => Render(type, indent)));

    private static string RenderUnionTypes(IReadOnlyList<NamedTypeNode> types, int indent) => types.Count == 0
        ? string.Empty
        : " = " + string.Join(" | ", types.Select(type => Render(type, indent)));

    private static string RenderDescription(StringValueNode? description, int indent)
    {
        if (description is null) return string.Empty;
        var value = Render(description, indent);
        var pad = new string(' ', indent * 2);
        return pad + value + "\n" + pad;
    }

    private static string OperationName(OperationType operation) => operation switch
    {
        OperationType.Query => "query",
        OperationType.Mutation => "mutation",
        OperationType.Subscription => "subscription",
        _ => throw new ArgumentOutOfRangeException(nameof(operation)),
    };
}
