using System.Text.Json;
using Salep.ClientGenerator.Cli;
using Salep.ClientGenerator.Generation;
using Xunit;

namespace Salep.ClientGenerator.MSBuild.Tests;

public sealed class CSharpCliAuxiliaryPathRegressionTests
{
    [Theory]
    [InlineData("generate", "--stamp-file", "schema.graphql")]
    [InlineData("generate", "--sources-file", "salep.json")]
    [InlineData("generate", "--sources-file", "operation.graphql")]
    [InlineData("generate", "--stamp-file", "SCHEMA.graphql")]
    [InlineData("generate", "--stamp-file", "Generated/GraphQLClient.cs")]
    [InlineData("generate", "--sources-file", "Generated/.salep.manifest.json")]
    [InlineData("generate", "--stamp-file", "Generated/.salep.lock")]
    [InlineData("generate", "--sources-file", "Generated/.salep-scriban.manifest.json")]
    [InlineData("generate", "--sources-file", "Generated")]
    [InlineData("inputs", "--inputs-file", "salep.json")]
    [InlineData("inputs", "--inputs-file", "Generated/SchemaTypes.cs")]
    public void Auxiliary_paths_cannot_overwrite_inputs_current_outputs_or_control_files(string command, string option, string path)
    {
        using var fixture = new Fixture();
        fixture.AssertRejectedWithoutWrites([command, "--config", "salep.json", "--working-directory", fixture.Root, option, path]);
    }

    [Theory]
    [InlineData("track.txt", "TRACK.txt")]
    [InlineData("tracking", "tracking/sources.txt")]
    public void Auxiliary_file_paths_cannot_conflict_with_each_other(string first, string second)
    {
        using var fixture = new Fixture();
        fixture.AssertRejectedWithoutWrites(["generate", "--config", "salep.json", "--working-directory", fixture.Root, "--sources-file", first, "--stamp-file", second]);
    }

    [Fact]
    public void Inputs_command_protects_profile_configuration()
    {
        using var fixture = new Fixture();
        File.WriteAllText(Path.Combine(fixture.Root, "profile.json"), """{"version":1,"kind":"profile"}""");
        fixture.AssertRejectedWithoutWrites(["inputs", "--config", "profile.json", "--working-directory", fixture.Root, "--inputs-file", "PROFILE.json"]);
    }

    [Fact]
    public void Auxiliary_paths_cannot_replace_stale_owned_outputs()
    {
        using var fixture = new Fixture();
        fixture.WriteConfig(emitAgentInstructions: true);
        ScribanGenerator.Generate(new(Path.Combine(fixture.Root, "salep.json")));
        fixture.WriteConfig(emitAgentInstructions: false);
        fixture.AssertRejectedWithoutWrites(["generate", "--config", "salep.json", "--working-directory", fixture.Root, "--sources-file", "Generated/AGENTS.md"]);
    }

    [Fact]
    public void Generator_cannot_delete_an_owned_file_used_as_a_template_input()
    {
        using var fixture = new Fixture();
        File.WriteAllText(Path.Combine(fixture.Root, "agent.scriban"), "// custom marker");
        fixture.WriteConfig(true, new Dictionary<string, string> { ["client-agent-instructions"] = "agent.scriban" });
        ScribanGenerator.Generate(new(Path.Combine(fixture.Root, "salep.json")));
        fixture.WriteConfig(false, new Dictionary<string, string> { ["client.annotations"] = "Generated/agents.md" });
        var before = fixture.Snapshot();
        var error = Assert.Throws<ScribanConfigurationException>(() => ScribanGenerator.Generate(new(Path.Combine(fixture.Root, "salep.json"))));
        Assert.Equal("SALEPS1007", error.Diagnostic.Code);
        Assert.Contains("inputs cannot overlap", error.Diagnostic.Message);
        fixture.AssertPreserved(before);
    }

