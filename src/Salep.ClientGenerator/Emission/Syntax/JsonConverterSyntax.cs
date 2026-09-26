using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using static Salep.ClientGenerator.Emission.Syntax.Cs;

namespace Salep.ClientGenerator.Emission.Syntax;

internal enum UnionRepresentation { Dunet, Native, Projection }
internal sealed record JsonVariant(string WireName, string TypeName, string? WrapperType = null);

// Shared mechanics for schema unions, interface results and operation-specific polymorphic shapes.
internal sealed class JsonConverterSyntax(string name, IReadOnlyList<JsonVariant> variants, UnionRepresentation representation, string description)
{
    public ClassDeclarationSyntax Generate() => Class($"{name}JsonConverter", [Read(), Write()], bases: [$"JsonConverter<{name}>"]);

    private MethodDeclarationSyntax Read()
    {
        var body = new List<StatementSyntax>();
        if (representation != UnionRepresentation.Projection)
            body.Add(If(Equal(Name("reader.TokenType"), Name("JsonTokenType.Null")), representation == UnionRepresentation.Native
                ? Throw($"A null JSON value cannot be deserialized as union {name}.", "JsonException") : Return(Null)));
        body.Add(Local("document", Invoke(Name("JsonDocument.ParseValue"), Ref("reader")), disposable: true));
        body.Add(If(Not(Invoke(Name("document.RootElement.TryGetProperty"), SyntaxFactory.Argument(String("__typename")), OutVar("typeNameElement"))),
            Throw($"Missing __typename{description}.", "JsonException")));
        body.Add(Local("typeName", Call("typeNameElement.GetString")));
        if (representation != UnionRepresentation.Projection)
            body.Add(If(Call("string.IsNullOrWhiteSpace", Name("typeName")), Throw($"Missing __typename{description}.", "JsonException")));
        body.Add(Local("json", Call("document.RootElement.GetRawText")));
        var arms = variants.Select(variant => SyntaxFactory.SwitchExpressionArm(SyntaxFactory.ConstantPattern(String(variant.WireName)), Deserialize(variant))).ToList();
        var unknown = representation == UnionRepresentation.Projection ? String("Unknown __typename.")
            : Add(Add(String("Unknown __typename '"), Name("typeName")), String($"'{description}."));
        arms.Add(SyntaxFactory.SwitchExpressionArm(SyntaxFactory.DiscardPattern(), ThrowExpression("JsonException", unknown)));
        body.Add(Return(SyntaxFactory.SwitchExpression(Name("typeName")).WithArms(SyntaxFactory.SeparatedList(arms))));
        return Method(representation == UnionRepresentation.Native ? name : $"{name}?", "Read",
            [Parameter("Utf8JsonReader", "reader").WithModifiers(Modifiers(SyntaxKind.RefKeyword)), Parameter("Type", "typeToConvert"), Parameter("JsonSerializerOptions", "options")],
            body, Modifiers(SyntaxKind.PublicKeyword, SyntaxKind.OverrideKeyword));
    }

    private ExpressionSyntax Deserialize(JsonVariant variant)
    {
        var value = GenericCall("JsonSerializer", "Deserialize", [Type(variant.TypeName)], Name("json"), Name("options"));
        return representation switch
        {
            UnionRepresentation.Native => SyntaxFactory.CastExpression(Type(name), Suppress(value)),
            UnionRepresentation.Dunet => New(variant.WrapperType!, Suppress(value)),
            _ => value
        };
    }

    private MethodDeclarationSyntax Write()
    {
        var body = new List<StatementSyntax>();
        if (representation == UnionRepresentation.Dunet)
            body.Add(If(IsNull(Name("value")), Statement(Call("writer.WriteNullValue")), Return()));
        var sections = variants.Select(variant => SyntaxFactory.SwitchSection()
            .WithLabels(SyntaxFactory.SingletonList<SwitchLabelSyntax>(SyntaxFactory.CasePatternSwitchLabel(
                SyntaxFactory.DeclarationPattern(Type(representation == UnionRepresentation.Dunet ? variant.WrapperType! : variant.TypeName), SyntaxFactory.SingleVariableDesignation(Identifier("variant"))),
                SyntaxFactory.Token(SyntaxKind.ColonToken))))
            .WithStatements(SyntaxFactory.List<StatementSyntax>([
                Statement(Call("JsonSerializer.Serialize", Name("writer"), Name(representation == UnionRepresentation.Dunet ? "variant.Value" : "variant"), Name("options"))), Return()]))).ToList();
        sections.Add(SyntaxFactory.SwitchSection().WithLabels(SyntaxFactory.SingletonList<SwitchLabelSyntax>(SyntaxFactory.DefaultSwitchLabel()))
            .WithStatements(SyntaxFactory.SingletonList(Throw(representation == UnionRepresentation.Projection ? "Unknown response variant." : $"Unknown variant{description}.", "JsonException"))));
        body.Add(SyntaxFactory.SwitchStatement(Name(representation == UnionRepresentation.Native ? "value.Value" : "value")).WithSections(SyntaxFactory.List(sections)));
        return Method("void", "Write", [Parameter("Utf8JsonWriter", "writer"), Parameter(name, "value"), Parameter("JsonSerializerOptions", "options")], body,
            Modifiers(SyntaxKind.PublicKeyword, SyntaxKind.OverrideKeyword));
    }
}
