using System.Xml.Linq;
using System.Diagnostics;
using System.Text;
using System.Security;
using Salep.ClientGenerator.Cli;
using Xunit;

namespace Salep.ClientGenerator.MSBuild.Tests;

public sealed class IntegrationContractTests
{
    [Fact]
    public void Cli_validates_and_generates_sources_for_a_configuration()
    {
        using var fixture = new Fixture();
        fixture.WriteInputs();
        fixture.WriteConfig("{\"version\":1,\"kind\":\"client\",\"schema\":\"schema.graphql\",\"operations\":\"graphql\",\"output\":\"Generated\",\"namespace\":\"Example.Client\"}");

        Assert.Equal(0, Program.Run(["validate", "--config", "salep.json", "--working-directory", fixture.Root], TextWriter.Null, TextWriter.Null));
        var sourcesFile = Path.Combine(fixture.Root, "obj", "sources.txt");
        Assert.Equal(0, Program.Run(["generate", "--config", "salep.json", "--working-directory", fixture.Root, "--sources-file", sourcesFile], TextWriter.Null, TextWriter.Null));

        Assert.True(File.Exists(Path.Combine(fixture.Root, "Generated", "SchemaTypes.cs")));
        Assert.True(File.Exists(Path.Combine(fixture.Root, "Generated", ".salep.manifest.json")));
        Assert.Equal(5, File.ReadAllLines(sourcesFile).Length);
    }

    [Fact]
    public void Cli_rejects_sources_file_for_validation_and_reports_json_diagnostic()
    {
        var parsed = ScribanCliArguments.Parse(["validate", "--config", "a.json", "--sources-file", "out.txt"]);
        Assert.Contains(parsed.Errors, item => item.Contains("does not write", StringComparison.Ordinal));

        using var stderr = new StringWriter();
        Assert.Equal(1, Program.Run(["validate", "--config", "missing.json"], TextWriter.Null, stderr));
        Assert.Contains("SALEPS1001", stderr.ToString(), StringComparison.Ordinal);
    }

