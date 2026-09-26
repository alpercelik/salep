using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Salep.ClientGenerator.Config;
using Salep.ClientGenerator.Emission.Client;
using static Salep.ClientGenerator.Emission.Syntax.Cs;
using static Salep.ClientGenerator.Emission.Testing.TestSyntax;

namespace Salep.ClientGenerator.Emission.Testing;

internal sealed class PayloadTestSyntax(GeneratorConfig config, ClientNames names)
{
    private readonly TestSyntax _test = new(config, names);
    public MemberDeclarationSyntax Generate()
    {
        List<MemberDeclarationSyntax> members =
        [
            Record("DummyResponse", []), SharedTypesSyntax.PositionalRecord("DummyVariables", [Parameter("string", "Value")]),
            Record("DummyUser", []), Record("DummyUsersResponse", [Property("List<DummyUser>?", "Users")]),
            Record("DummyListResponse", [Property("List<string?>", "Posts", initializer: New("List<string?>"))]),
            SerializePayload(), OmitNullVariables(), Multipart(),
            .. new ErrorResponseTestSyntax(_test).Generate()
        ];
        if (config.UseHttpGet) members.Add(Get());
        if (config.EnableBatching) members.Add(Batch());
        return Class("GraphQLClientPayloadTests", members);
    }

    private IEnumerable<StatementSyntax> Operation(bool query, bool variables = false) =>
    [
        Local("operation", GenericCall("Substitute", "For", [Type($"{names.OperationContract}<DummyResponse, DummyVariables>")])),
        Statement(Call("operation.OperationName.Returns", String("TestOp"))),
        Statement(Call("operation.Query.Returns", String(query ? "query TestOp { __typename }" : "mutation TestOp { __typename }"))),
        Statement(Call("operation.Variables.Returns", variables ? New("DummyVariables", String("value")) : SyntaxFactory.CastExpression(Type("DummyVariables?"), Null)))
    ];
    private static StatementSyntax Execute() => Statement(Await(Call("client.ExecuteAsync", Name("operation"), TestCancellation), configureAwait: false));
    private static IEnumerable<StatementSyntax> ReadPayload() =>
    [Local("payload", Call("handler.ContentText.ShouldNotBeNull")), Local("document", Call("JsonDocument.Parse", Name("payload")), disposable: true)];
    private static ExpressionSyntax JsonProperty(ExpressionSyntax value, string name) => Call(Member(value, "GetProperty"), String(name));
    private static ExpressionSyntax JsonString(ExpressionSyntax value) => Suppress(Call(Member(value, "GetString")));

    private MethodDeclarationSyntax SerializePayload() => Fact("ExecuteAsync_SerializesOperationPayload",
    [
        .. _test.Client(_test.Json("{\"data\":null}")), .. Operation(query: false, variables: true), Execute(), .. ReadPayload(),
        Assert(JsonString(JsonProperty(Name("document.RootElement"), "operationName")), "ShouldBe", String("TestOp")),
        Assert(JsonString(JsonProperty(Name("document.RootElement"), "query")), "ShouldContain", String("TestOp")),
        Assert(JsonString(JsonProperty(JsonProperty(Name("document.RootElement"), "variables"), "value")), "ShouldBe", String("value"))
    ], async: true);
    private MethodDeclarationSyntax OmitNullVariables() => Fact("ExecuteAsync_Omits_Null_Variables",
    [
        .. _test.Client(_test.Json("{\"data\":null}")), .. Operation(query: false), Execute(), .. ReadPayload(),
        Assert(Invoke(Name("document.RootElement.TryGetProperty"), SyntaxFactory.Argument(String("variables")),
            SyntaxFactory.Argument(Name("_")).WithRefKindKeyword(SyntaxFactory.Token(SyntaxKind.OutKeyword))), "ShouldBeFalse")
    ], async: true);
    private MethodDeclarationSyntax Get()
    {
        var uri = new UriBuilder("https://example.com/graphql")
        {
            Query = "query=" + System.Net.WebUtility.UrlEncode("query TestOp { __typename }") + "&operationName=TestOp"
        }.Uri;
        var fits = uri.AbsoluteUri.Length <= config.MaxGetUrlLength;
        List<StatementSyntax> statements =
        [
            .. _test.Client(_test.Json("{\"data\":null}")), .. Operation(query: true), Execute(),
            Local("request", Call("handler.LastRequest.ShouldNotBeNull")),
            Assert("request.Method", "ShouldBe", Name(fits ? "HttpMethod.Get" : "HttpMethod.Post"))
        ];
        if (fits)
        {
            statements.Add(Assert("request.RequestUri", "ShouldNotBeNull"));
            statements.Add(Assert(Member(Suppress(Name("request.RequestUri")), "Query"), "ShouldContain", String("operationName=TestOp")));
            statements.Add(Assert("request.RequestUri.Query", "ShouldContain", String("query=")));
        }
        return Fact(fits ? "ExecuteAsync_Uses_Get_For_Query" : "ExecuteAsync_Falls_Back_To_Post_For_Long_Query_Url", statements, async: true);
    }
    private MethodDeclarationSyntax Batch() => Fact("ExecuteBatchAsync_Sends_Array_Payload",
    [
        .. _test.Client(_test.Json("[{\"data\":null},{\"data\":null}]")), .. Operation(query: true),
        Statement(Await(Call("client.ExecuteBatchAsync", Array(Name("operation"), Name("operation")), TestCancellation), configureAwait: false)),
        .. ReadPayload(), Assert("document.RootElement.ValueKind", "ShouldBe", Name("JsonValueKind.Array")),
        Assert(Call("document.RootElement.GetArrayLength"), "ShouldBe", Number(2))
    ], async: true);
    private MethodDeclarationSyntax Multipart() => Fact("ExecuteIncrementalAsync_Parses_Multipart_Response",
    [
        Local("boundary", String("boundary123")),
        Local("body", Call("string.Join", String("\r\n"), Array(Add(String("--"), Name("boundary")), String("Content-Type: application/json"), Name("string.Empty"),
            String("{\"data\":null}"), Add(String("--"), Name("boundary")), String("Content-Type: application/json"), Name("string.Empty"), String("{\"data\":null}"), Add(Add(String("--"), Name("boundary")), String("--"))))),
        Local("response", ((ObjectCreationExpressionSyntax)New("HttpResponseMessage", Name("HttpStatusCode.OK"))).WithInitializer(SyntaxFactory.InitializerExpression(SyntaxKind.ObjectInitializerExpression,
            SyntaxFactory.SingletonSeparatedList(Assign("Content", New("StringContent", Name("body"), Name("Encoding.UTF8"))))))),
        Set("response.Content.Headers.ContentType", New("MediaTypeHeaderValue", String("multipart/mixed"))),
        Statement(Call("response.Content.Headers.ContentType.Parameters.Add", New("NameValueHeaderValue", String("boundary"), Name("boundary")))),
        Local("handler", New("TestHttpMessageHandler", Lambda("_", Name("response")))), Local("httpClient", New("HttpClient", Name("handler"))),
        Local("client", New(names.Client, Name("httpClient"), New("Uri", String("https://example.com/graphql")))), .. Operation(query: true),
        Local("results", New("List<GraphQLResponse<DummyResponse>>")),
        ((ForEachStatementSyntax)ForEach("item", Call("client.ExecuteIncrementalAsync", Name("operation"), TestCancellation), Statement(Call("results.Add", Name("item")))))
            .WithAwaitKeyword(SyntaxFactory.Token(SyntaxKind.AwaitKeyword)),
        Assert("results.Count", "ShouldBe", Number(2))
    ], async: true);
}
