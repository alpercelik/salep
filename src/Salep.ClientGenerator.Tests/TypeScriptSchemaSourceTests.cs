using System.Net;
using System.Net.Http;
using System.Text.Json;
using Salep.ClientGenerator.Model;
using Salep.ClientGenerator.TypeScript.Generation;
using Salep.GraphQLParser;
using Xunit;

namespace Salep.ClientGenerator.Tests;

public sealed class TypeScriptSchemaSourceTests
{
    [Fact]
    public void Multiple_local_schema_sources_merge_extensions_and_track_relative_inputs()
    {
        WithProject(root =>
        {
            File.WriteAllText(Path.Combine(root, "base.graphql"), "type Query { title: String }");
            File.WriteAllText(Path.Combine(root, "extension.graphql"), "extend type Query { id: ID! }");
            File.WriteAllText(Path.Combine(root, "operation.graphql"), "query Read { id title }");
            var config = Config(root, """["base.graphql","extension.graphql"]""");
            TypeScriptGenerator.Generate(new(config));
            var source = File.ReadAllText(Path.Combine(root, "Generated", "gql", "graphql.ts"));
            Assert.Contains("\"id\": string", source);
            using var manifest = JsonDocument.Parse(File.ReadAllText(Path.Combine(root, "Generated", ".salep.manifest.json")));
            Assert.True(manifest.RootElement.GetProperty("Inputs").TryGetProperty("../extension.graphql", out _));
            Assert.Contains("extend type Query", File.ReadAllText(Path.Combine(root, "Generated", "schema.graphql")));
        });
    }

    [Fact]
    public void Introspection_json_preserves_schema_roots_descriptions_and_oneof_types()
    {
        WithProject(root =>
        {
            File.WriteAllText(Path.Combine(root, "schema.json"), Introspection);
            File.WriteAllText(Path.Combine(root, "operation.graphql"), "query Read($value: Choice) { echo(value: $value) }");
            TypeScriptGenerator.Generate(new(Config(root, "\"schema.json\"")));
            var source = File.ReadAllText(Path.Combine(root, "Generated", "gql", "graphql.ts"));
            Assert.Contains("Choice docs", source);
            Assert.Contains("\"id\"?: never", source);
            Assert.Contains("\"name\"?: never", source);
            Assert.Contains("query: Root", File.ReadAllText(Path.Combine(root, "Generated", "schema.graphql")));
        });
    }

    [Theory]
    [InlineData("get")]
    [InlineData("introspection")]
    public void Explicit_remote_sources_use_headers_and_manifest_ids_without_credentials(string method)
    {
        WithProject(root =>
        {
            using var client = new HttpClient(new SchemaHandler(request =>
            {
                Assert.Equal(method == "get" ? HttpMethod.Get : HttpMethod.Post, request.Method);
                Assert.Equal("credential-value", Assert.Single(request.Headers.GetValues("X-Schema-Token")));
                if (method == "introspection") Assert.Contains("__schema", request.Content!.ReadAsStringAsync().GetAwaiter().GetResult());
                return new(HttpStatusCode.OK) { Content = new StringContent(method == "get" ? "type Query { title: String }" : Introspection) };
            }));
            File.WriteAllText(Path.Combine(root, "operation.graphql"), method == "get" ? "query Read { title }" : "query Read { echo }");
            var descriptor = JsonSerializer.Serialize(new { url = "https://schema.example.test/api?token=secret-query", method, headers = new Dictionary<string, string> { ["X-Schema-Token"] = "credential-value" } });
            TypeScriptGenerator.Generate(new(Config(root, descriptor), SchemaHttpClient: client));
            var manifest = File.ReadAllText(Path.Combine(root, "Generated", ".salep.manifest.json"));
            Assert.DoesNotContain("secret-query", manifest);
            Assert.DoesNotContain("credential-value", manifest);
            Assert.Contains("schema/0", manifest);
        });
    }