    [Fact]
    public void Msbuild_targets_run_before_compile_and_include_cli_generated_files()
    {
        var targetsPath = Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, "Salep.ClientGenerator.targets"));
        var document = XDocument.Load(targetsPath);
        XNamespace ns = document.Root!.Name.Namespace;
        var generate = document.Descendants(ns + "Target").Single(element => (string?)element.Attribute("Name") == "SalepGenerate");
        var include = document.Descendants(ns + "Target").Single(element => (string?)element.Attribute("Name") == "_SalepIncludeGeneratedFiles");
        var removePrevious = document.Descendants(ns + "Target").Single(element => (string?)element.Attribute("Name") == "_SalepRemovePreviouslyOwnedCompileItems");

        Assert.Equal("CoreCompile", (string?)generate.Attribute("BeforeTargets"));
        Assert.Contains("_SalepRemovePreviouslyOwnedCompileItems", (string?)generate.Attribute("DependsOnTargets"), StringComparison.Ordinal);
        Assert.Contains("<Compile Remove=", removePrevious.ToString(), StringComparison.Ordinal);
        Assert.Contains("Salep.ClientGenerator.Cli.dll", generate.ToString(), StringComparison.Ordinal);
        Assert.Contains("--target-framework", generate.ToString(), StringComparison.Ordinal);
        Assert.Contains("<Compile Include=", include.ToString(), StringComparison.Ordinal);
    }

    [Fact]
    public void Imported_targets_generate_compile_and_rebuild_when_an_operation_changes()
    {
        using var fixture = new Fixture();
        fixture.WriteInputs();
        var templates = Directory.CreateDirectory(Path.Combine(fixture.Root, "templates"));
        var schemaTemplate = Path.Combine(templates.FullName, "schema.scriban-cs");
        File.WriteAllText(schemaTemplate, "// schema template version one\n");
        fixture.WriteConfig("{\"version\":1,\"kind\":\"client\",\"schema\":\"schema.graphql\",\"operations\":\"graphql\",\"output\":\"Generated\",\"namespace\":\"Example.Client\",\"templates\":{\"schema\":\"templates/schema.scriban-cs\"}}");
        var toolDirectory = AppContext.BaseDirectory.TrimEnd(Path.DirectorySeparatorChar);
        var props = Path.Combine(toolDirectory, "Salep.ClientGenerator.props");
        var targets = Path.Combine(toolDirectory, "Salep.ClientGenerator.targets");
        File.WriteAllText(Path.Combine(fixture.Root, "Fixture.csproj"), $"""
            <Project Sdk="Microsoft.NET.Sdk">
              <PropertyGroup>
                <TargetFramework>net10.0</TargetFramework>
                <LangVersion>latest</LangVersion>
                <Nullable>enable</Nullable>
                <TreatWarningsAsErrors>true</TreatWarningsAsErrors>
                <SalepToolPath>{SecurityElement.Escape(toolDirectory)}</SalepToolPath>
              </PropertyGroup>
              <Import Project="{SecurityElement.Escape(props)}" />
              <Import Project="{SecurityElement.Escape(targets)}" />
            </Project>
            """);

        var first = BuildFixture(fixture.Root);
        Assert.True(first.ExitCode == 0, first.Output);
        Assert.True(File.Exists(Path.Combine(fixture.Root, "obj", "Debug", "net10.0", "salep.generated-sources.txt")));
        Assert.Contains("schema template version one", File.ReadAllText(Path.Combine(fixture.Root, "Generated", "SchemaTypes.cs")), StringComparison.Ordinal);
        var second = BuildFixture(fixture.Root, noRestore: true);
        Assert.True(second.ExitCode == 0, second.Output);
        Assert.DoesNotContain("Generated 6 files", second.Output, StringComparison.Ordinal);

        File.WriteAllText(Path.Combine(fixture.Root, "graphql", "hello.graphql"), "query Hello { bye }");
        var changed = BuildFixture(fixture.Root, noRestore: true);
        Assert.True(changed.ExitCode == 0, changed.Output);
        Assert.Contains("Generated 6 files", changed.Output, StringComparison.Ordinal);
        Assert.Contains("bye", File.ReadAllText(Path.Combine(fixture.Root, "Generated", "Operations.cs")), StringComparison.Ordinal);

        File.WriteAllText(schemaTemplate, "// schema template version two\n");
        var templateChanged = BuildFixture(fixture.Root, noRestore: true);
        Assert.True(templateChanged.ExitCode == 0, templateChanged.Output);
        Assert.Contains("Generated 6 files", templateChanged.Output, StringComparison.Ordinal);
        Assert.Contains("schema template version two", File.ReadAllText(Path.Combine(fixture.Root, "Generated", "SchemaTypes.cs")), StringComparison.Ordinal);
    }

    [Fact]
    public void Nested_fragment_changes_rebuild_without_replacing_the_client_template()
    {
        using var fixture = new Fixture();
        fixture.WriteInputs();
        var fragment = Path.Combine(fixture.Root, "members.scriban-cs");
        File.WriteAllText(fragment, "    public string ConsumerMarker => \"one\";\n");
        fixture.WriteConfig("""
            {"version":1,"kind":"client","schema":"schema.graphql","operations":"graphql","output":"Generated","namespace":"Example.Client","templates":{"client.members":"members.scriban-cs"}}
            """);
        var tool = AppContext.BaseDirectory.TrimEnd(Path.DirectorySeparatorChar);
        File.WriteAllText(Path.Combine(fixture.Root, "Fixture.csproj"), $"""
            <Project Sdk="Microsoft.NET.Sdk">
              <PropertyGroup>
                <TargetFramework>net10.0</TargetFramework><Nullable>enable</Nullable>
                <TreatWarningsAsErrors>true</TreatWarningsAsErrors>
                <SalepToolPath>{SecurityElement.Escape(tool)}</SalepToolPath>
              </PropertyGroup>
              <Import Project="{SecurityElement.Escape(Path.Combine(tool, "Salep.ClientGenerator.props"))}" />
              <Import Project="{SecurityElement.Escape(Path.Combine(tool, "Salep.ClientGenerator.targets"))}" />
            </Project>
            """);
        var first = BuildFixture(fixture.Root);
        Assert.True(first.ExitCode == 0, first.Output);
        var output = Path.Combine(fixture.Root, "Generated", "GraphQLClient.cs");
        Assert.Contains("ConsumerMarker => \"one\"", File.ReadAllText(output), StringComparison.Ordinal);
        Assert.Contains("ExecuteIncrementalAsync", File.ReadAllText(output), StringComparison.Ordinal);
        var inputList = Path.Combine(fixture.Root, "obj", "Debug", "net10.0", "salep.inputs.txt");
        Assert.Contains(File.ReadAllLines(inputList), input =>
            input.EndsWith(Path.DirectorySeparatorChar + Path.GetFileName(fixture.Root) + Path.DirectorySeparatorChar + Path.GetFileName(fragment), StringComparison.Ordinal)
            && File.ReadAllText(input) == File.ReadAllText(fragment));
        var unchanged = BuildFixture(fixture.Root, noRestore: true);
        Assert.True(unchanged.ExitCode == 0, unchanged.Output);
        Assert.DoesNotContain("Generated 6 files", unchanged.Output, StringComparison.Ordinal);
        File.WriteAllText(fragment, "    public string ConsumerMarker => \"two\";\n");
        var changed = BuildFixture(fixture.Root, noRestore: true);
        Assert.True(changed.ExitCode == 0, changed.Output);
        Assert.Contains("Generated 6 files", changed.Output, StringComparison.Ordinal);
        Assert.Contains("ConsumerMarker => \"two\"", File.ReadAllText(output), StringComparison.Ordinal);
    }

    [Fact]
    public void Multi_target_build_removes_obsolete_sample_sources_from_each_framework()
    {
        using var fixture = new Fixture();
        fixture.WriteInputs();
        var config = "{\"version\":1,\"kind\":\"client\",\"schema\":\"schema.graphql\",\"operations\":\"graphql\",\"output\":\"Generated\",\"namespace\":\"Example.Client\",\"emitSample\":true}";
        fixture.WriteConfig(config);
        var tool = AppContext.BaseDirectory.TrimEnd(Path.DirectorySeparatorChar);
        File.WriteAllText(Path.Combine(fixture.Root, "Fixture.csproj"), $"""
            <Project Sdk="Microsoft.NET.Sdk">
              <PropertyGroup>
                <TargetFrameworks>net10.0;net11.0</TargetFrameworks>
                <LangVersion>latest</LangVersion><Nullable>enable</Nullable>
                <TreatWarningsAsErrors>true</TreatWarningsAsErrors>
                <SalepToolPath>{SecurityElement.Escape(tool)}</SalepToolPath>
              </PropertyGroup>
              <Import Project="{SecurityElement.Escape(Path.Combine(tool, "Salep.ClientGenerator.props"))}" />
              <Import Project="{SecurityElement.Escape(Path.Combine(tool, "Salep.ClientGenerator.targets"))}" />
            </Project>
            """);
        var first = BuildFixture(fixture.Root);
        Assert.True(first.ExitCode == 0, first.Output);
        Assert.True(File.Exists(Path.Combine(fixture.Root, "Generated", "Operations.Sample.cs")));
        fixture.WriteConfig(config.Replace("\"emitSample\":true", "\"emitSample\":false", StringComparison.Ordinal));
        var second = BuildFixture(fixture.Root, noRestore: true);
        Assert.True(second.ExitCode == 0, second.Output);
        Assert.False(File.Exists(Path.Combine(fixture.Root, "Generated", "Operations.Sample.cs")));
        foreach (var framework in new[] { "net10.0", "net11.0" })
            Assert.True(File.Exists(Path.Combine(fixture.Root, "bin", "Debug", framework, "Fixture.dll")));
    }

    [Fact]
    public void Imported_targets_build_base_clients_first_and_validate_project_references()
    {
        using var fixture = new Fixture();
        File.WriteAllText(Path.Combine(fixture.Root, "schema.graphql"), "type Product { id: ID! } type Query { hello: String! product: Product! }");
        Directory.CreateDirectory(Path.Combine(fixture.Root, "parent-ops"));
        Directory.CreateDirectory(Path.Combine(fixture.Root, "child-ops"));
        File.WriteAllText(Path.Combine(fixture.Root, "parent-ops", "parent.graphql"), "query ParentHello { hello }");
        File.WriteAllText(Path.Combine(fixture.Root, "child-ops", "child.graphql"), "query ChildProduct { product { id } }");
        var parentDirectory = Directory.CreateDirectory(Path.Combine(fixture.Root, "Parent")).FullName;
        var childDirectory = Directory.CreateDirectory(Path.Combine(fixture.Root, "Child")).FullName;
        var toolDirectory = AppContext.BaseDirectory.TrimEnd(Path.DirectorySeparatorChar);
        WriteProject(parentDirectory, "Parent", toolDirectory, "../schema.graphql", "../parent-ops", "Example.Parent");
        WriteProject(childDirectory, "Child", toolDirectory, "../schema.graphql", "../child-ops", "Example.Child", "../Parent/Parent.csproj");

        var standalone = BuildFixture(fixture.Root, project: "Child/Child.csproj");
        Assert.True(standalone.ExitCode == 0, standalone.Output);
        Assert.True(File.Exists(Path.Combine(childDirectory, "Generated", "SchemaTypes.cs")));

        var childConfigPath = Path.Combine(childDirectory, "salep.json");
        var childConfig = System.Text.Json.JsonSerializer.Deserialize<Dictionary<string, object?>>(File.ReadAllText(childConfigPath))!;
        childConfig["baseClient"] = "../Parent/salep.json";
        File.WriteAllText(childConfigPath, System.Text.Json.JsonSerializer.Serialize(childConfig));
        var result = BuildFixture(fixture.Root, noRestore: true, project: "Child/Child.csproj");

        Assert.True(result.ExitCode == 0, result.Output);
        Assert.True(File.Exists(Path.Combine(parentDirectory, "Generated", ".salep.manifest.json")));
        Assert.True(File.Exists(Path.Combine(childDirectory, "Generated", "Operations.cs")));
        Assert.False(File.Exists(Path.Combine(childDirectory, "Generated", "SchemaTypes.cs")));
        Assert.Contains("global::Example.Parent.IGraphQLOperation", File.ReadAllText(Path.Combine(childDirectory, "Generated", "Operations.cs")), StringComparison.Ordinal);
        Assert.Contains("global::Example.Parent.UnionJsonConverters", File.ReadAllText(Path.Combine(childDirectory, "Generated", "GraphQLClient.cs")), StringComparison.Ordinal);
    }

    private static void WriteProject(string directory, string name, string toolDirectory, string schema, string operations,
        string generatedNamespace, string? projectReference = null, string? baseClient = null)
    {
        var config = new Dictionary<string, object?>
        {
            ["version"] = 1, ["kind"] = "client", ["schema"] = schema, ["operations"] = operations,
            ["output"] = "Generated", ["namespace"] = generatedNamespace
        };
        if (baseClient is not null) config["baseClient"] = baseClient;
        File.WriteAllText(Path.Combine(directory, "salep.json"), System.Text.Json.JsonSerializer.Serialize(config));
        var reference = projectReference is null ? string.Empty : $"<ProjectReference Include=\"{SecurityElement.Escape(projectReference)}\" />";
        File.WriteAllText(Path.Combine(directory, name + ".csproj"), $"""
            <Project Sdk="Microsoft.NET.Sdk">
              <PropertyGroup>
                <TargetFramework>net10.0</TargetFramework>
                <LangVersion>latest</LangVersion>
                <Nullable>enable</Nullable>
                <TreatWarningsAsErrors>true</TreatWarningsAsErrors>
                <SalepToolPath>{SecurityElement.Escape(toolDirectory)}</SalepToolPath>
              </PropertyGroup>
              <ItemGroup>{reference}</ItemGroup>
              <Import Project="{SecurityElement.Escape(Path.Combine(toolDirectory, "Salep.ClientGenerator.props"))}" />
              <Import Project="{SecurityElement.Escape(Path.Combine(toolDirectory, "Salep.ClientGenerator.targets"))}" />
            </Project>
            """);
    }

    private static (int ExitCode, string Output) BuildFixture(string workingDirectory, bool noRestore = false, string project = "Fixture.csproj")
    {
        var start = new ProcessStartInfo("dotnet") { WorkingDirectory = workingDirectory, RedirectStandardOutput = true, RedirectStandardError = true, UseShellExecute = false };
        start.ArgumentList.Add("build");
        start.ArgumentList.Add(project);
        if (noRestore) start.ArgumentList.Add("--no-restore");
        start.ArgumentList.Add("--disable-build-servers");
        start.ArgumentList.Add("-m:1");
        start.ArgumentList.Add("-v:minimal");
        start.ArgumentList.Add("-p:NuGetAudit=false");
        start.Environment["DOTNET_CLI_USE_MSBUILD_SERVER"] = "0";
        using var process = Process.Start(start)!;
        var stdout = process.StandardOutput.ReadToEndAsync();
        var stderr = process.StandardError.ReadToEndAsync();
        process.WaitForExit();
        return (process.ExitCode, stdout.GetAwaiter().GetResult() + stderr.GetAwaiter().GetResult());
    }

    private sealed class Fixture : IDisposable
    {
        public string Root { get; } = Path.Combine(Path.GetTempPath(), "salep-cli-" + Guid.NewGuid().ToString("N"));
        public Fixture() => Directory.CreateDirectory(Root);
        public void WriteInputs()
        {
            File.WriteAllText(Path.Combine(Root, "schema.graphql"), "type Query { hello: String! bye: String! }");
            var operations = Path.Combine(Root, "graphql");
            Directory.CreateDirectory(operations);
            File.WriteAllText(Path.Combine(operations, "hello.graphql"), "query Hello { hello }");
        }
        public void WriteConfig(string contents) => File.WriteAllText(Path.Combine(Root, "salep.json"), contents);
        public void Dispose() => Directory.Delete(Root, true);
    }
}