    [Fact]
    public void Separate_msbuild_style_tracking_files_remain_supported()
    {
        using var fixture = new Fixture();
        string[] prefix = ["--config", "salep.json", "--working-directory", fixture.Root];
        Assert.Equal(0, Program.Run(["inputs", ..prefix, "--inputs-file", "obj/Debug/net10.0/salep.inputs"], TextWriter.Null, TextWriter.Null));
        Assert.Equal(0, Program.Run(["generate", ..prefix, "--sources-file", "obj/Debug/net10.0/salep.sources", "--stamp-file", "obj/Debug/net10.0/salep.stamp"], TextWriter.Null, TextWriter.Null));
        Assert.NotEmpty(File.ReadAllLines(Path.Combine(fixture.Root, "obj/Debug/net10.0/salep.inputs")));
        Assert.Contains("GraphQLClient.cs", File.ReadAllText(Path.Combine(fixture.Root, "obj/Debug/net10.0/salep.sources")));
        Assert.NotEmpty(File.ReadAllText(Path.Combine(fixture.Root, "obj/Debug/net10.0/salep.stamp")));
    }

    [Fact]
    public void Preflight_protects_every_assembly_in_the_published_input_inventory_without_writing()
    {
        using var fixture = new Fixture();
        var toolDirectory = Path.GetDirectoryName(typeof(ScribanGenerator).Assembly.Location)!;
        // Resolve hypothetical output paths within the tool boundary without publishing.
        fixture.WriteConfig(outputDirectory: Path.Combine(toolDirectory, "preflight-" + Guid.NewGuid().ToString("N")));
        var options = new ScribanGeneratorOptions(Path.Combine(fixture.Root, "salep.json"), AllowedReadRoots: [fixture.Root], SolutionDirectory: toolDirectory);
        var assemblies = ScribanGenerator.GetInputFiles(options).Where(file => file.EndsWith(".dll", StringComparison.Ordinal)).ToArray();
        Assert.NotEmpty(assemblies);
        foreach (var assembly in assemblies)
        {
            var before = File.ReadAllBytes(assembly);
            // Preflight only: even a regression cannot publish over a live assembly.
            var error = Assert.Throws<ScribanConfigurationException>(() => ScribanGenerator.ValidateAuxiliaryOutputPaths(options, [assembly]));
            Assert.Equal("SALEPS1007", error.Diagnostic.Code);
            Assert.Contains("Tracking files cannot overlap", error.Diagnostic.Message);
            Assert.Equal(before, File.ReadAllBytes(assembly));
        }
    }

    [Fact]
    public void Caller_can_protect_its_entrypoint_as_an_additional_input()
    {
        using var fixture = new Fixture();
        var entrypoint = Path.Combine(fixture.Root, "copied-cli.dll");
        File.Copy(typeof(Program).Assembly.Location, entrypoint);
        var before = File.ReadAllBytes(entrypoint);
        var options = new ScribanGeneratorOptions(Path.Combine(fixture.Root, "salep.json"));
        var error = Assert.Throws<ScribanConfigurationException>(() => ScribanGenerator.ValidateAuxiliaryOutputPaths(options, [entrypoint], [entrypoint]));
        Assert.Equal("SALEPS1007", error.Diagnostic.Code);
        Assert.Contains("Tracking files cannot overlap", error.Diagnostic.Message);
        Assert.Equal(before, File.ReadAllBytes(entrypoint));
        Assert.False(Directory.Exists(Path.Combine(fixture.Root, "Generated")));
    }

    [Fact]
    public void Caller_can_protect_every_file_in_its_copied_tool_directory()
    {
        using var fixture = new Fixture();
        var toolDirectory = Path.Combine(fixture.Root, "copied-tool");
        Directory.CreateDirectory(toolDirectory);
        var dependency = Path.Combine(toolDirectory, "required-dependency.dll");
        File.Copy(typeof(Program).Assembly.Location, dependency);
        var before = File.ReadAllBytes(dependency);
        var options = new ScribanGeneratorOptions(Path.Combine(fixture.Root, "salep.json"));
        var error = Assert.Throws<ScribanConfigurationException>(() => ScribanGenerator.ValidateAuxiliaryOutputPaths(options, [dependency], [toolDirectory]));
        Assert.Equal("SALEPS1007", error.Diagnostic.Code);
        Assert.Contains("Tracking files cannot overlap", error.Diagnostic.Message);
        Assert.Equal(before, File.ReadAllBytes(dependency));
        Assert.False(Directory.Exists(Path.Combine(fixture.Root, "Generated")));
    }

