using Salep.ClientGenerator.Config;
using Salep.ClientGenerator.Diagnostics;
using Salep.ClientGenerator.Model;
using Salep.GraphQLParser;
using Xunit;

namespace Salep.ClientGenerator.Tests;

public sealed class DiagnosticsReporterTests
{
    [Fact]
    public void UnusedScalarsAndDirectiveDefinitionsDoNotWarn()
    {
        var warnings = Collect("""
            scalar BigInt
            scalar Json
            directive @metadata(value: Json) repeatable on OBJECT | FIELD_DEFINITION
            type Query @metadata(value: {}) { ok: Boolean! }
            """);

        Assert.Empty(warnings);
    }

    [Theory]
    [InlineData("type Query { value: Custom }")]
    [InlineData("type Query { value: [[Custom!]!]! }")]
    [InlineData("type Query { ok(value: [[Custom!]!]!): Boolean }")]
    [InlineData("input Filter { values: [[Custom!]!]! } type Query { ok(filter: Filter): Boolean }")]
    [InlineData("interface Node { value: Custom } type Query { node: Node }")]
    [InlineData("interface Node { ok(value: Custom): Boolean } type Query { node: Node }")]
    public void UnmappedScalarInGeneratedSchemaContractsStillWarns(string schema)
    {
        var warnings = Collect("scalar Custom " + schema);

        Assert.Equal(["Scalar 'Custom' has no mapping. It will be generated as string."], warnings);
    }

    [Fact]
    public void ScalarUsedOnlyByAnOperationVariableStillWarns()
    {
        var warnings = Collect("""
            scalar Custom
            directive @tag(value: [[Custom!]!]!) on QUERY
            type Query { ok: Boolean }
            """, "query Read($value: [[Custom!]!]!) @tag(value: $value) { ok }");

        Assert.Equal(["Scalar 'Custom' has no mapping. It will be generated as string."], warnings);
    }

    [Fact]
    public void ExplicitMappingsSuppressUsedScalarWarnings()
    {
        var warnings = Collect("scalar Json type Query { value: Json }",
            config: """{"scalarMappings":{"Json":"System.Text.Json.JsonElement"}}""");

        Assert.Empty(warnings);
    }

    [Fact]
    public void WarningsAreDistinctSortedAndScalarNamesAreCaseSensitive()
    {
        var warnings = Collect("""
            scalar Zed
            scalar Alpha
            scalar alpha
            type Query { first: Zed second: [Zed!] third: Alpha fourth: alpha }
            """, config: """{"scalarMappings":{"Alpha":"string"}}""");

        Assert.Equal([
            "Scalar 'Zed' has no mapping. It will be generated as string.",
            "Scalar 'alpha' has no mapping. It will be generated as string."
        ], warnings);
    }

    private static IReadOnlyList<string> Collect(
        string schema,
        string operation = "query Read { __typename }",
        string config = "{}")
    {
        var directory = Path.Combine(Path.GetTempPath(), "salep-diagnostics-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(directory);
        try
        {
            var configPath = Path.Combine(directory, "salep.json");
            FixtureConfiguration.Write(configPath, config);
            var model = SchemaModel.FromDocument(Utf8GraphQLParser.Parse(schema), GeneratorConfig.Load(configPath));
            var operations = Utf8GraphQLParser.Parse(operation).Definitions.OfType<OperationDefinitionNode>().ToArray();
            return DiagnosticsReporter.CollectWarnings(model, operations);
        }
        finally
        {
            Directory.Delete(directory, recursive: true);
        }
    }
}
