using System.Text;
using System.Text.Json;

namespace Salep.ClientGenerator.Model;

/// <summary>Controls source-level GraphQL normalization applied before target code generation.</summary>
public sealed record GraphQlOperationDocumentOptions
{
    /// <summary>Adds unaliased __typename selections for interface and union response objects.</summary>
    public bool AddTypename { get; init; } = true;

    /// <summary>Removes operation variables not referenced by the operation or reachable fragments.</summary>
    public bool OmitUnusedVariables { get; init; }

    /// <summary>Replaces operation variables with defaults when no fragment spreads are used.</summary>
    public bool InlineDefaultVariables { get; init; }
}

/// <summary>Formats a normalized operation and its transitive fragments as valid GraphQL source.</summary>
public sealed class GraphQlOperationDocumentFormatter(GraphQlSchemaModel schema, GraphQlExecutableDocument executable)
{
    /// <summary>Formats one operation, including only fragments it references directly or transitively.</summary>
    public string Format(GraphQlOperationDefinition operation, GraphQlOperationDocumentOptions? options = null)
    {
        ArgumentNullException.ThrowIfNull(operation);
        options ??= new GraphQlOperationDocumentOptions();
        var fragments = executable.Fragments
            .GroupBy(fragment => fragment.Name, StringComparer.Ordinal)
            .ToDictionary(group => group.Key, group => group.First(), StringComparer.Ordinal);
        var normalized = NormalizeVariables(operation, options);
        var referencedNames = ReferencedFragments(normalized.SelectionSet, fragments);
        var outputOperation = options.AddTypename
            ? normalized with { SelectionSet = AddTypename(normalized.SelectionSet, RootTypeName(normalized.OperationType)) }
            : normalized;

        var documents = new List<string> { PrintOperation(outputOperation) };
        foreach (var fragment in executable.Fragments.Where(fragment => referencedNames.Contains(fragment.Name)))
        {
            var outputFragment = options.AddTypename
                ? fragment with { SelectionSet = AddTypename(fragment.SelectionSet, fragment.TypeCondition) }
                : fragment;
            documents.Add(PrintFragment(outputFragment));
        }

        return string.Join(Environment.NewLine + Environment.NewLine, documents);
    }

    /// <summary>Applies the configured variable omission and default-inlining policies without formatting source.</summary>
    public GraphQlOperationDefinition NormalizeVariables(GraphQlOperationDefinition operation, GraphQlOperationDocumentOptions? options = null)
    {
        ArgumentNullException.ThrowIfNull(operation);
        options ??= new GraphQlOperationDocumentOptions();
        var fragments = executable.Fragments
            .GroupBy(fragment => fragment.Name, StringComparer.Ordinal)
            .ToDictionary(group => group.Key, group => group.First(), StringComparer.Ordinal);
        return ApplyVariablePolicies(operation, fragments, options);
    }

    private GraphQlOperationDefinition ApplyVariablePolicies(
        GraphQlOperationDefinition operation,
        IReadOnlyDictionary<string, GraphQlFragmentDefinition> fragments,
        GraphQlOperationDocumentOptions options)
    {
        var hasSpreads = ContainsFragmentSpread(operation.SelectionSet);
        if (options.InlineDefaultVariables && !hasSpreads)
        {
            var defaults = operation.Variables.Where(variable => variable.DefaultValue is not null)
                .ToDictionary(variable => variable.Name, variable => variable.DefaultValue!, StringComparer.Ordinal);
            if (defaults.Count > 0)
            {
                operation = operation with
                {
                    Directives = ReplaceVariables(operation.Directives, defaults),
                    SelectionSet = ReplaceVariables(operation.SelectionSet, defaults),
                    Variables = operation.Variables.Where(variable => !defaults.ContainsKey(variable.Name)).ToArray()
                };
            }
        }

        if (!options.OmitUnusedVariables)
        {
            return operation;
        }

        var used = new HashSet<string>(StringComparer.Ordinal);
        CollectVariables(operation.Directives, used);
        CollectVariables(operation.SelectionSet, used);
        var reachable = ReferencedFragments(operation.SelectionSet, fragments);
        foreach (var fragment in fragments.Values.Where(fragment => reachable.Contains(fragment.Name)))
        {
            CollectVariables(fragment.Directives, used);
            CollectVariables(fragment.SelectionSet, used);
        }

        return operation with { Variables = operation.Variables.Where(variable => used.Contains(variable.Name)).ToArray() };
    }

