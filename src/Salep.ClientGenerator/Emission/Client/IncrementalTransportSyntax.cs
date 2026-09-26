using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using static Salep.ClientGenerator.Emission.Syntax.Cs;

namespace Salep.ClientGenerator.Emission.Client;

internal sealed class IncrementalTransportSyntax(ClientNames names)
{
    public MethodDeclarationSyntax Generate() => Method("IAsyncEnumerable<GraphQLResponse<TResponse>>", "ExecuteIncrementalAsync",
        [Parameter($"{names.OperationContract}<TResponse, TVariables>", "operation"), Cancellation(enumerator: true)],
        [
            HttpTransportSyntax.Request(), .. HttpTransportSyntax.SendPost("request"), Statement(Call("response.EnsureSuccessStatusCode")),
            Local("contentType", SyntaxFactory.ConditionalAccessExpression(Name("response.Content.Headers.ContentType"),
                Call(SyntaxFactory.MemberBindingExpression(SyntaxFactory.IdentifierName("ToString"))))),
            Local("stream", Await(Call("response.Content.ReadAsStreamAsync", Name("cancellationToken")))),
            If(Invoke(Name("IsMultipart"), SyntaxFactory.Argument(Name("contentType")), OutVar("boundary")),
                Local("reader", New("StreamReader", Name("stream"), Name("Encoding.UTF8")), disposable: true),
                Local("body", Await(Call("reader.ReadToEndAsync", Name("cancellationToken")))),
                ForEach("partJson", Call("SplitMultipartJson", Name("body"), Name("boundary")),
                    Local("result", GenericCall("JsonSerializer", "Deserialize", [Type("GraphQLResponse<TResponse>")], Name("partJson"), Name("_jsonOptions"))),
                    If(IsNull(Name("result"), negated: true), Yield(Name("result")))),
                SyntaxFactory.YieldStatement(SyntaxKind.YieldBreakStatement)),
            Local("single", Await(GenericCall("JsonSerializer", "DeserializeAsync", [Type("GraphQLResponse<TResponse>")], Name("stream"), Name("_jsonOptions"), Name("cancellationToken")))),
            If(IsNull(Name("single"), negated: true), Yield(Name("single")))
        ], Modifiers(SyntaxKind.PublicKeyword, SyntaxKind.AsyncKeyword), "TResponse", "TVariables");
}
