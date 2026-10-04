using Salep.ClientGenerator.Model;

namespace Salep.ClientGenerator.TypeScript.Generation;

/// <summary>Separates local selections from the document sent to a GraphQL server.</summary>
public sealed class TypeScriptClientDocumentPlanner(GraphQlSchemaModel schema, GraphQlExecutableDocument document)
{
    public void ValidateLocalSelections(IReadOnlyList<GraphQlSchemaType> localTypes)
    {
        var fields = localTypes.SelectMany(type => type switch
        {
            GraphQlObjectType obj => obj.Fields.Select(field => (obj.Name, field.Name)),
            GraphQlInterfaceType face => face.Fields.Select(field => (face.Name, field.Name)),
            _ => Enumerable.Empty<(string, string)>()
        }).ToHashSet();
        var semantics = new GraphQlSchemaSemantics(schema);
        void Validate(GraphQlSelectionSet set, string parent, bool local, HashSet<string> active)
        {
            foreach (var selection in set.Selections)
            {
                var selectedLocal = local || selection.Directives.Any(directive => directive.Name == "client");
                switch (selection)
                {
                    case GraphQlFieldSelection field:
                        if (fields.Contains((parent, field.Name)) && !selectedLocal)
                            throw new InvalidDataException(field.Source.Diagnostic($"Local field '{parent}.{field.Name}' requires @client."));
                        if (field.SelectionSet is not null)
                        {
                            var definition = semantics.Fields(parent).Single(item => item.Name == field.Name);
                            Validate(field.SelectionSet, GraphQlSchemaSemantics.NamedType(definition.Type), selectedLocal, active);
                        }
                        break;
                    case GraphQlInlineFragmentSelection inline:
                        Validate(inline.SelectionSet, inline.TypeCondition ?? parent, selectedLocal, active);
                        break;
                    case GraphQlFragmentSpreadSelection spread when active.Add(spread.Name):
                        var fragment = document.Fragments.Single(item => item.Name == spread.Name);
                        Validate(fragment.SelectionSet, fragment.TypeCondition, selectedLocal, active);
                        active.Remove(spread.Name);
                        break;
                }
            }
        }
        foreach (var operation in document.Operations) Validate(operation.SelectionSet, semantics.Root(operation.OperationType), false, new(StringComparer.Ordinal));
    }

    public bool HasLocalSelections(GraphQlOperationDefinition operation)
    {
        var visited = new HashSet<string>(StringComparer.Ordinal);
        bool Has(GraphQlSelectionSet set) => set.Selections.Any(selection => selection.Directives.Any(directive => directive.Name == "client") || selection switch
        {
            GraphQlFieldSelection { SelectionSet: not null } field => Has(field.SelectionSet),
            GraphQlInlineFragmentSelection inline => Has(inline.SelectionSet),
            GraphQlFragmentSpreadSelection spread when visited.Add(spread.Name) => Has(document.Fragments.Single(fragment => fragment.Name == spread.Name).SelectionSet),
            _ => false
        });
        return Has(operation.SelectionSet);
    }

    /// <summary>Returns null for an entirely local operation; otherwise preserves the remote parent shape.</summary>
    public string? RemoteQuery(GraphQlOperationDefinition operation)
    {
        var selections = Remote(operation.SelectionSet, new HashSet<string>(StringComparer.Ordinal));
        if (selections.Selections.Count == 0) return null;
        var remote = operation with { SelectionSet = selections, ResponseProjection = null };
        return new GraphQlOperationDocumentFormatter(schema, new([remote], [])).Format(remote, new() { OmitUnusedVariables = true });
    }

    /// <summary>Builds a serializable local execution plan over the complete selected response.</summary>
    public object Plan(GraphQlOperationDefinition operation) => Selections(operation.SelectionSet, new GraphQlSchemaSemantics(schema).Root(operation.OperationType), new(StringComparer.Ordinal));

    public static object InputValue(GraphQlValue value) => value.Kind switch
    {
        GraphQlValueKind.Variable => new { kind = "variable", name = value.Text },
        GraphQlValueKind.ListLiteral => new { kind = "list", items = value.Items!.Select(InputValue).ToArray() },
        GraphQlValueKind.ObjectLiteral => new { kind = "object", fields = value.Fields!.ToDictionary(field => field.Name, field => InputValue(field.Value)) },
        GraphQlValueKind.IntegerLiteral or GraphQlValueKind.FloatLiteral => new { kind = "literal", value = (object)double.Parse(value.Text!, System.Globalization.CultureInfo.InvariantCulture) },
        GraphQlValueKind.BooleanLiteral => new { kind = "literal", value = (object?)value.Boolean },
        GraphQlValueKind.NullLiteral => new { kind = "literal", value = (object?)null },
        _ => new { kind = "literal", value = (object?)value.Text }
    };

