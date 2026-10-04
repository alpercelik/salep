using System.Text.Json;
using Salep.ClientGenerator.Generation;
using Xunit;

namespace Salep.ClientGenerator.Tests;

public sealed class CSharpOperationOwnershipSignatureTests
{
    [Fact]
    public void Identical_inherited_operations_reuse_ownership_across_filenames_and_coordinates()
    {
        using var fixture = new Fixture();
        fixture.WriteOperation("Base", "query Read { alpha }");
        fixture.WriteOperation("Module", "# copied with different coordinates\n\nquery Read {\n alpha\n}", "copied.graphql");
        fixture.GenerateBase();
        var generated = fixture.GenerateModule("copied.graphql");
        Assert.DoesNotContain("ReadOperation", fixture.GeneratedSource(generated));
    }

    [Theory]
    [InlineData("query Read { alpha }", "query Read { bravo }")]
    [InlineData("query Read($n: Int = 1) { alpha(flag: $n) }", "query Read($n: Int = 2) { alpha(flag: $n) }")]
    [InlineData("query Read { alpha(flag: 1) }", "query Read { alpha(flag: 2) }")]
    public void Equal_length_semantic_changes_conflict_with_the_inherited_operation(string original, string changed)
    {
        using var fixture = new Fixture();
        fixture.WriteOperation("Base", original);
        fixture.WriteOperation("Module", changed);
        fixture.GenerateBase();
        var error = Assert.Throws<ScribanConfigurationException>(() => fixture.GenerateModule());
        Assert.Equal("SALEPS2004", error.Diagnostic.Code);
        Assert.Equal("operations.Read", error.Diagnostic.Property);
        Assert.False(Directory.Exists(fixture.ModuleOutput));
    }

    [Fact]
    public void Reachable_fragments_are_canonicalized_by_name_and_unused_fragments_are_excluded()
    {
        using var fixture = new Fixture();
        fixture.WriteOperation("Base", "query Read { ...A } fragment A on Query { ...B } fragment B on Query { alpha } fragment Unused on Query { alpha }");
        fixture.WriteOperation("Module", "fragment Unused on Query { bravo } fragment B on Query { alpha } fragment A on Query { ...B } query Read { ...A }");
        fixture.GenerateBase();
        var generated = fixture.GenerateModule();
        Assert.DoesNotContain("ReadOperation", fixture.GeneratedSource(generated));
    }

    [Fact]
    public void Changing_a_transitively_reachable_fragment_conflicts_with_inherited_ownership()
    {
        using var fixture = new Fixture();
        fixture.WriteOperation("Base", "query Read { ...A } fragment A on Query { ...B } fragment B on Query { alpha }");
        fixture.WriteOperation("Module", "query Read { ...A } fragment A on Query { ...B } fragment B on Query { bravo }");
        fixture.GenerateBase();
        var error = Assert.Throws<ScribanConfigurationException>(() => fixture.GenerateModule());
        Assert.Equal("SALEPS2004", error.Diagnostic.Code);
        Assert.False(Directory.Exists(fixture.ModuleOutput));
    }

    private sealed class Fixture : IDisposable
    {
        private string Root { get; } = Path.Combine(Path.GetTempPath(), "salep-operation-ownership-" + Guid.NewGuid().ToString("N"));
        public string ModuleOutput => Path.Combine(Root, "Module", "Generated");
        public Fixture()
        {
            Directory.CreateDirectory(Root);
            File.WriteAllText(Path.Combine(Root, "fixture.slnx"), "<Solution />");
            File.WriteAllText(Path.Combine(Root, "schema.graphql"), "type Query { alpha(flag: Int): String bravo: String }");
            Directory.CreateDirectory(Path.Combine(Root, "Base"));
            Directory.CreateDirectory(Path.Combine(Root, "Module"));
            WriteConfig("Base");
        }
        public void WriteOperation(string client, string source, string name = "operation.graphql")
            => File.WriteAllText(Path.Combine(Root, client, name), source);
        public void GenerateBase() => ScribanGenerator.Generate(new(Path.Combine(Root, "Base", "salep.json")));
        public ScribanGeneratorResult GenerateModule(string operation = "operation.graphql")
        {
            WriteConfig("Module", operation);
            return ScribanGenerator.Generate(new(Path.Combine(Root, "Module", "salep.json")));
        }
        public string GeneratedSource(ScribanGeneratorResult result)
            => string.Join("\n", result.GeneratedFiles.Where(file => file.EndsWith(".cs", StringComparison.Ordinal)).Select(File.ReadAllText));
        private void WriteConfig(string client, string operation = "operation.graphql")
        {
            var config = new Dictionary<string, object?> { ["version"] = 1, ["kind"] = "client", ["schema"] = "../schema.graphql", ["operations"] = operation, ["namespace"] = "Ownership." + client, ["output"] = "Generated", ["unionRepresentation"] = "dunet" };
            if (client == "Module") config["baseClient"] = "../Base/salep.json";
            File.WriteAllText(Path.Combine(Root, client, "salep.json"), JsonSerializer.Serialize(config));
        }
        public void Dispose() => Directory.Delete(Root, true);
    }
}
