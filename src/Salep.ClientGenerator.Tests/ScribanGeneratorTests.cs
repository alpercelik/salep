using Salep.ClientGenerator.Generation;
using Xunit;

namespace Salep.ClientGenerator.Tests;

public sealed class ScribanGeneratorTests
{
    [Fact]
    public async Task Concurrent_manifest_reads_and_generation_observe_complete_owned_outputs()
    {
        using var fixture = new GeneratorFixture();
        fixture.WriteInputs();
        fixture.WriteConfig("{\"version\":1,\"kind\":\"client\",\"schema\":\"schema.graphql\",\"operations\":\"graphql\",\"namespace\":\"Example.Client\",\"output\":\"Generated\"}");
        ScribanGenerator.Generate(new("scriban.json", fixture.Root));
        var schema = Path.Combine(fixture.Root, "schema.graphql");
        File.AppendAllText(schema, "\n" + string.Join('\n', Enumerable.Range(0, 200).Select(index => $"type Extra{index} {{ name: String }}")));
        var tasks = Enumerable.Range(0, 12).Select(worker => Task.Run(() =>
        {
            for (var iteration = 0; iteration < 4; iteration++)
            {
                if (worker % 3 == 0) ScribanGenerator.Generate(new("scriban.json", fixture.Root));
                else if (worker % 3 == 1) ScribanGenerator.Validate(new("scriban.json", fixture.Root));
                else Assert.NotEmpty(ScribanGenerator.GetInputFiles(new("scriban.json", fixture.Root)));
            }
        }, TestContext.Current.CancellationToken));
        await Task.WhenAll(tasks);
        ScribanGenerator.Validate(new("scriban.json", fixture.Root));
        Assert.Contains("Extra199", File.ReadAllText(Path.Combine(fixture.Root, "Generated/SchemaTypes.cs")), StringComparison.Ordinal);
    }

    [Fact]
    public void Validate_and_generate_publish_owned_sources_and_manifest()
    {
        using var fixture = new GeneratorFixture();
        fixture.WriteInputs();
        fixture.WriteConfig("{\"version\":1,\"kind\":\"client\",\"schema\":\"schema.graphql\",\"operations\":\"graphql\",\"namespace\":\"Example.Client\",\"output\":\"Generated\"}");

        var validation = ScribanGenerator.Validate(new("scriban.json", fixture.Root));
        var generated = ScribanGenerator.Generate(new("scriban.json", fixture.Root));

        Assert.Empty(validation.GeneratedFiles);
        Assert.Equal(6, generated.GeneratedFiles.Count);
        Assert.True(File.Exists(Path.Combine(fixture.Root, "Generated", ScribanGenerator.ManifestFileName)));
        Assert.Contains("Example.Client", File.ReadAllText(Path.Combine(fixture.Root, "Generated", "SchemaTypes.cs")), StringComparison.Ordinal);
        var inputs = ScribanGenerator.GetInputFiles(new("scriban.json", fixture.Root));
        Assert.Contains(GeneratorPathPolicy.Normalize(Path.Combine(fixture.Root, "scriban.json")), inputs);
        Assert.Contains(GeneratorPathPolicy.Normalize(Path.Combine(fixture.Root, "schema.graphql")), inputs);
        Assert.Contains(GeneratorPathPolicy.Normalize(Path.Combine(fixture.Root, "graphql", "hello.graphql")), inputs);
        Assert.Contains(GeneratorPathPolicy.Normalize(Path.Combine(fixture.Root, "Generated", "SchemaTypes.cs")), inputs);
    }

