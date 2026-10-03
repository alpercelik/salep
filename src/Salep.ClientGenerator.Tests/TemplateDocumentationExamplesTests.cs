using System.Net;
using System.Runtime.Loader;
using System.Text.Json;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Salep.ClientGenerator.Generation;
using Xunit;

namespace Salep.ClientGenerator.Tests;

public sealed class TemplateDocumentationExamplesTests
{
    [Fact]
    public async Task Documented_overrides_compile_and_execute_with_profiles_defaults_and_raw_or_escaped_queries()
    {
        var repository = new DirectoryInfo(AppContext.BaseDirectory);
        while (repository is not null && !File.Exists(Path.Combine(repository.FullName, "Directory.Build.props"))) repository = repository.Parent;
        Assert.NotNull(repository);
        var original = Path.Combine(repository.FullName, "docs", "client-generator", "examples", "template-customization");
        var root = Path.Combine(Path.GetTempPath(), "salep-doc-templates-" + Guid.NewGuid().ToString("N"));
        try
        {
            foreach (var file in Directory.EnumerateFiles(original, "*", SearchOption.AllDirectories))
            {
                var destination = Path.Combine(root, Path.GetRelativePath(original, file));
                Directory.CreateDirectory(Path.GetDirectoryName(destination)!);
                File.Copy(file, destination);
            }
            string? rawQuery = null;
            foreach (var example in new[] { ("salep.json", "Generated", "Example.Api"), ("salep.wrapper.json", "WrappedGenerated", "Example.Wrapped"), ("salep.escaped-query.json", "EscapedGenerated", "Example.Escaped") })
            {
                ScribanGenerator.Generate(new(example.Item1, root));
                var sources = Directory.GetFiles(Path.Combine(root, example.Item2), "*.cs").Select(File.ReadAllText).ToArray();
                var compilation = CSharpCompilation.Create("TemplateDocs" + Guid.NewGuid().ToString("N"),
                    sources.Select(source => CSharpSyntaxTree.ParseText(source, cancellationToken: TestContext.Current.CancellationToken)),
                    ((string)AppContext.GetData("TRUSTED_PLATFORM_ASSEMBLIES")!).Split(Path.PathSeparator).Select(path => MetadataReference.CreateFromFile(path)),
                    new CSharpCompilationOptions(OutputKind.DynamicallyLinkedLibrary, nullableContextOptions: NullableContextOptions.Enable));
                using var stream = new MemoryStream();
                var result = compilation.Emit(stream, cancellationToken: TestContext.Current.CancellationToken);
                Assert.True(result.Success, string.Join(Environment.NewLine, result.Diagnostics.Where(item => item.Severity == DiagnosticSeverity.Error)));
                var context = new AssemblyLoadContext("TemplateDocs", isCollectible: true);
                try
                {
                    var assembly = context.LoadFromStream(new MemoryStream(stream.ToArray()));
                    var operationType = assembly.GetType(example.Item3 + ".CurrentUserOperation", throwOnError: true)!;
                    var operation = Activator.CreateInstance(operationType, new object?[] { null })!;
                    var query = (string)operationType.GetProperty("Query")!.GetValue(operation)!;
                    rawQuery ??= query;
                    Assert.Equal(rawQuery, query);
                    using var handler = new RecordingHandler();
                    using var httpClient = new HttpClient(handler);
                    var clientType = assembly.GetType(example.Item3 + ".ExampleClient", throwOnError: true)!;
                    var client = Activator.CreateInstance(clientType, httpClient, new Uri("https://example.test/graphql"))!;
                    var execute = clientType.GetMethods().Single(method => method.Name == "ExecuteAsync").MakeGenericMethod(
                        assembly.GetType(example.Item3 + ".CurrentUserResponse", throwOnError: true)!, assembly.GetType(example.Item3 + ".CurrentUserVariables", throwOnError: true)!);
                    var running = (Task)execute.Invoke(client, [operation, TestContext.Current.CancellationToken])!;
                    await running.WaitAsync(TestContext.Current.CancellationToken);
                    Assert.Equal(query, handler.Query);
                    Assert.Equal(example.Item1 == "salep.json" ? HttpMethod.Get : HttpMethod.Post, handler.Method);
                    if (example.Item1 != "salep.escaped-query.json")
                    {
                        Assert.Equal("my-app", httpClient.DefaultRequestHeaders.GetValues("X-Application").Single());
                        Assert.Equal("CurrentUser", operationType.GetProperty("DocumentName")!.GetValue(operation));
                        var user = assembly.GetType(example.Item3 + ".User", throwOnError: true)!;
                        Assert.DoesNotContain("GraphQlTypeName", JsonSerializer.Serialize(Activator.CreateInstance(user), user), StringComparison.Ordinal);
                    }
                    if (example.Item1 == "salep.json") Assert.Equal("ExampleClient", clientType.GetProperty("ConsumerName")!.GetValue(client));
                    if (example.Item1 == "salep.wrapper.json")
                    {
                        Assert.Equal("shared-profile", clientType.GetProperty("ConsumerName")!.GetValue(client));
                        Assert.Contains(sources, source => source.StartsWith("// Consumer-owned header", StringComparison.Ordinal) && source.Contains("// Consumer response-reader wrapper", StringComparison.Ordinal));
                    }
                }
                finally { context.Unload(); }
            }
            ScribanGenerator.Generate(new("salep.tests.json", root));
            var metadata = File.ReadAllText(Path.Combine(root, "GeneratedTests", "OperationsMetadataTests.cs"));
            Assert.Contains("// Metadata regression for CurrentUser", metadata, StringComparison.Ordinal);
            foreach (var file in Directory.GetFiles(Path.Combine(root, "GeneratedTests"), "*.cs"))
                Assert.DoesNotContain(CSharpSyntaxTree.ParseText(File.ReadAllText(file), cancellationToken: TestContext.Current.CancellationToken).GetDiagnostics(TestContext.Current.CancellationToken), diagnostic => diagnostic.Severity == DiagnosticSeverity.Error);
        }
        finally { if (Directory.Exists(root)) Directory.Delete(root, true); }
    }

    private sealed class RecordingHandler : HttpMessageHandler
    {
        public string? Query { get; private set; }
        public HttpMethod? Method { get; private set; }
        protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            Method = request.Method;
            if (request.Method == HttpMethod.Get)
                Query = WebUtility.UrlDecode(request.RequestUri!.Query.TrimStart('?').Split('&').Single(item => item.StartsWith("query=", StringComparison.Ordinal))["query=".Length..]);
            else
            {
                using var body = JsonDocument.Parse(await request.Content!.ReadAsStringAsync(cancellationToken));
                Query = body.RootElement.GetProperty("query").GetString();
            }
            return new(HttpStatusCode.OK) { Content = new StringContent("{\"data\":{\"user\":{\"id\":\"1\",\"name\":\"Ada\"}}}") };
        }
    }
}
