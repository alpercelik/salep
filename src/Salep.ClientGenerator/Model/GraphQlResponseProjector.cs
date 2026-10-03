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
                new GraphQlNamedTypeReference("String", selection.Source), [], selection.Source);
        }

        var definition = FindField(parentTypeName, selection.Name);
        var childSelections = selections.Where(field => field.SelectionSet is not null)
            .SelectMany(field => field.SelectionSet!.Selections).ToArray();
        var variants = Array.Empty<GraphQlResponseProjection>();
        if (definition is not null && childSelections.Length > 0 && NamedTypeName(definition.Type) is { } childTypeName)
        {
            var childSet = new GraphQlSelectionSet(childSelections, selection.Source);
            var concreteTypes = ConcreteTypes(childTypeName).ToArray();
            variants = concreteTypes.Select(concreteType => ProjectObject(childSet, concreteType, fragmentPath)).ToArray();
        }

        return new GraphQlResponseFieldProjection(selection.ResponseName, selection.Name, definition?.Type, variants, selection.Source);
    }

    private IReadOnlyList<IReadOnlyList<GraphQlFieldSelection>> CollectFields(
        GraphQlSelectionSet selections,
        string concreteTypeName,
        HashSet<string> fragmentPath)
    {
        var ordered = new List<List<GraphQlFieldSelection>>();
        var byResponseName = new Dictionary<string, List<GraphQlFieldSelection>>(StringComparer.Ordinal);
        Visit(selections, fragmentPath);
        return ordered;

        void Visit(GraphQlSelectionSet selectionSet, HashSet<string> visitedFragments)
        {
            foreach (var selection in selectionSet.Selections)
            {
                switch (selection)
                {
                    case GraphQlFieldSelection field:
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
                        Visit(inline.SelectionSet, visitedFragments);
                        break;
                    case GraphQlFragmentSpreadSelection spread when visitedFragments.Add(spread.Name):
                        var fragment = document.Fragments.FirstOrDefault(item => StringComparer.Ordinal.Equals(item.Name, spread.Name));
                        if (fragment is not null && AppliesTo(fragment.TypeCondition, concreteTypeName))
                        {
                            Visit(fragment.SelectionSet, visitedFragments);
                        }

                        visitedFragments.Remove(spread.Name);
                        break;
                }
            }
        }
    }

    private IEnumerable<string> ConcreteTypes(string typeName)
    {
        if (schema.Types.OfType<GraphQlObjectType>().Any(type => !type.IsExtension && StringComparer.Ordinal.Equals(type.Name, typeName)))
        {
            yield return typeName;
            yield break;
        }

        if (schema.Types.OfType<GraphQlInterfaceType>().Any(type => !type.IsExtension && StringComparer.Ordinal.Equals(type.Name, typeName)))
        {
            foreach (var objectType in schema.ObjectTypes.Where(type => ImplementsInterface(type.Name, typeName)).OrderBy(type => type.Source.Start))
            {
                yield return objectType.Name;
            }

            yield break;
        }

        foreach (var union in schema.Types.OfType<GraphQlUnionType>().Where(type => StringComparer.Ordinal.Equals(type.Name, typeName)))
        {
            foreach (var member in union.Members.Distinct(StringComparer.Ordinal))
            {
                yield return member;
            }
        }
    }

    private bool AppliesTo(string? condition, string concreteTypeName)
    {
        if (condition is null || StringComparer.Ordinal.Equals(condition, concreteTypeName))
        {
            return true;
        }

        if (schema.Types.OfType<GraphQlInterfaceType>().Any(type => !type.IsExtension && StringComparer.Ordinal.Equals(type.Name, condition)))
        {
            return ImplementsInterface(concreteTypeName, condition);
        }

        return schema.Types.OfType<GraphQlUnionType>()
            .Where(type => StringComparer.Ordinal.Equals(type.Name, condition))
            .Any(union => union.Members.Contains(concreteTypeName, StringComparer.Ordinal));
    }

    private bool ImplementsInterface(string objectTypeName, string targetInterface, HashSet<string>? visited = null)
    {
        visited ??= new HashSet<string>(StringComparer.Ordinal);
        if (!visited.Add(objectTypeName))
        {
            return false;
        }

        try
        {
            foreach (var objectType in schema.Types.OfType<GraphQlObjectType>().Where(type => StringComparer.Ordinal.Equals(type.Name, objectTypeName)))
            {
                foreach (var interfaceName in objectType.Interfaces)
                {
                    if (StringComparer.Ordinal.Equals(interfaceName, targetInterface) || ImplementsInterface(interfaceName, targetInterface, visited))
                    {
                        return true;
                    }
                }
            }

            foreach (var interfaceType in schema.Types.OfType<GraphQlInterfaceType>().Where(type => StringComparer.Ordinal.Equals(type.Name, objectTypeName)))
            {
                foreach (var interfaceName in interfaceType.Interfaces)
                {
                    if (StringComparer.Ordinal.Equals(interfaceName, targetInterface) || ImplementsInterface(interfaceName, targetInterface, visited))
                    {
                        return true;
                    }
                }
            }

            return false;
        }
        finally
        {
            visited.Remove(objectTypeName);
        }
    }

    private GraphQlFieldDefinition? FindField(string typeName, string fieldName)
        => schema.Types.OfType<GraphQlObjectType>().Where(type => StringComparer.Ordinal.Equals(type.Name, typeName))
            .SelectMany(type => type.Fields)
            .Concat(schema.Types.OfType<GraphQlInterfaceType>().Where(type => StringComparer.Ordinal.Equals(type.Name, typeName)).SelectMany(type => type.Fields))
            .FirstOrDefault(field => StringComparer.Ordinal.Equals(field.Name, fieldName));

    private string RootTypeName(string operationType)
    {
        var configured = schema.SchemaDefinitions
            .SelectMany(item => item.RootOperations)
            .FirstOrDefault(item => StringComparer.Ordinal.Equals(item.Operation, operationType))?.TypeName;
        if (!string.IsNullOrEmpty(configured))
        {
            return configured;
        }

        return operationType switch
        {
            "query" => "Query",
            "mutation" => "Mutation",
            "subscription" => "Subscription",
            _ => throw new ArgumentOutOfRangeException(nameof(operationType), operationType, "Unknown GraphQL operation type.")
        };
    }

    private static string? NamedTypeName(GraphQlTypeReference type) => type switch
    {
        GraphQlNamedTypeReference named => named.Name,
        GraphQlListTypeReference list => NamedTypeName(list.ElementType),
        GraphQlNonNullTypeReference nonNull => NamedTypeName(nonNull.NullableType),
        _ => null
    };
}
