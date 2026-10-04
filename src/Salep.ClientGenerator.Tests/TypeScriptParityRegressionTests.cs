using Salep.ClientGenerator.Model;
using Salep.ClientGenerator.TypeScript.Generation;
using Salep.ClientGenerator.TypeScript.Targets;
using Salep.ClientGenerator.TypeScript.Templates;
using Salep.GraphQLParser;
using Xunit;

namespace Salep.ClientGenerator.Tests;

public sealed class TypeScriptParityRegressionTests
{
    [Theory]
    [InlineData("query Check { item { id } }", "required argument")]
    [InlineData("query Check { item(id: $id) { id } }", "Undefined variable")]
    [InlineData("query Check { item(id: \"1\") }", "requires a selection")]
    [InlineData("query Check { text { id } }", "cannot have a selection")]
    [InlineData("query Check { item(id: \"1\") { value: id value: name } }", "Conflicting fields")]
    [InlineData("query Check($id: Int!) { item(id: $id) { id } }", "incompatible type")]
    [InlineData("query Check { item(id: null) { id } }", "Null")]
    [InlineData("query Check { item(id: \"1\", unknown: 1) { id } }", "Unknown argument")]
    [InlineData("query Check { item(id: \"1\", id: \"2\") { id } }", "Duplicate argument")]
    [InlineData("query Check($id: ID, $id: ID) { item(id: $id) { id } }", "Duplicate variable")]
    [InlineData("query Check($unused: ID) { text }", "Unused variable")]
    [InlineData("query Check { text @include(if: 1) }", "Invalid value")]
    [InlineData("query Check { text @include }", "required argument")]
    [InlineData("query Check { text @include(if: true) @include(if: false) }", "not repeatable")]
    [InlineData("query Check @include(if: true) { text }", "invalid at QUERY")]
    [InlineData("query Check { text @missing }", "Unknown directive")]
    [InlineData("query Check($id: Item) { text }", "non-input type")]
    [InlineData("query Check($id: ID! = null) { item(id: $id) { id } }", "Null")]
    [InlineData("query Check { item(id: \"1\") { ... on Query { text } } }", "cannot apply")]
    [InlineData("query Check { text } fragment Bad on String { x }", "composite type")]
    [InlineData("query Check { item(id: \"1\") { id } item(id: \"2\") { id } }", "Conflicting fields")]
    [InlineData("query Check { options(value: {}) }", "required input field")]
    [InlineData("query Check { options(value: {tag: OTHER}) }", "Invalid value")]
    [InlineData("query Check { options(value: {tag: NOVEL, extra: 1}) }", "Unknown input field")]
    [InlineData("query Check { options(value: {tag: NOVEL, tag: NOVEL}) }", "Duplicate input")]
    [InlineData("query Check { item(id: \"1\") { ...Loop } } fragment Loop on Item { child { ...Loop } }", "cycle")]
    public void Invalid_operations_fail_before_touching_owned_output(string query, string message)
    {
        WithProject((root, config) =>
        {
            TypeScriptGenerator.Generate(new(config));
            var output = Path.Combine(root, "Generated", "gql", "graphql.ts");
            var original = File.ReadAllText(output);
            var manifest = File.ReadAllText(Path.Combine(root, "Generated", ".salep.manifest.json"));
            File.WriteAllText(Path.Combine(root, "operation.graphql"), query);
            var error = Assert.Throws<InvalidDataException>(() => TypeScriptGenerator.Generate(new(config)));
            Assert.Contains(message, error.Message, StringComparison.OrdinalIgnoreCase);
            Assert.Equal(original, File.ReadAllText(output));
            Assert.Equal(manifest, File.ReadAllText(Path.Combine(root, "Generated", ".salep.manifest.json")));
        });
    }

    [Theory]
    [InlineData("type Query { value: Missing }", "non-output")]
    [InlineData("type Query { value: String value: String }", "Duplicate")]
    [InlineData("type Query { value: String } extend type Missing { id: ID }", "one definition")]
    [InlineData("type Query { value: String } union Broken = String", "object members")]
    [InlineData("type Query { value: String } input Loop { child: Loop! }", "cycle")]
    [InlineData("interface Node { id: ID! } type Query implements Node { id: String }", "interface field")]
    public void Invalid_schemas_fail_before_publication(string schema, string message)
    {
        WithProject((root, config) =>
        {
            File.WriteAllText(Path.Combine(root, "schema.graphql"), schema);
            var error = Assert.Throws<InvalidDataException>(() => TypeScriptGenerator.Generate(new(config)));
            Assert.Contains(message, error.Message, StringComparison.OrdinalIgnoreCase);
            Assert.False(Directory.Exists(Path.Combine(root, "Generated")));
        });
    }

    [Theory]
    [InlineData("query Check($id: ID = \"1\") { item(id: $id) { id } }")]
    [InlineData("query Check($id: ID!) { item(id: $id) { id } }")]
    [InlineData("query Check { options(value: {tag: NOVEL}) }")]
    [InlineData("query Check { text } fragment Unused on Item { id }")]
    public void Valid_defaults_variables_literals_and_unused_fragment_libraries_are_accepted(string query)
    {
        WithProject((root, config) =>
        {
            File.WriteAllText(Path.Combine(root, "operation.graphql"), query);
            Assert.Equal(7, TypeScriptGenerator.Generate(new(config)).Count);
        });
    }