    [Fact]
    public void Configured_template_overrides_are_relative_to_config_and_tracked_as_inputs()
    {
        using var fixture = new GeneratorFixture();
        fixture.WriteInputs();
        var templates = Directory.CreateDirectory(Path.Combine(fixture.Root, "templates"));
        var templatePath = Path.Combine(templates.FullName, "schema.scriban-cs");
        File.WriteAllText(templatePath, "// customized schema for {{ target.default_namespace }}\n");
        fixture.WriteConfig("""
            {
              "version": 1,
              "kind": "client",
              "schema": "schema.graphql",
              "operations": "graphql",
              "namespace": "Example.Customized",
              "output": "Generated",
              "templates": { "schema": "templates/schema.scriban-cs" }
            }
            """);

        ScribanGenerator.Generate(new("scriban.json", fixture.Root));

        var schemaOutput = Path.Combine(fixture.Root, "Generated", "SchemaTypes.cs");
        Assert.Equal("// customized schema for Example.Customized" + Environment.NewLine, File.ReadAllText(schemaOutput));
        Assert.Contains(GeneratorPathPolicy.Normalize(templatePath), ScribanGenerator.GetInputFiles(new("scriban.json", fixture.Root)));

        File.WriteAllText(templatePath, "// updated schema for {{ target.default_namespace }}\n");
        ScribanGenerator.Generate(new("scriban.json", fixture.Root));

        Assert.Equal("// updated schema for Example.Customized" + Environment.NewLine, File.ReadAllText(schemaOutput));
    }

    [Fact]
    public void Configured_template_override_rejects_unknown_template_keys()
    {
        using var fixture = new GeneratorFixture();
        fixture.WriteInputs();
        fixture.WriteConfig("""
            {
              "version": 1,
              "kind": "client",
              "schema": "schema.graphql",
              "operations": "graphql",
              "templates": { "not-a-template": "custom.scriban-cs" }
            }
            """);

        var error = Assert.Throws<ScribanConfigurationException>(() => ScribanGenerator.Validate(new("scriban.json", fixture.Root)));

        Assert.Equal("SALEPS1003", error.Diagnostic.Code);
        Assert.Equal("templates.not-a-template", error.Diagnostic.Property);
    }

    [Fact]
    public void Generation_refuses_to_overwrite_an_unowned_file()
    {
        using var fixture = new GeneratorFixture();
        fixture.WriteInputs();
        fixture.WriteConfig("{\"version\":1,\"kind\":\"client\",\"schema\":\"schema.graphql\",\"operations\":\"graphql\",\"output\":\"Generated\"}");
        var output = Path.Combine(fixture.Root, "Generated");
        Directory.CreateDirectory(output);
        File.WriteAllText(Path.Combine(output, "SchemaTypes.cs"), "// developer owned");

        var error = Assert.Throws<ScribanConfigurationException>(() => ScribanGenerator.Generate(new("scriban.json", fixture.Root)));

        Assert.Equal("SALEPS2003", error.Diagnostic.Code);
        Assert.Equal("// developer owned", File.ReadAllText(Path.Combine(output, "SchemaTypes.cs")));
    }

    [Fact]
    public void Profile_supplies_schema_and_generation_settings()
    {
        using var fixture = new GeneratorFixture();
        fixture.WriteInputs();
        fixture.WriteConfig("{\"version\":1,\"kind\":\"profile\",\"schema\":\"schema.graphql\",\"unionRepresentation\":\"native\",\"useHttpGet\":true}", "profile.json");
        fixture.WriteConfig("{\"version\":1,\"kind\":\"client\",\"profile\":\"profile.json\",\"operations\":\"graphql\",\"output\":\"Generated\"}");

        ScribanGenerator.Generate(new("scriban.json", fixture.Root));
        var client = File.ReadAllText(Path.Combine(fixture.Root, "Generated", "GraphQLClient.cs"));

        Assert.Contains("Salep.Generated", File.ReadAllText(Path.Combine(fixture.Root, "Generated", "SchemaTypes.cs")), StringComparison.Ordinal);
        Assert.Contains("static bool UseHttpGet => true", client, StringComparison.Ordinal);
    }

