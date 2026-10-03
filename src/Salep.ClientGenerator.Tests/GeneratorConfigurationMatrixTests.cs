using System.Text.Json;
using System.Reflection;
using System.Runtime.Loader;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Salep.ClientGenerator.Generation;
using Xunit;

namespace Salep.ClientGenerator.Tests;

public sealed partial class GeneratorContractTests
{
    public static IEnumerable<object[]> ConfigurationCases()
    {
        // Exhaust all six behavior switches, rather than sampling correlated combinations.
        for (var mask = 0; mask < 64; mask++) yield return [mask];
    }

    [Theory]
    [MemberData(nameof(ConfigurationCases))]
    public async Task Configuration_matrix_matches_contract_and_compiles(int mask)
    {
        using var fixture = new MatrixFixture();
        fixture.WriteInputs("""
            scalar Token
            enum Status { ACTIVE INACTIVE }
            input Filter { status: Status! names: [String!]! token: Token optional: Int }
            type Item { id: ID! names: [String] nested: [[Int!]!]! }
            type Query { items(filter: Filter, count: Int): [Item]! }
            type Mutation { update(filter: Filter!): Item! }
            type Subscription { changed: Item }
            """, """
            query Items($filter: Filter, $count: Int = 2, $unused: String) { aliased: items(filter: $filter, count: $count) { id names nested } }
            query FragmentItems($count: Int = 2) { ...Root }
            fragment Root on Query { items(count: $count) { ...Fields } }
            fragment Fields on Item { id names }
            mutation Update($filter: Filter!) { update(filter: $filter) { id names } }
            subscription Changed { changed { id } }
            """);
        var settings = new Dictionary<string, object?>(StringComparer.Ordinal)
        {
            ["omitUnusedVariables"] = (mask & 1) != 0,
            ["inlineDefaultVariables"] = (mask & 2) != 0,
            ["useHttpGet"] = (mask & 4) != 0,
            ["enableBatching"] = (mask & 8) != 0,
            ["emitSample"] = (mask & 16) != 0,
            ["indentSize"] = new[] { 0, 2, 4, 16 }[mask % 4],
            ["maxGetUrlLength"] = mask % 2 == 0 ? 1 : 2048,
            ["scalars"] = new Dictionary<string, object?>
            {
                ["Token"] = new { type = "Guid", isValueType = true, sampleExpression = "Guid.Empty", sampleJson = "\"00000000-0000-0000-0000-000000000000\"" }
            }
        };
        fixture.Generate(settings, rawJson: (mask & 32) != 0);
        AssertContract($"Matrix-{mask:D2}", [(Path.Combine(fixture.Root, "Scriban"), false), (Path.Combine(fixture.Root, "ScribanTests"), true)]);
        await fixture.CompileAndRunAsync();
    }

    [Theory]
    [InlineData(1701)]
    [InlineData(90210)]
    [InlineData(65537)]
    public async Task Seeded_schema_and_operation_corpus_matches_contract_and_compiles(int seed)
    {
        var random = new Random(seed);
        for (var iteration = 0; iteration < 12; iteration++)
        {
            using var fixture = new MatrixFixture();
            var wrappers = new[] { "String", "String!", "[String]", "[String!]!", "[[String!]!]" };
            var fields = Enumerable.Range(0, random.Next(1, 9))
                .Select(index => $"field{index}: {wrappers[random.Next(wrappers.Length)]}").ToArray();
            var selection = string.Join(' ', Enumerable.Range(0, fields.Length).Select(index => $"alias{index}: field{index}"));
            fixture.WriteInputs($$"""
                extend type Item { added: Int! }
                type Item { {{string.Join(' ', fields)}} }
                enum Choice { FIRST }
                extend enum Choice { SECOND }
                input Filter { choice: Choice }
                extend input Filter { names: [String!] }
                type Query { item(filter: Filter): Item }
                extend type Query { items: [Item!]! }
                """, $$"""
                query Read{{iteration}}($filter: Filter) { item(filter: $filter) { {{selection}} added } items { ...Selected } }
                fragment Selected on Item { {{selection}} added }
                """);
            fixture.Generate(new Dictionary<string, object?> { ["omitUnusedVariables"] = true });
            await fixture.CompileAndRunAsync();
        }
    }

