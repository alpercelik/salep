using System.Text.Json;
using Salep.ClientGenerator.Config;
using Salep.ClientGenerator.Generation;
using Xunit;

namespace Salep.ClientGenerator.Tests;

public sealed class ConfigAndManifestTests
{
    [Theory]
    [InlineData("{}")]
    [InlineData("{\"version\":2,\"kind\":\"client\"}")]
    [InlineData("{\"version\":\"1\",\"kind\":\"client\"}")]
    [InlineData("{\"version\":1,\"kind\":\"unknown\"}")]
    [InlineData("{\"version\":1,\"kind\":\"profile\",\"version\":1}")]
    [InlineData("{\"version\":1,\"kind\":\"profile\",\"output\":\"x\"}")]
    [InlineData("{\"version\":1,\"kind\":\"profile\",\"useNativeUnions\":true}")]
    [InlineData("{\"version\":1,\"kind\":\"profile\",\"maxLineWidth\":120}")]
    [InlineData("{\"version\":1,\"kind\":\"profile\",\"indentSize\":-1}")]
    [InlineData("{\"version\":1,\"kind\":\"profile\",\"maxGetUrlLength\":0}")]
    [InlineData("{\"version\":1,\"kind\":\"profile\",\"unionRepresentation\":\"bad\"}")]
    [InlineData("{\"version\":1,\"kind\":\"profile\",\"scalarPreset\":\"bad\"}")]
    [InlineData("{\"version\":1,\"kind\":\"profile\",\"scalars\":{\"Money\":{\"type\":\"decimal\"}}}")]
    public void InvalidConfigurationHasStructuredErrorAndDoesNotWrite(string json)
    {
        using var fixture = new ContractFixture();
        var path = Path.Combine(fixture.Root, "salep.json"); File.WriteAllText(path, json);
        var error = Assert.Throws<ConfigurationException>(() => SalepGenerator.Generate(new(path)));
        Assert.StartsWith("SALEP1", error.Diagnostic.Code);
        Assert.Equal(path, error.Diagnostic.ConfigurationPath);
        Assert.False(Directory.Exists(Path.Combine(fixture.Root, "Generated")));
    }

    [Theory]
    [InlineData("unionRepresentation", "native")]
    [InlineData("unionRepresentation", "dunet")]
    [InlineData("useHttpGet", true)]
    [InlineData("useHttpGet", false)]
    [InlineData("enableBatching", true)]
    [InlineData("omitUnusedVariables", true)]
    [InlineData("inlineDefaultVariables", true)]
    [InlineData("clientName", "Other")]
    [InlineData("generatedNamespace", "Other")]
    [InlineData("scalarPreset", "builtin")]
    [InlineData("scalars", "invalid")]
    public void TestsCannotRedefineClientBehavior(string property, object value)
    {
        using var fixture = new ContractFixture(); var client = fixture.Client(native: property == "unionRepresentation" && Equals(value, "dunet"));
        ContractFixture.Generate(client);
        var tests = fixture.Tests(client); ContractFixture.Change(tests, property, value);
        var before = File.ReadAllBytes(Path.Combine(ContractFixture.Output(client), GenerationManifest.FileName));
        var error = Assert.Throws<ConfigurationException>(() => ContractFixture.Generate(tests));
        Assert.Equal("SALEP1003", error.Diagnostic.Code); Assert.Equal(property, error.Diagnostic.Property);
        Assert.Contains(client, error.Diagnostic.Guidance);
        Assert.Equal(before, File.ReadAllBytes(Path.Combine(ContractFixture.Output(client), GenerationManifest.FileName)));
    }

    [Fact]
    public void ProfilesResolvePathsAtDeclarationAndReplaceScalarDefinitions()
    {
        using var fixture = new ContractFixture();
        var parent = fixture.Write("Profile", new { version = 1, kind = "profile", schema = "../schema.graphql", scalarPreset = "nodatime", useHttpGet = true });
        var profile = fixture.Write("ChildProfile", new { version = 1, kind = "profile", extends = parent, scalars = new { DateTime = new { type = "DateTimeOffset", isValueType = true } } });
        var client = fixture.Write("Client", new { version = 1, kind = "client", profile, useHttpGet = false });
        var resolved = ConfigurationResolver.Resolve(client);
        Assert.Equal(fixture.Schema, resolved.Schema); Assert.False(resolved.Settings.UseHttpGet);
        Assert.Equal("DateTimeOffset", resolved.Settings.Scalars["DateTime"].Type);
        Assert.Null(resolved.Settings.Scalars["DateTime"].SampleExpression);
        Assert.Equal("NodaTime.Instant", resolved.Settings.Scalars["Instant"].Type);
        ContractFixture.Generate(client);
        var tests = fixture.Tests(client); // No DateTime field is used, so incomplete unused scalar fixtures are allowed.
        ContractFixture.Generate(tests);
    }

