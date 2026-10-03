using System.Text.Json;
using Salep.ClientGenerator.Generation;
using Xunit;

namespace Salep.ClientGenerator.Tests;

public sealed class GeneratorInputValidationTests
{
    [Theory]
    [InlineData("suites", "[\"samples\"]")]
    [InlineData("suites", "[\"unknown\"]")]
    [InlineData("suites", "[\"operations\",\"operations\"]")]
    [InlineData("suites", "true")]
    [InlineData("rawJsonLiterals", "\"yes\"")]
    [InlineData("useHttpGet", "true")]
    public void Invalid_test_configuration_matches_client_owned_policy(string key, string json)
    {
        using var fixture = new ValidationFixture();
        File.WriteAllText(fixture.SchemaPath, "type Query { value: String }");
        File.WriteAllText(fixture.OperationPath, "query Value { value }");
        var scribanClient = fixture.WriteConfig("scriban.json", "Scriban");
        string Write(string name, string client)
        {
            var config = new Dictionary<string, object?>
            {
                ["version"] = 1, ["kind"] = "tests", ["client"] = client, ["output"] = name,
                [key] = JsonSerializer.Deserialize<JsonElement>(json)
            };
            var path = Path.Combine(fixture.Root, name + ".json");
            File.WriteAllText(path, JsonSerializer.Serialize(config));
            return path;
        }
        var scriban = Write("ScribanTests", scribanClient);
        var error = Assert.Throws<ScribanConfigurationException>(() => ScribanGenerator.Generate(new(scriban, fixture.Root)));
        Assert.Equal(key == "useHttpGet" ? "SALEPS1003" : json == "[\"samples\"]" ? "SALEPS1005" : "SALEPS1001", error.Diagnostic.Code);
        Assert.False(Directory.Exists(Path.Combine(fixture.Root, "ScribanTests")));
    }

    [Theory]
    [InlineData("version", "2")]
    [InlineData("kind", "\"unknown\"")]
    [InlineData("namespace", "\"broken-name\"")]
    [InlineData("clientName", "\"123Client\"")]
    [InlineData("unionRepresentation", "\"unknown\"")]
    [InlineData("scalarPreset", "\"unknown\"")]
    [InlineData("useHttpGet", "\"yes\"")]
    [InlineData("enableBatching", "1")]
    [InlineData("omitUnusedVariables", "null")]
    [InlineData("inlineDefaultVariables", "[]")]
    [InlineData("maxGetUrlLength", "0")]
    [InlineData("maxGetUrlLength", "2147483648")]
    [InlineData("indentSize", "-1")]
    [InlineData("indentSize", "17")]
    [InlineData("unknownOption", "true")]
    [InlineData("scalars", "[]")]
    [InlineData("scalars", "{\"Token\":{\"type\":\"Guid\"}}")]
    public void Invalid_configuration_is_rejected_without_writing_outputs(string key, string json)
    {
        using var fixture = new ValidationFixture();
        File.WriteAllText(fixture.SchemaPath, "type Query { value: String }");
        File.WriteAllText(fixture.OperationPath, "query Value { value }");
        var scribanPath = fixture.WriteConfig("scriban.json", "Scriban");
        foreach (var path in new[] { scribanPath })
        {
            var config = JsonSerializer.Deserialize<Dictionary<string, JsonElement>>(File.ReadAllText(path))!;
            config[key] = JsonSerializer.Deserialize<JsonElement>(json);
            File.WriteAllText(path, JsonSerializer.Serialize(config));
        }
        var scriban = Assert.Throws<ScribanConfigurationException>(() => ScribanGenerator.Generate(new(scribanPath, fixture.Root)));
        Assert.Equal(key == "unknownOption" ? "SALEPS1003" : "SALEPS1001", scriban.Diagnostic.Code);
        Assert.False(Directory.Exists(Path.Combine(fixture.Root, "Scriban")));
    }

