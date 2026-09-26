using System.Text.Json;
using Salep.ClientGenerator.Config;
using Salep.ClientGenerator.Generation;
using Xunit;

namespace Salep.ClientGenerator.Tests;

public sealed class GenerationDedupTests
{
    [Fact]
    public void OmittingAnAncestorsCustomScalarMappingIsARepresentationConflict()
    {
        using var fixture = new ContractFixture(); var a = fixture.Client("A"); var b = fixture.Client("B", a);
        ContractFixture.Change(a, "scalars", new { Money = new { type = "decimal", isValueType = true } });
        ContractFixture.Generate(a);
        var error = Assert.Throws<ConfigurationException>(() => ContractFixture.Generate(b));
        Assert.Equal("scalars.Money", error.Diagnostic.Property);
        Assert.Equal("SALEP2004", error.Diagnostic.Code);
        Assert.False(Directory.Exists(ContractFixture.Output(b)));
    }

    [Fact]
    public void ARelocatedCheckoutCanRegenerateItsOwnOutput()
    {
        using var fixture = new ContractFixture(); var client = fixture.Client(); ContractFixture.Generate(client);
        var folder = Path.GetDirectoryName(client)!;
        var destination = folder + "Relocated"; Directory.Move(folder, destination);
        var relocated = Path.Combine(destination, "salep.json"); ContractFixture.Generate(relocated);
        Assert.Equal(relocated, ContractFixture.Manifest(relocated).Configuration);
    }

    [Fact]
    public void NewOutputsCannotOverwriteUnownedFiles()
    {
        using var fixture = new ContractFixture(); var client = fixture.Client(); ContractFixture.Generate(client);
        var path = Path.Combine(ContractFixture.Output(client), "Operations.Sample.cs"); File.WriteAllText(path, "// developer owned");
        ContractFixture.Change(client, "emitSample", true);
        Assert.Equal("SALEP2003", Assert.Throws<ConfigurationException>(() => ContractFixture.Generate(client)).Diagnostic.Code);
        Assert.Equal("// developer owned", File.ReadAllText(path));
    }

    [Fact]
    public void QualifiedSymbolCollisionsAreRejectedBeforeWriting()
    {
        using var fixture = new ContractFixture(); var a = fixture.Client("A"); var b = fixture.Client("B", a);
        ContractFixture.Generate(a); ContractFixture.Change(b, "namespace", "Generated.A");
        Assert.Equal("SALEP2004", Assert.Throws<ConfigurationException>(() => ContractFixture.Generate(b)).Diagnostic.Code);
        Assert.False(Directory.Exists(ContractFixture.Output(b)));
        Assert.Contains("symbol:Generated.A.UnionJsonConverters", ContractFixture.Manifest(a).Symbols.Keys);
    }

    [Fact]
    public void TransportOnlyTestsDoNotRequireUnusedScalarSamples()
    {
        using var fixture = new ContractFixture(); File.WriteAllText(fixture.Schema, "scalar Money type Query { price: Money }");
        var client = fixture.Client(); ContractFixture.Change(client, "scalars", new { Money = new { type = "decimal", isValueType = true } });
        ContractFixture.Generate(client);
        ContractFixture.Generate(fixture.Tests(client, new { suites = new[] { "transport" } }));
    }

    [Fact]
    public void OperationFreeAncestorsOwnActuallyEmittedTypesAndInterface()
    {
        using var fixture = new ContractFixture(); var parent = fixture.Client("Base"); var child = fixture.Client("Child", parent, "query GetUser { user { id } }");
        ContractFixture.Generate(parent); ContractFixture.Generate(child);
        Assert.True(File.Exists(Path.Combine(ContractFixture.Output(parent), "GraphQLSharedTypes.cs")));
        Assert.Contains("shared:IGraphQLOperation", ContractFixture.Manifest(parent).Symbols.Keys);
        Assert.False(File.Exists(Path.Combine(ContractFixture.Output(child), "SchemaTypes.cs")));
        Assert.False(File.Exists(Path.Combine(ContractFixture.Output(child), "GraphQLSharedTypes.cs")));
        Assert.Contains("global::Generated.Base.IGraphQLOperation", File.ReadAllText(Path.Combine(ContractFixture.Output(child), "Operations.cs")));
    }

