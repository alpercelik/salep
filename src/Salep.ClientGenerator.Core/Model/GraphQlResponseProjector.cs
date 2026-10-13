namespace Salep.ClientGenerator.Model;

/// <summary>Expands fragments and abstract GraphQL types into target-neutral response shapes.</summary>
public sealed class GraphQlResponseProjector(GraphQlSchemaModel schema, GraphQlExecutableDocument document)
{
    /// <summary>Builds the operation response shape using schema roots, fields, and fragment conditions.</summary>
    public GraphQlResponseProjection Project(GraphQlOperationDefinition operation)
    {
        ArgumentNullException.ThrowIfNull(operation);
        var rootTypeName = RootTypeName(operation.OperationType);
        return ProjectObject(operation.SelectionSet, rootTypeName, new HashSet<string>(StringComparer.Ordinal));
    }

    /// <summary>Projects a fragment selection for one compatible concrete object type.</summary>
    public GraphQlResponseProjection ProjectFragment(GraphQlFragmentDefinition fragment, string concreteTypeName)
        => ProjectObject(fragment.SelectionSet, concreteTypeName, new HashSet<string>(StringComparer.Ordinal));

    private GraphQlResponseProjection ProjectObject(GraphQlSelectionSet selections, string objectTypeName, HashSet<string> fragmentPath)
    {
        var fields = CollectFields(selections, objectTypeName, fragmentPath);
        return new GraphQlResponseProjection(objectTypeName, fields.Select(field => ProjectField(field, objectTypeName, fragmentPath)).ToArray());
    }

    private GraphQlResponseFieldProjection ProjectField(
        IReadOnlyList<GraphQlFieldSelection> selections,
        string parentTypeName,
        HashSet<string> fragmentPath)
    {
        var selection = selections[0];
        if (StringComparer.Ordinal.Equals(selection.Name, "__typename"))
        {
            return new GraphQlResponseFieldProjection(selection.ResponseName, selection.Name,
                new GraphQlNamedTypeReference("String", selection.Source), [], selection.Source, selections.All(IsConditional));
        }

        var definition = FindField(parentTypeName, selection.Name);
        var childSelections = selections.Where(field => field.SelectionSet is not null)
            .SelectMany(field => field.SelectionSet!.Selections.Select(child => InheritConditions(child, field.Directives))).ToArray();
        var variants = Array.Empty<GraphQlResponseProjection>();
        if (definition is not null && childSelections.Length > 0 && NamedTypeName(definition.Type) is { } childTypeName)
        {
            var childSet = new GraphQlSelectionSet(childSelections, selection.Source);
            var concreteTypes = ConcreteTypes(childTypeName).ToArray();
            variants = concreteTypes.Select(concreteType => ProjectObject(childSet, concreteType, fragmentPath)).ToArray();
        }

        return new GraphQlResponseFieldProjection(selection.ResponseName, selection.Name, definition?.Type, variants, selection.Source, selections.All(IsConditional));
    }

    private IReadOnlyList<IReadOnlyList<GraphQlFieldSelection>> CollectFields(
        GraphQlSelectionSet selections,
        string concreteTypeName,
        HashSet<string> fragmentPath)
    {
        var ordered = new List<List<GraphQlFieldSelection>>();
        var byResponseName = new Dictionary<string, List<GraphQlFieldSelection>>(StringComparer.Ordinal);
        Visit(selections, fragmentPath, []);
        return ordered;

        void Visit(GraphQlSelectionSet selectionSet, HashSet<string> visitedFragments, IReadOnlyList<GraphQlDirective> inherited)
        {
            foreach (var selection in selectionSet.Selections)
            {
                if (GraphQlSelectionConditions.Excluded(inherited.Concat(selection.Directives).ToArray())) continue;
                switch (selection)
                {
                    case GraphQlFieldSelection selected:
                        var field = selected with { Directives = inherited.Concat(selected.Directives).ToArray() };
                        if (byResponseName.TryGetValue(field.ResponseName, out var existing))
                        {
                            existing.Add(field);
                        }
                        else
                        {
                            var group = new List<GraphQlFieldSelection> { field };
                            byResponseName.Add(field.ResponseName, group);
                            ordered.Add(group);
                        }

                        break;
                    case GraphQlInlineFragmentSelection inline when AppliesTo(inline.TypeCondition, concreteTypeName):
                        Visit(inline.SelectionSet, visitedFragments, inherited.Concat(inline.Directives).ToArray());
                        break;
                    case GraphQlFragmentSpreadSelection spread when visitedFragments.Add(spread.Name):
                        var fragment = document.Fragments.FirstOrDefault(item => StringComparer.Ordinal.Equals(item.Name, spread.Name));
                        if (fragment is not null && AppliesTo(fragment.TypeCondition, concreteTypeName))
                        {
                            Visit(fragment.SelectionSet, visitedFragments, inherited.Concat(spread.Directives).Concat(fragment.Directives).ToArray());
                        }

                        visitedFragments.Remove(spread.Name);
                        break;
                }
            }
        }
    }

    private static bool IsConditional(GraphQlFieldSelection selection)
        => GraphQlSelectionConditions.Conditional(selection.Directives);

    private static GraphQlSelection InheritConditions(GraphQlSelection selection, IReadOnlyList<GraphQlDirective> parent)
    {
        var directives = parent.Where(directive => directive.Name is "skip" or "include").Concat(selection.Directives).ToArray();
        return selection switch
        {
            GraphQlFieldSelection field => field with { Directives = directives },
            GraphQlInlineFragmentSelection inline => inline with { Directives = directives },
            GraphQlFragmentSpreadSelection spread => spread with { Directives = directives },
            _ => throw new InvalidDataException("Unknown selection kind.")
        };
    }

    private IEnumerable<string> ConcreteTypes(string typeName) => new GraphQlSchemaSemantics(schema).PossibleTypes(typeName);
    private bool AppliesTo(string? condition, string concreteTypeName) => new GraphQlSchemaSemantics(schema).Applies(condition, concreteTypeName);
    private GraphQlFieldDefinition? FindField(string typeName, string fieldName) => new GraphQlSchemaSemantics(schema).Fields(typeName).FirstOrDefault(field => field.Name == fieldName);
    private string RootTypeName(string operationType) => new GraphQlSchemaSemantics(schema).Root(operationType);

    private static string? NamedTypeName(GraphQlTypeReference type) => type switch
    {
        GraphQlNamedTypeReference named => named.Name,
        GraphQlListTypeReference list => NamedTypeName(list.ElementType),
        GraphQlNonNullTypeReference nonNull => NamedTypeName(nonNull.NullableType),
        _ => null
    };
}