    [Fact]
    public async Task Inherited_profiles_and_local_overrides_match()
    {
        using var fixture = new MatrixFixture();
        fixture.WriteInputs("scalar Token type Query { token(id: Token, count: Int): Token }", "query Token($id: Token, $count: Int = 2, $unused: String) { token(id: $id, count: $count) }");
        File.WriteAllText(Path.Combine(fixture.Root, "parent.json"), JsonSerializer.Serialize(new
        {
            version = 1, kind = "profile", useHttpGet = true, enableBatching = true, inlineDefaultVariables = true, indentSize = 2,
            scalars = new { Token = new { type = "Guid", isValueType = true, sampleExpression = "Guid.Empty", sampleJson = "\"00000000-0000-0000-0000-000000000000\"" } }
        }));
        File.WriteAllText(Path.Combine(fixture.Root, "profile.json"), JsonSerializer.Serialize(new
        {
            version = 1, kind = "profile", extends = "parent.json", omitUnusedVariables = true
        }));
        fixture.Generate(new Dictionary<string, object?> { ["profile"] = "profile.json", ["useHttpGet"] = false });
        await fixture.CompileAndRunAsync();
        foreach (var backend in new[] { "Scriban" })
        {
            var query = OperationQueries(Path.Combine(fixture.Root, backend))["Token"];
            Assert.DoesNotContain("$unused", query, StringComparison.Ordinal);
            Assert.DoesNotContain("$count", query, StringComparison.Ordinal);
            Assert.Contains("count: 2", query, StringComparison.Ordinal);
        }
    }

    [Fact]
    public async Task Schema_extensions_change_the_owned_contract_and_selected_responses()
    {
        using var fixture = new MatrixFixture();
        fixture.WriteInputs("""
            schema { query: ReadRoot }
            extend schema { mutation: WriteRoot }
            scalar Token
            extend scalar Token @specifiedBy(url: "https://example.com/token")
            interface Entity { id: ID! }
            interface Node { id: ID! }
            extend interface Node implements Entity { label: String }
            type Item { id: ID! label: String }
            extend type Item implements Node { added: Int! }
            type Other { name: String! }
            union Selection = Item
            extend union Selection = Other
            input Filter { text: String }
            extend input Filter { values: [Int!]! }
            enum Status { FIRST }
            extend enum Status { SECOND }
            type ReadRoot { item: Item }
            extend type ReadRoot { status: Status! }
            type WriteRoot { update(filter: Filter!): Item! }
            """, """
            query Read { item { id label alias: added } status }
            mutation Update($filter: Filter!) { update(filter: $filter) { added } }
            """);
        // The interface result is a native union even when no explicit union is declared.
#if NET11_0_OR_GREATER
        fixture.Generate(new Dictionary<string, object?> { ["unionRepresentation"] = "native" });
        await fixture.CompileAndRunAsync();
        foreach (var backend in new[] { "Scriban" })
        {
            var schema = File.ReadAllText(Path.Combine(fixture.Root, backend, "SchemaTypes.cs"));
            Assert.Contains("public int Added", schema, StringComparison.Ordinal);
            Assert.Contains("public required List<int> Values", schema, StringComparison.Ordinal);
            Assert.Contains("SECOND", schema, StringComparison.Ordinal);
            var operations = File.ReadAllText(Path.Combine(fixture.Root, backend, "Operations.cs"));
            Assert.Contains("public int Alias", operations, StringComparison.Ordinal);
            Assert.Contains("public Status Status", operations, StringComparison.Ordinal);
        }
#else
        await Task.CompletedTask;
        fixture.Generate(new Dictionary<string, object?>());
        Assert.Contains("public int Alias", File.ReadAllText(Path.Combine(fixture.Root, "Scriban", "Operations.cs")), StringComparison.Ordinal);
#endif
    }

