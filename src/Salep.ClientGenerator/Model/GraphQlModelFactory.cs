using Salep.GraphQLParser;

namespace Salep.ClientGenerator.Model;

/// <summary>Builds immutable, language-neutral models from Salep GraphQL parser documents.</summary>
public static class GraphQlModelFactory
{
    /// <summary>Projects schema definitions and extensions without adding target-language decisions.</summary>
    public static GraphQlSchemaModel CreateSchema(DocumentNode document)
    {
        ArgumentNullException.ThrowIfNull(document);
        var types = new List<GraphQlSchemaType>();
        var schemas = new List<GraphQlSchemaDefinition>();
        var directiveDefinitions = new List<GraphQlDirectiveDefinition>();

        foreach (var definition in document.Definitions)
        {
            switch (definition)
            {
                case ScalarTypeDefinitionNode node:
                    types.Add(new GraphQlScalarType(node.Name.Value, Description(node.Description), Directives(node.Directives), false, Span(node)));
                    break;
                case ScalarTypeExtensionNode node:
                    types.Add(new GraphQlScalarType(node.Name.Value, null, Directives(node.Directives), true, Span(node)));
                    break;
                case ObjectTypeDefinitionNode node:
                    types.Add(new GraphQlObjectType(node.Name.Value, Description(node.Description), Fields(node.Fields), Names(node.Interfaces), Directives(node.Directives), false, Span(node)));
                    break;
                case ObjectTypeExtensionNode node:
                    types.Add(new GraphQlObjectType(node.Name.Value, null, Fields(node.Fields), Names(node.Interfaces), Directives(node.Directives), true, Span(node)));
                    break;
                case InterfaceTypeDefinitionNode node:
                    types.Add(new GraphQlInterfaceType(node.Name.Value, Description(node.Description), Fields(node.Fields), Names(node.Interfaces), Directives(node.Directives), false, Span(node)));
                    break;
                case InterfaceTypeExtensionNode node:
                    types.Add(new GraphQlInterfaceType(node.Name.Value, null, Fields(node.Fields), Names(node.Interfaces), Directives(node.Directives), true, Span(node)));
                    break;
                case UnionTypeDefinitionNode node:
                    types.Add(new GraphQlUnionType(node.Name.Value, Description(node.Description), Names(node.Types), Directives(node.Directives), false, Span(node)));
                    break;
                case UnionTypeExtensionNode node:
                    types.Add(new GraphQlUnionType(node.Name.Value, null, Names(node.Types), Directives(node.Directives), true, Span(node)));
                    break;
                case EnumTypeDefinitionNode node:
                    types.Add(new GraphQlEnumType(node.Name.Value, Description(node.Description), EnumValues(node.Values), Directives(node.Directives), false, Span(node)));
                    break;
                case EnumTypeExtensionNode node:
                    types.Add(new GraphQlEnumType(node.Name.Value, null, EnumValues(node.Values), Directives(node.Directives), true, Span(node)));
                    break;
                case InputObjectTypeDefinitionNode node:
                    types.Add(new GraphQlInputObjectType(node.Name.Value, Description(node.Description), InputValues(node.Fields), Directives(node.Directives), false, Span(node)));
                    break;
                case InputObjectTypeExtensionNode node:
                    types.Add(new GraphQlInputObjectType(node.Name.Value, null, InputValues(node.Fields), Directives(node.Directives), true, Span(node)));
                    break;
                case SchemaDefinitionNode node:
                    schemas.Add(new GraphQlSchemaDefinition(Description(node.Description), RootOperations(node.OperationTypes), Directives(node.Directives), false, Span(node)));
                    break;
                case SchemaExtensionNode node:
                    schemas.Add(new GraphQlSchemaDefinition(null, RootOperations(node.OperationTypes), Directives(node.Directives), true, Span(node)));
                    break;
                case DirectiveDefinitionNode node:
                    directiveDefinitions.Add(new GraphQlDirectiveDefinition(node.Name.Value, Description(node.Description), InputValues(node.Arguments), node.Repeatable,
                        Names(node.Locations), Directives(node.Directives), Span(node)));
                    break;
            }
        }

        return new GraphQlSchemaModel(types.ToArray(), schemas.ToArray(), directiveDefinitions.ToArray());
    }

