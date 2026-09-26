using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using static Salep.ClientGenerator.Emission.Syntax.Cs;

namespace Salep.ClientGenerator.Emission.Client;

internal static class MultipartSyntax
{
    public static IEnumerable<MemberDeclarationSyntax> Generate() =>
    [
        Method("bool", "IsMultipart", [Parameter("string?", "contentType"), Parameter("string", "boundary").WithModifiers(Modifiers(SyntaxKind.OutKeyword))],
        [
            Set("boundary", Name("string.Empty")),
            If(Call("string.IsNullOrWhiteSpace", Name("contentType")), Return(Bool(false))),
            If(Not(Call("contentType.Contains", String("multipart/mixed"), Name("StringComparison.OrdinalIgnoreCase"))), Return(Bool(false))),
            Local("boundaryIndex", Call("contentType.IndexOf", String("boundary="), Name("StringComparison.OrdinalIgnoreCase"))),
            If(Binary(SyntaxKind.LessThanExpression, Name("boundaryIndex"), Number(0)), Return(Bool(false))),
            Local("value", Call(Member(Call("contentType.Substring", Add(Name("boundaryIndex"), Member(String("boundary="), "Length"))), "Trim"))),
            Set("boundary", Call("value.Trim", Char('"'))), Return(Not(Call("string.IsNullOrWhiteSpace", Name("boundary"))))
        ], Modifiers(SyntaxKind.PrivateKeyword, SyntaxKind.StaticKeyword)),
        Method("IEnumerable<string>", "SplitMultipartJson", [Parameter("string", "body"), Parameter("string", "boundary")],
        [
            Local("delimiter", Add(String("--"), Name("boundary"))),
            Local("sections", Call("body.Split", Array(Name("delimiter")), Name("StringSplitOptions.RemoveEmptyEntries"))),
            ForEach("section", Name("sections"),
                If(Call("section.StartsWith", String("--"), Name("StringComparison.Ordinal")), SyntaxFactory.ContinueStatement()),
                Local("headerEnd", Call("section.IndexOf", String("\r\n\r\n"), Name("StringComparison.Ordinal"))),
                If(Binary(SyntaxKind.LessThanExpression, Name("headerEnd"), Number(0)), SyntaxFactory.ContinueStatement()),
                Local("json", Call(Member(Call("section.Substring", Add(Name("headerEnd"), Number(4))), "Trim"))),
                If(Equal(Name("json.Length"), Number(0)), SyntaxFactory.ContinueStatement()), Yield(Name("json")))
        ], Modifiers(SyntaxKind.PrivateKeyword, SyntaxKind.StaticKeyword))
    ];
}
