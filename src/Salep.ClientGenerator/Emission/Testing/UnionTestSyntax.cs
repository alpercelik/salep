using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Salep.ClientGenerator.Model;
using Salep.ClientGenerator.Utilities;
using static Salep.ClientGenerator.Emission.Syntax.Cs;
using static Salep.ClientGenerator.Emission.Testing.TestSyntax;

namespace Salep.ClientGenerator.Emission.Testing;

internal sealed class UnionTestSyntax(SchemaModel schema)
{
    private readonly SampleValues _values = new(schema);
    public MemberDeclarationSyntax Generate()
    {
        var tests = new List<MemberDeclarationSyntax>();
        foreach (var union in schema.UnionTypes.Values)
        {
            var name = CSharpNaming.ToTypeName(union.Name.Value);
            tests.AddRange(Variants(name, union.Types.Select(type => type.Name.Value).ToArray()));
            if (union.Types.Count > 0) tests.AddRange(InvalidValues(name));
        }
        foreach (var contract in schema.InterfaceTypes.Values)
            tests.AddRange(Variants(SchemaModel.GetInterfaceResultTypeName(contract.Name.Value), schema.GetInterfaceImplementations(contract.Name.Value).Select(type => type.Name.Value).ToArray()));
        tests.Add(JsonOptions());
        return Class("UnionConverterTests", tests);
    }
    private IEnumerable<MemberDeclarationSyntax> Variants(string name, IReadOnlyList<string> cases)
    {
        foreach (var wireName in cases)
        {
            var member = CSharpNaming.ToTypeName(wireName);
            var body = new List<StatementSyntax>
            {
                Local("sample", _values.Object(wireName)), Local("options", Call("CreateJsonOptions")),
                Local("node", Call(Member(Suppress(Call("JsonSerializer.SerializeToNode", Name("sample"), Name("options"))), "AsObject"))),
                Statement(Assign(SyntaxFactory.ElementAccessExpression(Name("node"), SyntaxFactory.BracketedArgumentList(SyntaxFactory.SingletonSeparatedList(SyntaxFactory.Argument(String("__typename"))))), String(wireName))),
                Local("json", Call("node.ToJsonString", NewObject("JsonSerializerOptions", [Assign("WriteIndented", Bool(true))]))),
                Local("result", GenericCall("JsonSerializer", "Deserialize", [Type(name)], Name("json"), Name("options")))
            };
            if (schema.Config.UseNativeUnions)
                body.Add(Assert(SyntaxFactory.ParenthesizedExpression(SyntaxFactory.IsPatternExpression(Name("result.Value"), SyntaxFactory.TypePattern(Type(member)))), "ShouldBeTrue"));
            else
            {
                body.Add(Assert("result", "ShouldNotBeNull"));
                body.Add(Statement(GenericCall("result", "ShouldBeOfType", [Type($"{name}.{member}")])));
            }
            yield return Fact($"{name}_Deserializes_{member}", body);
        }
        if (cases.FirstOrDefault() is not { } first) yield break;
        var firstName = CSharpNaming.ToTypeName(first);
        var value = _values.Object(first);
        yield return Fact($"{name}_Write_Serializes_{firstName}",
        [
            schema.Config.UseNativeUnions ? Local("value", value, type: name) : Local("value", New($"{name}.{firstName}", value)),
            Local("options", Call("CreateJsonOptions")), Local("json", GenericCall("JsonSerializer", "Serialize", [Type(name)], Name("value"), Name("options"))),
            Assert("json", "ShouldNotBeNullOrWhiteSpace")
        ]);
    }
    private static IEnumerable<MemberDeclarationSyntax> InvalidValues(string name) =>
    [
        Fact($"{name}_Read_Returns_Null_When_Json_Null", [Local("options", Call("CreateJsonOptions")),
            Local("result", GenericCall("JsonSerializer", "Deserialize", [Type($"{name}?")], String("null"), Name("options"))), Assert("result", "ShouldBeNull")]),
        Throws(name, "Missing", "{\"id\":\"sample\"}"), Throws(name, "Unknown", "{\"__typename\":\"Unknown\"}")
    ];
    private static MemberDeclarationSyntax Throws(string name, string scenario, string json) => Fact($"{name}_Read_Throws_When_Typename_{scenario}",
    [
        Local("options", Call("CreateJsonOptions")), Local("json", String(json)),
        Statement(GenericCall("Should", "Throw", [Type("JsonException")], SyntaxFactory.ParenthesizedLambdaExpression(
            GenericCall("JsonSerializer", "Deserialize", [Type(name)], Name("json"), Name("options")))))
    ]);
    private MethodDeclarationSyntax JsonOptions()
    {
        var body = new List<StatementSyntax>
        {
            Local("options", NewObject("JsonSerializerOptions", [Assign("PropertyNamingPolicy", Name("JsonNamingPolicy.CamelCase")), Assign("PropertyNameCaseInsensitive", Bool(true))])),
            Statement(Call("options.Converters.Add", New("JsonStringEnumConverter")))
        };
        if (schema.Config.RequiresNodaTime()) body.Add(Statement(Call("options.ConfigureForNodaTime", Name("DateTimeZoneProviders.Tzdb"))));
        foreach (var registry in schema.Config.ConverterRegistries.DefaultIfEmpty("UnionJsonConverters"))
            body.Add(Statement(Call(registry + ".Register", Name("options"))));
        body.Add(Return(Name("options")));
        return Method("JsonSerializerOptions", "CreateJsonOptions", [], body, Modifiers(SyntaxKind.PrivateKeyword, SyntaxKind.StaticKeyword));
    }
}
