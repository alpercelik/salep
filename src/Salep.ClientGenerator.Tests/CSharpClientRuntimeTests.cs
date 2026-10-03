using System.Net;
using System.Net.Http.Headers;
using System.Reflection;
using System.Runtime.Loader;
using System.Text;
using System.Text.Json;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Salep.ClientGenerator.Model;
using Salep.ClientGenerator.Targets;
using Salep.GraphQLParser;
using Xunit;

namespace Salep.ClientGenerator.Tests;

public sealed class CSharpClientRuntimeTests
{
    [Fact]
    public async Task GeneratedClient_SendsPostAndDeserializesResponse()
    {
        var schema = GraphQlModelFactory.CreateSchema(Parse("type Product { id: ID! } type Query { product(id: ID!): Product! }"));
        var executable = GraphQlModelFactory.CreateExecutable(schema, [Parse("query Lookup($id: ID!) { product(id: $id) { id } }")]);
        var generator = new ScribanCSharpTemplateGenerator();
        var target = new CSharpCodeGenerationTarget();
        var bundle = generator.GenerateClientSources(schema, executable, target,
            new CSharpClientGenerationOptions { RegisterUnionConverters = false });
        Assert.Equal(new[] { "SchemaTypes.cs", "Operations.cs", "GraphQLClient.cs", "UnionJsonConverters.cs", "GraphQLSharedTypes.cs" },
            bundle.Select(file => file.FileName));
        var sources = bundle.Select(file => file.Content).Concat(new[]
        {
            generator.GenerateClient(target, new CSharpClientGenerationOptions { ClientClassName = "GetGraphQLClient", UseHttpGet = true, RegisterUnionConverters = false }),
            generator.GenerateClient(target, new CSharpClientGenerationOptions { ClientClassName = "BoundedGraphQLClient", UseHttpGet = true, MaxGetUrlLength = 1, RegisterUnionConverters = false }),
            generator.GenerateClient(target, new CSharpClientGenerationOptions { ClientClassName = "BatchGraphQLClient", EnableBatching = true, RegisterUnionConverters = false })
        });
        var compilation = CSharpCompilation.Create("ScribanRuntimeFixture",
            sources.Select(source => CSharpSyntaxTree.ParseText("#nullable enable\n" + source,
                cancellationToken: TestContext.Current.CancellationToken)),
            ((string)AppContext.GetData("TRUSTED_PLATFORM_ASSEMBLIES")!).Split(Path.PathSeparator)
                .Select(path => MetadataReference.CreateFromFile(path)),
            new CSharpCompilationOptions(OutputKind.DynamicallyLinkedLibrary, nullableContextOptions: NullableContextOptions.Enable));

        using var assemblyImage = new MemoryStream();
        var emit = compilation.Emit(assemblyImage, cancellationToken: TestContext.Current.CancellationToken);
        Assert.True(emit.Success, string.Join(Environment.NewLine, emit.Diagnostics.Where(item => item.Severity == DiagnosticSeverity.Error)));

        var assembly = AssemblyLoadContext.Default.LoadFromStream(new MemoryStream(assemblyImage.ToArray()));
        var variablesType = assembly.GetType("Salep.Generated.LookupVariables", throwOnError: true)!;
        var variables = Activator.CreateInstance(variablesType)!;
        variablesType.GetProperty("Id")!.SetValue(variables, "p-42");
        var operationType = assembly.GetType("Salep.Generated.LookupOperation", throwOnError: true)!;
        var operation = Activator.CreateInstance(operationType, variables)!;
        var clientType = assembly.GetType("Salep.Generated.GraphQLClient", throwOnError: true)!;
        var handler = new RecordingHandler("{\"data\":{\"product\":{\"id\":\"p-42\"}}}");
        using var httpClient = new HttpClient(handler);
        var client = Activator.CreateInstance(clientType, httpClient, new Uri("https://example.test/graphql"))!;
        var responseType = assembly.GetType("Salep.Generated.LookupResponse", throwOnError: true)!;
        var operationsType = assembly.GetType("Salep.Generated.GraphQLOperations", throwOnError: true)!;
        var api = Activator.CreateInstance(operationsType, client)!;
        var lookup = operationsType.GetMethods().Single(method => method.Name == "LookupAsync");
        var running = (Task)lookup.Invoke(api, [variables, TestContext.Current.CancellationToken])!;
        await running.WaitAsync(TestContext.Current.CancellationToken);

        Assert.Equal(HttpMethod.Post, handler.Method);
        using var request = JsonDocument.Parse(handler.RequestBody!);
        Assert.Equal("Lookup", request.RootElement.GetProperty("operationName").GetString());
        var expectedQuery = new GraphQlOperationDocumentFormatter(schema, executable).Format(executable.Operations.Single());
        Assert.Equal(expectedQuery, operationType.GetProperty("Query")!.GetValue(operation));
        Assert.Equal(expectedQuery, request.RootElement.GetProperty("query").GetString());
        Assert.Equal("p-42", request.RootElement.GetProperty("variables").GetProperty("id").GetString());
        var data = running.GetType().GetProperty("Result")!.GetValue(running)!;
        var response = data.GetType().GetProperty("Data")!.GetValue(data)!;
        var product = responseType.GetProperty("Product")!.GetValue(response)!;
        Assert.Equal("p-42", product.GetType().GetProperty("Id")!.GetValue(product));

        var getHandler = new RecordingHandler("{\"data\":{\"product\":{\"id\":\"p-42\"}}}");
        using var getHttpClient = new HttpClient(getHandler);
        var getClient = Activator.CreateInstance(assembly.GetType("Salep.Generated.GetGraphQLClient", throwOnError: true)!, getHttpClient,
            new Uri("https://example.test/graphql"))!;
        var getExecute = getClient.GetType().GetMethods().Single(method => method.Name == "ExecuteAsync")
            .MakeGenericMethod(responseType, variablesType);
        var getRunning = (Task)getExecute.Invoke(getClient, [operation, TestContext.Current.CancellationToken])!;
        await getRunning.WaitAsync(TestContext.Current.CancellationToken);
        Assert.Equal(HttpMethod.Get, getHandler.Method);
        Assert.Contains("operationName=Lookup", getHandler.RequestUri!.Query, StringComparison.Ordinal);
        Assert.Contains("variables=", getHandler.RequestUri.Query, StringComparison.Ordinal);
        var encodedQuery = getHandler.RequestUri.Query.TrimStart('?').Split('&').Single(item => item.StartsWith("query=", StringComparison.Ordinal))["query=".Length..];
        Assert.Equal(expectedQuery, WebUtility.UrlDecode(encodedQuery));

        var boundedHandler = new RecordingHandler("{\"data\":{\"product\":{\"id\":\"p-42\"}}}");
        using var boundedHttpClient = new HttpClient(boundedHandler);
        var boundedClient = Activator.CreateInstance(assembly.GetType("Salep.Generated.BoundedGraphQLClient", throwOnError: true)!, boundedHttpClient,
            new Uri("https://example.test/graphql"))!;
        var boundedExecute = boundedClient.GetType().GetMethods().Single(method => method.Name == "ExecuteAsync")
            .MakeGenericMethod(responseType, variablesType);
        var boundedRunning = (Task)boundedExecute.Invoke(boundedClient, [operation, TestContext.Current.CancellationToken])!;
        await boundedRunning.WaitAsync(TestContext.Current.CancellationToken);
        Assert.Equal(HttpMethod.Post, boundedHandler.Method);

        var batchHandler = new RecordingHandler("[{\"data\":{\"product\":{\"id\":\"p-42\"}}}]");
        using var batchHttpClient = new HttpClient(batchHandler);
        var batchClient = Activator.CreateInstance(assembly.GetType("Salep.Generated.BatchGraphQLClient", throwOnError: true)!, batchHttpClient,
            new Uri("https://example.test/graphql"))!;
        var contractType = assembly.GetType("Salep.Generated.IGraphQLOperation`2", throwOnError: true)!.MakeGenericType(responseType, variablesType);
        var operationList = (System.Collections.IList)Activator.CreateInstance(typeof(List<>).MakeGenericType(contractType))!;
        operationList.Add(operation);
        var batchMethod = batchClient.GetType().GetMethods().Single(method => method.Name == "ExecuteBatchAsync")
            .MakeGenericMethod(responseType, variablesType);
        var batchRunning = (Task)batchMethod.Invoke(batchClient, [operationList, TestContext.Current.CancellationToken])!;
        await batchRunning.WaitAsync(TestContext.Current.CancellationToken);
        Assert.Equal(HttpMethod.Post, batchHandler.Method);
        using var batchRequest = JsonDocument.Parse(batchHandler.RequestBody!);
        Assert.Equal(JsonValueKind.Array, batchRequest.RootElement.ValueKind);
        Assert.Equal("Lookup", batchRequest.RootElement[0].GetProperty("operationName").GetString());

        var incrementalHandler = new RecordingHandler(
            "--graphql\r\nContent-Type: application/json\r\n\r\n{\"data\":{\"product\":{\"id\":\"p-42\"}}}\r\n--graphql--\r\n",
            "multipart/mixed; boundary=\"graphql\"");
        using var incrementalHttpClient = new HttpClient(incrementalHandler);
        var incrementalClient = Activator.CreateInstance(clientType, incrementalHttpClient, new Uri("https://example.test/graphql"))!;
        var incrementalMethod = clientType.GetMethods().Single(method => method.Name == "ExecuteIncrementalAsync")
            .MakeGenericMethod(responseType, variablesType);
        var incremental = incrementalMethod.Invoke(incrementalClient, [operation, TestContext.Current.CancellationToken])!;
        var incrementalResponseType = assembly.GetType("Salep.Generated.GraphQLResponse`1", throwOnError: true)!.MakeGenericType(responseType);
        var firstResultTask = (Task<object>)typeof(CSharpClientRuntimeTests).GetMethod(nameof(ReadFirstAsync), BindingFlags.NonPublic | BindingFlags.Static)!
            .MakeGenericMethod(incrementalResponseType).Invoke(null, [incremental, TestContext.Current.CancellationToken])!;
        var incrementalResult = await firstResultTask.WaitAsync(TestContext.Current.CancellationToken);
        var incrementalData = incrementalResult.GetType().GetProperty("Data")!.GetValue(incrementalResult)!;
        var incrementalProduct = responseType.GetProperty("Product")!.GetValue(incrementalData)!;
        Assert.Equal("p-42", incrementalProduct.GetType().GetProperty("Id")!.GetValue(incrementalProduct));
    }

    private static DocumentNode Parse(string source)
        => global::Salep.GraphQLParser.GraphQLParser.Parse(new SourceText(source.AsMemory()));

    private static async Task<object> ReadFirstAsync<T>(IAsyncEnumerable<T> enumerable, CancellationToken cancellationToken)
    {
        await using var enumerator = enumerable.GetAsyncEnumerator(cancellationToken);
        if (!await enumerator.MoveNextAsync()) throw new InvalidOperationException("The incremental response contained no entries.");
        return enumerator.Current!;
    }

    private sealed class RecordingHandler(string responseJson, string contentType = "application/json") : HttpMessageHandler
    {
        public HttpMethod? Method { get; private set; }
        public string? RequestBody { get; private set; }
        public Uri? RequestUri { get; private set; }

        protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            Method = request.Method;
            RequestUri = request.RequestUri;
            RequestBody = request.Content is null ? null : await request.Content.ReadAsStringAsync(cancellationToken);
            var content = new StringContent(responseJson, Encoding.UTF8);
            content.Headers.ContentType = MediaTypeHeaderValue.Parse(contentType);
            return new HttpResponseMessage(HttpStatusCode.OK) { Content = content };
        }
    }
}
