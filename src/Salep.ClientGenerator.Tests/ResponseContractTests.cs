using System.Reflection;
using System.Text.Json;
using System.Text.Json.Serialization;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Salep.ClientGenerator.Generation;
using Xunit;

namespace Salep.ClientGenerator.Tests;

public sealed class ResponseContractTests
{
    [Fact]
    public void WireNamesSurviveVariableInputObjectAndEnumRoundTrips()
    {
        using var fixture = new Fixture("""
            enum State { IN_PROGRESS DONE }
            input UserInput { display_name: String, state: State }
            type User { display_name: String, state: State }
            type Query { user(user_id: ID, input: UserInput): User }
            """, "query Names($user_id: ID, $input: UserInput) { user(user_id: $user_id, input: $input) { display_name state } }");
        var assembly = fixture.Compile();
        var variablesType = assembly.GetType("Review.Generated.NamesVariables", true)!;
        const string variablesJson = """{"user_id":"1","input":{"display_name":"Ada","state":"IN_PROGRESS"}}""";
        var variables = JsonSerializer.Deserialize(variablesJson, variablesType, Options)!;
        var serialized = JsonSerializer.Serialize(variables, variablesType, Options);
        using var json = JsonDocument.Parse(serialized);
        Assert.Equal("1", json.RootElement.GetProperty("user_id").GetString());
        Assert.Equal("Ada", json.RootElement.GetProperty("input").GetProperty("display_name").GetString());
        Assert.Equal("IN_PROGRESS", json.RootElement.GetProperty("input").GetProperty("state").GetString());
        var response = Deserialize(assembly, "NamesResponse", """{"user":{"display_name":"Ada","state":"IN_PROGRESS"}}""");
        Assert.Equal("Ada", Property(Property(response, "User"), "DisplayName"));
        Assert.Throws<JsonException>(() => Deserialize(assembly, "NamesResponse", """{"user":{"state":"UNKNOWN"}}"""));
    }

    [Fact]
    public void RootFragmentsAndRepeatedFieldsProduceOneMergedResponseProperty()
    {
        using var fixture = new Fixture("type User { id: ID!, name: String } type Query { user: User }", """
            query Root { ...RootFields ... on Query { user { id } } }
            fragment RootFields on Query { ...MoreRoot }
            fragment MoreRoot on Query { user { label: name } }
            """);
        var assembly = fixture.Compile();
        var response = Deserialize(assembly, "RootResponse", """{"user":{"id":"1","label":"Ada"}}""");
        Assert.Single(response.GetType().GetProperties());
        Assert.Equal("1", Property(Property(response, "User"), "Id"));
        Assert.Equal("Ada", Property(Property(response, "User"), "Label"));
    }

    [Fact]
    public void NestedAliasesPreserveListsNullsAndDistinctSelections()
    {
        using var fixture = new Fixture("type User { id: ID!, name: String } type Query { users: [User] }", """
            query First { users { ...UserFields } }
            fragment UserFields on User { first_name: name }
            query Second { users { secondName: name } }
            query Plain { users { name } }
            """);
        var assembly = fixture.Compile();
        var first = Deserialize(assembly, "FirstResponse", """{"users":[null,{"first_name":"Ada"}]}""");
        var list = (System.Collections.IList)Property(first, "Users");
        Assert.Null(list[0]);
        Assert.Equal("Ada", Property(list[1]!, "FirstName"));
        var second = Deserialize(assembly, "SecondResponse", """{"users":[{"secondName":"Grace"}]}""");
        Assert.Equal("Grace", Property(((System.Collections.IList)Property(second, "Users"))[0]!, "SecondName"));
        var empty = Deserialize(assembly, "FirstResponse", """{"users":null}""");
        Assert.Null(empty.GetType().GetProperty("Users")!.GetValue(empty));
        Assert.Contains("List<User?>? Users", fixture.Read("Operations.cs"));
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void AbstractSelectionsWithAliasesDeserializeConcreteShapes(bool useInterface)
    {
        var abstractSchema = useInterface
            ? "interface Node { id: ID! } type User implements Node { id: ID!, name: String } type Post implements Node { id: ID!, title: String }"
            : "union Node = User | Post type User { id: ID!, name: String } type Post { id: ID!, title: String }";
        using var fixture = new Fixture(abstractSchema + " type Query { nodes: [Node] }", """
            query Abstract { nodes { kind: __typename ... on User { label: name } ... on Post { heading: title } } }
            """, includeSchemaInCompilation: false);
        var assembly = fixture.Compile();
        var response = Deserialize(assembly, "AbstractResponse", """{"nodes":[{"__typename":"User","kind":"User","label":"Ada"},{"__typename":"Post","kind":"Post","heading":"Hello"},null]}""");
        var nodes = (System.Collections.IList)Property(response, "Nodes");
        Assert.Equal("Ada", Property(nodes[0]!, "Label"));
        Assert.Equal("User", Property(nodes[0]!, "Kind"));
        Assert.Equal("Hello", Property(nodes[1]!, "Heading"));
        Assert.Null(nodes[2]);
        Assert.Throws<JsonException>(() => Deserialize(assembly, "AbstractResponse", """{"nodes":[{"__typename":"Unknown"}]}"""));
        Assert.Throws<JsonException>(() => Deserialize(assembly, "AbstractResponse", """{"nodes":[{"label":"Ada"}]}"""));
    }

    [Fact]
    public void TypenameIsInsertedInsideNamedFragmentsAndAlongsideAliasedOrConditionalFields()
    {
        using var fixture = new Fixture("type User { id: ID! } union Result = User type Container { result: Result } type Query { container: Container }", """
            query Fragment { container { ...Outer } }
            fragment Outer on Container { ...Inner }
            fragment Inner on Container { result { kind: __typename ... on User { id } } }
            query Conditional { container { result { __typename @skip(if: true) ... on User { id } } } }
            """);
        var generated = fixture.Read("Operations.cs");
        // Parse the emitted operation strings as GraphQL, rather than checking formatting.
        var syntax = CSharpSyntaxTree.ParseText(generated, cancellationToken: TestContext.Current.CancellationToken).GetRoot(TestContext.Current.CancellationToken);
        var queries = syntax.DescendantNodes().OfType<Microsoft.CodeAnalysis.CSharp.Syntax.PropertyDeclarationSyntax>()
            .Where(property => property.Identifier.ValueText == "Query")
            .Select(property => ((Microsoft.CodeAnalysis.CSharp.Syntax.LiteralExpressionSyntax)property.ExpressionBody!.Expression).Token.ValueText);
        foreach (var query in queries)
        {
            var document = Salep.GraphQLParser.Utf8GraphQLParser.Parse(query);
            var checkedSelections = 0;
            Salep.GraphQLParser.GraphQLAstVisitor.Visit(document, node =>
            {
                if (node is Salep.GraphQLParser.FieldNode { Name.Value: "result", SelectionSet: not null } field)
                {
                    Assert.Contains(field.SelectionSet.Selections.OfType<Salep.GraphQLParser.FieldNode>(),
                        selected => selected.Name.Value == "__typename" && selected.Alias is null && selected.Directives.Count == 0);
                    checkedSelections++;
                }
                return Salep.GraphQLParser.GraphQLVisitControl.Continue;
            });
            Assert.True(checkedSelections > 0);
        }
    }

    [Fact]
    public void SingleFileAndDirectoryOperationInputsGenerateTheSameOperations()
    {
        using var fixture = new Fixture("type Query { name: String }", "query Single { name }");
        var expected = fixture.Read("Operations.cs");
        SetOperations(fixture.ConfigPath, Path.Combine(fixture.Root, "ops", "query.graphql"));
        FixtureConfiguration.Generate(new(fixture.ConfigPath, WorkingDirectory: fixture.Root));
        Assert.Equal(expected, fixture.Read("Operations.cs"));
        SetOperations(fixture.ConfigPath, Path.Combine(fixture.Root, "ops", "*.graphql"));
        FixtureConfiguration.Generate(new(fixture.ConfigPath, WorkingDirectory: fixture.Root));
        Assert.Equal(expected, fixture.Read("Operations.cs"));
    }

    private static readonly JsonSerializerOptions Options = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        PropertyNameCaseInsensitive = true,
        Converters = { new JsonStringEnumConverter() }
    };

