using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Salep.ClientGenerator.Generation;
using Xunit;

namespace Salep.ClientGenerator.Tests;

public sealed class ConfigurationUpgradeTests : IDisposable
{
    private readonly string root = Path.Combine(Path.GetTempPath(), "salep-config-upgrade-" + Guid.NewGuid().ToString("N"));

    public ConfigurationUpgradeTests()
    {
        Directory.CreateDirectory(root);
        File.WriteAllText(Path.Combine(root, "schema.graphql"), "type Query { value: String }");
        File.WriteAllText(Path.Combine(root, "operation.graphql"), "query Read { value }");
    }

    private ScribanGeneratorOptions Options(string config = "salep.json") => new(config, root, SolutionDirectory: root);
    private string ConfigPath => Path.Combine(root, "salep.json");
    private string Client(int version) => $$"""{"version":{{version}},"kind":"client","schema":"schema.graphql","operations":"operation.graphql","output":"Generated"}""";

    [Theory]
    [InlineData("\n", "utf8")]
    [InlineData("\r\n", "utf8-bom")]
    [InlineData("\r", "utf8")]
    [InlineData("\r\n", "utf16")]
    [InlineData("\n", "utf16-be")]
    public void Generation_upgrades_only_the_version_token_and_keeps_the_manifest_current(string newline, string encodingName)
    {
        var encoding = encodingName switch { "utf8-bom" => new UTF8Encoding(true), "utf16" => Encoding.Unicode, "utf16-be" => Encoding.BigEndianUnicode, _ => new UTF8Encoding(false) };
        // Unicode before the version token exercises byte offsets independently of character offsets.
        var original = "{" + newline + "  \"$schema\": \"https://example.test/schéma.json\"," + newline + "  \"version\" : 1," + newline + "  \"kind\": \"client\", \"schema\": \"schema.graphql\", \"operations\": \"operation.graphql\"" + newline + "}";
        File.WriteAllText(ConfigPath, original, encoding);
        var before = File.ReadAllBytes(ConfigPath);
        ScribanGenerator.Validate(Options());
        ScribanGenerator.GetInputFiles(Options());
        Assert.Equal(before, File.ReadAllBytes(ConfigPath));
        var result = ScribanGenerator.Generate(Options());
        Assert.Contains(result.Logs, log => log.Contains("from version 1 to 2", StringComparison.Ordinal));
        var expected = original.Replace("\"version\" : 1", "\"version\" : 2", StringComparison.Ordinal);
        Assert.Equal(expected, File.ReadAllText(ConfigPath));
        Assert.Equal([..encoding.GetPreamble(), ..encoding.GetBytes(expected)], File.ReadAllBytes(ConfigPath));
        var manifestPath = Path.Combine(root, "Generated", ScribanGenerator.ManifestFileName);
        var manifest = File.ReadAllText(manifestPath);
        using var document = JsonDocument.Parse(manifest);
        var hash = Convert.ToHexString(SHA256.HashData(File.ReadAllBytes(ConfigPath)));
        Assert.Equal(hash, document.RootElement.GetProperty("Inputs").GetProperty("../salep.json").GetString());
        var configTimestamp = File.GetLastWriteTimeUtc(ConfigPath);
        var generatedTimestamp = File.GetLastWriteTimeUtc(Path.Combine(root, "Generated", "SchemaTypes.cs"));
        Assert.DoesNotContain(ScribanGenerator.Generate(Options()).Logs, log => log.StartsWith("Upgraded", StringComparison.Ordinal));
        ScribanGenerator.Validate(Options());
        Assert.Equal(configTimestamp, File.GetLastWriteTimeUtc(ConfigPath));
        Assert.Equal(generatedTimestamp, File.GetLastWriteTimeUtc(Path.Combine(root, "Generated", "SchemaTypes.cs")));
        Assert.Equal(manifest, File.ReadAllText(manifestPath));
    }

    [Fact]
    public void Released_consumer_profiles_clients_modules_and_tests_upgrade_without_changing_settings()
    {
        var fixtures = Path.Combine(AppContext.BaseDirectory, "Fixtures", "Configurations", "V1");
        var originals = new Dictionary<string, string>();
        foreach (var source in Directory.EnumerateFiles(fixtures, "*.json", SearchOption.AllDirectories))
        {
            var destination = Path.Combine(root, "Opinionated", Path.GetRelativePath(fixtures, source));
            Directory.CreateDirectory(Path.GetDirectoryName(destination)!);
            File.Copy(source, destination);
            originals[destination] = File.ReadAllText(destination);
        }
        var schema = Path.Combine(root, "Salep.Samples.GraphQLServer", "Generated", "schema.graphql");
        Directory.CreateDirectory(Path.GetDirectoryName(schema)!);
        File.Copy(Path.Combine(root, "schema.graphql"), schema);
        foreach (var role in new[] { "Client", "Module" })
        {
            var operations = Path.Combine(root, "Opinionated", "Salep.Samples.Opinionated." + role, "graphql");
            Directory.CreateDirectory(operations);
            File.WriteAllText(Path.Combine(operations, "operation.graphql"), "query " + role + "Value { value }");
        }
        foreach (var role in new[] { "Client", "Module", "Client.Tests", "Module.Tests" })
        {
            var config = Path.Combine("Opinionated", "Salep.Samples.Opinionated." + role, "salep.json");
            ScribanGenerator.Generate(Options(config));
            ScribanGenerator.Validate(Options(config));
        }
        foreach (var pair in originals)
            Assert.Equal(pair.Value.Replace("\"version\": 1", "\"version\": 2", StringComparison.Ordinal), File.ReadAllText(pair.Key));
    }