    [Fact]
    public void Remote_http_failure_does_not_publish_output()
    {
        WithProject(root =>
        {
            using var client = new HttpClient(new SchemaHandler(_ => new(HttpStatusCode.Unauthorized)));
            File.WriteAllText(Path.Combine(root, "operation.graphql"), "query Read { title }");
            Assert.Throws<InvalidDataException>(() => TypeScriptGenerator.Generate(new(Config(root, "\"https://schema.example.test/schema\""), SchemaHttpClient: client)));
            Assert.False(Directory.Exists(Path.Combine(root, "Generated")));
        });
    }

    [Theory]
    [InlineData("query Read { echo(value: {}) }", false)]
    [InlineData("query Read { echo(value: {id: null}) }", false)]
    [InlineData("query Read { echo(value: {id: \"1\", name: \"x\"}) }", false)]
    [InlineData("query Read($id: ID) { echo(value: {id: $id}) }", false)]
    [InlineData("query Read($id: ID = \"1\") { echo(value: {id: $id}) }", false)]
    [InlineData("query Read($id: ID!) { echo(value: {id: $id}) }", true)]
    [InlineData("query Read { echo(value: {name: \"x\"}) }", true)]
    public void Oneof_literals_and_member_variables_follow_exactly_one_nonnull_contract(string query, bool valid)
    {
        var schema = GraphQlModelFactory.CreateSchema(Parse("type Query { echo(value: Choice): String } input Choice @oneOf { id: ID name: String }"));
        var document = GraphQlModelFactory.CreateExecutable([Parse(query)]);
        var validator = new GraphQlDocumentValidator(schema, document);
        if (valid) validator.Validate();
        else Assert.Throws<InvalidDataException>(validator.Validate);
    }

    [Theory]
    [InlineData("input Choice @oneOf { id: ID! }")]
    [InlineData("input Choice @oneOf { id: ID = \"1\" }")]
    public void Oneof_schema_fields_cannot_be_required_or_have_defaults(string input)
    {
        var schema = GraphQlModelFactory.CreateSchema(Parse("type Query { echo(value: Choice): String } " + input));
        Assert.Throws<InvalidDataException>(() => new GraphQlDocumentValidator(schema, new([], [])).Validate());
    }

    private sealed class SchemaHandler(Func<HttpRequestMessage, HttpResponseMessage> respond) : HttpMessageHandler
    {
        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken) => Task.FromResult(respond(request));
    }
    private static DocumentNode Parse(string source) => global::Salep.GraphQLParser.GraphQLParser.Parse(new SourceText(source.AsMemory()));
    private static string Config(string root, string schema)
    {
        var file = Path.Combine(root, "salep.json");
        File.WriteAllText(file, "{\"version\":1,\"target\":\"typescript\",\"schema\":" + schema + ",\"documents\":\"operation.graphql\",\"output\":\"Generated\"}");
        return file;
    }
    private static void WithProject(Action<string> test)
    {
        var root = Path.Combine(Path.GetTempPath(), "salep-schema-sources-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(root);
        try { test(root); } finally { Directory.Delete(root, true); }
    }
    private const string Introspection = """
        {"data":{"__schema":{"queryType":{"name":"Root"},"mutationType":null,"subscriptionType":null,"types":[
          {"kind":"OBJECT","name":"Root","interfaces":[],"fields":[{"name":"echo","args":[{"name":"value","type":{"kind":"INPUT_OBJECT","name":"Choice"},"defaultValue":null}],"type":{"kind":"SCALAR","name":"String"}}]},
          {"kind":"INPUT_OBJECT","name":"Choice","description":"Choice docs","isOneOf":true,"inputFields":[{"name":"id","type":{"kind":"SCALAR","name":"ID"},"defaultValue":null},{"name":"name","type":{"kind":"SCALAR","name":"String"},"defaultValue":null}]}
        ],"directives":[]}}}
        """;
}