    [Fact]
    public void Schema_and_operation_parsing_and_globs_respect_limits()
    {
        using var fixture = new GeneratorFixture();
        File.WriteAllText(Path.Combine(fixture.Root, "schema.graphql"),
            "directive @mark on OBJECT | QUERY type Query @mark @mark @mark @mark @mark { hello: String! bye: String! }");
        var operations = Path.Combine(fixture.Root, "graphql");
        Directory.CreateDirectory(operations);
        File.WriteAllText(Path.Combine(operations, "hello.graphql"), "query Hello @mark @mark @mark @mark @mark { hello }");
        File.WriteAllText(Path.Combine(operations, "ignore.graphql"), "query Ignore { bye }");
        fixture.WriteConfig("{\"version\":1,\"kind\":\"client\",\"schema\":\"schema.graphql\",\"operations\":\"graphql/h*.graphql\",\"namespace\":\"Example.Scriban\",\"output\":\"ScribanGenerated\"}");

        ScribanGenerator.Generate(new("scriban.json", fixture.Root));
        var scriban = File.ReadAllText(Path.Combine(fixture.Root, "ScribanGenerated", "Operations.cs"));

        Assert.Contains("Hello", scriban, StringComparison.Ordinal);
        Assert.DoesNotContain("Ignore", scriban, StringComparison.Ordinal);
    }

    [Fact]
    public void Client_configuration_rejects_test_only_raw_json_setting()
    {
        using var fixture = new GeneratorFixture();
        fixture.WriteInputs();
        fixture.WriteConfig("{\"version\":1,\"kind\":\"client\",\"schema\":\"schema.graphql\",\"operations\":\"graphql\",\"rawJsonLiterals\":false}");

        var error = Assert.Throws<ScribanConfigurationException>(() => ScribanGenerator.Validate(new("scriban.json", fixture.Root)));

        Assert.Equal("SALEPS1003", error.Diagnostic.Code);
        Assert.Equal("rawJsonLiterals", error.Diagnostic.Property);
    }

    [Fact]
    public void Scalar_presets_inherit_and_can_be_overridden_by_profiles_and_clients()
    {
        using var fixture = new GeneratorFixture();
        File.WriteAllText(Path.Combine(fixture.Root, "schema.graphql"), "scalar DateTime type Query { now: DateTime! }");
        Directory.CreateDirectory(Path.Combine(fixture.Root, "graphql"));
        File.WriteAllText(Path.Combine(fixture.Root, "graphql", "now.graphql"), "query Now { now }");
        fixture.WriteConfig("{\"version\":1,\"kind\":\"profile\",\"schema\":\"schema.graphql\",\"scalarPreset\":\"nodatime\"}", "nodatime-profile.json");
        fixture.WriteConfig("{\"version\":1,\"kind\":\"client\",\"profile\":\"nodatime-profile.json\",\"operations\":\"graphql\",\"output\":\"Inherited\"}", "inherited.json");
        fixture.WriteConfig("{\"version\":1,\"kind\":\"client\",\"profile\":\"nodatime-profile.json\",\"scalarPreset\":\"builtin\",\"operations\":\"graphql\",\"output\":\"Builtin\"}", "builtin.json");
        fixture.WriteConfig("{\"version\":1,\"kind\":\"client\",\"profile\":\"nodatime-profile.json\",\"scalars\":{\"DateTime\":{\"type\":\"DateTimeOffset\",\"isValueType\":true}},\"operations\":\"graphql\",\"output\":\"Custom\"}", "custom.json");

        ScribanGenerator.Generate(new("inherited.json", fixture.Root));
        ScribanGenerator.Generate(new("builtin.json", fixture.Root));
        ScribanGenerator.Generate(new("custom.json", fixture.Root));
        var inherited = File.ReadAllText(Path.Combine(fixture.Root, "Inherited", "Operations.cs"));
        var builtin = File.ReadAllText(Path.Combine(fixture.Root, "Builtin", "Operations.cs"));
        var custom = File.ReadAllText(Path.Combine(fixture.Root, "Custom", "Operations.cs"));

        Assert.Contains("NodaTime.Instant", inherited, StringComparison.Ordinal);
        Assert.Contains("DateTime Now", builtin, StringComparison.Ordinal);
        Assert.Contains("DateTimeOffset", custom, StringComparison.Ordinal);
        Assert.DoesNotContain("NodaTime.Instant", builtin, StringComparison.Ordinal);
        Assert.DoesNotContain("NodaTime.Instant", custom, StringComparison.Ordinal);
    }