    private HashSet<string> ReferencedFragments(
        GraphQlSelectionSet selectionSet,
        IReadOnlyDictionary<string, GraphQlFragmentDefinition> fragments)
    {
        var found = new HashSet<string>(StringComparer.Ordinal);
        var pending = new Queue<string>();
        Collect(selectionSet);
        while (pending.TryDequeue(out var name))
        {
            if (!fragments.TryGetValue(name, out var fragment)) continue;
            Collect(fragment.SelectionSet);
        }

        return found;

        void Collect(GraphQlSelectionSet selections)
        {
            foreach (var selection in selections.Selections)
            {
                switch (selection)
                {
                    case GraphQlFragmentSpreadSelection spread when found.Add(spread.Name):
                        pending.Enqueue(spread.Name);
                        break;
                    case GraphQlFieldSelection { SelectionSet: not null } field:
                        Collect(field.SelectionSet);
                        break;
                    case GraphQlInlineFragmentSelection inline:
                        Collect(inline.SelectionSet);
                        break;
                }
            }
        }
    }

    private GraphQlSelectionSet AddTypename(GraphQlSelectionSet selectionSet, string parentType)
    {
        var selections = new List<GraphQlSelection>();
        var isAbstract = schema.InterfaceTypes.Any(type => StringComparer.Ordinal.Equals(type.Name, parentType))
            || schema.UnionTypes.Any(type => StringComparer.Ordinal.Equals(type.Name, parentType));
        if (isAbstract && !selectionSet.Selections.OfType<GraphQlFieldSelection>().Any(field =>
                StringComparer.Ordinal.Equals(field.Name, "__typename") && field.Alias is null && field.Directives.Count == 0))
        {
            selections.Add(new GraphQlFieldSelection("__typename", null, [], [], null, selectionSet.Source));
        }

        foreach (var selection in selectionSet.Selections)
        {
            selections.Add(selection switch
            {
                GraphQlFieldSelection { SelectionSet: not null } field when NamedTypeName(FindField(parentType, field.Name)?.Type) is { } childType
                    => field with { SelectionSet = AddTypename(field.SelectionSet, childType) },
                GraphQlInlineFragmentSelection inline
                    => inline with { SelectionSet = AddTypename(inline.SelectionSet, inline.TypeCondition ?? parentType) },
                _ => selection
            });
        }

        return selectionSet with { Selections = selections.ToArray() };
    }

    private GraphQlFieldDefinition? FindField(string parentType, string fieldName)
        => schema.Types.OfType<GraphQlObjectType>().Where(type => StringComparer.Ordinal.Equals(type.Name, parentType))
            .SelectMany(type => type.Fields)
            .Concat(schema.Types.OfType<GraphQlInterfaceType>().Where(type => StringComparer.Ordinal.Equals(type.Name, parentType)).SelectMany(type => type.Fields))
            .FirstOrDefault(field => StringComparer.Ordinal.Equals(field.Name, fieldName));

    private string RootTypeName(string operationType)
    {
        var configured = schema.SchemaDefinitions.SelectMany(item => item.RootOperations)
            .FirstOrDefault(item => StringComparer.Ordinal.Equals(item.Operation, operationType))?.TypeName;
        if (!string.IsNullOrEmpty(configured)) return configured;
        return operationType switch
        {
            "query" => "Query",
            "mutation" => "Mutation",
            "subscription" => "Subscription",
            _ => throw new ArgumentOutOfRangeException(nameof(operationType), operationType, "Unknown GraphQL operation type.")
        };
    }

    private static bool ContainsFragmentSpread(GraphQlSelectionSet selectionSet)
        => selectionSet.Selections.Any(selection => selection switch
        {
            GraphQlFragmentSpreadSelection => true,
            GraphQlFieldSelection { SelectionSet: not null } field => ContainsFragmentSpread(field.SelectionSet),
            GraphQlInlineFragmentSelection inline => ContainsFragmentSpread(inline.SelectionSet),
            _ => false
        });

    private static GraphQlSelectionSet ReplaceVariables(GraphQlSelectionSet selectionSet, IReadOnlyDictionary<string, GraphQlValue> defaults)
        => selectionSet with
        {
            Selections = selectionSet.Selections.Select(selection => selection switch
            {
                GraphQlFieldSelection field => field with
                {
                    Arguments = ReplaceVariables(field.Arguments, defaults),
                    Directives = ReplaceVariables(field.Directives, defaults),
                    SelectionSet = field.SelectionSet is null ? null : ReplaceVariables(field.SelectionSet, defaults)
                },
                GraphQlFragmentSpreadSelection spread => spread with
                {
                    Arguments = ReplaceVariables(spread.Arguments, defaults),
                    Directives = ReplaceVariables(spread.Directives, defaults)
                },
                GraphQlInlineFragmentSelection inline => inline with
                {
                    Directives = ReplaceVariables(inline.Directives, defaults),
                    SelectionSet = ReplaceVariables(inline.SelectionSet, defaults)
                },
                _ => selection
            }).ToArray()
        };

