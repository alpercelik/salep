using Microsoft.CodeAnalysis.CSharp.Syntax;
using static Salep.ClientGenerator.Emission.Syntax.Cs;
using Salep.GraphQLParser;
using Salep.ClientGenerator.Model;
using Salep.ClientGenerator.Utilities;

namespace Salep.ClientGenerator.Emission;

internal sealed class OperationsEmitter(
    SchemaModel schema,
    IReadOnlyList<OperationDefinitionNode> operations,
    IReadOnlyList<FragmentDefinitionNode> fragments,
    string operationInterfaceTypeName = "IGraphQLOperation")
{
    private readonly IReadOnlyDictionary<string, FragmentDefinitionNode> _fragmentsByName = fragments
        .GroupBy(fragment => fragment.Name.Value, StringComparer.Ordinal)
        .ToDictionary(group => group.Key, group => group.First(), StringComparer.Ordinal);
    private readonly OperationVariablePolicies _variables = new(schema.Config, fragments);
    public string Generate()
    {
        var members = new List<MemberDeclarationSyntax>();

        var rootQuery = schema.GetRootTypeName(OperationType.Query);
        var rootMutation = schema.GetRootTypeName(OperationType.Mutation);
        var rootSubscription = schema.GetRootTypeName(OperationType.Subscription);

        foreach (var operation in operations)
        {
            switch (operation.Operation)
            {
                case OperationType.Query:
                    members.AddRange(GenerateOperationMembers(operation, rootQuery));
                    break;
                case OperationType.Mutation:
                    members.AddRange(GenerateOperationMembers(operation, rootMutation));
                    break;
                case OperationType.Subscription:
                    members.AddRange(GenerateOperationMembers(operation, rootSubscription));
                    break;
            }
        }

        return RoslynEmitter.EmitFile(schema.Config, schema.Config.GeneratedNamespace,
            ["System", "System.Collections.Generic", "System.Text.Json", "System.Text.Json.Serialization"], members);
    }

    private IReadOnlyList<MemberDeclarationSyntax> GenerateOperationMembers(OperationDefinitionNode operation, string rootTypeName)
    {
        var operationName = operation.Name?.Value;
        if (string.IsNullOrWhiteSpace(operationName))
        {
            return [];
        }

        operation = _variables.Apply(operation);
        var members = new List<MemberDeclarationSyntax>();
        var baseName = CSharpNaming.ToTypeName(operationName);
        var typeName = $"{baseName}Operation";
        var variablesTypeName = $"{baseName}Variables";
        var responseTypeName = $"{baseName}Response";
        members.Add(Record(typeName,
        [
            ReadOnlyProperty("string", "OperationName", String(operationName)),
            ReadOnlyProperty("string", "Query", String(GetCompleteOperationDocument(AddTypename(operation, rootTypeName)))),
            Property($"{variablesTypeName}?", "Variables"),
            Constructor(typeName, [Parameter($"{variablesTypeName}?", "variables", Null)], Set("Variables", Name("variables")))
        ], bases: [$"{operationInterfaceTypeName}<{responseTypeName}, {variablesTypeName}>"]));
        members.Add(GenerateVariables(operation, variablesTypeName));
        members.AddRange(new OperationResponseEmitter(schema, _fragmentsByName).Generate(operation.SelectionSet, rootTypeName, responseTypeName));
        return members;
    }

    private string GetCompleteOperationDocument(OperationDefinitionNode operation)
    {
        var referencedNames = new HashSet<string>(StringComparer.Ordinal);
        CollectFragmentSpreads(operation.SelectionSet, referencedNames);

        var pendingNames = new Queue<string>(referencedNames);
        while (pendingNames.TryDequeue(out var name))
        {
            if (!_fragmentsByName.TryGetValue(name, out var fragment)) continue;

            var nestedNames = new HashSet<string>(StringComparer.Ordinal);
            CollectFragmentSpreads(fragment.SelectionSet, nestedNames);
            foreach (var nestedName in nestedNames)
            {
                if (referencedNames.Add(nestedName)) pendingNames.Enqueue(nestedName);
            }
        }

        var definitions = fragments
            .Where(fragment => referencedNames.Contains(fragment.Name.Value))
            .Select(fragment => fragment.WithSelectionSet(AddTypename(fragment.SelectionSet, fragment.TypeCondition.Name.Value)).ToString().Trim());
        return string.Join(Environment.NewLine + Environment.NewLine,
            new[] { operation.ToString().Trim() }.Concat(definitions));
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

    private OperationDefinitionNode AddTypename(OperationDefinitionNode operation, string rootTypeName)
    {
        var selectionSet = AddTypename(operation.SelectionSet, rootTypeName);

        return operation.WithSelectionSet(selectionSet);
    }

    private SelectionSetNode AddTypename(SelectionSetNode selectionSet, string parentTypeName)
    {
        var selections = new List<ISelectionNode>();
        var shouldAddTypename = schema.IsUnionTypeName(parentTypeName) || schema.IsInterfaceTypeName(parentTypeName);
        var hasTypename = selectionSet.Selections
            .OfType<FieldNode>()
            .Any(field => field.Name.Value == "__typename" && field.Alias is null && field.Directives.Count == 0);

        if (shouldAddTypename && !hasTypename)
        {
            selections.Add(new FieldNode(
                new("__typename"),
                null,
                [],
                [],
                null,
                new SourceLocation(selectionSet.Location.Start, selectionSet.Location.Start)));
        }

        foreach (var selection in selectionSet.Selections)
        {
            selections.Add(selection switch
            {
                FieldNode { SelectionSet: not null } field =>
                    AddFieldTypename(field, parentTypeName),
                InlineFragmentNode fragment =>
                    AddFragmentTypename(fragment, parentTypeName),
                _ => selection
            });
        }

        return selectionSet.WithSelections(selections);
    }

    private FieldNode AddFieldTypename(FieldNode field, string parentTypeName)
    {
        var fieldTypeName = schema.GetFieldReturnTypeName(parentTypeName, field.Name.Value);
        if (string.IsNullOrWhiteSpace(fieldTypeName))
        {
            return field;
        }

        var updatedSelection = AddTypename(field.SelectionSet!, fieldTypeName);
        
        return field.WithSelectionSet(updatedSelection);
    }

    private InlineFragmentNode AddFragmentTypename(InlineFragmentNode fragment, string parentTypeName)
    {
        var fragmentTypeName = fragment.TypeCondition?.Name.Value ?? parentTypeName;
        var updatedSelection = AddTypename(fragment.SelectionSet, fragmentTypeName);
        
        return fragment.WithSelectionSet(updatedSelection);
    }

    private MemberDeclarationSyntax GenerateVariables(OperationDefinitionNode operation, string name)
        => Record(name, operation.VariableDefinitions.Select(variable =>
            Property(schema.ResolveType(variable.Type), CSharpNaming.ToPropertyName(variable.Variable.Name.Value),
                initializer: variable.Type is NonNullTypeNode && !schema.IsValueType(variable.Type) ? Suppress(Default) : null)
                .AddAttributeLists(Attribute("JsonPropertyName", String(variable.Variable.Name.Value)))));
}