    [Fact]
    public void Configuration_rejects_empty_and_nested_duplicate_properties()
    {
        using var fixture = new GeneratorFixture();
        fixture.WriteInputs();
        fixture.WriteConfig("{\"version\":1,\"kind\":\"client\",\"schema\":\"schema.graphql\",\"operations\":\"graphql\",\"output\":\"\"}");
        var emptyValue = Assert.Throws<ScribanConfigurationException>(() => ScribanGenerator.Validate(new("scriban.json", fixture.Root)));
        Assert.Equal("SALEPS1001", emptyValue.Diagnostic.Code);
        Assert.Equal("output", emptyValue.Diagnostic.Property);

        fixture.WriteConfig("{\"version\":1,\"kind\":\"client\",\"schema\":\"schema.graphql\",\"operations\":\"graphql\",\"scalars\":{\"String\":{\"type\":\"string\",\"type\":\"object\",\"isValueType\":false}}}");
        var duplicate = Assert.Throws<ScribanConfigurationException>(() => ScribanGenerator.Validate(new("scriban.json", fixture.Root)));
        Assert.Equal("SALEPS1001", duplicate.Diagnostic.Code);
        Assert.Equal("$.scalars.String.type", duplicate.Diagnostic.Property);

        fixture.WriteConfig("{\"version\":1,\"kind\":\"client\",\"schema\":\"schema.graphql\",\"operations\":\"graphql\"}", "client.json");
        fixture.WriteConfig("{\"version\":1,\"kind\":\"tests\",\"client\":\"client.json\",\"suites\":[\"transport\",\"transport\"]}", "tests.json");
        var duplicateSuite = Assert.Throws<ScribanConfigurationException>(() => ScribanGenerator.Validate(new("tests.json", fixture.Root)));
        Assert.Equal("SALEPS1001", duplicateSuite.Diagnostic.Code);
        Assert.Equal("suites", duplicateSuite.Diagnostic.Property);
    }

    [Fact]
    public void NodaTime_scalar_preset_configures_generated_json_serialization()
    {
        using var fixture = new GeneratorFixture();
        File.WriteAllText(Path.Combine(fixture.Root, "schema.graphql"), "scalar DateTime type Query { now: DateTime! }");
        Directory.CreateDirectory(Path.Combine(fixture.Root, "graphql"));
        File.WriteAllText(Path.Combine(fixture.Root, "graphql", "now.graphql"), "query Now { now }");
        fixture.WriteConfig("{\"version\":1,\"kind\":\"profile\",\"schema\":\"schema.graphql\",\"scalarPreset\":\"nodatime\"}", "profile.json");
        fixture.WriteConfig("{\"version\":1,\"kind\":\"client\",\"profile\":\"profile.json\",\"operations\":\"graphql\",\"namespace\":\"Example.Client\",\"output\":\"Generated\"}");

        ScribanGenerator.Generate(new("scriban.json", fixture.Root));
        var schema = File.ReadAllText(Path.Combine(fixture.Root, "Generated", "SchemaTypes.cs"));
        var client = File.ReadAllText(Path.Combine(fixture.Root, "Generated", "GraphQLClient.cs"));

        Assert.Contains("using NodaTime;", schema, StringComparison.Ordinal);
        Assert.Contains("using NodaTime.Serialization.SystemTextJson;", client, StringComparison.Ordinal);
        Assert.Contains("_jsonOptions.ConfigureForNodaTime(DateTimeZoneProviders.Tzdb);", client, StringComparison.Ordinal);
    }