    private static IReadOnlyList<GraphQlDirective> ReplaceVariables(
        IReadOnlyList<GraphQlDirective> directives,
        IReadOnlyDictionary<string, GraphQlValue> defaults)
        => directives.Select(directive => directive with { Arguments = ReplaceVariables(directive.Arguments, defaults) }).ToArray();

    private static IReadOnlyList<GraphQlArgument> ReplaceVariables(
        IReadOnlyList<GraphQlArgument> arguments,
        IReadOnlyDictionary<string, GraphQlValue> defaults)
        => arguments.Select(argument => argument with { Value = ReplaceVariables(argument.Value, defaults) }).ToArray();

    private static GraphQlValue ReplaceVariables(GraphQlValue value, IReadOnlyDictionary<string, GraphQlValue> defaults)
    {
        if (value.Kind == GraphQlValueKind.Variable && value.Text is not null && defaults.TryGetValue(value.Text, out var replacement))
        {
            return replacement;
        }

        return value with
        {
            Items = value.Items?.Select(item => ReplaceVariables(item, defaults)).ToArray(),
            Fields = value.Fields?.Select(field => field with { Value = ReplaceVariables(field.Value, defaults) }).ToArray()
        };
    }

    private static void CollectVariables(IReadOnlyList<GraphQlDirective> directives, ISet<string> used)
    {
        foreach (var directive in directives) CollectVariables(directive.Arguments, used);
    }

    private static void CollectVariables(IReadOnlyList<GraphQlArgument> arguments, ISet<string> used)
    {
        foreach (var argument in arguments) CollectVariables(argument.Value, used);
    }

    private static void CollectVariables(GraphQlValue value, ISet<string> used)
    {
        if (value.Kind == GraphQlValueKind.Variable && value.Text is not null) used.Add(value.Text);
        if (value.Items is not null) foreach (var item in value.Items) CollectVariables(item, used);
        if (value.Fields is not null) foreach (var field in value.Fields) CollectVariables(field.Value, used);
    }

    private static void CollectVariables(GraphQlSelectionSet selectionSet, ISet<string> used)
    {
        foreach (var selection in selectionSet.Selections)
        {
            switch (selection)
            {
                case GraphQlFieldSelection field:
                    CollectVariables(field.Arguments, used);
                    CollectVariables(field.Directives, used);
                    if (field.SelectionSet is not null) CollectVariables(field.SelectionSet, used);
                    break;
                case GraphQlFragmentSpreadSelection spread:
                    CollectVariables(spread.Arguments, used);
                    CollectVariables(spread.Directives, used);
                    break;
                case GraphQlInlineFragmentSelection inline:
                    CollectVariables(inline.Directives, used);
                    CollectVariables(inline.SelectionSet, used);
                    break;
            }
        }
    }

    private static string PrintOperation(GraphQlOperationDefinition operation)
    {
        var builder = new StringBuilder(operation.OperationType);
        if (operation.Name is not null) builder.Append(' ').Append(operation.Name);
        if (operation.Variables.Count > 0)
        {
            builder.Append('(');
            for (var index = 0; index < operation.Variables.Count; index++)
            {
                if (index > 0) builder.Append(", ");
                var variable = operation.Variables[index];
                builder.Append('$').Append(variable.Name).Append(": ").Append(PrintType(variable.Type));
                if (variable.DefaultValue is not null) builder.Append(" = ").Append(PrintValue(variable.DefaultValue));
                AppendDirectives(builder, variable.Directives);
            }

            builder.Append(')');
        }

        AppendDirectives(builder, operation.Directives);
        builder.Append(' ');
        AppendSelectionSet(builder, operation.SelectionSet, 0);
        return builder.ToString();
    }

