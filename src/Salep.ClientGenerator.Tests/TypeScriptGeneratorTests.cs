using System.Text.Json;
using Salep.ClientGenerator.TypeScript.Generation;
using Salep.ClientGenerator.TypeScript.Templates;
using Xunit;

namespace Salep.ClientGenerator.Tests;

public sealed class TypeScriptGeneratorTests
{
    [Fact]
    public void Fragment_only_documents_deduplicate_and_emit_discriminators_with_schema_export()
    {
        var root = Path.Combine(Path.GetTempPath(), "salep-ts-fragment-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(root);
        try
        {
            const string schema = "type Query { item: Entry } interface Entry { id: ID! } type Book implements Entry { id: ID! title: String! }";
            const string fragment = "fragment Summary on Entry { id ... on Book { title } }";
            File.WriteAllText(Path.Combine(root, "schema.graphql"), schema);
            File.WriteAllText(Path.Combine(root, "one.graphql"), fragment);
            File.WriteAllText(Path.Combine(root, "two.tsx"), "const document = graphql(`" + fragment + "`);");
            var config = Path.Combine(root, "salep.typescript.json");
            File.WriteAllText(config, """{"version":1,"target":"typescript","schema":"schema.graphql","documents":["one.graphql","two.tsx"],"output":"Generated"}""");
            var files = TypeScriptGenerator.Generate(new(config));
            var source = File.ReadAllText(Assert.Single(files, file => file.EndsWith("/gql/graphql.ts", StringComparison.Ordinal)));
            Assert.Contains("SummaryFragmentDocument", source);
            Assert.Contains("\"value\":\"__typename\"", source);
            Assert.Equal(schema, File.ReadAllText(Path.Combine(root, "Generated", "schema.graphql")));
            Assert.Equal(7, files.Count);
        }
        finally { Directory.Delete(root, true); }
    }

    [Fact]
    public void Template_overrides_compose_defaults_and_reject_filesystem_includes()
    {
        var catalog = new TypeScriptTemplateCatalog(new Dictionary<string, string>
        {
            ["property"] = "// custom property\n{{ include 'default:property' }}"
        });
        var rendered = catalog.Render("property", new { name = "\"value\"", optional = "?", type = "string" });
        Assert.Contains("// custom property", rendered);
        Assert.Contains("string", rendered);
        Assert.All(TypeScriptTemplateCatalog.Files.Keys, key => Assert.NotEmpty(TypeScriptTemplateCatalog.ReadDefault(key)));
        var invalid = new TypeScriptTemplateCatalog(new Dictionary<string, string> { ["index"] = "{{ include '../secret.txt' }}" });
        Assert.ThrowsAny<Exception>(() => invalid.Render("index", new { }));
    }

    [Fact]
    public void Static_templates_ignore_comments_strings_and_unrelated_dynamic_templates()
    {
        var documents = TypeScriptDocumentExtractor.Extract(""""
            // gql`query Fake { nope }`
            const pattern = /gql`query Fake { nope }`/;
            const message = "graphql(`query Fake { nope }`)";
            const element = <Component value={1} />;
            const unrelated = `hello ${`nested ${1}`}`;
            const query = graphql(/* static */ `query Actual { value }`);
            const mutation = gql`mutation Update { update }`;
            """");
        Assert.Equal(["query Actual { value }", "mutation Update { update }"], documents);
        Assert.Throws<InvalidDataException>(() => TypeScriptDocumentExtractor.Extract("gql`query X { ...${fragment} }`"));
    }

    [Theory]
    [InlineData("query X { item { ...Missing } }", "Unknown fragment")]
    [InlineData("query X { item { ...A } } fragment A on Item { ...B } fragment B on Item { ...A }", "Fragment cycle")]
    [InlineData("query X { item { unknown } }", "Unknown field")]
    [InlineData("query X { item { id } } query X { other: item { id } }", "unique names")]
    public void Invalid_contracts_fail_before_publication(string operation, string message)
    {
        var root = Path.Combine(Path.GetTempPath(), "salep-ts-invalid-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(root);
        try
        {
            File.WriteAllText(Path.Combine(root, "schema.graphql"), "type Query { item: Item } type Item { id: ID! }");
            File.WriteAllText(Path.Combine(root, "operation.graphql"), operation);
            var config = Path.Combine(root, "salep.typescript.json");
            File.WriteAllText(config, """{"version":1,"target":"typescript","schema":"schema.graphql","documents":"operation.graphql","output":"Generated"}""");
            var error = Assert.Throws<InvalidDataException>(() => TypeScriptGenerator.Generate(new(config)));
            Assert.Contains(message, error.Message);
            Assert.False(File.Exists(Path.Combine(root, "Generated", "gql", "graphql.ts")));
        }
        finally { Directory.Delete(root, true); }
    }

    [Theory]
    [InlineData("input-traversal")]
    [InlineData("output-traversal")]
    [InlineData("duplicate-config")]
    [InlineData("template-traversal")]
    [InlineData("output-alias")]
    [InlineData("schema-alias")]
    [InlineData("documents-alias")]
    public void Filesystem_and_configuration_redirection_are_rejected(string mutation)
    {
        var root = Path.Combine(Path.GetTempPath(), "salep-ts-paths-" + Guid.NewGuid().ToString("N"));
        var project = Path.Combine(root, "project");
        Directory.CreateDirectory(project);
        try
        {
            File.WriteAllText(Path.Combine(project, "schema.graphql"), "type Query { value: String }");
            File.WriteAllText(Path.Combine(project, "operation.graphql"), "query Example { value }");
            File.WriteAllText(Path.Combine(root, "outside.graphql"), "type Query { value: String }");
            var config = Path.Combine(project, "salep.typescript.json");
            File.WriteAllText(config, """{"version":1,"target":"typescript","schema":"schema.graphql","documents":"operation.graphql","output":"Generated"}""");
            var options = new TypeScriptGeneratorOptions(config);
            var output = Assert.Single(TypeScriptGenerator.Generate(options), file => file.EndsWith("/gql/graphql.ts", StringComparison.Ordinal));
            var original = File.ReadAllText(output);
            var manifestPath = Path.Combine(project, "Generated", ".salep.manifest.json");
            if (mutation is "input-traversal" or "output-traversal")
            {
                var manifest = System.Text.Json.Nodes.JsonNode.Parse(File.ReadAllText(manifestPath))!.AsObject();
                if (mutation == "input-traversal") manifest["Inputs"]!["../../../outside.graphql"] = "forged";
                else manifest["Outputs"]!["../outside.ts"] = "forged";
                File.WriteAllText(manifestPath, manifest.ToJsonString());
            }
            else if (mutation == "duplicate-config")
                File.WriteAllText(config, """{"version":1,"version":1,"target":"typescript","schema":"schema.graphql","documents":"operation.graphql","output":"Generated"}""");
            else if (mutation.EndsWith("-alias", StringComparison.Ordinal))
            {
                var configuration = System.Text.Json.Nodes.JsonNode.Parse(File.ReadAllText(config))!.AsObject();
                var property = mutation[..mutation.IndexOf('-', StringComparison.Ordinal)];
                configuration[property] = property == "output" ? "Generated./nested" : "directory./" + (property == "schema" ? "schema.graphql" : "*.graphql");
                File.WriteAllText(config, configuration.ToJsonString());
            }
            else
                File.WriteAllText(config, """{"version":1,"target":"typescript","schema":"schema.graphql","documents":"operation.graphql","output":"Generated","templates":{"client":"../outside.graphql"}}""");
            Assert.Throws<InvalidDataException>(() => TypeScriptGenerator.Generate(options));
            Assert.Equal(original, File.ReadAllText(output));
            Assert.False(File.Exists(Path.Combine(project, "outside.ts")));
        }
        finally { Directory.Delete(root, true); }
    }

    [Fact]
    public void Generator_projects_conditional_fields_fragments_nullability_and_preserves_owned_output()
    {
        var root = Path.Combine(Path.GetTempPath(), "salep-ts-test-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(root);
        try
        {
            File.WriteAllText(Path.Combine(root, "schema.graphql"), "type Query { values: [[String!]!]! item(id: ID): Item } type Item { id: ID! name: String! }");
            File.WriteAllText(Path.Combine(root, "queries.ts"), "const query = graphql(`query Example($show: Boolean!, $id: ID = \"default\") { values item(id: $id) { id ...Name @include(if: $show) } } fragment Name on Item { name }`);");
            var config = Path.Combine(root, "salep.typescript.json");
            File.WriteAllText(config, """{"version":1,"target":"typescript","schema":"schema.graphql","documents":"*.ts","output":"Generated","fragmentMasking":false}""");
            var options = new TypeScriptGeneratorOptions(config);
            TypeScriptGenerator.Generate(options, validateOnly: true);
            Assert.False(Directory.Exists(Path.Combine(root, "Generated")));
            var file = Assert.Single(TypeScriptGenerator.Generate(options), file => file.EndsWith("/gql/graphql.ts", StringComparison.Ordinal));
            var source = File.ReadAllText(file);
            Assert.Contains("ReadonlyArray<ReadonlyArray<string>>", source);
            Assert.Contains("\"name\"?: string", source);
            Assert.Contains("\"id\"?: string | null", source);
            var stamp = File.GetLastWriteTimeUtc(file);
            TypeScriptGenerator.Generate(options);
            Assert.Equal(stamp, File.GetLastWriteTimeUtc(file));
            File.WriteAllText(Path.Combine(root, "Generated", "consumer.ts"), "// keep");
            File.AppendAllText(file, "// modified");
            Assert.Throws<InvalidDataException>(() => TypeScriptGenerator.Generate(options));
            Assert.Equal("// keep", File.ReadAllText(Path.Combine(root, "Generated", "consumer.ts")));
            File.WriteAllText(file, source);
            using var manifest = JsonDocument.Parse(File.ReadAllText(Path.Combine(root, "Generated", ".salep.manifest.json")));
            Assert.All(manifest.RootElement.GetProperty("Inputs").EnumerateObject(), input => Assert.False(Path.IsPathRooted(input.Name)));
            File.WriteAllText(config, """{"version":1,"target":"typescript","schema":"schema.graphql","documents":"*.ts","output":"../escape"}""");
            Assert.Throws<InvalidDataException>(() => TypeScriptGenerator.Generate(options));
        }
        finally { Directory.Delete(root, true); }
    }
}