    [Fact]
    public void Base_client_shares_schema_and_transport_contracts_and_tests_select_owned_operations()
    {
        using var fixture = new GeneratorFixture();
        File.WriteAllText(Path.Combine(fixture.Root, "schema.graphql"), "type Product { id: ID! } type Query { hello: String! product: Product! }");
        Directory.CreateDirectory(Path.Combine(fixture.Root, "parent-ops"));
        Directory.CreateDirectory(Path.Combine(fixture.Root, "child-ops"));
        File.WriteAllText(Path.Combine(fixture.Root, "parent-ops", "operation.graphql"), "query ParentHello { hello }");
        File.WriteAllText(Path.Combine(fixture.Root, "child-ops", "operation.graphql"), "query ChildHello { product { id } }");
        fixture.WriteConfig("{\"version\":1,\"kind\":\"client\",\"schema\":\"schema.graphql\",\"operations\":\"parent-ops\",\"namespace\":\"Example.Parent\",\"output\":\"ParentGenerated\"}", "parent.json");
        fixture.WriteConfig("{\"version\":1,\"kind\":\"client\",\"schema\":\"schema.graphql\",\"operations\":\"child-ops\",\"namespace\":\"Example.Child\",\"output\":\"ChildGenerated\",\"baseClient\":\"parent.json\"}", "child.json");
        fixture.WriteConfig("{\"version\":1,\"kind\":\"tests\",\"client\":\"child.json\",\"output\":\"ChildTests\",\"suites\":[\"operations\"],\"emitAgentInstructions\":false}", "child-tests.json");

        Assert.Throws<ScribanConfigurationException>(() => ScribanGenerator.Generate(new("child.json", fixture.Root)));
        ScribanGenerator.Generate(new("parent.json", fixture.Root));
        var missingProjectReference = Assert.Throws<ScribanConfigurationException>(() =>
            ScribanGenerator.Validate(new("child.json", fixture.Root, new(null, null, []))));
        Assert.Equal("SALEPS3002", missingProjectReference.Diagnostic.Code);
        ScribanGenerator.Generate(new("child.json", fixture.Root));
        ScribanGenerator.Generate(new("child-tests.json", fixture.Root));

        var childOperations = File.ReadAllText(Path.Combine(fixture.Root, "ChildGenerated", "Operations.cs"));
        var childClient = File.ReadAllText(Path.Combine(fixture.Root, "ChildGenerated", "GraphQLClient.cs"));
        var childTests = File.ReadAllText(Path.Combine(fixture.Root, "ChildTests", "OperationsMetadataTests.cs"));
        Assert.False(File.Exists(Path.Combine(fixture.Root, "ChildGenerated", "SchemaTypes.cs")));
        Assert.Contains("global::Example.Parent.IGraphQLOperation", childOperations, StringComparison.Ordinal);
        Assert.Contains("global::Example.Parent.UnionJsonConverters", childClient, StringComparison.Ordinal);
        Assert.Contains("ChildHello", childTests, StringComparison.Ordinal);
        Assert.DoesNotContain("ParentHello", childTests, StringComparison.Ordinal);
        Assert.Contains(GeneratorPathPolicy.Normalize(Path.Combine(fixture.Root, "ParentGenerated", "SchemaTypes.cs")), ScribanGenerator.GetInputFiles(new("child.json", fixture.Root)));
    }

    private sealed class GeneratorFixture : IDisposable
    {
        public string Root { get; } = Path.Combine(Path.GetTempPath(), "salep-generator-" + Guid.NewGuid().ToString("N"));
        public GeneratorFixture() => Directory.CreateDirectory(Root);
        public void WriteInputs()
        {
            File.WriteAllText(Path.Combine(Root, "schema.graphql"), "type Query { hello: String! }");
            var operations = Path.Combine(Root, "graphql");
            Directory.CreateDirectory(operations);
            File.WriteAllText(Path.Combine(operations, "hello.graphql"), "query Hello { hello }");
        }
        public void WriteConfig(string content, string name = "scriban.json") => File.WriteAllText(Path.Combine(Root, name), content);
        public void Dispose() => Directory.Delete(Root, true);
    }
}
