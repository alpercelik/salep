using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using static Salep.ClientGenerator.Emission.Syntax.Cs;

namespace Salep.ClientGenerator.Emission.Testing;

internal static class HttpHandlerTestSyntax
{
    public static MemberDeclarationSyntax Generate() => Class("TestHttpMessageHandler",
    [
        Field("Func<HttpRequestMessage, HttpResponseMessage>", "_handler"),
        Constructor("TestHttpMessageHandler", [Parameter("Func<HttpRequestMessage, HttpResponseMessage>", "handler")], Set("_handler", Name("handler"))),
        PrivateSet("HttpRequestMessage?", "LastRequest"), PrivateSet("string?", "ContentText"),
        Method("Task<HttpResponseMessage>", "SendAsync", [Parameter("HttpRequestMessage", "request"), Parameter("CancellationToken", "cancellationToken")],
        [
            Set("LastRequest", Name("request")),
            Set("ContentText", Conditional(IsNull(Name("request.Content")), Null,
                Call(Member(Call(Member(Call("request.Content.ReadAsStringAsync", Name("cancellationToken")), "GetAwaiter")), "GetResult")))),
            Return(Call("Task.FromResult", Call("_handler", Name("request"))))
        ], Modifiers(SyntaxKind.ProtectedKeyword, SyntaxKind.OverrideKeyword)),
        Method("HttpResponseMessage", "JsonResponse", [Parameter("string", "json")],
            [Return(((ObjectCreationExpressionSyntax)New("HttpResponseMessage", Name("HttpStatusCode.OK"))).WithInitializer(SyntaxFactory.InitializerExpression(SyntaxKind.ObjectInitializerExpression,
                SyntaxFactory.SingletonSeparatedList(Assign("Content", New("StringContent", Name("json")))))))], PublicStatic)
    ], Modifiers(SyntaxKind.InternalKeyword, SyntaxKind.SealedKeyword), "HttpMessageHandler");

    private static PropertyDeclarationSyntax PrivateSet(string type, string name)
    {
        var property = Property(type, name, init: false);
        return property.WithAccessorList(property.AccessorList!.WithAccessors(SyntaxFactory.List(property.AccessorList.Accessors.Select(accessor =>
            accessor.IsKind(SyntaxKind.SetAccessorDeclaration) ? accessor.WithModifiers(Modifiers(SyntaxKind.PrivateKeyword)) : accessor))));
    }
}
