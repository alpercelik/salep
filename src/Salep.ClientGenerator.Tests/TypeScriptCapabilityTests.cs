using Salep.ClientGenerator.Model;
using Salep.ClientGenerator.TypeScript.Generation;
using Salep.ClientGenerator.TypeScript.Targets;
using Salep.ClientGenerator.TypeScript.Templates;
using Salep.GraphQLParser;
using Xunit;

namespace Salep.ClientGenerator.Tests;

public sealed class TypeScriptCapabilityTests
{
    [Fact]
    public void Custom_tags_replace_defaults_and_skip_unrelated_source()
    {
        var documents = TypeScriptDocumentExtractor.Extract("""
            const ignored = gql`query Ignored { x }`;
            // custom`query Comment { x }`
            const fake = "custom`query String { x }`";
            const selected = custom`query Selected { x }`;
            const called = request(`query Called { x }`);
            """, ["custom", "request"]);
        Assert.Equal(["query Selected { x }", "query Called { x }"], documents);
        Assert.Throws<InvalidDataException>(() => TypeScriptDocumentExtractor.Extract("custom`query Bad { ${selection} }`", ["custom"]));
    }

    [Fact]
    public void Schema_descriptions_render_as_safe_customizable_jsdoc()
    {
        var schema = GraphQlModelFactory.CreateSchema(Parse(""""
            "Genres" enum Genre { NOVEL }
            "Search input" input Search { "Requested genre" genre: Genre }
            type Query { """First line
            text with */ terminator
            last line""" title: String! }
            """"));
        var document = GraphQlModelFactory.CreateExecutable(schema, [Parse("query Read { title }")]);
        var renderer = new TypeScriptSourceRenderer(schema, document, new(), new());
        var source = renderer.SchemaTypes() + renderer.Operations();
        Assert.Contains(" * Genres", source);
        Assert.Contains(" * Search input", source);
        Assert.Contains(" * Requested genre", source);
        Assert.Contains(" * text with * / terminator", source);
        var custom = new TypeScriptTemplateCatalog(new Dictionary<string, string> { ["documentation"] = "// custom docs\n" });
        Assert.Contains("// custom docs", new TypeScriptSourceRenderer(schema, document, new(), custom).SchemaTypes());
    }

    [Fact]
    public void Persisted_ids_are_platform_independent_and_include_fragment_content()
    {
        var schema = GraphQlModelFactory.CreateSchema(Parse("type Query { title: String }"));
        var document = GraphQlModelFactory.CreateExecutable(schema, [Parse("query Read { ...Fields } fragment Fields on Query { title }")]);
        var source = new TypeScriptSourceRenderer(schema, document, new(), new(), persistedOperations: true).Operations();
        var query = new GraphQlOperationDocumentFormatter(schema, document).Format(document.Operations[0])
            .Replace("\r\n", "\n", StringComparison.Ordinal).Replace('\r', '\n');
        var id = TypeScriptSourceRenderer.OperationId(query);
        Assert.Equal(64, id.Length);
        Assert.Equal(id, TypeScriptSourceRenderer.OperationId(query.Replace("\n", "\r\n", StringComparison.Ordinal)));
        Assert.Equal(id, TypeScriptSourceRenderer.OperationId(query.Replace('\n', '\r')));
        Assert.Contains(id, source);
        Assert.NotEqual(id, TypeScriptSourceRenderer.OperationId(query.Replace("title", "other", StringComparison.Ordinal)));
    }

    [Fact]
    public void Persisted_inventory_matches_documents_and_is_removed_when_disabled()
    {
        var root = Path.Combine(Path.GetTempPath(), "salep-persist-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(root);
        try
        {
            File.WriteAllText(Path.Combine(root, "schema.graphql"), "type Query { title: String }");
            File.WriteAllText(Path.Combine(root, "operations.graphql"), "query Read { title }");
            var config = Path.Combine(root, "salep.json");
            const string settings = """{"version":1,"target":"typescript","schema":"schema.graphql","documents":["operations.graphql"],"persistedOperations":true}""";
            File.WriteAllText(config, settings);
            TypeScriptGenerator.Generate(new(config));
            var inventory = Path.Combine(root, "Generated", "operations.json");
            using (var json = System.Text.Json.JsonDocument.Parse(File.ReadAllText(inventory)))
            {
                var entry = Assert.Single(json.RootElement.GetProperty("operations").EnumerateObject());
                Assert.Equal("Read", entry.Value.GetProperty("name").GetString());
                Assert.Equal(TypeScriptSourceRenderer.OperationId(entry.Value.GetProperty("query").GetString()!), entry.Name);
                Assert.Contains(entry.Name, File.ReadAllText(Path.Combine(root, "Generated", "gql", "graphql.ts")));
            }
            File.WriteAllText(config, settings.Replace("true", "false", StringComparison.Ordinal));
            TypeScriptGenerator.Generate(new(config));
            Assert.False(File.Exists(inventory));
        }
        finally { Directory.Delete(root, true); }
    }

    [Fact]
    public void Scalar_enum_value_and_deprecated_field_documentation_is_retained()
    {
        var schema = GraphQlModelFactory.CreateSchema(Parse("""
            "Timestamp docs" scalar Timestamp
            enum Genre { "Old genre" OLD @deprecated(reason: "Use NEW") NEW }
            input Search { genre: Genre @deprecated }
            type Query { old: Timestamp @deprecated(reason: "Use current */ safely") }
            """));
        var document = GraphQlModelFactory.CreateExecutable(schema, [Parse("query Read { old }")]);
        var renderer = new TypeScriptSourceRenderer(schema, document, new(), new());
        var source = renderer.SchemaTypes() + renderer.Operations();
        Assert.Contains("Timestamp docs", source);
        Assert.Contains("Old genre", source);
        Assert.Contains("@deprecated Use NEW", source);
        Assert.Contains("@deprecated No longer supported", source);
        Assert.Contains("@deprecated Use current * / safely", source);
    }

    [Fact]
    public void Per_operation_layout_shares_types_and_cleans_previous_modules()
    {
        var root = Path.Combine(Path.GetTempPath(), "salep-partition-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(root);
        try
        {
            File.WriteAllText(Path.Combine(root, "schema.graphql"), "enum Genre { NEW } type Query { genre: Genre }");
            File.WriteAllText(Path.Combine(root, "operations.graphql"), "query Read { genre } query Other { genre }");
            var config = Path.Combine(root, "salep.json");
            const string settings = """{"version":1,"target":"typescript","schema":"schema.graphql","documents":["operations.graphql"],"outputLayout":"per-operation"}""";
            File.WriteAllText(config, settings);
            TypeScriptGenerator.Generate(new(config));
            var output = Path.Combine(root, "Generated", "gql");
            var operation = Path.Combine(output, "operations", "operation-Read.ts");
            Assert.Contains("Genre", File.ReadAllText(operation));
            Assert.DoesNotContain("OtherDocument", File.ReadAllText(operation));
            Assert.Contains("./operations/operation-Read", File.ReadAllText(Path.Combine(output, "graphql.ts")));
            Assert.Contains("export type Genre", File.ReadAllText(Path.Combine(output, "shared.ts")));
            File.WriteAllText(config, settings.Replace("per-operation", "combined", StringComparison.Ordinal));
            TypeScriptGenerator.Generate(new(config));
            Assert.False(File.Exists(operation));
            Assert.False(File.Exists(Path.Combine(output, "shared.ts")));
            Assert.Contains("OtherDocument", File.ReadAllText(Path.Combine(output, "graphql.ts")));
        }
        finally { Directory.Delete(root, true); }
    }

    [Fact]
    public void Introspection_operations_validate_and_project_standard_types()
    {
        var schema = GraphQlModelFactory.CreateSchema(Parse("schema { query: Root } type Root { title: String }"));
        var document = GraphQlModelFactory.CreateExecutable(schema, [Parse("""
            query Inspect($name: String!) {
              __schema { queryType { name kind } directives { locations isRepeatable } }
              __type(name: $name) { fields(includeDeprecated: true) { name args { defaultValue isDeprecated } type { kind ofType { name } } } isOneOf }
            }
            """)]);
        new GraphQlDocumentValidator(schema, document).Validate();
        var renderer = new TypeScriptSourceRenderer(schema, document, new(), new());
        var source = renderer.SchemaTypes() + renderer.Operations();
        Assert.Contains("SCALAR", source);
        Assert.Contains("INPUT_FIELD_DEFINITION", source);
        Assert.Contains("\"isOneOf\": boolean | null", source);
        Assert.Contains("\"isDeprecated\": boolean", source);
        Assert.Contains("InspectDocument", source);
        var invalid = GraphQlModelFactory.CreateExecutable(schema, [Parse("query Invalid { __type { name } }")]);
        Assert.Throws<InvalidDataException>(() => new GraphQlDocumentValidator(schema, invalid).Validate());
    }

    [Fact]
    public void Local_document_planning_removes_local_fields_and_variables_but_keeps_remote_parents()
    {
        var schema = GraphQlModelFactory.CreateSchema(Parse("type Query { book: Book local: String } type Book { id: ID title: String }") );
        var document = GraphQlModelFactory.CreateExecutable([Parse("""
            query Read($local: Boolean!) { book { ...Fields title @client @include(if: $local) } local @client }
            fragment Fields on Book { id }
            query EntirelyLocal { local @client }
            query Parent { book { title @client } }
            """)]);
        var planner = new TypeScriptClientDocumentPlanner(schema, document);
        var remote = planner.RemoteQuery(document.Operations[0])!;
        Assert.DoesNotContain("@client", remote);
        Assert.DoesNotContain("$local", remote);
        Assert.DoesNotContain("title", remote);
        Assert.Contains("id", remote);
        Assert.Null(planner.RemoteQuery(document.Operations[1]));
        Assert.Contains("__typename", planner.RemoteQuery(document.Operations[2])!);
        var serverDocument = GraphQlModelFactory.CreateExecutable([Parse(remote)]);
        new GraphQlDocumentValidator(schema, serverDocument).Validate();
    }

    [Fact]
    public void Local_resolver_contracts_preserve_arguments_nullability_and_async_results()
    {
        var server = GraphQlModelFactory.CreateSchema(Parse("type Query { book: Book } type Book { id: ID! }"));
        var local = GraphQlModelFactory.CreateSchema(Parse("extend type Book { label(prefix: String!, suffix: String = \"!\"): String! }") );
        var schema = server with { Types = server.Types.Concat(local.Types).ToArray() };
        var renderer = new TypeScriptSourceRenderer(schema, new([], []), new(), new());
        var source = renderer.ResolverTypes(local.Types);
        Assert.Contains("LocalResolvers<TContext = unknown>", source);
        Assert.Contains("\"prefix\": string", source);
        Assert.Contains("\"suffix\"?: string | null", source);
        Assert.Contains("string | Promise<string>", source);
        Assert.Contains("ResolverParentTypes[\"Book\"]", source);
        Assert.Contains("\"id\"?: string", source);
    }

    [Fact]
    public void Partition_filenames_are_distinct_on_case_insensitive_filesystems_and_import_only_types()
    {
        var root = Path.Combine(Path.GetTempPath(), "salep-case-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(root);
        try
        {
            File.WriteAllText(Path.Combine(root, "schema.graphql"), "enum title { A } type Query { title: String }");
            File.WriteAllText(Path.Combine(root, "operation.graphql"), "query Read { title } query read { title }");
            var config = Path.Combine(root, "salep.json");
            File.WriteAllText(config, """{"version":1,"target":"typescript","schema":"schema.graphql","documents":["operation.graphql"],"outputLayout":"per-operation"}""");
            TypeScriptGenerator.Generate(new(config));
            var files = Directory.GetFiles(Path.Combine(root, "Generated", "gql", "operations"));
            Assert.Equal(2, files.Distinct(StringComparer.OrdinalIgnoreCase).Count());
            foreach (var file in files) Assert.DoesNotContain("import type { title", File.ReadAllText(file));
            TypeScriptGenerator.Generate(new(config));
        }
        finally { Directory.Delete(root, true); }
    }

    [Fact]
    public void Unmarked_local_fields_fail_before_a_server_document_can_be_sent()
    {
        var server = GraphQlModelFactory.CreateSchema(Parse("type Query { book: Book } type Book { id: ID! }"));
        var local = GraphQlModelFactory.CreateSchema(Parse("extend type Book { label: String! }"));
        var schema = server with { Types = server.Types.Concat(local.Types).ToArray() };
        var invalid = GraphQlModelFactory.CreateExecutable([Parse("query Read { book { label } }")]);
        Assert.Throws<InvalidDataException>(() => new TypeScriptClientDocumentPlanner(schema, invalid).ValidateLocalSelections(local.Types));
        var valid = GraphQlModelFactory.CreateExecutable([Parse("query Read { book { ...Local @client } } fragment Local on Book { label }")]);
        new TypeScriptClientDocumentPlanner(schema, valid).ValidateLocalSelections(local.Types);
    }

    private static DocumentNode Parse(string source) => global::Salep.GraphQLParser.GraphQLParser.Parse(new SourceText(source.AsMemory()));
}