    private static string PrintFragment(GraphQlFragmentDefinition fragment)
    {
        var builder = new StringBuilder("fragment ").Append(fragment.Name);
        if (fragment.Variables.Count > 0)
        {
            builder.Append('(');
            for (var index = 0; index < fragment.Variables.Count; index++)
            {
                if (index > 0) builder.Append(", ");
                var variable = fragment.Variables[index];
                builder.Append('$').Append(variable.Name).Append(": ").Append(PrintType(variable.Type));
                if (variable.DefaultValue is not null) builder.Append(" = ").Append(PrintValue(variable.DefaultValue));
                AppendDirectives(builder, variable.Directives);
            }

            builder.Append(')');
        }

        builder.Append(" on ").Append(fragment.TypeCondition);
        AppendDirectives(builder, fragment.Directives);
        builder.Append(' ');
        AppendSelectionSet(builder, fragment.SelectionSet, 0);
        return builder.ToString();
    }

    private static void AppendSelectionSet(StringBuilder builder, GraphQlSelectionSet selectionSet, int indent)
    {
        builder.Append('{');
        if (selectionSet.Selections.Count == 0)
        {
            builder.Append('}');
            return;
        }

        foreach (var selection in selectionSet.Selections)
        {
            builder.AppendLine().Append(' ', (indent + 1) * 2);
            switch (selection)
            {
                case GraphQlFieldSelection field:
                    if (field.Alias is not null) builder.Append(field.Alias).Append(": ");
                    builder.Append(field.Name);
                    AppendArguments(builder, field.Arguments);
                    AppendDirectives(builder, field.Directives);
                    if (field.SelectionSet is not null)
                    {
                        builder.Append(' ');
                        AppendSelectionSet(builder, field.SelectionSet, indent + 1);
                    }

                    break;
                case GraphQlFragmentSpreadSelection spread:
                    builder.Append("...").Append(spread.Name);
                    AppendArguments(builder, spread.Arguments);
                    AppendDirectives(builder, spread.Directives);
                    break;
                case GraphQlInlineFragmentSelection inline:
                    builder.Append("...");
                    if (inline.TypeCondition is not null) builder.Append(" on ").Append(inline.TypeCondition);
                    AppendDirectives(builder, inline.Directives);
                    builder.Append(' ');
                    AppendSelectionSet(builder, inline.SelectionSet, indent + 1);
                    break;
            }
        }

        builder.AppendLine().Append(' ', indent * 2).Append('}');
    }

    private static void AppendArguments(StringBuilder builder, IReadOnlyList<GraphQlArgument> arguments)
    {
        if (arguments.Count == 0) return;
        builder.Append('(');
        for (var index = 0; index < arguments.Count; index++)
        {
            if (index > 0) builder.Append(", ");
            builder.Append(arguments[index].Name).Append(": ").Append(PrintValue(arguments[index].Value));
        }

        builder.Append(')');
    }

    private static void AppendDirectives(StringBuilder builder, IReadOnlyList<GraphQlDirective> directives)
    {
        foreach (var directive in directives)
        {
            builder.Append(" @").Append(directive.Name);
            AppendArguments(builder, directive.Arguments);
        }
    }

    private static string PrintType(GraphQlTypeReference type) => type switch
    {
        GraphQlNamedTypeReference named => named.Name,
        GraphQlListTypeReference list => $"[{PrintType(list.ElementType)}]",
        GraphQlNonNullTypeReference nonNull => $"{PrintType(nonNull.NullableType)}!",
        _ => throw new NotSupportedException($"Unsupported GraphQL type reference '{type.GetType().Name}'.")
    };

    private static string PrintValue(GraphQlValue value) => value.Kind switch
    {
        GraphQlValueKind.Variable => $"${value.Text}",
        GraphQlValueKind.IntegerLiteral or GraphQlValueKind.FloatLiteral => value.Text ?? "0",
        GraphQlValueKind.StringLiteral => JsonSerializer.Serialize(value.Text ?? string.Empty),
        GraphQlValueKind.BooleanLiteral => value.Boolean == true ? "true" : "false",
        GraphQlValueKind.NullLiteral => "null",
        GraphQlValueKind.EnumLiteral => value.Text ?? string.Empty,
        GraphQlValueKind.ListLiteral => $"[{string.Join(", ", value.Items?.Select(PrintValue) ?? [])}]",
        GraphQlValueKind.ObjectLiteral => $"{{{string.Join(", ", value.Fields?.Select(field => $"{field.Name}: {PrintValue(field.Value)}") ?? [])}}}",
        _ => throw new ArgumentOutOfRangeException(nameof(value), value.Kind, "Unknown GraphQL value kind.")
    };

    private static string? NamedTypeName(GraphQlTypeReference? type) => type switch
    {
        GraphQlNamedTypeReference named => named.Name,
        GraphQlListTypeReference list => NamedTypeName(list.ElementType),
        GraphQlNonNullTypeReference nonNull => NamedTypeName(nonNull.NullableType),
        _ => null
    };
}