    [Fact]
    public void CyclesAndWrongRoleReferencesFail()
    {
        using var fixture = new ContractFixture(); var client = fixture.Client();
        ContractFixture.Change(client, "baseClient", client);
        Assert.Equal("SALEP1002", Assert.Throws<ConfigurationException>(() => ConfigurationResolver.Resolve(client)).Diagnostic.Code);
        var profile = fixture.Write("Profile", new { version = 1, kind = "profile" });
        ContractFixture.Change(client, "baseClient", profile);
        Assert.Equal("SALEP1004", Assert.Throws<ConfigurationException>(() => ConfigurationResolver.Resolve(client)).Diagnostic.Code);
        ContractFixture.Change(profile, "extends", profile);
        Assert.Equal("SALEP1002", Assert.Throws<ConfigurationException>(() => ConfigurationResolver.Resolve(profile)).Diagnostic.Code);
    }

    [Theory]
    [InlineData("type Query { name: String }", false)]
    [InlineData("interface Node { id: ID! } type Query { node: Node }", false)]
    [InlineData("type User { id: ID! } union Result = User type Query { result: Result }", true)]
    [InlineData("interface Node { id: ID! } type User implements Node { id: ID! } type Query { node: Node }", true)]
    public void DunetIsImportedOnlyForEmittedUnionCases(string schema, bool expected)
    {
        using var fixture = new ContractFixture(); File.WriteAllText(fixture.Schema, schema);
        var client = fixture.Client(); ContractFixture.Generate(client);
        Assert.Equal(expected, File.ReadAllText(Path.Combine(ContractFixture.Output(client), "SchemaTypes.cs")).Contains("using Dunet;", StringComparison.Ordinal));
    }

    [Fact]
    public void NativeUnionsAndAgentInstructionsUseTheClientContract()
    {
        using var fixture = new ContractFixture(); File.WriteAllText(fixture.Schema, "type Cat { id: ID! } type Dog { id: ID! } union Pet = Cat | Dog type Query { pet: Pet }");
        var client = fixture.Client(native: true); ContractFixture.Change(client, "emitAgentInstructions", true);
        ContractFixture.Generate(client); var source = File.ReadAllText(Path.Combine(ContractFixture.Output(client), "SchemaTypes.cs"));
        Assert.Contains("union Pet", source); Assert.DoesNotContain("Dunet", source);
        Assert.Contains("C# 15", File.ReadAllText(Path.Combine(ContractFixture.Output(client), "agents.md")));
        Assert.Equal("preview", ContractFixture.Manifest(client).RequiredLanguage);
    }

    [Fact]
    public void MissingSampleDataIsReportedBeforeFilesChange()
    {
        using var fixture = new ContractFixture(); File.WriteAllText(fixture.Schema, "scalar Money type Query { price: Money }");
        var client = fixture.Client(operations: "query Price { price }");
        ContractFixture.Change(client, "scalars", new { Money = new { type = "decimal", isValueType = true } });
        ContractFixture.Generate(client);
        var tests = fixture.Tests(client);
        Assert.Equal("SALEP1005", Assert.Throws<ConfigurationException>(() => ContractFixture.Generate(tests)).Diagnostic.Code);
        Assert.False(Directory.Exists(ContractFixture.Output(tests)));
    }

    [Fact]
    public void ConfigPathSelectionUsesWorkingDirectoryAndRequiresAnExistingFile()
    {
        using var fixture = new ContractFixture(); var client = fixture.Client();
        Assert.Equal(client, GeneratorConfig.ResolveConfigPath(Path.GetDirectoryName(client), fixture.Root));
        Assert.Equal(Path.Combine(fixture.Root, "salep.json"), GeneratorConfig.ResolveConfigPath(null, fixture.Root));
        Assert.Throws<ConfigurationException>(() => ConfigurationResolver.Resolve(Path.Combine(fixture.Root, "missing.json")));
    }
}