    [Fact]
    public void Default_backend_upgrades_former_scriban_manifest_filename()
    {
        using var fixture = new MatrixFixture();
        fixture.WriteInputs("type Query { value: String }", "query Value { value }");
        fixture.Generate(new Dictionary<string, object?>());
        var directory = Path.Combine(fixture.Root, "Scriban");
        var current = Path.Combine(directory, ".salep.manifest.json");
        var previous = Path.Combine(directory, ".salep-scriban.manifest.json");
        var contents = File.ReadAllText(current);
        File.Move(current, previous);
        ScribanGenerator.Generate(new(Path.Combine(fixture.Root, "Scriban.json"), fixture.Root));
        Assert.Equal(contents, File.ReadAllText(current));
        Assert.False(File.Exists(previous));
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void Default_backend_upgrades_released_roslyn_ownership_manifests(bool tamper)
    {
        using var fixture = new MatrixFixture();
        fixture.WriteInputs("type Query { value: String }", "query Value { value }");
        var config = Path.Combine(fixture.Root, "salep.json");
        File.WriteAllText(config, JsonSerializer.Serialize(new
        {
            version = 1, kind = "client", schema = "schema.graphql", operations = "graphql",
            output = "Generated", @namespace = "Upgrade.Client", emitAgentInstructions = false
        }));
        var legacy = JsonSerializer.Deserialize<Dictionary<string, string>>(
            File.ReadAllText(Path.Combine(AppContext.BaseDirectory, "Fixtures", "LegacyOwnership.json")))!;
        foreach (var file in legacy)
        {
            var target = Path.Combine(fixture.Root, file.Key);
            Directory.CreateDirectory(Path.GetDirectoryName(target)!);
            File.WriteAllText(target, file.Value);
        }
        var directory = Path.Combine(fixture.Root, "Generated");
        var manifest = Path.Combine(directory, ".salep.manifest.json");
        if (tamper)
        {
            File.WriteAllText(manifest, File.ReadAllText(manifest).Replace("Upgrade.Client", "Tampered.Client", StringComparison.Ordinal));
            var before = File.ReadAllText(Path.Combine(directory, "Operations.cs"));
            var error = Assert.Throws<ScribanConfigurationException>(() => ScribanGenerator.Generate(new(config, fixture.Root)));
            Assert.Equal("SALEPS2001", error.Diagnostic.Code);
            Assert.Equal(before, File.ReadAllText(Path.Combine(directory, "Operations.cs")));
            return;
        }
        var unrelated = Path.Combine(directory, "Unrelated.cs");
        File.WriteAllText(unrelated, "// consumer-owned source");
        File.AppendAllText(Path.Combine(fixture.Root, "graphql/operations.graphql"), "\nquery Added { value }");
        ScribanGenerator.Generate(new(WorkingDirectory: fixture.Root));
        using var document = JsonDocument.Parse(File.ReadAllText(manifest));
        Assert.Equal(2, document.RootElement.GetProperty("Version").GetInt32());
        Assert.Equal("../salep.json", document.RootElement.GetProperty("Configuration").GetString());
        Assert.Contains("AddedOperation", File.ReadAllText(Path.Combine(directory, "Operations.cs")), StringComparison.Ordinal);
        Assert.Equal("// consumer-owned source", File.ReadAllText(unrelated));
        ScribanGenerator.Validate(new(WorkingDirectory: fixture.Root));
    }

    [Fact]
    public void Manifests_use_relative_paths_and_survive_relocation()
    {
        const string manifestName = ".salep.manifest.json";
        var root = Path.Combine(Path.GetTempPath(), "salep-portable-" + Guid.NewGuid().ToString("N"));
        var original = Path.Combine(root, "original workspace");
        var moved = Path.Combine(root, "moved workspace");
        Directory.CreateDirectory(original);
        try
        {
            File.WriteAllText(Path.Combine(original, "schema.graphql"), "type Query { value: String }");
            File.WriteAllText(Path.Combine(original, "base.graphql"), "query Value { value }");
            File.WriteAllText(Path.Combine(original, "module.graphql"), "query ModuleValue { value }");
            File.WriteAllText(Path.Combine(original, "profile.json"), "{\"version\":1,\"kind\":\"profile\",\"useHttpGet\":true}");
            foreach (var name in new[] { "base", "module" })
                File.WriteAllText(Path.Combine(original, name + ".json"), JsonSerializer.Serialize(new
                {
                    version = 1, kind = "client", profile = "profile.json", schema = "schema.graphql", operations = name + ".graphql",
                    output = name + "/Generated", @namespace = "Portable." + char.ToUpperInvariant(name[0]) + name[1..],
                    baseClient = name == "module" ? "base.json" : null, emitAgentInstructions = false
                }, new JsonSerializerOptions { DefaultIgnoreCondition = System.Text.Json.Serialization.JsonIgnoreCondition.WhenWritingNull }));
            File.WriteAllText(Path.Combine(original, "tests.json"), JsonSerializer.Serialize(new
            {
                version = 1, kind = "tests", client = "module.json", output = "tests/Generated", emitAgentInstructions = false
            }));
            void Generate(string directory, string name)
            {
                var config = Path.Combine(directory, name + ".json");
                ScribanGenerator.Generate(new(config, directory));
            }
            foreach (var name in new[] { "base", "module", "tests" }) Generate(original, name);
            var snapshots = new Dictionary<string, string>();
            foreach (var name in new[] { "base", "module", "tests" })
            {
                var manifestPath = Path.Combine(original, name, "Generated", manifestName);
                var text = File.ReadAllText(manifestPath);
                Assert.DoesNotContain(original, text, StringComparison.Ordinal);
                using var document = JsonDocument.Parse(text);
                var manifest = document.RootElement;
                foreach (var property in new[] { "Configuration", "ConfigurationIdentity", "Output" })
                    if (manifest.TryGetProperty(property, out var value)) Assert.False(Path.IsPathRooted(value.GetString()!));
                foreach (var property in new[] { "Inputs", "Dependencies" })
                    foreach (var path in manifest.GetProperty(property).EnumerateObject())
                    {
                        Assert.False(Path.IsPathRooted(path.Name));
                        Assert.DoesNotContain("\\", path.Name, StringComparison.Ordinal);
                        Assert.True(File.Exists(Path.GetFullPath(path.Name, Path.GetDirectoryName(manifestPath)!)));
                    }
                if (manifest.TryGetProperty("Symbols", out var symbols))
                    foreach (var symbol in symbols.EnumerateObject()) Assert.False(Path.IsPathRooted(symbol.Value.GetProperty("Owner").GetString()!));
                snapshots[name] = text;
            }
            Directory.Move(original, moved);
            // Generate dependents first: this verifies moved base contracts before rewriting them.
            Generate(moved, "tests");
            Generate(moved, "module");
            Generate(moved, "base");
            foreach (var name in snapshots.Keys)
                Assert.Equal(snapshots[name], File.ReadAllText(Path.Combine(moved, name, "Generated", manifestName)));
            File.AppendAllText(Path.Combine(moved, "module.graphql"), "\nquery Added { value }");
            Generate(moved, "module");
            Generate(moved, "tests");
            Assert.Contains("AddedOperation", File.ReadAllText(Path.Combine(moved, "module/Generated/Operations.cs")), StringComparison.Ordinal);
        }
        finally { Directory.Delete(root, recursive: true); }
    }

    [Fact]
    public void Owned_outputs_are_recovered_by_regeneration_after_missing_or_modified_files()
    {
        using var fixture = new MatrixFixture();
        fixture.WriteInputs("type Query { value: String }", "query Value { value }");
        fixture.Generate(new Dictionary<string, object?>());
        foreach (var backend in new[] { "Scriban" })
        {
            var operations = Path.Combine(fixture.Root, backend, "Operations.cs");
            var schema = Path.Combine(fixture.Root, backend, "SchemaTypes.cs");
            var expected = File.ReadAllText(operations);
            File.Delete(operations);
            File.WriteAllText(schema, "// owned file was changed outside the generator\n");
            var config = Path.Combine(fixture.Root, backend + ".json");
            ScribanGenerator.Generate(new(config, fixture.Root));
            Assert.Equal(expected, File.ReadAllText(operations));
            Assert.Contains("record Query", File.ReadAllText(schema), StringComparison.Ordinal);
        }
    }

    [Fact]
    public void Contract_comparison_detects_consumer_visible_mutations()
    {
        using var fixture = new MatrixFixture();
        fixture.WriteInputs("type Query { value: String }", "query Value { value }");
        fixture.Generate(new Dictionary<string, object?>());
        var path = Path.Combine(fixture.Root, "Scriban/Operations.cs");
        var original = File.ReadAllText(path);
        var originalSurface = CSharpSurface(Path.Combine(fixture.Root, "Scriban"));
        foreach (var replacement in new[]
        {
            original.Replace("string? Value", "string Value", StringComparison.Ordinal),
            original.Replace("JsonPropertyName(\"value\")", "JsonPropertyName(\"other\")", StringComparison.Ordinal),
            original.Replace("get; init;", "get; set;", StringComparison.Ordinal),
            original.Replace("CancellationToken cancellationToken = default", "CancellationToken cancellationToken", StringComparison.Ordinal)
        })
        {
            Assert.NotEqual(original, replacement);
            File.WriteAllText(path, replacement);
            Assert.False(originalSurface.SetEquals(CSharpSurface(Path.Combine(fixture.Root, "Scriban"))));
        }
    }

    private sealed class MatrixFixture : IDisposable
    {
        public string Root { get; } = Path.Combine(Path.GetTempPath(), "salep-configuration-contract-" + Guid.NewGuid().ToString("N"));

        public MatrixFixture() => Directory.CreateDirectory(Path.Combine(Root, "graphql"));

        public void WriteInputs(string schema, string operations)
        {
            File.WriteAllText(Path.Combine(Root, "schema.graphql"), schema);
            File.WriteAllText(Path.Combine(Root, "graphql/operations.graphql"), operations);
        }

        public void Generate(Dictionary<string, object?> settings, bool rawJson = true)
        {
            foreach (var backend in new[] { "Scriban" })
            {
                var config = new Dictionary<string, object?>(settings, StringComparer.Ordinal)
                {
                    ["version"] = 1, ["kind"] = "client", ["schema"] = "schema.graphql", ["operations"] = "graphql",
                    ["namespace"] = "Matrix.Client", ["clientName"] = "CustomClient", ["output"] = backend,
                    ["emitAgentInstructions"] = false, ["unionRepresentation"] = settings.GetValueOrDefault("unionRepresentation", "dunet")
                };
                var clientPath = Path.Combine(Root, backend + ".json");
                File.WriteAllText(clientPath, JsonSerializer.Serialize(config));
                var testsPath = Path.Combine(Root, backend + "Tests.json");
                File.WriteAllText(testsPath, JsonSerializer.Serialize(new
                {
                    version = 1, kind = "tests", client = clientPath, output = backend + "Tests",
                    emitAgentInstructions = false, rawJsonLiterals = rawJson, indentSize = settings.GetValueOrDefault("indentSize", 4)
                }));
                GenerateBackend(backend, clientPath);
                GenerateBackend(backend, testsPath);
                var before = Snapshot(backend);
                GenerateBackend(backend, clientPath);
                GenerateBackend(backend, testsPath);
                Assert.Equal(before, Snapshot(backend));
            }
        }

        private void GenerateBackend(string backend, string config)
        {
            ScribanGenerator.Generate(new(config, Root));
        }

        private string[] Snapshot(string backend) => new[] { backend, backend + "Tests" }
            .SelectMany(folder => Directory.EnumerateFiles(Path.Combine(Root, folder)).Order(StringComparer.Ordinal).Select(File.ReadAllText)).ToArray();

        public async Task CompileAndRunAsync()
        {
            var references = ((string)AppContext.GetData("TRUSTED_PLATFORM_ASSEMBLIES")!).Split(Path.PathSeparator)
                .Select(path => MetadataReference.CreateFromFile(path)).ToArray();
            foreach (var backend in new[] { "Scriban" })
            {
                var trees = new[] { backend, backend + "Tests" }.SelectMany(folder =>
                        Directory.EnumerateFiles(Path.Combine(Root, folder), "*.cs"))
                    .Select(path => CSharpSyntaxTree.ParseText(File.ReadAllText(path), new CSharpParseOptions(LanguageVersion.Preview), path: path, cancellationToken: TestContext.Current.CancellationToken));
                var compilation = CSharpCompilation.Create("Matrix_" + Guid.NewGuid().ToString("N"), trees, references,
                    new CSharpCompilationOptions(OutputKind.DynamicallyLinkedLibrary, nullableContextOptions: NullableContextOptions.Enable));
                using var output = new MemoryStream();
                var result = compilation.Emit(output, cancellationToken: TestContext.Current.CancellationToken);
                Assert.True(result.Success, string.Join(Environment.NewLine, result.Diagnostics.Where(diagnostic => diagnostic.Severity == DiagnosticSeverity.Error)));
                var assembly = AssemblyLoadContext.Default.LoadFromStream(new MemoryStream(output.ToArray()));
                var facts = assembly.GetTypes().SelectMany(type => type.GetMethods()
                    .Where(method => method.GetCustomAttribute<FactAttribute>() is not null)
                    .Select(method => (Type: type, Method: method))).ToArray();
                Assert.NotEmpty(facts);
                foreach (var fact in facts)
                {
                    var instance = Activator.CreateInstance(fact.Type);
                    try
                    {
                        if (fact.Method.Invoke(instance, null) is Task task) await task;
                    }
                    catch (Exception error)
                    {
                        Assert.Fail($"{backend} generated test {fact.Type.Name}.{fact.Method.Name} failed: {error}");
                    }
                    finally
                    {
                        if (instance is IDisposable disposable) disposable.Dispose();
                        if (instance is IAsyncDisposable asyncDisposable) await asyncDisposable.DisposeAsync();
                    }
                }
            }
        }

        public void Dispose() => Directory.Delete(Root, recursive: true);
    }
}
