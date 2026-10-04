using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;
using Salep.ClientGenerator.Generation;
using Xunit;

namespace Salep.ClientGenerator.Tests;

public sealed class GeneratorPathSecurityTests : IDisposable
{
    private readonly string root = Path.Combine(Path.GetTempPath(), "salep-paths-" + Guid.NewGuid().ToString("N"));
    private string Project => Path.Combine(root, "project");
    private string Shared => Path.Combine(root, "shared");

    public GeneratorPathSecurityTests()
    {
        Directory.CreateDirectory(Project);
        Directory.CreateDirectory(Shared);
        File.WriteAllText(Path.Combine(Project, "schema.graphql"), "type Query { hello: String }");
        Directory.CreateDirectory(Path.Combine(Project, "graphql"));
        File.WriteAllText(Path.Combine(Project, "graphql", "hello.graphql"), "query Hello { hello }");
        File.WriteAllText(Path.Combine(Shared, "schema.graphql"), "type Query { hello: String }");
    }

    private void Config(string property, object value)
    {
        var config = new Dictionary<string, object>
        {
            ["version"] = 1, ["kind"] = "client", ["schema"] = "schema.graphql",
            ["operations"] = "graphql", ["output"] = "Generated", ["emitAgentInstructions"] = false
        };
        config[property] = value;
        File.WriteAllText(Path.Combine(Project, "salep.json"), JsonSerializer.Serialize(config));
    }

    [Theory]
    [InlineData("output", "../shared/generated")]
    [InlineData("output", "..\\shared\\generated")]
    [InlineData("output", ".")]
    [InlineData("output", "CON/generated")]
    [InlineData("output", "Generated:stream")]
    [InlineData("output", "directory./generated")]
    [InlineData("schema", "../shared/schema.graphql")]
    [InlineData("operations", "../shared/**/*.graphql")]
    [InlineData("operations", "graphql/*/../../shared/*.graphql")]
    [InlineData("profile", "../shared/profile.json")]
    [InlineData("baseClient", "../shared/salep.json")]
    public void Configuration_cannot_escape_project_without_caller_grant(string property, string value)
    {
        Config(property, value);
        Assert.Throws<ScribanConfigurationException>(() => ScribanGenerator.Generate(new("salep.json", Project)));
        Assert.False(Directory.Exists(Path.Combine(Project, "Generated")));
        Assert.False(Directory.Exists(Path.Combine(Shared, "generated")));
    }

    [Fact]
    public void Shared_read_grant_does_not_grant_writes_and_is_not_self_granted_by_json()
    {
        Config("schema", "../shared/schema.graphql");
        var options = new ScribanGeneratorOptions("salep.json", Project, AllowedReadRoots: [Shared]);
        var generated = ScribanGenerator.Generate(options);
        Assert.All(generated.GeneratedFiles, file => Assert.DoesNotContain("\\", file));
        Assert.All(ScribanGenerator.GetInputFiles(options), file => Assert.DoesNotContain("\\", file));
        Config("output", "../shared/generated");
        Assert.Throws<ScribanConfigurationException>(() => ScribanGenerator.Generate(options));
        Config("allowedReadRoots", new[] { Shared });
        Assert.Throws<ScribanConfigurationException>(() => ScribanGenerator.Validate(new("salep.json", Project)));
    }

    [Fact]
    public void Absolute_paths_and_sibling_prefixes_cannot_bypass_boundary()
    {
        Config("schema", Path.Combine(Shared, "schema.graphql"));
        Assert.Throws<ScribanConfigurationException>(() => ScribanGenerator.Validate(new("salep.json", Project)));
        Config("output", Project + "-other/Generated");
        Assert.Throws<ScribanConfigurationException>(() => ScribanGenerator.Generate(new("salep.json", Project)));
        Assert.False(Directory.Exists(Project + "-other"));
    }

    [Theory]
    [InlineData(".sln")]
    [InlineData(".slnx")]
    public void Solution_boundary_allows_sibling_inputs_and_shared_output(string extension)
    {
        File.WriteAllText(Path.Combine(root, "Consumer" + extension), "");
        Config("schema", "../shared/schema.graphql");
        var configPath = Path.Combine(Project, "salep.json");
        var json = JsonNode.Parse(File.ReadAllText(configPath))!.AsObject();
        json["output"] = "../shared/generated";
        File.WriteAllText(configPath, json.ToJsonString());
        var result = ScribanGenerator.Generate(new("salep.json", Project));
        Assert.All(result.GeneratedFiles, file => Assert.StartsWith(GeneratorPathPolicy.Normalize(Path.Combine(Shared, "generated")) + "/", file, StringComparison.Ordinal));
        Config("output", "../../escaped");
        Assert.Throws<ScribanConfigurationException>(() => ScribanGenerator.Generate(new("salep.json", Project)));
    }

    [Fact]
    public void Caller_can_pin_a_solution_boundary_without_a_solution_file()
    {
        Config("schema", "../shared/schema.graphql");
        ScribanGenerator.Generate(new("salep.json", Project, SolutionDirectory: root));
        Config("schema", "../../outside.graphql");
        Assert.Throws<ScribanConfigurationException>(() => ScribanGenerator.Validate(new("salep.json", Project, SolutionDirectory: root)));
    }

