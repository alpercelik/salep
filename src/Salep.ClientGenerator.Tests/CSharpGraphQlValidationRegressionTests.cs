using System.Text.Json;
using Salep.ClientGenerator.Generation;
using Xunit;

namespace Salep.ClientGenerator.Tests;

public sealed class CSharpGraphQlValidationRegressionTests
{
    [Theory]
    [InlineData("query Read { missing }", "Unknown field")]
    [InlineData("query Read { value }", "Missing required argument")]
    [InlineData("query Read { value(count: 1, other: 2) }", "Unknown argument")]
    [InlineData("query Read { value(count: \"bad\") }", "Invalid value")]
    [InlineData("query Read { value(count: $missing) }", "Undefined variable")]
    [InlineData("query Read($n: String!) { value(count: $n) }", "incompatible type")]
    [InlineData("query Read($n: Int! = \"bad\") { value(count: $n) }", "Invalid value")]
    [InlineData("query Read { value(count: 1) @unknown }", "Unknown directive")]
    [InlineData("query Read { value(count: 1) } fragment Unused on Query { missing }", "Unknown field")]
    public void Invalid_executable_contracts_report_the_operation_source_before_publication(string query, string expected)
    {
        using var fixture = new Fixture();
        File.WriteAllText(fixture.Operation, query);
        var error = Assert.Throws<ScribanConfigurationException>(() => ScribanGenerator.Generate(new(fixture.Config)));
        AssertGraphQlError(error, "graphql/read.graphql(", expected);
        Assert.False(Directory.Exists(fixture.Output));
    }

    [Theory]
    [InlineData("type Query { value: Missing }", "non-output type")]
    [InlineData("type Query { value: String value: Int }", "Duplicate schema member")]
    [InlineData("type Query { value: String } type Query { value: String }", "exactly one definition")]
    public void Invalid_schema_contracts_report_the_schema_source(string schema, string expected)
    {
        using var fixture = new Fixture();
        File.WriteAllText(fixture.Schema, schema);
        File.WriteAllText(fixture.Operation, "query Read { value }");
        var error = Assert.Throws<ScribanConfigurationException>(() => ScribanGenerator.Validate(new(fixture.Config)));
        AssertGraphQlError(error, "schema.graphql(", expected);
        Assert.False(Directory.Exists(fixture.Output));
    }

    [Fact]
    public void Semantic_diagnostics_keep_original_crlf_file_coordinates()
    {
        using var fixture = new Fixture();
        File.WriteAllText(fixture.Operation, "query Read {\r\n  missing\r\n}");
        var error = Assert.Throws<ScribanConfigurationException>(() => ScribanGenerator.Validate(new(fixture.Config)));
        AssertGraphQlError(error, "graphql/read.graphql(2,3)", "Unknown field");
    }

    [Theory]
    [InlineData("type Extra { value: String }\nquery Read { value(count: 1) }", false)]
    [InlineData("type Query { value(count: Int!): String }\nquery Ignored { value(count: 1) }", true)]
    public void Definitions_in_the_wrong_input_kind_are_rejected(string source, bool schema)
    {
        using var fixture = new Fixture();
        File.WriteAllText(schema ? fixture.Schema : fixture.Operation, source);
        var error = Assert.Throws<ScribanConfigurationException>(() => ScribanGenerator.Validate(new(fixture.Config)));
        AssertGraphQlError(error, schema ? "schema.graphql(2,1)" : "graphql/read.graphql(1,1)", schema ? "type-system" : "operations and fragments");
    }