    private static object Property(object value, string name) => value.GetType().GetProperty(name)!.GetValue(value)!;
    private static object Deserialize(Assembly assembly, string name, string json) => JsonSerializer.Deserialize(json, assembly.GetType("Review.Generated." + name, true)!, Options)!;

    private static void SetOperations(string path, string operations)
    {
        var node = System.Text.Json.Nodes.JsonNode.Parse(File.ReadAllText(path))!;
        node["operations"] = operations;
        File.WriteAllText(path, node.ToJsonString());
    }

    private sealed class Fixture : IDisposable
    {
        public string Root { get; } = Path.Combine(Path.GetTempPath(), "salep_response_test_" + Guid.NewGuid().ToString("N"));
        public string ConfigPath => Path.Combine(Root, "salep.json");
        private readonly bool includeSchemaInCompilation;
        public Fixture(string schema, string operations, bool includeSchemaInCompilation = true)
        {
            this.includeSchemaInCompilation = includeSchemaInCompilation;
            Directory.CreateDirectory(Path.Combine(Root, "ops"));
            File.WriteAllText(Path.Combine(Root, "schema.graphql"), schema);
            File.WriteAllText(Path.Combine(Root, "ops", "query.graphql"), operations);
            FixtureConfiguration.Write(ConfigPath, JsonSerializer.Serialize(new
            {
                schemaPath = "schema.graphql", operationsPath = "ops", outputDirectory = "Generated",
                generatedNamespace = "Review.Generated", useNativeUnions = true,
                generateTests = false, generateSample = false, emitAgentInstructions = false
            }));
            FixtureConfiguration.Generate(new(ConfigPath, WorkingDirectory: Root));
        }
        public string Read(string name) => File.ReadAllText(Path.Combine(Root, "Generated", name));
        public Assembly Compile()
        {
            var sources = new List<string> { Read("Operations.cs"), Read("GraphQLSharedTypes.cs") };
            if (includeSchemaInCompilation && File.Exists(Path.Combine(Root, "Generated", "SchemaTypes.cs"))) sources.Add(Read("SchemaTypes.cs"));
            var references = ((string)AppContext.GetData("TRUSTED_PLATFORM_ASSEMBLIES")!).Split(Path.PathSeparator)
                .Select(path => MetadataReference.CreateFromFile(path));
            var compilation = CSharpCompilation.Create("ResponseFixture" + Guid.NewGuid().ToString("N"),
                sources.Select(source => CSharpSyntaxTree.ParseText(source, new CSharpParseOptions(LanguageVersion.Preview))),
                references, new CSharpCompilationOptions(OutputKind.DynamicallyLinkedLibrary, nullableContextOptions: NullableContextOptions.Enable));
            using var stream = new MemoryStream();
            var result = compilation.Emit(stream);
            Assert.True(result.Success, string.Join(Environment.NewLine, result.Diagnostics));
            return Assembly.Load(stream.ToArray());
        }
        public void Dispose() => Directory.Delete(Root, true);
    }
}
