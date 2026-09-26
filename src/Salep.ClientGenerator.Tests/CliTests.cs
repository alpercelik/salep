using Salep.ClientGenerator.Cli;
using Salep.ClientGenerator.Config;
using Salep.ClientGenerator.Generation;
using Xunit;

namespace Salep.ClientGenerator.Tests;

public sealed class CliTests
{
    [Theory]
    [InlineData("--schema")]
    [InlineData("--operations")]
    [InlineData("--output")]
    [InlineData("--tests-output")]
    [InlineData("--generate-client")]
    [InlineData("--generate-tests")]
    [InlineData("--emit-agent-instructions")]
    [InlineData("--update-project-packages")]
    [InlineData("--no-update-project-packages")]
    [InlineData("--unknown")]
    public void RemovedOverridesAreRejected(string flag)
    {
        var result = SalepCliParser.Parse([flag]); Assert.False(result.IsSuccess);
        Assert.Contains($"Unrecognized argument '{flag}'.", result.Errors);
    }
    [Theory]
    [InlineData("--config=salep.json")]
    [InlineData("-c:salep.json")]
    public void InlineConfigSelectionIsSupported(string arg)
    { var result = SalepCliParser.Parse([arg]); Assert.True(result.IsSuccess); Assert.Equal("salep.json", result.Options.ConfigPath); }
    [Theory]
    [InlineData("--config")]
    [InlineData("--working-directory")]
    [InlineData("--config=")]
    public void MissingValuesAreRejected(string arg) => Assert.False(SalepCliParser.Parse([arg]).IsSuccess);
    [Theory]
    [InlineData("--help", "Usage:")]
    [InlineData("-h", "Usage:")]
    [InlineData("--version", "")]
    public void HelpAndVersionDoNotRequireConfig(string arg, string expected)
    { using var stdout = new StringWriter(); using var stderr = new StringWriter(); Assert.Equal(0, Program.Run([arg], stdout, stderr)); Assert.Contains(expected, stdout.ToString()); Assert.Empty(stderr.ToString()); }
    [Fact]
    public void ConfiglessGenerationFailsWithStructuredDiagnostic()
    {
        using var fixture = new ContractFixture(); using var error = new StringWriter();
        Assert.Equal(1, Program.Run(["--working-directory", fixture.Root], new StringWriter(), error));
        Assert.Contains("SALEP1001", error.ToString());
    }
    [Fact]
    public void ValidateDoesNotCreateOutputAndGenerationWritesAnExactSourceList()
    {
        using var fixture = new ContractFixture(); var config = fixture.Client(); using var error = new StringWriter(); using var output = new StringWriter();
        Assert.Equal(0, Program.Run(["validate", "--config", config], output, error)); Assert.False(Directory.Exists(ContractFixture.Output(config)));
        var list = Path.Combine(fixture.Root, "sources.txt");
        Assert.Equal(0, Program.Run(["generate", "--config", config, "--sources-file", list], output, error));
        Assert.All(File.ReadAllLines(list), path => { Assert.EndsWith(".cs", path); Assert.True(File.Exists(path)); });
        Assert.Equal(ContractFixture.Manifest(config).Files.Keys.Count(name => name.EndsWith(".cs", StringComparison.Ordinal)), File.ReadAllLines(list).Length);
        Assert.False(SalepCliParser.Parse(["validate", "--sources-file", list]).IsSuccess);
    }
    [Fact]
    public void BuildContextChecksNativeCompilerAndProjectReferenceChain()
    {
        using var fixture = new ContractFixture(); var config = fixture.Client(native: true);
        Assert.Equal("SALEP3001", Assert.Throws<ConfigurationException>(() => SalepGenerator.Generate(new(config, Environment: new("net10.0", "latest", [])))).Diagnostic.Code);
        SalepGenerator.Generate(new(config, Environment: new("net11.0", "preview", [])));
        var tests = fixture.Tests(config);
        Assert.Equal("SALEP3002", Assert.Throws<ConfigurationException>(() => SalepGenerator.Generate(new(tests, Environment: new("net11.0", "preview", [])))).Diagnostic.Code);
        SalepGenerator.Generate(new(tests, Environment: new("net11.0", "preview", [config])));
    }
}
