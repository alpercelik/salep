using SyntaxKind = Microsoft.CodeAnalysis.CSharp.SyntaxKind;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Salep.ClientGenerator.Emission.Syntax;
using static Salep.ClientGenerator.Emission.Syntax.Cs;
using Salep.ClientGenerator.Model;
using Salep.GraphQLParser;
using Salep.ClientGenerator.Utilities;

namespace Salep.ClientGenerator.Emission;

// Keep schema types for ordinary selections; aliases need operation-specific shapes.
internal sealed class OperationResponseEmitter(
    SchemaModel schema,
    IReadOnlyDictionary<string, FragmentDefinitionNode> fragments)
{
    private readonly List<MemberDeclarationSyntax> _members = [];
    private string _prefix = string.Empty;
    private int _nextType;

    public IReadOnlyList<MemberDeclarationSyntax> Generate(SelectionSetNode selection, string rootType, string responseType)
    {
        _prefix = responseType;
        EmitObject(selection, rootType, responseType);
        return _members;
    }

    private string NewTypeName() => $"{_prefix}Selection{++_nextType}";

    private void EmitObject(SelectionSetNode selection, string graphQlType, string typeName, string? baseType = null)
    {
        var properties = new List<MemberDeclarationSyntax>();
            foreach (var field in CollectFields(selection, graphQlType))
            {
                var responseName = field.Alias?.Value ?? field.Name.Value;
                var typeNode = schema.GetFieldTypeNode(graphQlType, field.Name.Value);
                var fieldType = field.Name.Value == "__typename" ? "string" : typeNode is null ? "object" : schema.ResolveType(typeNode);
                var childType = schema.GetFieldReturnTypeName(graphQlType, field.Name.Value);
                if (field.SelectionSet is not null && typeNode is not null && childType is not null
                    && ContainsAlias(field.SelectionSet, new HashSet<string>(StringComparer.Ordinal)))
                {
                    var projectionType = NewTypeName();
                    if (schema.ObjectTypes.ContainsKey(childType))
                    {
                        EmitObject(field.SelectionSet, childType, projectionType);
                    }
                    else
                    {
                        EmitAbstract(field.SelectionSet, childType, projectionType);
                    }
                    fieldType = ReplaceNamedType(typeNode, projectionType);
                }

                properties.Add(Property(fieldType, CSharpNaming.ToPropertyName(responseName), initializer: Suppress(Default))
                    .AddAttributeLists(Attribute("JsonPropertyName", String(responseName))));
            }
        _members.Add(Record(typeName, properties, bases: baseType is null ? [] : [baseType]));
    }

    private void EmitAbstract(SelectionSetNode selection, string graphQlType, string typeName)
    {
        var concreteTypes = schema.UnionTypes.TryGetValue(graphQlType, out var union)
            ? union.Types.Select(type => type.Name.Value).ToArray()
            : schema.GetInterfaceImplementations(graphQlType).Select(type => type.Name.Value).ToArray();
        var cases = concreteTypes.Select(name => (GraphQlName: name, TypeName: NewTypeName())).ToArray();
        _members.Add(Record(typeName, [], Modifiers(SyntaxKind.PublicKeyword, SyntaxKind.AbstractKeyword))
            .AddAttributeLists(Attribute("JsonConverter", SyntaxFactory.TypeOfExpression(Type($"{typeName}JsonConverter")))));
        foreach (var variant in cases) EmitObject(selection, variant.GraphQlName, variant.TypeName, typeName);
        _members.Add(new JsonConverterSyntax(typeName, cases.Select(variant => new JsonVariant(variant.GraphQlName, variant.TypeName)).ToArray(),
            UnionRepresentation.Projection, "").Generate());
    }

    private IReadOnlyList<FieldNode> CollectFields(SelectionSetNode selection, string concreteType)
    {
        var fields = new Dictionary<string, FieldNode>(StringComparer.Ordinal);
        Visit(selection, new HashSet<string>(StringComparer.Ordinal));
        return fields.Values.ToArray();

        void Visit(SelectionSetNode current, HashSet<string> visiting)
        {
            foreach (var item in current.Selections)
            {
                switch (item)
                {
                    case FieldNode field:
                        var key = field.Alias?.Value ?? field.Name.Value;
                        if (fields.TryGetValue(key, out var existing) && existing.SelectionSet is not null && field.SelectionSet is not null)
                        {
                            var merged = existing.SelectionSet.Selections.Concat(field.SelectionSet.Selections)
                                .OrderBy(item => item.Location.Start).Cast<SelectionNode>().ToArray();
                            // Fragment selections may originate from separate source ranges/files.
                            var location = new SourceLocation(merged.Min(item => item.Location.Start), merged.Max(item => item.Location.End));
                            fields[key] = existing.WithSelectionSet(new SelectionSetNode(merged, location));
                        }
                        else
                        {
                            fields.TryAdd(key, field);
                        }
                        break;
                    case InlineFragmentNode inline when AppliesTo(inline.TypeCondition?.Name.Value, concreteType):
                        Visit(inline.SelectionSet, visiting);
                        break;
                    case FragmentSpreadNode spread when visiting.Add(spread.Name.Value):
                        if (fragments.TryGetValue(spread.Name.Value, out var fragment) && AppliesTo(fragment.TypeCondition.Name.Value, concreteType))
                        {
                            Visit(fragment.SelectionSet, visiting);
                        }
                        visiting.Remove(spread.Name.Value);
                        break;
                }
            }
        }
    }

    private bool AppliesTo(string? condition, string concreteType) => condition is null || condition == concreteType
        || (schema.UnionTypes.TryGetValue(condition, out var union) && union.Types.Any(type => type.Name.Value == concreteType))
        || (schema.InterfaceTypes.ContainsKey(condition) && schema.GetInterfaceImplementations(condition).Any(type => type.Name.Value == concreteType));

    private bool ContainsAlias(SelectionSetNode selection, HashSet<string> visited)
    {
        foreach (var item in selection.Selections)
        {
            switch (item)
            {
                case FieldNode field when field.Alias is not null:
                    return true;
                case FieldNode { SelectionSet: not null } field when ContainsAlias(field.SelectionSet, visited):
                    return true;
                case InlineFragmentNode inline when ContainsAlias(inline.SelectionSet, visited):
                    return true;
                case FragmentSpreadNode spread when visited.Add(spread.Name.Value):
                    if (fragments.TryGetValue(spread.Name.Value, out var fragment) && ContainsAlias(fragment.SelectionSet, visited)) return true;
                    break;
            }
        }
        return false;
    }

    private static string ReplaceNamedType(ITypeNode type, string name) => type switch
    {
        NonNullTypeNode nonNull => ReplaceNonNullType(nonNull.Type, name),
        _ => ReplaceNonNullType(type, name) + "?"
    };

    private static string ReplaceNonNullType(ITypeNode type, string name) => type is ListTypeNode list
        ? $"List<{ReplaceNamedType(list.Type, name)}>"
        : name;
}