    private object[] Selections(GraphQlSelectionSet set, string parent, HashSet<string> active, bool inheritedLocal = false)
    {
        var semantics = new GraphQlSchemaSemantics(schema);
        object[] Conditions(GraphQlSelection selection, string directive) => selection.Directives.Where(item => item.Name == directive)
            .Select(item => InputValue(item.Arguments.Single(argument => argument.Name == "if").Value)).ToArray();
        return set.Selections.Select(selection =>
        {
            switch (selection)
            {
                case GraphQlFieldSelection field:
                    var definition = semantics.Fields(parent).FirstOrDefault(item => item.Name == field.Name);
                    var argumentDefaults = definition?.Arguments.Where(argument => argument.DefaultValue is not null).ToDictionary(argument => argument.Name, argument => InputValue(argument.DefaultValue!)) ?? new Dictionary<string, object>();
                    var arguments = new Dictionary<string, object>(argumentDefaults, StringComparer.Ordinal);
                    foreach (var argument in field.Arguments) arguments[argument.Name] = InputValue(argument.Value);
                    return (object)new
                    {
                        name = field.Name, responseName = field.ResponseName, parentType = parent,
                        local = inheritedLocal || field.Directives.Any(item => item.Name == "client"), arguments, argumentDefaults,
                        include = Conditions(field, "include"), skip = Conditions(field, "skip"),
                        children = field.SelectionSet is null ? [] : Selections(field.SelectionSet, GraphQlSchemaSemantics.NamedType(definition!.Type), active)
                    };
                case GraphQlInlineFragmentSelection inline:
                    return new { parentType = parent, typeCondition = inline.TypeCondition, possibleTypes = inline.TypeCondition is null ? [] : semantics.PossibleTypes(inline.TypeCondition), include = Conditions(inline, "include"), skip = Conditions(inline, "skip"), children = Selections(inline.SelectionSet, inline.TypeCondition ?? parent, active, inheritedLocal || inline.Directives.Any(item => item.Name == "client")) };
                case GraphQlFragmentSpreadSelection spread:
                    if (!active.Add(spread.Name)) throw new InvalidDataException(spread.Source.Diagnostic("Fragment cycle in local plan."));
                    var fragment = document.Fragments.Single(item => item.Name == spread.Name);
                    var children = Selections(fragment.SelectionSet, fragment.TypeCondition, active, inheritedLocal || spread.Directives.Any(item => item.Name == "client"));
                    active.Remove(spread.Name);
                    return new { parentType = parent, typeCondition = fragment.TypeCondition, possibleTypes = semantics.PossibleTypes(fragment.TypeCondition), include = Conditions(spread, "include"), skip = Conditions(spread, "skip"), children };
                default: throw new InvalidDataException("Unknown local selection.");
            }
        }).ToArray();
    }

    private GraphQlSelectionSet Remote(GraphQlSelectionSet set, HashSet<string> active)
    {
        var selections = new List<GraphQlSelection>();
        foreach (var selection in set.Selections)
        {
            if (selection.Directives.Any(directive => directive.Name == "client")) continue;
            switch (selection)
            {
                case GraphQlFieldSelection field:
                    if (field.SelectionSet is null) selections.Add(field);
                    else
                    {
                        var children = Remote(field.SelectionSet, active);
                        // A remote parent is needed even if every selected child is resolved locally.
                        if (!children.Selections.OfType<GraphQlFieldSelection>().Any(item => item.Name == "__typename" && item.Alias is null))
                            children = children with { Selections = children.Selections.Append(new GraphQlFieldSelection("__typename", null, [], [], null, field.Source)).ToArray() };
                        selections.Add(field with { SelectionSet = children });
                    }
                    break;
                case GraphQlInlineFragmentSelection inline:
                    var nested = Remote(inline.SelectionSet, active);
                    if (nested.Selections.Count > 0) selections.Add(inline with { SelectionSet = nested });
                    break;
                case GraphQlFragmentSpreadSelection spread:
                    if (!active.Add(spread.Name)) throw new InvalidDataException(spread.Source.Diagnostic("Fragment cycle in local document."));
                    var fragment = document.Fragments.FirstOrDefault(item => item.Name == spread.Name)
                        ?? throw new InvalidDataException(spread.Source.Diagnostic("Unknown local document fragment."));
                    if (!fragment.Directives.Any(directive => directive.Name == "client"))
                    {
                        var fields = Remote(fragment.SelectionSet, active);
                        if (fields.Selections.Count > 0)
                            selections.Add(new GraphQlInlineFragmentSelection(fragment.TypeCondition, spread.Directives, fields, spread.Source));
                    }
                    active.Remove(spread.Name);
                    break;
            }
        }
        return set with { Selections = selections };
    }
}
