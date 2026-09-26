using System.Text.Json;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Salep.ClientGenerator.Config;
using Salep.ClientGenerator.Emission.Client;
using static Salep.ClientGenerator.Emission.Syntax.Cs;

namespace Salep.ClientGenerator.Emission.Testing;

internal sealed class TestSyntax(GeneratorConfig config, ClientNames names)
{
    public static ExpressionSyntax TestCancellation => Name("TestContext.Current.CancellationToken");
    public static MethodDeclarationSyntax Fact(string name, IEnumerable<StatementSyntax> body, bool async = false)
        => Method(async ? "Task" : "void", name, [], body, async ? Modifiers(SyntaxKind.PublicKeyword, SyntaxKind.AsyncKeyword) : Public).AddAttributeLists(Attribute("Fact"));
    public static StatementSyntax Assert(ExpressionSyntax actual, string assertion, params ExpressionSyntax[] expected)
        => Statement(Call(Member(actual, assertion), expected));
    public static StatementSyntax Assert(string actual, string assertion, params ExpressionSyntax[] expected) => Assert(Name(actual), assertion, expected);
    public ExpressionSyntax Json(string json)
    {
        try
        {
            using var document = JsonDocument.Parse(json);
            json = JsonSerializer.Serialize(document.RootElement, new JsonSerializerOptions { WriteIndented = true });
        }
        catch (JsonException) { /* Invalid payload fixtures must preserve their original bytes. */ }
        if (!config.UseRawStrings) return String(json);
        // Literal spelling is constructed once here; emitted C# members and bodies remain syntax nodes.
        var longestRun = 0;
        var run = 0;
        foreach (var character in json)
        {
            run = character == '"' ? run + 1 : 0;
            longestRun = Math.Max(longestRun, run);
        }
        var quoteCount = Math.Max(3, longestRun + 1);
        var delimiter = new string('"', quoteCount);
        return SyntaxFactory.LiteralExpression(SyntaxKind.StringLiteralExpression,
            SyntaxFactory.Token(default, SyntaxKind.MultiLineRawStringLiteralToken, delimiter + "\n" + json + "\n" + delimiter, json, default));
    }
    public IEnumerable<StatementSyntax> Client(ExpressionSyntax json) =>
    [
        Local("handler", New("TestHttpMessageHandler", Lambda("_", Call("TestHttpMessageHandler.JsonResponse", json)))),
        Local("httpClient", New("HttpClient", Name("handler"))),
        Local("client", New(names.Client, Name("httpClient"), New("Uri", String("https://example.com/graphql"))))
    ];
    public static ExpressionSyntax Index(ExpressionSyntax value, int index) => SyntaxFactory.ElementAccessExpression(value,
        SyntaxFactory.BracketedArgumentList(SyntaxFactory.SingletonSeparatedList(SyntaxFactory.Argument(Number(index)))));
    public static ExpressionSyntax Index(string value, int index) => Index(Name(value), index);
}