    /// <summary>Projects operations and fragments from one or more executable documents.</summary>
    public static GraphQlExecutableDocument CreateExecutable(IEnumerable<DocumentNode> documents)
    {
        ArgumentNullException.ThrowIfNull(documents);
        var operations = new List<GraphQlOperationDefinition>();
        var fragments = new List<GraphQlFragmentDefinition>();
        foreach (var document in documents)
        {
            ArgumentNullException.ThrowIfNull(document);
            foreach (var definition in document.Definitions)
            {
                switch (definition)
                {
                    case OperationDefinitionNode operation:
                        operations.Add(new GraphQlOperationDefinition(OperationName(operation.Operation), operation.Name?.Value,
                            operation.VariableDefinitions.Select(Variable).ToArray(), Directives(operation.Directives), SelectionSet(operation.SelectionSet), Span(operation)));
                        break;
                    case FragmentDefinitionNode fragment:
                        fragments.Add(new GraphQlFragmentDefinition(fragment.Name.Value, fragment.TypeCondition.Name.Value,
                            fragment.VariableDefinitions.Select(Variable).ToArray(), Directives(fragment.Directives), SelectionSet(fragment.SelectionSet),
                            Description(fragment.Description), Span(fragment)));
                        break;
                }
            }
        }

        return new GraphQlExecutableDocument(operations.ToArray(), fragments.ToArray());
    }

    /// <summary>Projects executable documents and expands their response projections against a schema.</summary>
    public static GraphQlExecutableDocument CreateExecutable(GraphQlSchemaModel schema, IEnumerable<DocumentNode> documents)
    {
        ArgumentNullException.ThrowIfNull(schema);
        var executable = CreateExecutable(documents);
        var projector = new GraphQlResponseProjector(schema, executable);
        return executable with
        {
            Operations = executable.Operations
                .Select(operation => operation with { ResponseProjection = projector.Project(operation) })
                .ToArray()
        };
    }

    /// <summary>Converts a parser type reference to named/list/non-null language-neutral wrappers.</summary>
    public static GraphQlTypeReference Type(ITypeNode type)
    {
        ArgumentNullException.ThrowIfNull(type);
        return type switch
        {
            NamedTypeNode named => new GraphQlNamedTypeReference(named.Name.Value, Span(named)),
            ListTypeNode list => new GraphQlListTypeReference(Type(list.Type), Span(list)),
            NonNullTypeNode nonNull => new GraphQlNonNullTypeReference(Type(nonNull.Type), Span(nonNull)),
            _ => throw new NotSupportedException($"Unsupported GraphQL type node '{type.GetType().Name}'.")
        };
    }

    private static IReadOnlyList<GraphQlFieldDefinition> Fields(IEnumerable<FieldDefinitionNode> fields)
        => fields.Select(field => new GraphQlFieldDefinition(field.Name.Value, Description(field.Description), InputValues(field.Arguments),
            Type(field.Type), Directives(field.Directives), Span(field))).ToArray();

    private static IReadOnlyList<GraphQlInputValueDefinition> InputValues(IEnumerable<InputValueDefinitionNode> values)
        => values.Select(value => new GraphQlInputValueDefinition(value.Name.Value, Description(value.Description), Type(value.Type),
            Value(value.DefaultValue), Directives(value.Directives), Span(value))).ToArray();

    private static IReadOnlyList<GraphQlEnumValueDefinition> EnumValues(IEnumerable<EnumValueDefinitionNode> values)
        => values.Select(value => new GraphQlEnumValueDefinition(value.Name.Value, Description(value.Description), Directives(value.Directives), Span(value))).ToArray();

    private static IReadOnlyList<GraphQlRootOperation> RootOperations(IEnumerable<OperationTypeDefinitionNode> values)
        => values.Select(value => new GraphQlRootOperation(OperationName(value.Operation), value.Type.Name.Value, Span(value))).ToArray();