    [Theory]
    [InlineData("query Read { value(count: }")]
    [InlineData("query Read { value(count: ?) }")]
    public void Syntax_and_lexical_errors_have_graphql_guidance_and_source_locations(string source)
    {
        using var fixture = new Fixture();
        File.WriteAllText(fixture.Operation, source);
        var error = Assert.Throws<ScribanConfigurationException>(() => ScribanGenerator.Validate(new(fixture.Config)));
        AssertGraphQlError(error, "graphql/read.graphql(1,", "");
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void Invalid_contracts_preserve_previously_generated_files(bool validateOnly)
    {
        using var fixture = new Fixture();
        ScribanGenerator.Generate(new(fixture.Config));
        var before = Directory.EnumerateFiles(fixture.Output, "*", SearchOption.AllDirectories).ToDictionary(file => file, File.ReadAllBytes);
        File.WriteAllText(fixture.Operation, "query Read { missing }");
        Assert.Throws<ScribanConfigurationException>(() => validateOnly ? ScribanGenerator.Validate(new(fixture.Config)) : ScribanGenerator.Generate(new(fixture.Config)));
        Assert.Equal(before.Keys.Order(StringComparer.Ordinal), Directory.EnumerateFiles(fixture.Output, "*", SearchOption.AllDirectories).Order(StringComparer.Ordinal));
        foreach (var file in before) Assert.Equal(file.Value, File.ReadAllBytes(file.Key));
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void Csharp_keeps_configured_unused_variable_retention_and_filtering(bool omitUnusedVariables)
    {
        using var fixture = new Fixture();
        fixture.WriteConfig(omitUnusedVariables);
        File.WriteAllText(fixture.Operation, "query Read($unused: Int) { value(count: 1) }");
        var generated = ScribanGenerator.Generate(new(fixture.Config));
        var source = string.Join("\n", generated.GeneratedFiles.Where(file => file.EndsWith(".cs", StringComparison.Ordinal)).Select(File.ReadAllText));
        if (omitUnusedVariables) Assert.DoesNotContain("$unused", source);
        else Assert.Contains("$unused", source);
    }

    [Fact]
    public void Invalid_defaults_are_checked_before_inlining()
    {
        using var fixture = new Fixture();
        fixture.WriteConfig(inlineDefaultVariables: true);
        File.WriteAllText(fixture.Operation, "query Read($n: Int! = \"bad\") { value(count: $n) }");
        var error = Assert.Throws<ScribanConfigurationException>(() => ScribanGenerator.Validate(new(fixture.Config)));
        AssertGraphQlError(error, "graphql/read.graphql(", "Invalid value");
    }

    private static void AssertGraphQlError(ScribanConfigurationException error, string location, string expected)
    {
        Assert.Equal("SALEPS1008", error.Diagnostic.Code);
        Assert.Equal("graphql", error.Diagnostic.Property);
        Assert.Contains(location, error.Diagnostic.Message);
        if (expected.Length != 0) Assert.Contains(expected, error.Diagnostic.Message);
        Assert.Contains("GraphQL schema or operation source", error.Diagnostic.Guidance);
        Assert.DoesNotContain("grant external", error.Diagnostic.Guidance);
    }

    private sealed class Fixture : IDisposable
    {
        private string Root { get; } = Path.Combine(Path.GetTempPath(), "salep-csharp-validation-" + Guid.NewGuid().ToString("N"));
        public string Schema => Path.Combine(Root, "schema.graphql");
        public string Operation => Path.Combine(Root, "graphql", "read.graphql");
        public string Config => Path.Combine(Root, "salep.json");
        public string Output => Path.Combine(Root, "Generated");
        public Fixture()
        {
            Directory.CreateDirectory(Path.GetDirectoryName(Operation)!);
            File.WriteAllText(Schema, "type Query { value(count: Int!): String }");
            File.WriteAllText(Operation, "query Read { value(count: 1) }");
            WriteConfig();
        }
        public void WriteConfig(bool omitUnusedVariables = false, bool inlineDefaultVariables = false)
            => File.WriteAllText(Config, JsonSerializer.Serialize(new { version = 1, kind = "client", schema = "schema.graphql", operations = "graphql", @namespace = "Validation.Client", clientName = "GraphQLClient", output = "Generated", unionRepresentation = "dunet", omitUnusedVariables, inlineDefaultVariables }));
        public void Dispose() => Directory.Delete(Root, true);
    }
}