    [Theory]
    [InlineData(10_000, true)]
    [InlineData(10_001, false)]
    public void Directive_limit_boundary_is_enforced(int directiveCount, bool accepted)
    {
        using var fixture = new ValidationFixture();
        File.WriteAllText(fixture.SchemaPath, "directive @mark on FIELD type Query { value: String }");
        var directives = string.Join(' ', Enumerable.Repeat("@mark", directiveCount));
        File.WriteAllText(fixture.OperationPath, $"query Boundary {{ value {directives} }}");
        var scribanConfig = fixture.WriteConfig("scriban.json", "Scriban");

        var scriban = Record.Exception(() => ScribanGenerator.Generate(new(scribanConfig, fixture.Root)));

        if (accepted)
        {
            Assert.Null(scriban);
        }
        else
        {
            Assert.IsType<global::Salep.GraphQLParser.GraphQLResourceLimitException>(scriban);
            Assert.False(Directory.Exists(Path.Combine(fixture.Root, "Scriban")));
        }
    }

    [Fact]
    public void Malformed_schema_is_rejected_without_writing_outputs()
    {
        using var fixture = new ValidationFixture();
        File.WriteAllText(fixture.SchemaPath, "type Query {");
        File.WriteAllText(fixture.OperationPath, "query Boundary { value }");
        var scribanConfig = fixture.WriteConfig("scriban.json", "Scriban");

        var scriban = Record.Exception(() => ScribanGenerator.Generate(new(scribanConfig, fixture.Root)));

        Assert.IsType<global::Salep.GraphQLParser.GraphQLSyntaxException>(scriban);
        Assert.False(Directory.Exists(Path.Combine(fixture.Root, "Scriban")));
    }

    [Theory]
    [InlineData("query Boundary { value")]
    [InlineData("{ value }")]
    public void Malformed_or_unnamed_operations_are_rejected_without_writing_outputs(string operationSource)
    {
        using var fixture = new ValidationFixture();
        File.WriteAllText(fixture.SchemaPath, "type Query { value: String }");
        File.WriteAllText(fixture.OperationPath, operationSource);
        var scribanConfig = fixture.WriteConfig("scriban.json", "Scriban");

        var scriban = Record.Exception(() => ScribanGenerator.Generate(new(scribanConfig, fixture.Root)));

        if (operationSource == "{ value }")
            Assert.Equal("SALEPS1005", Assert.IsType<ScribanConfigurationException>(scriban).Diagnostic.Code);
        else
            Assert.IsType<global::Salep.GraphQLParser.GraphQLSyntaxException>(scriban);
        Assert.False(Directory.Exists(Path.Combine(fixture.Root, "Scriban")));
    }

    private sealed class ValidationFixture : IDisposable
    {
        public string Root { get; } = Path.Combine(Path.GetTempPath(), "salep-input-validation-" + Guid.NewGuid().ToString("N"));
        public string SchemaPath => Path.Combine(Root, "schema.graphql");
        public string OperationsDirectory => Path.Combine(Root, "graphql");
        public string OperationPath => Path.Combine(OperationsDirectory, "boundary.graphql");

        public ValidationFixture()
        {
            Directory.CreateDirectory(OperationsDirectory);
        }

        public string WriteConfig(string fileName, string output)
        {
            var configuration = new Dictionary<string, object?>(StringComparer.Ordinal)
            {
                ["version"] = 1,
                ["kind"] = "client",
                ["schema"] = SchemaPath,
                ["operations"] = OperationsDirectory,
                ["namespace"] = "Input.Validation",
                ["output"] = output,
                ["emitAgentInstructions"] = false,
                ["unionRepresentation"] = "native"
            };
            var path = Path.Combine(Root, fileName);
            File.WriteAllText(path, JsonSerializer.Serialize(configuration));
            return path;
        }

        public void Dispose() => Directory.Delete(Root, recursive: true);
    }
}
