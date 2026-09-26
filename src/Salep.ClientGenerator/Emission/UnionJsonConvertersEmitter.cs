using Microsoft.CodeAnalysis.CSharp.Syntax;
using Salep.ClientGenerator.Model;
using Salep.ClientGenerator.Utilities;
using Salep.ClientGenerator.Emission.Syntax;
using static Salep.ClientGenerator.Emission.Syntax.Cs;

namespace Salep.ClientGenerator.Emission;

internal sealed class UnionJsonConvertersEmitter(SchemaModel schema, IReadOnlySet<string>? includedNames = null)
{
    public string Generate()
    {
        var unions = schema.UnionTypes.Values.Where(union => includedNames is null || includedNames.Contains(union.Name.Value)).Select(union =>
            (Name: CSharpNaming.ToTypeName(union.Name.Value), Kind: "union", Cases: union.Types.Select(type => type.Name.Value).ToArray()))
            .Concat(schema.InterfaceTypes.Values.Where(contract => includedNames is null || includedNames.Contains(contract.Name.Value)).Select(contract =>
                (Name: SchemaModel.GetInterfaceResultTypeName(contract.Name.Value), Kind: "interface", Cases: schema.GetInterfaceImplementations(contract.Name.Value).Select(type => type.Name.Value).ToArray()))
                .Where(union => union.Cases.Length > 0)).ToArray();
        var members = new List<MemberDeclarationSyntax>
        {
            Class("UnionJsonConverters", [Method("void", "Register", [Parameter("JsonSerializerOptions", "options")],
                unions.Select(union => Statement(Call("options.Converters.Add", New($"{union.Name}JsonConverter")))), PublicStatic)], PublicStatic)
        };
        foreach (var union in unions)
        {
            var cases = union.Cases.Select(wireName => new JsonVariant(wireName,
                $"global::{schema.Config.TypeOwners.GetValueOrDefault(CSharpNaming.ToTypeName(wireName), schema.Config.GeneratedNamespace)}.{CSharpNaming.ToTypeName(wireName)}", $"{union.Name}.{CSharpNaming.ToTypeName(wireName)}")).ToArray();
            members.Add(new JsonConverterSyntax(union.Name, cases, schema.Config.UseNativeUnions ? UnionRepresentation.Native : UnionRepresentation.Dunet,
                $" for {union.Kind} {union.Name}").Generate());
        }
        return RoslynEmitter.EmitFile(schema.Config, schema.Config.GeneratedNamespace, ["System", "System.Text.Json", "System.Text.Json.Serialization"], members);
    }
}