    [Theory]
    [InlineData("missing")]
    [InlineData("invalid")]
    [InlineData("null")]
    [InlineData("changed-file")]
    [InlineData("missing-file")]
    [InlineData("changed-config")]
    [InlineData("changed-schema")]
    public void MissingInvalidOrStaleContractsFailBeforeChildWrites(string scenario)
    {
        using var fixture = new ContractFixture(); var parent = fixture.Client("Base"); var child = fixture.Client("Child", parent);
        ContractFixture.Generate(parent); var output = ContractFixture.Output(parent); var manifest = Path.Combine(output, GenerationManifest.FileName);
        switch (scenario)
        {
            case "missing": File.Delete(manifest); break;
            case "invalid": File.WriteAllText(manifest, "{invalid"); break;
            case "null": File.WriteAllText(manifest, "null"); break;
            case "changed-file": File.AppendAllText(Path.Combine(output, "SchemaTypes.cs"), "//edited"); break;
            case "missing-file": File.Delete(Path.Combine(output, "SchemaTypes.cs")); break;
            case "changed-config": ContractFixture.Change(parent, "useHttpGet", true); break;
            case "changed-schema": File.AppendAllText(fixture.Schema, "\ntype Other { id: ID }"); break;
        }
        var error = Assert.Throws<ConfigurationException>(() => ContractFixture.Generate(child));
        Assert.Contains(error.Diagnostic.Code, new[] { "SALEP2001", "SALEP2002" });
        Assert.False(Directory.Exists(ContractFixture.Output(child)));
    }

    [Theory]
    [InlineData("")]
    [InlineData("/nested")]
    public void OverlappingOutputsNeverDeleteAncestorFiles(string suffix)
    {
        using var fixture = new ContractFixture(); var parent = fixture.Client("Base"); ContractFixture.Generate(parent);
        var before = File.ReadAllBytes(Path.Combine(ContractFixture.Output(parent), "SchemaTypes.cs"));
        var child = fixture.Client("Child", parent); ContractFixture.Change(child, "output", ContractFixture.Output(parent) + suffix);
        Assert.Equal("SALEP2003", Assert.Throws<ConfigurationException>(() => ContractFixture.Generate(child)).Diagnostic.Code);
        Assert.Equal(before, File.ReadAllBytes(Path.Combine(ContractFixture.Output(parent), "SchemaTypes.cs")));
    }

    [Fact]
    public void AnUnrelatedConfigurationCannotTakeOverAnOutputDirectory()
    {
        using var fixture = new ContractFixture(); var a = fixture.Client("A"); var b = fixture.Client("B"); ContractFixture.Generate(a);
        ContractFixture.Change(b, "output", ContractFixture.Output(a));
        Assert.Equal("SALEP2003", Assert.Throws<ConfigurationException>(() => ContractFixture.Generate(b)).Diagnostic.Code);
    }

    [Fact]
    public void IdenticalOperationsAreDeduplicatedButConflictingOperationsFail()
    {
        using var fixture = new ContractFixture(); var a = fixture.Client("A", operations: "query GetUser { user { id } }");
        var b = fixture.Client("B", a, "query GetUser { user { id } }");
        ContractFixture.Generate(a); ContractFixture.Generate(b);
        Assert.DoesNotContain("GetUserOperation", File.ReadAllText(Path.Combine(ContractFixture.Output(b), "Operations.cs")));
        File.WriteAllText(Path.Combine(Path.GetDirectoryName(b)!, "graphql/query.graphql"), "query GetUser { user { __typename } }");
        Assert.Equal("SALEP2004", Assert.Throws<ConfigurationException>(() => ContractFixture.Generate(b)).Diagnostic.Code);
    }

