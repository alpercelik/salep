using SyntaxKind = Microsoft.CodeAnalysis.CSharp.SyntaxKind;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Salep.GraphQLParser;
using Salep.ClientGenerator.Model;
using Salep.ClientGenerator.Utilities;
using Salep.ClientGenerator.Emission.Client;
using static Salep.ClientGenerator.Emission.Syntax.Cs;

namespace Salep.ClientGenerator.Emission;

internal sealed class SchemaTypesEmitter(SchemaModel schema, IReadOnlySet<string>? includedNames = null)
{
    public string Generate()
    {
        var members = new List<MemberDeclarationSyntax>();
        var hasUnion = false;
        foreach (var definition in schema.Document.Definitions)
        {
            var definitionName = definition switch { ObjectTypeDefinitionNode d => d.Name.Value, InputObjectTypeDefinitionNode d => d.Name.Value, EnumTypeDefinitionNode d => d.Name.Value, InterfaceTypeDefinitionNode d => d.Name.Value, UnionTypeDefinitionNode d => d.Name.Value, _ => null };
            if (includedNames is not null && (definitionName is null || !includedNames.Contains(definitionName))) continue;
            switch (definition)
            {
                case ObjectTypeDefinitionNode obj:
                    members.Add(Record(CSharpNaming.ToTypeName(obj.Name.Value), obj.Fields.Select(field =>
                        Property(schema.ResolveType(field.Type), CSharpNaming.ToPropertyName(field.Name.Value), init: false, initializer: Suppress(Default))
                            .AddAttributeLists(Attribute("JsonPropertyName", String(field.Name.Value)))),
                        bases: obj.Interfaces.Select(type => CSharpNaming.ToInterfaceName(type.Name.Value)).ToArray()));
                    break;
                case InputObjectTypeDefinitionNode input:
                    members.Add(Record(CSharpNaming.ToTypeName(input.Name.Value), input.Fields.Select(field =>
                        Property(schema.ResolveType(field.Type), CSharpNaming.ToPropertyName(field.Name.Value), init: false,
                            modifiers: field.Type is NonNullTypeNode && !schema.IsValueType(field.Type) ? Modifiers(SyntaxKind.PublicKeyword, SyntaxKind.RequiredKeyword) : Public)
                            .AddAttributeLists(Attribute("JsonPropertyName", String(field.Name.Value))))));
                    break;
                case EnumTypeDefinitionNode enumeration:
                    members.Add(SyntaxFactory.EnumDeclaration(Identifier(CSharpNaming.ToTypeName(enumeration.Name.Value))).WithModifiers(Public)
                        .WithMembers(SyntaxFactory.SeparatedList(enumeration.Values.Select(value => SyntaxFactory.EnumMemberDeclaration(Identifier(CSharpNaming.ToEnumMemberName(value.Name.Value)))
                            .AddAttributeLists(Attribute("JsonStringEnumMemberName", String(value.Name.Value)))))));
                    break;
                case InterfaceTypeDefinitionNode contract:
                    members.Add(GenerateInterface(contract));
                    break;
                case UnionTypeDefinitionNode union:
                    members.Add(GenerateUnion(CSharpNaming.ToTypeName(union.Name.Value), union.Types.Select(type => CSharpNaming.ToTypeName(type.Name.Value))));
                    hasUnion = true;
                    break;
            }
        }
        foreach (var contract in schema.InterfaceTypes.Values)
        {
            if (includedNames is not null && !includedNames.Contains(contract.Name.Value)) continue;
            var cases = schema.GetInterfaceImplementations(contract.Name.Value);
            if (cases.Count == 0) continue;
            members.Add(GenerateUnion(SchemaModel.GetInterfaceResultTypeName(contract.Name.Value), cases.Select(type => CSharpNaming.ToTypeName(type.Name.Value))));
            hasUnion = true;
        }
        List<string> usings = ["System", "System.Collections.Generic", "System.Text.Json.Serialization"];
        if (hasUnion && !schema.Config.UseNativeUnions) usings.Add("Dunet");
        usings.AddRange(schema.Config.GetAdditionalUsings());
        return RoslynEmitter.EmitFile(schema.Config, schema.Config.GeneratedNamespace, usings.Distinct(StringComparer.Ordinal), members);
    }

    private MemberDeclarationSyntax GenerateInterface(InterfaceTypeDefinitionNode contract)
    {
        var inherited = new HashSet<string>(StringComparer.Ordinal);
        var visited = new HashSet<string>(StringComparer.Ordinal);
        foreach (var parent in contract.Interfaces) CollectFields(parent.Name.Value, inherited, visited);
        return SyntaxFactory.InterfaceDeclaration(Identifier(CSharpNaming.ToInterfaceName(contract.Name.Value))).WithModifiers(Public)
            .WithBaseList(Bases(contract.Interfaces.Select(type => CSharpNaming.ToInterfaceName(type.Name.Value))))
            .WithMembers(SyntaxFactory.List<MemberDeclarationSyntax>(contract.Fields.Select(field =>
                Property(schema.ResolveType(field.Type), CSharpNaming.ToPropertyName(field.Name.Value), init: false,
                    modifiers: inherited.Contains(field.Name.Value) ? Modifiers(SyntaxKind.NewKeyword) : default(SyntaxTokenList)))));
    }

    private MemberDeclarationSyntax GenerateUnion(string name, IEnumerable<string> caseNames)
    {
        var cases = caseNames.ToArray();
        if (schema.Config.UseNativeUnions)
#pragma warning disable RSEXPERIMENTAL006 // Native unions are an explicit opt-in tied to our pinned Roslyn version.
            return SyntaxFactory.UnionDeclaration(default, Public, SyntaxFactory.Token(SyntaxKind.UnionKeyword), Identifier(name), null,
                SyntaxFactory.ParameterList(SyntaxFactory.SeparatedList(cases.Select(type => SyntaxFactory.Parameter(default, default, Type(type), default, null)))),
                null, default, default, default, default, SyntaxFactory.Token(SyntaxKind.SemicolonToken));
#pragma warning restore RSEXPERIMENTAL006
        var partial = Modifiers(SyntaxKind.PublicKeyword, SyntaxKind.PartialKeyword);
        return Record(name, cases.Select(member => SharedTypesSyntax.PositionalRecord(member,
            [Parameter($"global::{schema.Config.TypeOwners.GetValueOrDefault(member, schema.Config.GeneratedNamespace)}.{member}", "Value")], partial)), partial).AddAttributeLists(Attribute("Union"));
    }

    private void CollectFields(string name, HashSet<string> fields, HashSet<string> visited)
    {
        if (!visited.Add(name) || !schema.InterfaceTypes.TryGetValue(name, out var contract)) return;
        fields.UnionWith(contract.Fields.Select(field => field.Name.Value));
        foreach (var parent in contract.Interfaces) CollectFields(parent.Name.Value, fields, visited);
    }
}