    private static string OperationName(OperationType operation) => operation switch
    {
        OperationType.Query => "query",
        OperationType.Mutation => "mutation",
        OperationType.Subscription => "subscription",
        _ => throw new ArgumentOutOfRangeException(nameof(operation), operation, "Unknown GraphQL operation type.")
    };

    private static IReadOnlyList<string> Names(IEnumerable<NamedTypeNode> values) => values.Select(value => value.Name.Value).ToArray();
    private static IReadOnlyList<string> Names(IEnumerable<NameNode> values) => values.Select(value => value.Value.ToString()).ToArray();

    private static IReadOnlyList<GraphQlDirective> Directives(IEnumerable<DirectiveNode> directives)
        => directives.Select(directive => new GraphQlDirective(directive.Name.Value, Arguments(directive.Arguments), Span(directive))).ToArray();

    private static IReadOnlyList<GraphQlArgument> Arguments(IEnumerable<ArgumentNode> arguments)
        => arguments.Select(argument => new GraphQlArgument(argument.Name.Value, Value(argument.Value)!, Span(argument))).ToArray();

    private static GraphQlVariableDefinition Variable(VariableDefinitionNode variable)
        => new(variable.Variable.Name.Value, Type(variable.Type), Value(variable.DefaultValue), Directives(variable.Directives),
            Description(variable.Description), Span(variable));

    private static GraphQlSelectionSet SelectionSet(SelectionSetNode selectionSet)
        => new(selectionSet.Selections.Select(Selection).ToArray(), Span(selectionSet));

    private static GraphQlSelection Selection(ISelectionNode selection)
        => selection switch
        {
            FieldNode field => new GraphQlFieldSelection(field.Name.Value, field.Alias?.Value, Arguments(field.Arguments), Directives(field.Directives),
                field.SelectionSet is null ? null : SelectionSet(field.SelectionSet), Span(field)),
            FragmentSpreadNode spread => new GraphQlFragmentSpreadSelection(spread.Name.Value, Arguments(spread.Arguments), Directives(spread.Directives), Span(spread)),
            InlineFragmentNode inline => new GraphQlInlineFragmentSelection(inline.TypeCondition?.Name.Value, Directives(inline.Directives), SelectionSet(inline.SelectionSet), Span(inline)),
            _ => throw new NotSupportedException($"Unsupported GraphQL selection node '{selection.GetType().Name}'.")
        };

    private static GraphQlValue? Value(IValueNode? value)
    {
        if (value is null) return null;
        return value switch
        {
            VariableNode variable => new(GraphQlValueKind.Variable, Text: variable.Name.Value, Source: Span(variable)),
            IntValueNode integer => new(GraphQlValueKind.IntegerLiteral, Text: integer.Value, Source: Span(integer)),
            FloatValueNode floating => new(GraphQlValueKind.FloatLiteral, Text: floating.Value, Source: Span(floating)),
            StringValueNode text => new(GraphQlValueKind.StringLiteral, Text: text.Value, IsBlockString: text.IsBlock, Source: Span(text)),
            BooleanValueNode boolean => new(GraphQlValueKind.BooleanLiteral, Boolean: boolean.Value, Source: Span(boolean)),
            NullValueNode nullValue => new(GraphQlValueKind.NullLiteral, Source: Span(nullValue)),
            EnumValueNode enumeration => new(GraphQlValueKind.EnumLiteral, Text: enumeration.Value, Source: Span(enumeration)),
            ListValueNode list => new(GraphQlValueKind.ListLiteral, Items: list.Values.Select(item => Value(item)!).ToArray(), Source: Span(list)),
            ObjectValueNode obj => new(GraphQlValueKind.ObjectLiteral, Fields: obj.Fields.Select(field => new GraphQlObjectField(field.Name.Value, Value(field.Value)!, Span(field))).ToArray(), Source: Span(obj)),
            _ => throw new NotSupportedException($"Unsupported GraphQL value node '{value.GetType().Name}'.")
        };
    }

    private static string? Description(StringValueNode? description) => description?.Value;

    private static GraphQlSourceSpan Span(AstNode node)
        => new(node.Location.Start, node.Location.End, node.Location.Line, node.Location.Column, node.HasLocation);
}