    [Theory]
    [InlineData(0)]
    [InlineData(3)]
    [InlineData(-1)]
    public void Unsupported_versions_fail_without_rewriting(int version)
    {
        var original = Client(version);
        File.WriteAllText(ConfigPath, original);
        var error = Assert.Throws<ScribanConfigurationException>(() => ScribanGenerator.Generate(Options()));
        Assert.Equal("SALEPS1001", error.Diagnostic.Code);
        Assert.Equal("version", error.Diagnostic.Property);
        Assert.Equal(original, File.ReadAllText(ConfigPath));
        Assert.False(Directory.Exists(Path.Combine(root, "Generated")));
    }

    [Theory]
    [InlineData("missing-schema")]
    [InlineData("unknown-property")]
    [InlineData("duplicate-version")]
    [InlineData("unowned-output")]
    public void Failed_generation_leaves_older_config_unchanged(string failure)
    {
        var original = Client(1);
        if (failure == "missing-schema") original = original.Replace("schema.graphql", "missing.graphql", StringComparison.Ordinal);
        if (failure == "unknown-property") original = original.Replace("\"kind\"", "\"obsolete\":true,\"kind\"", StringComparison.Ordinal);
        if (failure == "duplicate-version") original = original.Replace("\"version\":1", "\"version\":1,\"version\":1", StringComparison.Ordinal);
        if (failure == "unowned-output")
        {
            Directory.CreateDirectory(Path.Combine(root, "Generated"));
            File.WriteAllText(Path.Combine(root, "Generated", "Consumer.cs"), "// preserve");
        }
        File.WriteAllText(ConfigPath, original);
        Assert.Throws<ScribanConfigurationException>(() => ScribanGenerator.Generate(Options()));
        Assert.Equal(original, File.ReadAllText(ConfigPath));
    }

    [Fact]
    public void External_read_grant_does_not_upgrade_a_read_only_profile()
    {
        var project = Directory.CreateDirectory(Path.Combine(root, "project")).FullName;
        var profile = Path.Combine(root, "profile.json");
        const string original = """{"version":1,"kind":"profile"}""";
        File.WriteAllText(profile, original);
        File.Copy(Path.Combine(root, "schema.graphql"), Path.Combine(project, "schema.graphql"));
        File.Copy(Path.Combine(root, "operation.graphql"), Path.Combine(project, "operation.graphql"));
        var config = Path.Combine(project, "salep.json");
        File.WriteAllText(config, Client(1).Replace("\"kind\"", "\"profile\":\"../profile.json\",\"kind\"", StringComparison.Ordinal));
        var options = new ScribanGeneratorOptions(config, AllowedReadRoots: [root], SolutionDirectory: project);
        Assert.Contains(ScribanGenerator.Generate(options).Logs, log => log.Contains("read-only configuration", StringComparison.Ordinal));
        Assert.Equal(original, File.ReadAllText(profile));
        Assert.Contains("\"version\":2", File.ReadAllText(config), StringComparison.Ordinal);
        ScribanGenerator.Validate(options);
    }

    [Fact]
    public void Publication_failure_restores_the_previous_config_and_cleans_upgrade_temporary_files()
    {
        var original = Client(1);
        File.WriteAllText(ConfigPath, original);
        Directory.CreateDirectory(Path.Combine(root, "Generated", "SchemaTypes.cs"));
        var error = Record.Exception(() => ScribanGenerator.Generate(Options()));
        // Replacing a directory with a file reports different filesystem errors across platforms.
        Assert.True(error is IOException or UnauthorizedAccessException, $"Expected a filesystem publication failure, got {error?.GetType().FullName ?? "no exception"}.");
        Assert.Equal(original, File.ReadAllText(ConfigPath));
        Assert.Empty(Directory.EnumerateFiles(root, "*.salep-upgrade-*", SearchOption.AllDirectories));
    }

    [Fact]
    public async Task Concurrent_first_generation_upgrades_once_and_leaves_a_current_manifest()
    {
        File.WriteAllText(ConfigPath, Client(1));
        var results = await Task.WhenAll(Enumerable.Range(0, 8).Select(_ => Task.Run(() => ScribanGenerator.Generate(Options()), TestContext.Current.CancellationToken)));
        Assert.Single(results.SelectMany(result => result.Logs), log => log.StartsWith("Upgraded configuration", StringComparison.Ordinal));
        Assert.Equal(Client(2), File.ReadAllText(ConfigPath));
        var manifest = File.ReadAllText(Path.Combine(root, "Generated", ScribanGenerator.ManifestFileName));
        ScribanGenerator.Validate(Options());
        ScribanGenerator.Generate(Options());
        Assert.Equal(manifest, File.ReadAllText(Path.Combine(root, "Generated", ScribanGenerator.ManifestFileName)));
    }

    public void Dispose() => Directory.Delete(root, recursive: true);
}
