using Salep.ClientGenerator.TypeScript.Generation;
using Salep.GraphQLParser;
using Xunit;

namespace Salep.ClientGenerator.Tests;

public sealed class TypeScriptDocumentKindIterationTwoTests
{
    [Theory]
    [InlineData("type Query { title: String }", "query Read { title }\ntype Ignored { invalid: Missing }", "operations.graphql", "operations.graphql(2,1)")]
    [InlineData("type Query { title: String }\nquery Ignored { nonexistent }", "query Read { title }", "operations.graphql", "schema.graphql(2,1)")]
    [InlineData("type Query { title: String }", "const ignored = gql`\ntype Ignored { invalid: Missing }`;\nconst read=gql`query Read { title }`;", "operations.ts", "operations.ts(2,1)")]
    [InlineData("type Query { title: String }", "const ignored = gql`\\n type Ignored { invalid: Missing }`;\nconst view=<div>{gql`query Read { title }`}</div>;", "operations.tsx", "operations.tsx(1,24)")]
    public void Wrong_kind_definitions_are_rejected_at_the_original_source(string schema, string documents, string file, string expected)
    {
        WithInputs(schema, documents, file, config =>
        {
            var error = Assert.Throws<InvalidDataException>(() => TypeScriptGenerator.Generate(new(config)));
            Assert.Contains(expected, error.Message);
            Assert.False(Directory.Exists(Path.Combine(Path.GetDirectoryName(config)!, "Generated")));
        });
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void Parser_field_limits_have_original_operation_locations(bool embedded)
    {
        var operation = "query Read {\n" + string.Concat(Enumerable.Repeat("title\n", 2049)) + "}";
        var source = embedded ? "const read = gql`" + operation + "`;" : operation;
        var file = embedded ? "operations.ts" : "operations.graphql";
        WithInputs("type Query { title: String }", source, file, config =>
        {
            var error = Assert.Throws<InvalidDataException>(() => TypeScriptGenerator.Generate(new(config)));
            Assert.Contains(file + "(2050,1)", error.Message);
            Assert.IsType<GraphQLResourceLimitException>(error.InnerException);
            Assert.False(Directory.Exists(Path.Combine(Path.GetDirectoryName(config)!, "Generated")));
        });
    }

    [Fact]
    public void Parser_schema_limits_are_wrapped_with_schema_source_locations()
    {
        var schema = "type Query { title: " + new string('[', 201) + "String" + new string(']', 201) + " }";
        WithInputs(schema, "query Read { title }", "operations.graphql", config =>
        {
            var error = Assert.Throws<InvalidDataException>(() => TypeScriptGenerator.Generate(new(config)));
            Assert.Contains("schema.graphql(1,", error.Message);
            Assert.IsType<GraphQLResourceLimitException>(error.InnerException);
        });
    }

    [Fact]
    public void Resource_limits_inside_decoded_templates_use_original_offsets()
    {
        const string source = "const read=gql`query Read {\\n title\\n title }`;";
        var extracted = Assert.Single(TypeScriptDocumentExtractor.ExtractWithLocations(source));
        var input = new TypeScriptSourceDocument("operations.tsx", source, extracted.Text, extracted.Offsets);
        var error = Assert.Throws<InvalidDataException>(() => input.Parse(new ParserOptions(maxAllowedFields: 1)));
        Assert.Contains("operations.tsx(1,39)", error.Message);
        Assert.IsType<GraphQLResourceLimitException>(error.InnerException);
    }

    private static void WithInputs(string schema, string documents, string file, Action<string> action)
    {
        var root = Path.Combine(Path.GetTempPath(), "salep-ts-kind-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(root);
        try
        {
            File.WriteAllText(Path.Combine(root, "schema.graphql"), schema);
            File.WriteAllText(Path.Combine(root, file), documents);
            var config = Path.Combine(root, "salep.json");
            File.WriteAllText(config, "{\"version\":1,\"target\":\"typescript\",\"schema\":\"schema.graphql\",\"documents\":\"" + file + "\",\"output\":\"Generated\"}");
            action(config);
        }
        finally { Directory.Delete(root, true); }
    }
}
