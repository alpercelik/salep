using System.Diagnostics;
using Salep.ClientGenerator.Cli;
using Shouldly;
using Xunit;

namespace Salep.ClientGenerator.MSBuild.Tests;

public class IncrementalBuildTests
{
    private const string TestSchema = @"
type Query {
  user(id: ID!): User
}

type User {
  id: ID!
  name: String!
}
";

    private const string TestOperation = @"
query GetUser($id: ID!) {
  user(id: $id) {
    id
    name
  }
}
";

    [Fact]
    public void MSBuild_Targets_Execute_Cli_And_Support_Incremental_Builds()
    {
        var repoRoot = FindRepoRoot();
        var msbuildProps = Path.Combine(repoRoot, "src", "Salep.ClientGenerator.MSBuild", "Salep.ClientGenerator.props");
        var msbuildTargets = Path.Combine(repoRoot, "src", "Salep.ClientGenerator.MSBuild", "Salep.ClientGenerator.targets");
        var cliBinDir = Path.GetDirectoryName(typeof(SalepCliParser).Assembly.Location)!;

        // Ensure Salep.ClientGenerator.Cli is built
        if (!File.Exists(Path.Combine(cliBinDir, "Salep.ClientGenerator.Cli.dll")))
        {
            var buildCli = Process.Start(new ProcessStartInfo("dotnet")
            {
                Arguments = $"build \"{Path.Combine(repoRoot, "src", "Salep.ClientGenerator.Cli", "Salep.ClientGenerator.Cli.csproj")}\" -c Debug -p:EnforceCodeStyleInBuild=false -p:TreatWarningsAsErrors=false",
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                UseShellExecute = false
            });
            buildCli?.WaitForExit(60000);
        }

        var tempDir = Path.Combine(Path.GetTempPath(), "salep_msbuild_test_" + Guid.NewGuid().ToString("N"));

        try
        {
            Directory.CreateDirectory(tempDir);
            var opsDir = Path.Combine(tempDir, "Operations");
            Directory.CreateDirectory(opsDir);

            File.WriteAllText(Path.Combine(tempDir, "schema.graphql"), TestSchema);
            File.WriteAllText(Path.Combine(opsDir, "GetUser.graphql"), TestOperation);

            var salepJson = @"{
  ""version"": 1,
  ""kind"": ""client"",
  ""schema"": ""schema.graphql"",
  ""operations"": ""Operations"",
  ""output"": ""CustomSources"",
  ""namespace"": ""TestNamespace""
}";
            var configPath = Path.Combine(tempDir, "custom.json");
            File.WriteAllText(Path.Combine(tempDir, "salep.json"), "default config must not override the explicit selection");
            File.WriteAllText(configPath, salepJson);

            // Project file importing Salep.ClientGenerator.props and Salep.ClientGenerator.targets
            var csprojContent = $@"<Project Sdk=""Microsoft.NET.Sdk"">
  <Import Project=""{msbuildProps}"" />
  <PropertyGroup>
    <TargetFramework>net10.0</TargetFramework>
    <SalepToolPath>{cliBinDir}</SalepToolPath>
    <OutputType>Library</OutputType>
  </PropertyGroup>
  <ItemGroup>
    <SalepConfig Include=""custom.json"" />
  </ItemGroup>
  <Import Project=""{msbuildTargets}"" />
</Project>";

            var projFile = Path.Combine(tempDir, "TestProject.csproj");
            File.WriteAllText(projFile, csprojContent);

            // 1. First build - should run SalepGenerate
            var (exitCode1, stdOut1, stdErr1) = RunDotnet($"build \"{projFile}\" -t:SalepGenerate -v:n -p:EnforceCodeStyleInBuild=false -p:TreatWarningsAsErrors=false", tempDir);
            exitCode1.ShouldBe(0, $"Initial build failed:\n{stdOut1}\n{stdErr1}");

            var generatedDir = Path.Combine(tempDir, "CustomSources");
            Directory.Exists(generatedDir).ShouldBeTrue($"Generated directory was not created. Stdout:\n{stdOut1}\nStderr:\n{stdErr1}");
            File.Exists(Path.Combine(generatedDir, "SchemaTypes.cs")).ShouldBeTrue("SchemaTypes.cs was not created");
            File.Exists(Path.Combine(generatedDir, ".salep.manifest.json")).ShouldBeTrue("Manifest was not created");

            var manifestBefore = File.ReadAllText(Path.Combine(generatedDir, ".salep.manifest.json"));
            // 2. Every build invokes generation so missing outputs cannot be hidden by stale timestamps.
            var (exitCode2, stdOut2, stdErr2) = RunDotnet($"build \"{projFile}\" -t:SalepGenerate -p:EnforceCodeStyleInBuild=false -p:TreatWarningsAsErrors=false", tempDir);
            exitCode2.ShouldBe(0, $"Second build failed:\n{stdOut2}\n{stdErr2}");
            File.ReadAllText(Path.Combine(generatedDir, ".salep.manifest.json")).ShouldBe(manifestBefore);

            // 3. Delete one generated output - the next build must restore it.
            var generatedTypes = Path.Combine(generatedDir, "SchemaTypes.cs");
            File.Delete(generatedTypes);

            var (exitCode3, stdOut3, stdErr3) = RunDotnet($"build \"{projFile}\" -t:SalepGenerate -p:EnforceCodeStyleInBuild=false -p:TreatWarningsAsErrors=false", tempDir);
            exitCode3.ShouldBe(0, $"Build after deleting a generated output failed:\n{stdOut3}\n{stdErr3}");
            File.Exists(generatedTypes).ShouldBeTrue($"Deleted generated output was not restored. Stdout:\n{stdOut3}\nStderr:\n{stdErr3}");

            // 4. Modify schema - SalepGenerate should re-run
            File.AppendAllText(Path.Combine(tempDir, "schema.graphql"), "\ntype Extra { id: ID! }\n");

            var (exitCode4, stdOut4, stdErr4) = RunDotnet($"build \"{projFile}\" -t:SalepGenerate -p:EnforceCodeStyleInBuild=false -p:TreatWarningsAsErrors=false", tempDir);
            exitCode4.ShouldBe(0, $"Build after schema modification failed:\n{stdOut4}\n{stdErr4}");

            File.ReadAllText(generatedTypes).ShouldContain("record Extra");

            var (compileExit, compileOutput, compileError) = RunDotnet($"build \"{projFile}\" -p:TreatWarningsAsErrors=true", tempDir);
            compileExit.ShouldBe(0, compileOutput + compileError);
            compileOutput.ShouldNotContain("CS2002");
            var before = File.ReadAllText(generatedTypes);
            File.WriteAllText(projFile, csprojContent.Replace("<SalepConfig Include=\"custom.json\" />", "<SalepConfig Include=\"custom.json\" /><SalepConfig Include=\"another.json\" />", StringComparison.Ordinal));
            var (ambiguousExit, ambiguousOutput, ambiguousError) = RunDotnet($"build \"{projFile}\" -t:SalepGenerate", tempDir);
            ambiguousExit.ShouldNotBe(0);
            (ambiguousOutput + ambiguousError).ShouldContain("SALEP3003");
            File.ReadAllText(generatedTypes).ShouldBe(before);

        }
        finally
        {
            if (Directory.Exists(tempDir))
            {
                try { Directory.Delete(tempDir, true); } catch { }
            }
        }
    }

    [Fact]
    public void OperationFreeBaseAndDerivedClientCompileAsSeparateProjects()
    {
        var root = Path.Combine(Path.GetTempPath(), "salep_separate_contract_" + Guid.NewGuid().ToString("N"));
        var repo = FindRepoRoot();
        var props = Path.Combine(repo, "src/Salep.ClientGenerator.MSBuild/Salep.ClientGenerator.props");
        var targets = Path.Combine(repo, "src/Salep.ClientGenerator.MSBuild/Salep.ClientGenerator.targets");
        var cli = Path.GetDirectoryName(typeof(SalepCliParser).Assembly.Location)!;
        try
        {
            Directory.CreateDirectory(root);
            File.WriteAllText(Path.Combine(root, "schema.graphql"), "type User { id: ID! } type Query { user: User }");
            foreach (var name in new[] { "Base", "Child" })
            {
                var folder = Path.Combine(root, name); Directory.CreateDirectory(folder);
                var config = new System.Text.Json.Nodes.JsonObject
                {
                    ["version"] = 1, ["kind"] = "client", ["schema"] = "../schema.graphql",
                    ["namespace"] = "Generated." + name, ["clientName"] = name + "Api", ["output"] = "./CustomSources"
                };
                if (name == "Child") config["baseClient"] = "../Base/salep.json";
                File.WriteAllText(Path.Combine(folder, "salep.json"), config.ToJsonString());
                var references = name == "Child" ? "<ItemGroup><ProjectReference Include=\"../Base/Base.csproj\" /></ItemGroup>" : "";
                File.WriteAllText(Path.Combine(folder, name + ".csproj"), $"""
                    <Project Sdk="Microsoft.NET.Sdk">
                      <Import Project="{props}" />
                      <PropertyGroup><TargetFramework>net10.0</TargetFramework><Nullable>enable</Nullable><SalepToolPath>{cli}</SalepToolPath><TreatWarningsAsErrors>true</TreatWarningsAsErrors></PropertyGroup>
                      {references}
                      <Import Project="{targets}" />
                    </Project>
                    """);
            }
            Directory.CreateDirectory(Path.Combine(root, "Child/graphql"));
            File.WriteAllText(Path.Combine(root, "Child/graphql/query.graphql"), "query GetUser { user { id } }");
            var (exit, output, error) = RunDotnet("build Child/Child.csproj", root);
            exit.ShouldBe(0, output + error);
            File.Exists(Path.Combine(root, "Base/CustomSources/GraphQLSharedTypes.cs")).ShouldBeTrue();
            File.Exists(Path.Combine(root, "Child/CustomSources/GraphQLSharedTypes.cs")).ShouldBeFalse();
            File.ReadAllText(Path.Combine(root, "Child/CustomSources/Operations.cs")).ShouldContain("global::Generated.Base.IGraphQLOperation");
        }
        finally { if (Directory.Exists(root)) Directory.Delete(root, true); }
    }

    private static (int ExitCode, string StdOut, string StdErr) RunDotnet(string arguments, string workingDirectory)
    {
        var startInfo = new ProcessStartInfo("dotnet")
        {
            Arguments = arguments,
            WorkingDirectory = workingDirectory,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            UseShellExecute = false,
            CreateNoWindow = true
        };

        using var process = Process.Start(startInfo);
        process.ShouldNotBeNull();
        var stdout = process.StandardOutput.ReadToEndAsync();
        var stderr = process.StandardError.ReadToEndAsync();
        process.WaitForExit(60000).ShouldBeTrue("dotnet command timed out");
        return (process.ExitCode, stdout.GetAwaiter().GetResult(), stderr.GetAwaiter().GetResult());
    }

    private static string FindRepoRoot()
    {
        var current = AppContext.BaseDirectory;
        while (current != null && !File.Exists(Path.Combine(current, "src", "Salep.slnx")))
        {
            current = Directory.GetParent(current)?.FullName;
        }

        return current ?? throw new InvalidOperationException("Could not find repository root");
    }
}
