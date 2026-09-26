using Salep.GraphQLParser;
using Salep.ClientGenerator.Config;

namespace Salep.ClientGenerator.Emission;

internal sealed class OperationVariablePolicies(GeneratorConfig config, IReadOnlyList<FragmentDefinitionNode> fragments)
{
    private readonly IReadOnlyDictionary<string, FragmentDefinitionNode> _fragmentsByName = fragments
        .GroupBy(fragment => fragment.Name.Value, StringComparer.Ordinal)
        .ToDictionary(group => group.Key, group => group.First(), StringComparer.Ordinal);

    public OperationDefinitionNode Apply(OperationDefinitionNode operation)
    {
        if (config is { OmitUnusedVariables: false, InlineDefaultVariables: false })
        {
            return operation;
        }

        var hasFragmentSpreads = HasFragmentSpreads(operation.SelectionSet);
        if (config.InlineDefaultVariables && !hasFragmentSpreads)
        {
            var defaults = operation.VariableDefinitions
                .Where(def => def.DefaultValue is not null)
                .ToDictionary(def => def.Variable.Name.Value, def => def.DefaultValue!);

            if (defaults.Count > 0)
            {
                var updatedSelection = ReplaceVariables(operation.SelectionSet, defaults);
                var updatedDirectives = ReplaceVariables(operation.Directives, defaults);
                operation = operation
                    .WithSelectionSet(updatedSelection)
                    .WithDirectives(updatedDirectives)
                    .WithVariableDefinitions([
                        .. operation.VariableDefinitions
                            .Where(def => !defaults.ContainsKey(def.Variable.Name.Value))
                    ]);
            }
        }

        if (!config.OmitUnusedVariables) return operation;

        var usedVariables = CollectUsedVariables(operation);
        CollectVariablesFromReferencedFragments(operation.SelectionSet, usedVariables, new HashSet<string>(StringComparer.Ordinal));
        operation = operation.WithVariableDefinitions([
            .. operation.VariableDefinitions
                .Where(def => usedVariables.Contains(def.Variable.Name.Value))
        ]);

        return operation;
    }

    private void CollectVariablesFromReferencedFragments(
        SelectionSetNode selectionSet,
        HashSet<string> usedVariables,
        HashSet<string> visitedFragments)
    {
        var fragmentNames = new HashSet<string>(StringComparer.Ordinal);
        CollectFragmentSpreads(selectionSet, fragmentNames);
        foreach (var name in fragmentNames)
        {
            if (!visitedFragments.Add(name) || !_fragmentsByName.TryGetValue(name, out var fragment)) continue;

            CollectUsedVariables(fragment.Directives, usedVariables);
            CollectUsedVariables(fragment.SelectionSet, usedVariables);
            CollectVariablesFromReferencedFragments(fragment.SelectionSet, usedVariables, visitedFragments);
        }
    }

    private static bool HasFragmentSpreads(SelectionSetNode selectionSet)
    {
        foreach (var selection in selectionSet.Selections)
        {
            switch (selection)
            {
                case FragmentSpreadNode:
                    return true;
                case FieldNode { SelectionSet: not null } field:
                    if (HasFragmentSpreads(field.SelectionSet))
                    {
                        return true;
                    }

                    break;
                case InlineFragmentNode fragment:
                    if (HasFragmentSpreads(fragment.SelectionSet))
                    {
                        return true;
                    }

                    break;
            }
        }

        return false;
    }

    private static HashSet<string> CollectUsedVariables(OperationDefinitionNode operation)
    {
        var used = new HashSet<string>(StringComparer.Ordinal);

        CollectUsedVariables(operation.Directives, used);
        CollectUsedVariables(operation.SelectionSet, used);

        return used;
    }

    private static void CollectUsedVariables(SelectionSetNode selectionSet, HashSet<string> used)
    {
        foreach (var selection in selectionSet.Selections)
        {
            switch (selection)
            {
                case FieldNode field:
                    CollectUsedVariables(field.Arguments, used);
                    CollectUsedVariables(field.Directives, used);
                    if (field.SelectionSet is not null)
                    {
                        CollectUsedVariables(field.SelectionSet, used);
                    }

                    break;
                case InlineFragmentNode fragment:
                    CollectUsedVariables(fragment.Directives, used);
                    CollectUsedVariables(fragment.SelectionSet, used);
                    break;
                case FragmentSpreadNode spread:
                    CollectUsedVariables(spread.Directives, used);
                    break;
            }
        }
    }

    private static void CollectUsedVariables(IReadOnlyList<DirectiveNode> directives, HashSet<string> used)
    {
        foreach (var directive in directives)
        {
            CollectUsedVariables(directive.Arguments, used);
        }
    }