    [Theory]
    [InlineData("/tmp", "/private/tmp")]
    [InlineData("/private/tmp", "/tmp")]
    public void Preflight_protects_copied_tool_identity_across_macos_temporary_root_aliases(string lexicalRoot, string physicalRoot)
    {
        if (!OperatingSystem.IsMacOS()) return;
        using var fixture = new Fixture(lexicalRoot);
        var copiedTool = Path.Combine(fixture.Root, "copied-tool.dll");
        File.Copy(typeof(Program).Assembly.Location, copiedTool);
        var protectedAlias = Path.Combine(physicalRoot, Path.GetFileName(fixture.Root), "copied-tool.dll");
        var before = File.ReadAllBytes(copiedTool);
        var options = new ScribanGeneratorOptions(Path.Combine(fixture.Root, "salep.json"));
        // Preflight cannot corrupt even the copied tool if identity matching regresses.
        var error = Assert.Throws<ScribanConfigurationException>(() => ScribanGenerator.ValidateAuxiliaryOutputPaths(options, [copiedTool], [protectedAlias]));
        Assert.Equal("SALEPS1007", error.Diagnostic.Code);
        Assert.Contains("Tracking files cannot overlap", error.Diagnostic.Message);
        Assert.Equal(before, File.ReadAllBytes(copiedTool));
        Assert.False(Directory.Exists(Path.Combine(fixture.Root, "Generated")));
    }

    private sealed class Fixture : IDisposable
    {
        public string Root { get; }
        public Fixture(string? temporaryRoot = null)
        {
            Root = Path.Combine(temporaryRoot ?? Path.GetTempPath(), "salep-cli-auxiliary-" + Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(Root);
            File.WriteAllText(Path.Combine(Root, "schema.graphql"), "type Query { value: String }");
            File.WriteAllText(Path.Combine(Root, "operation.graphql"), "query Read { value }");
            WriteConfig();
        }
        public void WriteConfig(bool emitAgentInstructions = false, Dictionary<string, string>? templates = null, string? outputDirectory = null)
        {
            var config = new Dictionary<string, object?> { ["version"] = 1, ["kind"] = "client", ["schema"] = "schema.graphql", ["operations"] = "operation.graphql", ["namespace"] = "Auxiliary.Client", ["output"] = outputDirectory ?? "Generated", ["unionRepresentation"] = "dunet", ["emitAgentInstructions"] = emitAgentInstructions };
            if (templates is not null) config["templates"] = templates;
            File.WriteAllText(Path.Combine(Root, "salep.json"), JsonSerializer.Serialize(config));
        }
        public Dictionary<string, byte[]> Snapshot() => Directory.EnumerateFiles(Root, "*", SearchOption.AllDirectories).ToDictionary(file => file, File.ReadAllBytes, StringComparer.Ordinal);
        public void AssertPreserved(Dictionary<string, byte[]> before)
        {
            Assert.Equal(before.Keys.Order(StringComparer.Ordinal), Directory.EnumerateFiles(Root, "*", SearchOption.AllDirectories).Order(StringComparer.Ordinal));
            foreach (var file in before) Assert.Equal(file.Value, File.ReadAllBytes(file.Key));
        }
        public void AssertRejectedWithoutWrites(string[] args)
        {
            var before = Snapshot();
            using var error = new StringWriter();
            Assert.Equal(1, Program.Run(args, TextWriter.Null, error));
            Assert.Contains("SALEPS1007", error.ToString());
            Assert.Contains("Tracking files cannot overlap", error.ToString());
            AssertPreserved(before);
        }
        public void Dispose() => Directory.Delete(Root, true);
    }
}