    [Fact]
    public void SharedSchemaAndRepresentationConflictsAreRejected()
    {
        using var fixture = new ContractFixture(); var a = fixture.Client("A"); var b = fixture.Client("B", a, native: true); ContractFixture.Generate(a);
        Assert.Equal("SALEP2004", Assert.Throws<ConfigurationException>(() => ContractFixture.Generate(b)).Diagnostic.Code);
        ContractFixture.Change(b, "unionRepresentation", "dunet");
        var alternate = Path.Combine(fixture.Root, "alternate.graphql"); File.WriteAllText(alternate, "type User { id: Int! } type Query { user: User }");
        ContractFixture.Change(b, "schema", alternate);
        Assert.Equal("SALEP2004", Assert.Throws<ConfigurationException>(() => ContractFixture.Generate(b)).Diagnostic.Code);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void TestOnlyUnionCoverageUsesTheReferencedRepresentationAndPreservesClient(bool native)
    {
        using var fixture = new ContractFixture(); File.WriteAllText(fixture.Schema, "type Cat { id: ID! } type Dog { id: ID! } union Pet = Cat | Dog type Query { pet: Pet }");
        var a = fixture.Client("A", native: native); var b = fixture.Client("B", a, native: native); ContractFixture.Generate(a); ContractFixture.Generate(b);
        var manifest = File.ReadAllBytes(Path.Combine(ContractFixture.Output(b), GenerationManifest.FileName));
        var t = fixture.Tests(b, new { suites = new[] { "unions" } }); ContractFixture.Generate(t);
        var source = File.ReadAllText(Path.Combine(ContractFixture.Output(t), "UnionConverterTests.cs"));
        Assert.Equal(native, source.Contains("result.Value is Cat", StringComparison.Ordinal));
        Assert.Equal(!native, source.Contains("ShouldBeOfType<Pet.Cat>", StringComparison.Ordinal));
        Assert.Contains("global::Generated.A.Pet", source);
        Assert.Equal(manifest, File.ReadAllBytes(Path.Combine(ContractFixture.Output(b), GenerationManifest.FileName)));
    }

    [Fact]
    public async Task RepeatedAndConcurrentGenerationIsDeterministicAndOnlyRemovesOwnedFiles()
    {
        using var fixture = new ContractFixture(); var client = fixture.Client(); ContractFixture.Change(client, "emitSample", true); ContractFixture.Generate(client);
        var original = ContractFixture.Manifest(client).Fingerprint;
        await Task.WhenAll(Enumerable.Range(0, 4).Select(_ => Task.Run(() => ContractFixture.Generate(client), TestContext.Current.CancellationToken)));
        Assert.Equal(original, ContractFixture.Manifest(client).Fingerprint);
        var unowned = Path.Combine(ContractFixture.Output(client), "notes.txt"); File.WriteAllText(unowned, "preserve");
        ContractFixture.Change(client, "emitSample", false); ContractFixture.Generate(client);
        Assert.False(File.Exists(Path.Combine(ContractFixture.Output(client), "Operations.Sample.cs")));
        Assert.Equal("preserve", File.ReadAllText(unowned));
    }

    [Fact]
    public void TestSuiteSelectionAndMissingSamplesAreValidated()
    {
        using var fixture = new ContractFixture(); var client = fixture.Client(); ContractFixture.Generate(client);
        var tests = fixture.Tests(client, new { suites = new[] { "samples" } });
        Assert.Equal("SALEP1005", Assert.Throws<ConfigurationException>(() => ContractFixture.Generate(tests)).Diagnostic.Code);
        ContractFixture.Change(tests, "suites", new[] { "operations" }); ContractFixture.Generate(tests);
        Assert.True(File.Exists(Path.Combine(ContractFixture.Output(tests), "OperationsMetadataTests.cs")));
        Assert.False(File.Exists(Path.Combine(ContractFixture.Output(tests), "UnionConverterTests.cs")));
    }
}
