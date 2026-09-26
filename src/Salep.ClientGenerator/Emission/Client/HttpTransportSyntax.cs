using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Salep.ClientGenerator.Config;
using static Salep.ClientGenerator.Emission.Syntax.Cs;

namespace Salep.ClientGenerator.Emission.Client;

// Configuration decides which behavior to emit; syntax helpers only describe C#.
internal sealed class HttpTransportSyntax(GeneratorConfig config, ClientNames names, bool registerConverters)
{
    private const string Response = "GraphQLResponse<TResponse>";
    private string Operation => $"{names.OperationContract}<TResponse, TVariables>";
    private static ExpressionSyntax Cancel => Name("cancellationToken");

    public ClassDeclarationSyntax Generate() => Class(names.Client,
    [
        Field("HttpClient", "_httpClient"), Field("Uri", "_endpoint"), Field("JsonSerializerOptions", "_jsonOptions"),
        Field("bool", "_useHttpGet", Bool(config.UseHttpGet)), Field("bool", "_enableBatching", Bool(config.EnableBatching)),
        Field("int", "_maxGetUrlLength", Number(config.MaxGetUrlLength)), CreateConstructor(), Execute(), Batch(),
        new IncrementalTransportSyntax(names).Generate(), Post(), .. new HttpGetSyntax(names).Generate(), .. MultipartSyntax.Generate()
    ]);

    private ConstructorDeclarationSyntax CreateConstructor()
    {
        var statements = new List<StatementSyntax>
        {
            Set("_httpClient", Name("httpClient")), Set("_endpoint", Name("endpoint")),
            Set("_jsonOptions", NewObject("JsonSerializerOptions", [
                Assign("PropertyNamingPolicy", Name("JsonNamingPolicy.CamelCase")), Assign("PropertyNameCaseInsensitive", Bool(true)),
                Assign("DefaultIgnoreCondition", Name("JsonIgnoreCondition.WhenWritingNull"))])),
            Statement(Call("_jsonOptions.Converters.Add", New("JsonStringEnumConverter")))
        };
        if (config.RequiresNodaTime()) statements.Add(Statement(Call("_jsonOptions.ConfigureForNodaTime", Name("DateTimeZoneProviders.Tzdb"))));
        if (registerConverters)
            foreach (var registry in config.ConverterRegistries.DefaultIfEmpty("UnionJsonConverters"))
                statements.Add(Statement(Call(registry + ".Register", Name("_jsonOptions"))));
        return Constructor(names.Client, [Parameter("HttpClient", "httpClient"), Parameter("Uri", "endpoint")], statements.ToArray());
    }

    private MethodDeclarationSyntax Execute() => Method($"Task<{Response}>", "ExecuteAsync",
        [Parameter(Operation, "operation"), Cancellation()],
        [
            If(And(Name("_useHttpGet"), Call("IsQueryOperation", Name("operation.Query"))),
                Local("getUri", Call("BuildGetUri", Name("operation"))),
                If(IsNull(Name("getUri"), negated: true), [
                    Local("response", Await(Call("_httpClient.GetAsync", Name("getUri"), Cancel)), disposable: true),
                    .. ReadResponse(Response, New(Response))])),
            Return(Await(Call("ExecutePostAsync", Name("operation"), Cancel)))
        ], Modifiers(SyntaxKind.PublicKeyword, SyntaxKind.AsyncKeyword), "TResponse", "TVariables");

    private MethodDeclarationSyntax Batch() => Method($"Task<{Response}[]>", "ExecuteBatchAsync",
        [Parameter($"IReadOnlyList<{Operation}>", "operations"), Cancellation()],
        [
            If(Not(Name("_enableBatching")), Throw("Batching is disabled.")),
            Local("requests", Call(Member(Call("operations.Select", Lambda("item", New("GraphQLRequest<TVariables>",
                Name("item.Query"), Name("item.OperationName"), Name("item.Variables")))), "ToArray"))),
            .. SendPost("requests"),
            .. ReadResponse($"{Response}[]", GenericCall("Array", "Empty", [Type(Response)]))
        ], Modifiers(SyntaxKind.PublicKeyword, SyntaxKind.AsyncKeyword), "TResponse", "TVariables");

    private MethodDeclarationSyntax Post() => Method($"Task<{Response}>", "ExecutePostAsync",
        [Parameter(Operation, "operation"), Cancellation(optional: false)],
        [Request(), .. SendPost("request"), .. ReadResponse(Response, New(Response))],
        Modifiers(SyntaxKind.PrivateKeyword, SyntaxKind.AsyncKeyword), "TResponse", "TVariables");

    internal static StatementSyntax Request() => Local("request", New("GraphQLRequest<TVariables>", Name("operation.Query"), Name("operation.OperationName"), Name("operation.Variables")));
    internal static IEnumerable<StatementSyntax> SendPost(string request) =>
    [
        Local("payload", Call("JsonSerializer.Serialize", Name(request), Name("_jsonOptions"))),
        Local("content", New("StringContent", Name("payload"), Name("Encoding.UTF8"), String("application/json")), disposable: true),
        Local("response", Await(Call("_httpClient.PostAsync", Name("_endpoint"), Name("content"), Cancel)), disposable: true)
    ];
    private static IEnumerable<StatementSyntax> ReadResponse(string responseType, ExpressionSyntax fallback) =>
    [
        Statement(Call("response.EnsureSuccessStatusCode")),
        Local("stream", Await(Call("response.Content.ReadAsStreamAsync", Cancel))),
        Local("result", Await(GenericCall("JsonSerializer", "DeserializeAsync", [Type(responseType)], Name("stream"), Name("_jsonOptions"), Cancel))),
        Return(Coalesce(Name("result"), fallback))
    ];
}