    private static void CollectUsedVariables(IReadOnlyList<ArgumentNode> arguments, HashSet<string> used)
    {
        foreach (var argument in arguments)
        {
            CollectUsedVariables(argument.Value, used);
        }
    }

    private static void CollectUsedVariables(IValueNode value, HashSet<string> used)
    {
        switch (value)
        {
            case VariableNode variable:
                used.Add(variable.Name.Value);
                break;
            case ListValueNode list:
                foreach (var item in list.Items)
                {
                    CollectUsedVariables(item, used);
                }

                break;
            case ObjectValueNode obj:
                foreach (var field in obj.Fields)
                {
                    CollectUsedVariables(field.Value, used);
                }

                break;
        }
    }

    private static SelectionSetNode ReplaceVariables(
        SelectionSetNode selectionSet,
        IReadOnlyDictionary<string, IValueNode> defaults)
    {
        var selections = new List<ISelectionNode>();
        foreach (var selection in selectionSet.Selections)
        {
            selections.Add(selection switch
            {
                FieldNode field => ReplaceVariables(field, defaults),
                InlineFragmentNode fragment => ReplaceVariables(fragment, defaults),
                FragmentSpreadNode spread => ReplaceVariables(spread, defaults),
                _ => selection
            });
        }

        return selectionSet.WithSelections(selections);
    }

    private static FieldNode ReplaceVariables(FieldNode field, IReadOnlyDictionary<string, IValueNode> defaults)
    {
        var arguments = ReplaceVariables(field.Arguments, defaults);
        var directives = ReplaceVariables(field.Directives, defaults);
        var selectionSet = field.SelectionSet is null
            ? null
            : ReplaceVariables(field.SelectionSet, defaults);

        return field.WithArguments(arguments)
            .WithDirectives(directives)
            .WithSelectionSet(selectionSet);
    }

    private static InlineFragmentNode ReplaceVariables(InlineFragmentNode fragment,
        IReadOnlyDictionary<string, IValueNode> defaults)
    {
        var directives = ReplaceVariables(fragment.Directives, defaults);
        var selectionSet = ReplaceVariables(fragment.SelectionSet, defaults);

        return fragment.WithDirectives(directives).WithSelectionSet(selectionSet);
    }

    private static FragmentSpreadNode ReplaceVariables(FragmentSpreadNode spread,
        IReadOnlyDictionary<string, IValueNode> defaults)
    {
        var directives = ReplaceVariables(spread.Directives, defaults);

        return spread.WithDirectives(directives);
    }

    private static IReadOnlyList<ArgumentNode> ReplaceVariables(
        IReadOnlyList<ArgumentNode> arguments,
        IReadOnlyDictionary<string, IValueNode> defaults)
    {
        if (arguments.Count == 0)
        {
            return arguments;
        }

        var updated = new List<ArgumentNode>(arguments.Count);
        foreach (var argument in arguments)
        {
            var value = ReplaceVariables(argument.Value, defaults);
            updated.Add(argument.WithValue(value));
        }

        return updated;
    }

    private static IReadOnlyList<DirectiveNode> ReplaceVariables(
        IReadOnlyList<DirectiveNode> directives,
        IReadOnlyDictionary<string, IValueNode> defaults)
    {
        if (directives.Count == 0)
        {
            return directives;
        }

        var updated = new List<DirectiveNode>(directives.Count);

        foreach (var directive in directives)
        {
            var arguments = ReplaceVariables(directive.Arguments, defaults);
            updated.Add(directive.WithArguments(arguments));
        }

        return updated;
    }

    private static IValueNode ReplaceVariables(IValueNode value, IReadOnlyDictionary<string, IValueNode> defaults)
    {
        return value switch
        {
            VariableNode variable when defaults.TryGetValue(variable.Name.Value, out var replacement)
                => replacement,
            ListValueNode list => list.WithItems([.. list.Items.Select(item => ReplaceVariables(item, defaults))]),
            ObjectValueNode obj => obj.WithFields([
                .. obj.Fields
                    .Select(field => field.WithValue(ReplaceVariables(field.Value, defaults)))
            ]),
            _ => value
        };
    }

    private static void CollectFragmentSpreads(SelectionSetNode selectionSet, ISet<string> names)
    {
        foreach (var selection in selectionSet.Selections)
        {
            switch (selection)
            {
                case FragmentSpreadNode spread:
                    names.Add(spread.Name.Value);
                    break;
                case FieldNode { SelectionSet: not null } field:
                    CollectFragmentSpreads(field.SelectionSet, names);
                    break;
                case InlineFragmentNode fragment:
                    CollectFragmentSpreads(fragment.SelectionSet, names);
                    break;
            }
        }
    }

}