    [Fact]
    public void Conditional_duplicate_parents_keep_children_optional()
    {
        var source = Render("type Query { item: Item! } type Item { id: ID! name: String! }",
            "query Check($show: Boolean!) { item { id } item @include(if: $show) { name } }");
        Assert.Contains("\"id\": string", source);
        Assert.Contains("\"name\"?: string", source);
        Assert.DoesNotContain("\"item\"?:", source);
    }

    [Theory]
    [InlineData("@include(if: true)", true, false)]
    [InlineData("@skip(if: false)", true, false)]
    [InlineData("@include(if: false)", false, false)]
    [InlineData("@skip(if: true)", false, false)]
    [InlineData("@include(if: true) @skip(if: true)", false, false)]
    [InlineData("@include(if: $show)", true, true)]
    public void Literal_conditions_have_exact_presence(string directive, bool present, bool optional)
    {
        var source = Render("type Query { item: Item! } type Item { id: ID! name: String! }",
            "query Check($show: Boolean!) { item { id name " + directive + " } }").Split("export const", StringSplitOptions.None)[0];
        Assert.Equal(present, source.Contains("\"name\"", StringComparison.Ordinal));
        if (present) Assert.Contains(optional ? "\"name\"?: string" : "\"name\": string", source);
    }

    [Theory]
    [InlineData("type Query { hit: Hit! } union Hit = Book extend union Hit = Writer type Book { id: ID! } type Writer { id: ID! }", "query Check { hit { ...Summary } } fragment Summary on Hit { __typename ... on Book { id } ... on Writer { id } }", "Writer")]
    [InlineData("type Query { hit: Entry! } interface Entry { id: ID! } type Book { id: ID! } extend type Book implements Entry", "query Check { hit { ...Summary } } fragment Summary on Entry { id }", "id")]
    public void Standalone_fragments_resolve_schema_extensions(string schema, string query, string expected)
    {
        var model = Schema(schema);
        var document = Executable(model, query);
        new GraphQlDocumentValidator(model, document).Validate();
        var source = new TypeScriptSourceRenderer(model, document, new(), new(), false).Fragments();
        Assert.Contains(expected, source);
    }

    [Fact]
    public void Excluded_spreads_still_keep_ast_fragment_dependencies()
    {
        var schema = Schema("type Query { item: Item } type Item { id: ID }");
        var document = Executable(schema, "fragment Parent on Item { ...Child @skip(if: true) } fragment Child on Item { id }");
        var source = new TypeScriptSourceRenderer(schema, document, new(), new()).Fragments();
        var ast = source.Split("export const ParentFragmentDocument = ", StringSplitOptions.None)[1].Split(" as unknown", StringSplitOptions.None)[0];
        using var json = System.Text.Json.JsonDocument.Parse(ast);
        Assert.Equal(2, json.RootElement.GetProperty("definitions").GetArrayLength());
    }

    [Fact]
    public void Symbol_allocation_avoids_schema_operation_fragment_and_prefix_collisions()
    {
        var schema = Schema("enum ReadonlyArray { VALUE } enum Record { VALUE } input CheckResult { id: ID } input GqlCheckResult { id: ID } type Query { text(options: CheckResult): String } ");
        var document = Executable(schema, "query Check($options: CheckResult) { text(options: $options) } fragment Check on Query { text }");
        var target = new TypeScriptCodeGenerationTarget();
        target.PrepareNames(schema, document);
        Assert.NotEqual("ReadonlyArray", target.TypeName("ReadonlyArray"));
        Assert.NotEqual("Record", target.TypeName("Record"));
        Assert.NotEqual("CheckResult", target.TypeName("CheckResult"));
        Assert.NotEqual(target.TypeName("CheckResult"), target.TypeName("GqlCheckResult"));
        var source = new TypeScriptSourceRenderer(schema, document, target, new()).Operations();
        Assert.Contains(target.TypeName("CheckResult"), source);
    }

    private static void WithProject(Action<string, string> test)
    {
        var root = Path.Combine(Path.GetTempPath(), "salep-ts-parity-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(root);
        try
        {
            File.WriteAllText(Path.Combine(root, "schema.graphql"), "type Query { item(id: ID!): Item! text: String! count: Int options(value: Options!): String } type Item { id: ID! name: String! child: Item } enum Tag { NOVEL HISTORY } input Options { tag: Tag! }");
            File.WriteAllText(Path.Combine(root, "operation.graphql"), "query Check { text }");
            var config = Path.Combine(root, "salep.json");
            File.WriteAllText(config, """{"version":1,"target":"typescript","schema":"schema.graphql","documents":"operation.graphql","output":"Generated"}""");
            test(root, config);
        }
        finally { Directory.Delete(root, true); }
    }
    private static GraphQlSchemaModel Schema(string source) => GraphQlModelFactory.CreateSchema(global::Salep.GraphQLParser.GraphQLParser.Parse(new SourceText(source.AsMemory())));
    private static GraphQlExecutableDocument Executable(GraphQlSchemaModel schema, string source) => GraphQlModelFactory.CreateExecutable(schema, [global::Salep.GraphQLParser.GraphQLParser.Parse(new SourceText(source.AsMemory()))]);
    private static string Render(string schemaSource, string query)
    {
        var schema = Schema(schemaSource);
        var document = Executable(schema, query);
        return new TypeScriptSourceRenderer(schema, document, new(), new(), false).Operations();
    }
}
