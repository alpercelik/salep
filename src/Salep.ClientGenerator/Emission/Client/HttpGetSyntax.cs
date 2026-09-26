using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using static Salep.ClientGenerator.Emission.Syntax.Cs;

namespace Salep.ClientGenerator.Emission.Client;

internal sealed class HttpGetSyntax(ClientNames names)
{
    public IEnumerable<MemberDeclarationSyntax> Generate() =>
    [
        Method("bool", "IsQueryOperation", [Parameter("string", "query")],
            [Return(Call(Member(Call("query.TrimStart"), "StartsWith"), String("query"), Name("StringComparison.OrdinalIgnoreCase")))], Modifiers(SyntaxKind.PrivateKeyword)),
        Method("Uri?", "BuildGetUri", [Parameter($"{names.OperationContract}<TResponse, TVariables>", "operation")],
        [
            Local("variablesJson", Conditional(IsNull(Name("operation.Variables")), Null, Call("JsonSerializer.Serialize", Name("operation.Variables"), Name("_jsonOptions")))),
            Local("builder", New("UriBuilder", Name("_endpoint"))), Local("queryBuilder", New("StringBuilder")),
            Append("query", Name("operation.Query")), Append("operationName", Name("operation.OperationName")),
            If(Not(Call("string.IsNullOrWhiteSpace", Name("variablesJson"))), Append("variables", Name("variablesJson"))),
            Set("builder.Query", Call("queryBuilder.ToString")), Local("uri", Name("builder.Uri")),
            Return(Conditional(Binary(SyntaxKind.LessThanOrEqualExpression, Name("uri.AbsoluteUri.Length"), Name("_maxGetUrlLength")), Name("uri"), Null))
        ], Modifiers(SyntaxKind.PrivateKeyword), "TResponse", "TVariables"),
        Method("void", "AppendQueryParam", [Parameter("StringBuilder", "builder"), Parameter("string", "name"), Parameter("string", "value")],
        [
            If(Binary(SyntaxKind.GreaterThanExpression, Name("builder.Length"), Number(0)), Statement(Call("builder.Append", Char('&')))),
            Statement(Call("builder.Append", Call("WebUtility.UrlEncode", Name("name")))), Statement(Call("builder.Append", Char('='))),
            Statement(Call("builder.Append", Call("WebUtility.UrlEncode", Name("value"))))
        ], Modifiers(SyntaxKind.PrivateKeyword, SyntaxKind.StaticKeyword))
    ];
    private static StatementSyntax Append(string name, ExpressionSyntax value) => Statement(Call("AppendQueryParam", Name("queryBuilder"), String(name), value));
}
