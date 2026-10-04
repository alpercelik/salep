using System.Text.Json;
using Salep.ClientGenerator.TypeScript.Generation;
using Xunit;

namespace Salep.ClientGenerator.Tests;

public sealed class TypeScriptPartitionAndOwnershipRegressionTests
{
    [Fact]
    public void Partition_documents_import_runtime_type_and_avoid_barrel_export_collisions()
    {
        InFixture(root =>
        {
            File.WriteAllText(Path.Combine(root, "schema.graphql"), "enum ResolverFunction { VALUE } type Query { value: ResolverFunction }");
            var config = WriteConfig(root, "schema.graphql", "per-operation");
            TypeScriptGenerator.Generate(new(config));
            var operation = File.ReadAllText(Path.Combine(root, "Generated", "gql", "operations", "operation-Read.ts"));
            Assert.Contains("import type { GqlResolverFunction, TypedDocument } from '../shared.js';", operation);
            Assert.Contains("as unknown as TypedDocument<ReadResult, ReadVariables>", operation);
            var shared = File.ReadAllText(Path.Combine(root, "Generated", "gql", "shared.ts"));
            Assert.Contains("export type GqlResolverFunction =", shared);
            Assert.DoesNotContain("export type ResolverFunction =", shared);
        });
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void Case_aliased_schema_output_is_rejected_without_changing_inputs_or_inventory(bool validateOnly)
    {
        InFixture(root =>
        {
            var config = WriteConfig(root, "schema.graphql");
            TypeScriptGenerator.Generate(new(config));
            var alias = Path.Combine(root, "Generated", "Schema.graphql");
            File.WriteAllText(alias, File.ReadAllText(Path.Combine(root, "Generated", "schema.graphql")));
            File.WriteAllText(Path.Combine(root, "extra.graphql"), "enum Extra { VALUE }");
            WriteConfig(root, new[] { "Generated/Schema.graphql", "extra.graphql" });
            AssertPreservedOnOverlap(root, config, validateOnly);
        });
    }

    [Theory]
    [InlineData(".SALEP.MANIFEST.JSON")]
    [InlineData(".SALEP.LOCK")]
    public void Manifest_and_lock_aliases_cannot_be_template_inputs(string alias)
    {
        InFixture(root =>
        {
            var output = Path.Combine(root, "Generated");
            Directory.CreateDirectory(output);
            File.WriteAllText(Path.Combine(output, alias), "// template input");
            var config = WriteConfig(root, "schema.graphql", templates: new Dictionary<string, string> { ["client"] = "Generated/" + alias });
            AssertPreservedOnOverlap(root, config);
        });
    }

    [Theory]
    [InlineData("gql/shared.ts")]
    [InlineData("gql/SHARED.ts")]
    public void Previous_partition_outputs_cannot_become_inputs_when_switching_layout(string templatePath)
    {
        InFixture(root =>
        {
            var config = WriteConfig(root, "schema.graphql", "per-operation");
            TypeScriptGenerator.Generate(new(config));
            var alias = Path.Combine(root, "Generated", templatePath);
            File.WriteAllText(alias, File.ReadAllText(Path.Combine(root, "Generated", "gql", "shared.ts")));
            WriteConfig(root, "schema.graphql", templates: new Dictionary<string, string> { ["client"] = "Generated/" + templatePath });
            AssertPreservedOnOverlap(root, config);
        });
    }

    private static void AssertPreservedOnOverlap(string root, string config, bool validateOnly = false)
        => AssertPreservedOnOverlap(root, new TypeScriptGeneratorOptions(config), validateOnly);

    [Theory]
    [InlineData(false, false)]
    [InlineData(false, true)]
    [InlineData(true, false)]
    [InlineData(true, true)]
    public void Allowed_read_root_with_ancestor_alias_cannot_read_current_or_stale_outputs(bool stale, bool validateOnly)
    {
        InFixture(root =>
        {
            var aliasParent = Path.Combine(Path.GetTempPath(), "salep-ownership-alias-" + Guid.NewGuid().ToString("N"));
            try
            {
                try { Directory.CreateSymbolicLink(aliasParent, Path.GetDirectoryName(root)!); }
                catch (Exception error) when (error is UnauthorizedAccessException or PlatformNotSupportedException or IOException) { return; }
                // The selected read root is a regular directory; the alias is above it.
                var aliasRoot = Path.Combine(aliasParent, Path.GetFileName(root));
                var config = WriteConfig(root, "schema.graphql", stale ? "per-operation" : "combined");
                TypeScriptGenerator.Generate(new(config));
                var ownedName = stale ? "gql/shared.ts" : "gql/graphql.ts";
                var template = Path.Combine(aliasRoot, "Generated", ownedName);
                Assert.Equal(File.ReadAllBytes(Path.Combine(root, "Generated", ownedName)), File.ReadAllBytes(template));
                WriteConfig(root, "schema.graphql", templates: new Dictionary<string, string> { ["client"] = template });
                AssertPreservedOnOverlap(root, new TypeScriptGeneratorOptions(config, root, [aliasRoot]), validateOnly);
            }
            finally { if (new DirectoryInfo(aliasParent).LinkTarget is not null) Directory.Delete(aliasParent); }
        });
    }

    private static void AssertPreservedOnOverlap(string root, TypeScriptGeneratorOptions options, bool validateOnly)
    {
        var before = Directory.EnumerateFiles(root, "*", SearchOption.AllDirectories).ToDictionary(file => file, File.ReadAllBytes, StringComparer.Ordinal);
        var error = Assert.Throws<InvalidDataException>(() => TypeScriptGenerator.Generate(options, validateOnly));
        Assert.Contains("inputs cannot overlap", error.Message);
        Assert.Equal(before.Keys.Order(StringComparer.Ordinal), Directory.EnumerateFiles(root, "*", SearchOption.AllDirectories).Order(StringComparer.Ordinal));
        foreach (var file in before) Assert.Equal(file.Value, File.ReadAllBytes(file.Key));
    }

    private static string WriteConfig(string root, object schema, string layout = "combined", Dictionary<string, string>? templates = null)
    {
        var config = Path.Combine(root, "salep.json");
        var settings = new Dictionary<string, object> { ["version"] = 1, ["target"] = "typescript", ["schema"] = schema, ["documents"] = "operation.graphql", ["outputLayout"] = layout };
        if (templates is not null) settings["templates"] = templates;
        File.WriteAllText(config, JsonSerializer.Serialize(settings));
        return config;
    }

    private static void InFixture(Action<string> action)
    {
        var root = Path.Combine(Path.GetTempPath(), "salep-partition-ownership-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(root);
        try
        {
            File.WriteAllText(Path.Combine(root, "schema.graphql"), "type Query { value: String }");
            File.WriteAllText(Path.Combine(root, "operation.graphql"), "query Read { value }");
            action(root);
        }
        finally { Directory.Delete(root, true); }
    }
}
