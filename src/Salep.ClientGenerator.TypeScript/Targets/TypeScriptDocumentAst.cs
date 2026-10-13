using System.Text.Json;
using Salep.ClientGenerator.Model;

namespace Salep.ClientGenerator.TypeScript.Targets;

/// <summary>Projects Salep's neutral executable model into the standard GraphQL document AST wire shape.</summary>
public static class TypeScriptDocumentAst
{
    private static Dictionary<string, object?> Node(string kind, params (string Name, object? Value)[] values)
    {
        var node = new Dictionary<string, object?> { ["kind"] = kind };
        foreach (var (name, value) in values) if (value is not null) node[name] = value;
        return node;
    }
    private static object Name(string name) => Node("Name", ("value", name));
    private static object Type(GraphQlTypeReference type) => type switch
    {
        GraphQlNamedTypeReference named => Node("NamedType", ("name", Name(named.Name))),
        GraphQlListTypeReference list => Node("ListType", ("type", Type(list.ElementType))),
        GraphQlNonNullTypeReference required => Node("NonNullType", ("type", Type(required.NullableType))),
        _ => throw new InvalidDataException("Unknown GraphQL type wrapper.")
    };
    private static object Value(GraphQlValue value) => value.Kind switch
    {
        GraphQlValueKind.Variable => Node("Variable", ("name", Name(value.Text!))),
        GraphQlValueKind.IntegerLiteral => Node("IntValue", ("value", value.Text)),
        GraphQlValueKind.FloatLiteral => Node("FloatValue", ("value", value.Text)),
        GraphQlValueKind.StringLiteral => Node("StringValue", ("value", value.Text), ("block", value.IsBlockString)),
        GraphQlValueKind.BooleanLiteral => Node("BooleanValue", ("value", value.Boolean)),
        GraphQlValueKind.NullLiteral => Node("NullValue"),
        GraphQlValueKind.EnumLiteral => Node("EnumValue", ("value", value.Text)),
        GraphQlValueKind.ListLiteral => Node("ListValue", ("values", value.Items!.Select(Value).ToArray())),
        GraphQlValueKind.ObjectLiteral => Node("ObjectValue", ("fields", value.Fields!.Select(field => Node("ObjectField", ("name", Name(field.Name)), ("value", Value(field.Value)))).ToArray())),
        _ => throw new InvalidDataException("Unknown GraphQL literal.")
    };
    private static object Arguments(IReadOnlyList<GraphQlArgument> arguments) => arguments.Select(argument => Node("Argument", ("name", Name(argument.Name)), ("value", Value(argument.Value)))).ToArray();
    private static object Directives(IReadOnlyList<GraphQlDirective> directives) => directives.Select(directive => Node("Directive", ("name", Name(directive.Name)), ("arguments", Arguments(directive.Arguments)))).ToArray();
    private static object Selections(GraphQlSelectionSet selections) => Node("SelectionSet", ("selections", selections.Selections.Select(Selection).ToArray()));
    private static object Selection(GraphQlSelection selection) => selection switch
    {
        GraphQlFieldSelection field => Node("Field", ("name", Name(field.Name)), ("alias", field.Alias is null ? null : Name(field.Alias)), ("arguments", Arguments(field.Arguments)), ("directives", Directives(field.Directives)), ("selectionSet", field.SelectionSet is null ? null : Selections(field.SelectionSet))),
        GraphQlInlineFragmentSelection inline => Node("InlineFragment", ("typeCondition", inline.TypeCondition is null ? null : Node("NamedType", ("name", Name(inline.TypeCondition)))), ("directives", Directives(inline.Directives)), ("selectionSet", Selections(inline.SelectionSet))),
        GraphQlFragmentSpreadSelection spread => Node("FragmentSpread", ("name", Name(spread.Name)), ("directives", Directives(spread.Directives))),
        _ => throw new InvalidDataException("Unknown selection.")
    };
    private static object Fragment(GraphQlFragmentDefinition fragment) => Node("FragmentDefinition", ("name", Name(fragment.Name)), ("typeCondition", Node("NamedType", ("name", Name(fragment.TypeCondition)))), ("directives", Directives(fragment.Directives)), ("selectionSet", Selections(fragment.SelectionSet)));

    public static string Operation(GraphQlExecutableDocument normalized, string query, string? operationId = null, object? localExecution = null)
    {
        var operation = normalized.Operations.Single();
        var definitions = new List<object> { Node("OperationDefinition", ("operation", operation.OperationType), ("name", Name(operation.Name!)),
            ("variableDefinitions", operation.Variables.Select(variable => Node("VariableDefinition", ("variable", Node("Variable", ("name", Name(variable.Name)))), ("type", Type(variable.Type)), ("defaultValue", variable.DefaultValue is null ? null : Value(variable.DefaultValue)), ("directives", Directives(variable.Directives)))).ToArray()),
            ("directives", Directives(operation.Directives)), ("selectionSet", Selections(operation.SelectionSet))) };
        definitions.AddRange(normalized.Fragments.Select(Fragment));
        return JsonSerializer.Serialize(Node("Document", ("definitions", definitions), ("__meta__", new { query, operationName = operation.Name, operationType = operation.OperationType, operationId, localExecution })));
    }

    public static string FragmentDocument(GraphQlFragmentDefinition fragment, IReadOnlyList<GraphQlFragmentDefinition> dependencies)
        => JsonSerializer.Serialize(Node("Document", ("definitions", new[] { Fragment(fragment) }.Concat(dependencies.Select(Fragment)).ToArray())));
}
