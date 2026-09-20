using System.Text.Json.Nodes;
using GraphQLParser;

namespace GraphQLParser.Tests;

internal static class CanonicalAstJson
{
    public static JsonObject Project(AstNode node)
    {
        var result = new JsonObject { ["kind"] = Kind(node) };
        if (node.HasLocation) result["loc"] = Location(node.Location);
        switch (node)
        {
            case NameNode name:
                result["value"] = name.Value.ToString();
                break;
            case DocumentNode document:
                result["definitions"] = Nodes(document.Definitions);
                break;
            case OperationDefinitionNode operation:
                result["operation"] = Operation(operation.Operation);
                result["name"] = Node(operation.Name);
                result["variableDefinitions"] = Nodes(operation.VariableDefinitions);
                result["directives"] = Nodes(operation.Directives);
                result["selectionSet"] = Project(operation.SelectionSet);
                if (operation.Description is not null) result["description"] = Project(operation.Description);
                break;
            case FragmentDefinitionNode fragment:
                result["name"] = Project(fragment.Name);
                result["typeCondition"] = Project(fragment.TypeCondition);
                result["variableDefinitions"] = Nodes(fragment.VariableDefinitions);
                result["directives"] = Nodes(fragment.Directives);
                result["selectionSet"] = Project(fragment.SelectionSet);
                result["description"] = Node(fragment.Description);
                break;
            case SelectionSetNode selectionSet:
                result["selections"] = Nodes(selectionSet.Selections);
                break;
            case FieldNode field:
                result["alias"] = Node(field.Alias);
                result["name"] = Project(field.Name);
                result["arguments"] = Nodes(field.Arguments);
                result["directives"] = Nodes(field.Directives);
                result["selectionSet"] = Node(field.SelectionSet);
                break;
            case FragmentSpreadNode spread:
                result["name"] = Project(spread.Name);
                result["directives"] = Nodes(spread.Directives);
                break;
            case InlineFragmentNode inline:
                result["typeCondition"] = Node(inline.TypeCondition);
                result["directives"] = Nodes(inline.Directives);
                result["selectionSet"] = Project(inline.SelectionSet);
                break;
            case ArgumentNode argument:
                result["name"] = Project(argument.Name);
                result["value"] = Project(argument.Value);
                break;
            case DirectiveNode directive:
                result["name"] = Project(directive.Name);
                result["arguments"] = Nodes(directive.Arguments);
                break;
            case VariableDefinitionNode variableDefinition:
                result["variable"] = Project(variableDefinition.Variable);
                result["type"] = Project(variableDefinition.Type);
                result["defaultValue"] = Node(variableDefinition.DefaultValue);
                result["directives"] = Nodes(variableDefinition.Directives);
                if (variableDefinition.Description is not null) result["description"] = Project(variableDefinition.Description);
                break;
            case VariableNode variable:
                result["name"] = Project(variable.Name);
                break;
            case IntValueNode integer:
                result["value"] = integer.Value.ToString();
                break;
            case FloatValueNode floating:
                result["value"] = floating.Value.ToString();
                break;
            case StringValueNode text:
                result["value"] = text.Value.ToString();
                result["block"] = text.IsBlock;
                break;
            case BooleanValueNode boolean:
                result["value"] = boolean.Value;
                break;
            case NullValueNode:
                break;
            case EnumValueNode enumValue:
                result["value"] = enumValue.Value.ToString();
                break;
            case ListValueNode list:
                result["values"] = Nodes(list.Values);
                break;
            case ObjectValueNode objectValue:
                result["fields"] = Nodes(objectValue.Fields);
                break;
            case ObjectFieldNode objectField:
                result["name"] = Project(objectField.Name);
                result["value"] = Project(objectField.Value);
                break;
            case NamedTypeNode namedType:
                result["name"] = Project(namedType.Name);
                break;
            case ListTypeNode listType:
                result["type"] = Project(listType.Type);
                break;
            case NonNullTypeNode nonNull:
                result["type"] = Project(nonNull.Type);
                break;
            case SchemaDefinitionNode schema:
                result["description"] = Node(schema.Description);
                result["operationTypes"] = Nodes(schema.OperationTypes);
                result["directives"] = Nodes(schema.Directives);
                break;
            case SchemaExtensionNode schemaExtension:
                result["operationTypes"] = Nodes(schemaExtension.OperationTypes);
                result["directives"] = Nodes(schemaExtension.Directives);
                break;
            case OperationTypeDefinitionNode operationType:
                result["operation"] = Operation(operationType.Operation);
                result["type"] = Project(operationType.Type);
                break;
            case ScalarTypeDefinitionNode scalar:
                AddTypeDefinition(result, scalar);
                result["directives"] = Nodes(scalar.Directives);
                break;
            case ScalarTypeExtensionNode scalarExtension:
                AddTypeExtension(result, scalarExtension);
                result["directives"] = Nodes(scalarExtension.Directives);
                break;
            case ObjectTypeDefinitionNode objectType:
                AddTypeDefinition(result, objectType);
                result["interfaces"] = Nodes(objectType.Interfaces);
                result["directives"] = Nodes(objectType.Directives);
                result["fields"] = Nodes(objectType.Fields);
                break;
            case ObjectTypeExtensionNode objectExtension:
                AddTypeExtension(result, objectExtension);
                result["interfaces"] = Nodes(objectExtension.Interfaces);
                result["directives"] = Nodes(objectExtension.Directives);
                result["fields"] = Nodes(objectExtension.Fields);
                break;
            case InterfaceTypeDefinitionNode interfaceType:
                AddTypeDefinition(result, interfaceType);
                result["interfaces"] = Nodes(interfaceType.Interfaces);
                result["directives"] = Nodes(interfaceType.Directives);
                result["fields"] = Nodes(interfaceType.Fields);
                break;
            case InterfaceTypeExtensionNode interfaceExtension:
                AddTypeExtension(result, interfaceExtension);
                result["interfaces"] = Nodes(interfaceExtension.Interfaces);
                result["directives"] = Nodes(interfaceExtension.Directives);
                result["fields"] = Nodes(interfaceExtension.Fields);
                break;
            case UnionTypeDefinitionNode union:
                AddTypeDefinition(result, union);
                result["directives"] = Nodes(union.Directives);
                result["types"] = Nodes(union.Types);
                break;
            case UnionTypeExtensionNode unionExtension:
                AddTypeExtension(result, unionExtension);
                result["directives"] = Nodes(unionExtension.Directives);
                result["types"] = Nodes(unionExtension.Types);
                break;
            case EnumTypeDefinitionNode enumType:
                AddTypeDefinition(result, enumType);
                result["directives"] = Nodes(enumType.Directives);
                result["values"] = Nodes(enumType.Values);
                break;
            case EnumTypeExtensionNode enumExtension:
                AddTypeExtension(result, enumExtension);
                result["directives"] = Nodes(enumExtension.Directives);
                result["values"] = Nodes(enumExtension.Values);
                break;
            case InputObjectTypeDefinitionNode inputType:
                AddTypeDefinition(result, inputType);
                result["directives"] = Nodes(inputType.Directives);
                result["fields"] = Nodes(inputType.Fields);
                break;
            case InputObjectTypeExtensionNode inputExtension:
                AddTypeExtension(result, inputExtension);
                result["directives"] = Nodes(inputExtension.Directives);
                result["fields"] = Nodes(inputExtension.Fields);
                break;
            case FieldDefinitionNode fieldDefinition:
                result["description"] = Node(fieldDefinition.Description);
                result["name"] = Project(fieldDefinition.Name);
                result["arguments"] = Nodes(fieldDefinition.Arguments);
                result["type"] = Project(fieldDefinition.Type);
                result["directives"] = Nodes(fieldDefinition.Directives);
                break;
            case InputValueDefinitionNode inputValue:
                result["description"] = Node(inputValue.Description);
                result["name"] = Project(inputValue.Name);
                result["type"] = Project(inputValue.Type);
                result["defaultValue"] = Node(inputValue.DefaultValue);
                result["directives"] = Nodes(inputValue.Directives);
                break;
            case EnumValueDefinitionNode enumValueDefinition:
                result["description"] = Node(enumValueDefinition.Description);
                result["name"] = Project(enumValueDefinition.Name);
                result["directives"] = Nodes(enumValueDefinition.Directives);
                break;
            case DirectiveDefinitionNode directiveDefinition:
                result["description"] = Node(directiveDefinition.Description);
                result["name"] = Project(directiveDefinition.Name);
                result["arguments"] = Nodes(directiveDefinition.Arguments);
                result["directives"] = new JsonArray();
                result["repeatable"] = directiveDefinition.Repeatable;
                result["locations"] = Nodes(directiveDefinition.Locations);
                break;
            case TypeCoordinateNode coordinate:
                result["name"] = Project(coordinate.Name);
                break;
            case MemberCoordinateNode coordinate:
                result["name"] = Project(coordinate.Name);
                result["memberName"] = Project(coordinate.MemberName);
                break;
            case ArgumentCoordinateNode coordinate:
                result["name"] = Project(coordinate.Name);
                result["fieldName"] = Project(coordinate.FieldName);
                result["argumentName"] = Project(coordinate.ArgumentName);
                break;
            case DirectiveCoordinateNode coordinate:
                result["name"] = Project(coordinate.Name);
                break;
            case DirectiveArgumentCoordinateNode coordinate:
                result["name"] = Project(coordinate.Name);
                result["argumentName"] = Project(coordinate.ArgumentName);
                break;
            default:
                throw new NotSupportedException($"No canonical AST projection for {node.GetType().Name}.");
        }

        return result;
    }

    private static JsonNode? Node(AstNode? node) => node is null ? null : Project(node);
    private static JsonArray Nodes<T>(IEnumerable<T> nodes) where T : AstNode
    {
        var array = new JsonArray();
        foreach (var node in nodes) array.Add(Project(node));
        return array;
    }

    private static JsonArray Location(SourceLocation location) => new(location.Start, location.End);
    private static string Operation(OperationType operation) => operation switch
    {
        OperationType.Query => "query",
        OperationType.Mutation => "mutation",
        OperationType.Subscription => "subscription",
        _ => throw new ArgumentOutOfRangeException(nameof(operation)),
    };

    private static string Kind(AstNode node) => node.Kind.ToString();
    private static void AddTypeDefinition(JsonObject json, TypeDefinitionNode type)
    {
        json["description"] = Node(type.Description);
        json["name"] = Project(type.Name);
    }

    private static void AddTypeExtension(JsonObject json, TypeExtensionNode type) => json["name"] = Project(type.Name);
}