    [Fact]
    public void Backslash_paths_are_normalized_for_local_inputs_and_output()
    {
        Config("operations", "graphql\\hello.graphql");
        var configPath = Path.Combine(Project, "salep.json");
        var json = JsonNode.Parse(File.ReadAllText(configPath))!.AsObject();
        json["output"] = "nested\\Generated";
        File.WriteAllText(configPath, json.ToJsonString());
        var generated = ScribanGenerator.Generate(new("salep.json", Project));
        Assert.All(generated.GeneratedFiles, file => Assert.Contains("/nested/Generated/", file, StringComparison.Ordinal));
        var manifestPath = Path.Combine(Project, "nested", "Generated", ".salep.manifest.json");
        Assert.DoesNotContain("\\", File.ReadAllText(manifestPath));
    }

    [Theory]
    [InlineData(".salep.lock")]
    [InlineData(".salep.manifest.json")]
    [InlineData("SchemaTypes.cs")]
    public void Linked_owned_files_are_rejected_without_modifying_target(string name)
    {
        Config("output", "Generated");
        ScribanGenerator.Generate(new("salep.json", Project));
        var target = Path.Combine(Shared, "victim.txt");
        File.WriteAllText(target, "do not touch");
        var link = Path.Combine(Project, "Generated", name);
        File.Delete(link);
        File.CreateSymbolicLink(link, target);
        Assert.Throws<ScribanConfigurationException>(() => ScribanGenerator.Generate(new("salep.json", Project)));
        Assert.Equal("do not touch", File.ReadAllText(target));
    }

    [Fact]
    public void Template_read_and_linked_output_cannot_escape()
    {
        File.WriteAllText(Path.Combine(Shared, "template.scriban-cs"), "// shared");
        Config("templates", new Dictionary<string, string> { ["schema"] = "../shared/template.scriban-cs" });
        Assert.Throws<ScribanConfigurationException>(() => ScribanGenerator.Generate(new("salep.json", Project)));
        Directory.CreateSymbolicLink(Path.Combine(Project, "linked"), Shared);
        Config("output", "linked/generated");
        Assert.Throws<ScribanConfigurationException>(() => ScribanGenerator.Generate(new("salep.json", Project)));
        Assert.False(Directory.Exists(Path.Combine(Shared, "generated")));
    }

    [Fact]
    public void Linked_schema_and_recursive_glob_are_rejected_before_reading_outside()
    {
        File.CreateSymbolicLink(Path.Combine(Project, "linked.graphql"), Path.Combine(Shared, "schema.graphql"));
        Config("schema", "linked.graphql");
        Assert.Throws<ScribanConfigurationException>(() => ScribanGenerator.Generate(new("salep.json", Project)));
        Directory.CreateSymbolicLink(Path.Combine(Project, "graphql", "linked"), Shared);
        Config("operations", "graphql/**/*.graphql");
        Assert.Throws<ScribanConfigurationException>(() => ScribanGenerator.Generate(new("salep.json", Project)));
    }

    [Theory]
    [InlineData("../victim.cs")]
    [InlineData("..\\victim.cs")]
    [InlineData("/victim.cs")]
    [InlineData("C:\\victim.cs")]
    [InlineData("victim.cs:stream")]
    [InlineData(".salep.lock")]
    [InlineData("CON.cs")]
    [InlineData("victim.cs.")]
    public void Resealed_manifest_cannot_read_or_delete_paths_outside_output(string maliciousName)
    {
        Config("output", "Generated");
        ScribanGenerator.Generate(new("salep.json", Project));
        var victim = Path.Combine(Project, "victim.cs");
        File.WriteAllText(victim, "do not touch");
        var manifestPath = Path.Combine(Project, "Generated", ".salep.manifest.json");
        var manifest = JsonNode.Parse(File.ReadAllText(manifestPath))!.AsObject();
        manifest["Files"]![maliciousName] = Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes("do not touch")));
        manifest["Fingerprint"] = "";
        manifest["Fingerprint"] = Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(Canonical(JsonSerializer.SerializeToElement(manifest)))));
        File.WriteAllText(manifestPath, manifest.ToJsonString());
        Assert.Throws<ScribanConfigurationException>(() => ScribanGenerator.Generate(new("salep.json", Project)));
        Assert.Equal("do not touch", File.ReadAllText(victim));
    }

    private static string Canonical(JsonElement element) => element.ValueKind switch
    {
        JsonValueKind.Object => "{" + string.Join(",", element.EnumerateObject().OrderBy(property => property.Name, StringComparer.Ordinal).Select(property => JsonSerializer.Serialize(property.Name) + ":" + Canonical(property.Value))) + "}",
        JsonValueKind.Array => "[" + string.Join(",", element.EnumerateArray().Select(Canonical)) + "]",
        _ => element.GetRawText()
    };

    public void Dispose() => Directory.Delete(root, true);
}
