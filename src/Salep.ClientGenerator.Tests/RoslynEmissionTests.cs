using System.Text.Json;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Salep.ClientGenerator.Config;
using Salep.ClientGenerator.Emission;
using Salep.ClientGenerator.Generation;
using static Salep.ClientGenerator.Emission.Syntax.Cs;
using Xunit;

namespace Salep.ClientGenerator.Tests;

public sealed class RoslynEmissionTests
{
    [Theory]
    [InlineData("public class Broken {", "Valid.Namespace", "System")]
    [InlineData("public class Broken { public string Name => ; }", "Valid.Namespace", "System")]
    [InlineData("public class Good {}", "Invalid-namespace", "System")]
    [InlineData("public class Good {}", "Valid.Namespace", "System;")]
    [InlineData("???", "Valid.Namespace", "System")]
    public void RejectsMalformedMembersAndNames(string member, string namespaceName, string usingName)
    {
        using var fixture = new Fixture(new());
        var error = Assert.Throws<InvalidOperationException>(() => RoslynEmitter.Emit(
            GeneratorConfig.Load(fixture.ConfigPath), namespaceName, [usingName], [SyntaxFactory.ParseMemberDeclaration(member) ?? SyntaxFactory.IncompleteMember().WithType(SyntaxFactory.ParseTypeName(member))]));
        Assert.Contains(namespaceName, error.Message);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void LanguageVersionIsExplicitAndAllTypedMembersArePreserved(bool native)
    {
        using var fixture = new Fixture(new() { ["useNativeUnions"] = native });
        var config = GeneratorConfig.Load(fixture.ConfigPath);
        Assert.Equal(native ? LanguageVersion.Preview : LanguageVersion.CSharp14, RoslynEmitter.GetParseOptions(config).LanguageVersion);
        var output = RoslynEmitter.Emit(config, "Valid.Namespace", ["", "System"], [SyntaxFactory.ClassDeclaration("Good"), SyntaxFactory.ClassDeclaration("Other")]);
        Assert.Contains("class Good", output);
        Assert.Contains("class Other", output);
        Assert.Equal(2, CSharpSyntaxTree.ParseText(output, cancellationToken: TestContext.Current.CancellationToken).GetRoot(TestContext.Current.CancellationToken).DescendantNodes().OfType<ClassDeclarationSyntax>().Count());
    }

    [Theory]
    [InlineData(0)]
    [InlineData(2)]
    [InlineData(4)]
    [InlineData(8)]
    public void FormattingPreservesNestingAndDoesNotLeakBetweenFiles(int indentation)
    {
        using var fixture = new Fixture(new() { ["indentSize"] = indentation });
        var config = GeneratorConfig.Load(fixture.ConfigPath);
        MemberDeclarationSyntax[] members = [Class("Example", [Method("int", "Value", [], [Return(Number(42))])])];
        var output = RoslynEmitter.EmitFile(config, "Generated", ["", "System"], members);
        Assert.Contains("\n" + new string(' ', indentation) + "public int Value()", output);
        Assert.Contains("\n" + new string(' ', indentation * 2) + "return 42;", output);
        Assert.DoesNotContain("\r", output);
        Assert.EndsWith("\n", output);
        Assert.All(output.Split('\n').Where(string.IsNullOrWhiteSpace), line => Assert.Empty(line));
        Assert.Equal(output, RoslynEmitter.EmitFile(config, "Generated", ["", "System"], members));
    }

    [Theory]
    [InlineData("a\"b\\c\n\t")]
    [InlineData("\"\"\"\"\"")]
    [InlineData("Türkçe 🚀")]
    [InlineData("")]
    public void LiteralValuesSurviveFormattingAndReparsing(string value)
    {
        using var fixture = new Fixture(new());
        var output = RoslynEmitter.Emit(GeneratorConfig.Load(fixture.ConfigPath), "Generated", [],
            [Class("Example", [ReadOnlyProperty("string", "Value", String(value))])]);
        var tree = CSharpSyntaxTree.ParseText(output, cancellationToken: TestContext.Current.CancellationToken);
        Assert.Empty(tree.GetDiagnostics(TestContext.Current.CancellationToken));
        var literal = Assert.Single(tree.GetRoot(TestContext.Current.CancellationToken).DescendantNodes().OfType<LiteralExpressionSyntax>());
        Assert.Equal(value, literal.Token.ValueText);
    }

    public static IEnumerable<object[]> Configurations()
    {
        // Cartesian coverage of transport, literal format, scalar library and generated tests.
        for (var flags = 0; flags < 32; flags++) yield return [flags];
    }

    [Theory]
    [MemberData(nameof(Configurations))]
    public void ConfigurationCombinationsCompileIncludingGeneratedTests(int flags)
    {
        using var fixture = new Fixture(new()
        {
            ["useHttpGet"] = (flags & 1) != 0,
            ["enableBatching"] = (flags & 2) != 0,
            ["useRawStrings"] = (flags & 4) != 0,
            ["useNodaTime"] = (flags & 8) != 0,
            ["generateTests"] = (flags & 16) != 0,
            ["generateSample"] = (flags & 16) != 0,
            ["inlineDefaultVariables"] = (flags & 1) != 0,
            ["omitUnusedVariables"] = (flags & 2) != 0,
            ["clientClassName"] = "CatalogClient",
            ["scalarMappings"] = new Dictionary<string, string> { ["Money"] = "decimal" },
            ["scalarValueTypes"] = new Dictionary<string, bool> { ["Money"] = true },
            ["scalarSampleExpressions"] = new Dictionary<string, string> { ["Money"] = "12.5m" },
            ["scalarSampleJsonLiterals"] = new Dictionary<string, string> { ["Money"] = "12.5" }
        });
        var result = fixture.Generate();
        Assert.Empty(result.Warnings);
        fixture.Compile();
        Assert.Equal((flags & 16) != 0, Directory.Exists(Path.Combine(fixture.Root, "Tests")));
        var before = result.GeneratedFiles.Where(path => path.EndsWith(".cs", StringComparison.Ordinal))
            .ToDictionary(path => path, File.ReadAllText);
        fixture.Generate();
        foreach (var (path, source) in before) Assert.Equal(source, File.ReadAllText(path));
    }

    [Fact]
    public void InvalidConfigurationFailsAndDoesNotPoisonNextGeneration()
    {
        using var invalid = new Fixture(new() { ["clientClassName"] = "Bad-Client" });
        var error = Assert.Throws<ConfigurationException>(() => invalid.Generate());
        Assert.Contains("Bad-Client", error.Message);
        Assert.False(File.Exists(Path.Combine(invalid.Root, "Generated", "GraphQLClient.cs")));
        using var valid = new Fixture(new());
        valid.Generate();
        valid.Compile();
    }

    [Theory]
    [InlineData(false, false)]
    [InlineData(false, true)]
    [InlineData(true, false)]
    [InlineData(true, true)]
    public void UnionAndInterfaceEmissionUsesTheSelectedLanguage(bool native, bool raw)
    {
        using var fixture = new Fixture(new()
        {
            ["useNativeUnions"] = native, ["useRawStrings"] = raw,
            ["generateTests"] = true, ["generateSample"] = true
        });
        File.WriteAllText(Path.Combine(fixture.Root, "schema.graphql"),
            "interface Node { id: ID! } type Product implements Node { id: ID! } type Other { id: ID! } union Result = Product | Other type Query { result: Result, node: Node }");
        File.WriteAllText(Path.Combine(fixture.Root, "ops", "query.graphql"),
            "query Lookup { result { __typename ... on Product { id } ... on Other { id } } node { id } }");
        var result = fixture.Generate();
        var options = RoslynEmitter.GetParseOptions(GeneratorConfig.Load(fixture.ConfigPath));
        foreach (var path in result.GeneratedFiles.Where(path => path.EndsWith(".cs", StringComparison.Ordinal)))
            Assert.DoesNotContain(CSharpSyntaxTree.ParseText(File.ReadAllText(path), options, cancellationToken: TestContext.Current.CancellationToken)
                .GetDiagnostics(TestContext.Current.CancellationToken), diagnostic => diagnostic.Severity == DiagnosticSeverity.Error);
#if NET11_0_OR_GREATER
        if (native) fixture.Compile();
#endif
        // Dunet's source generator and native-union runtime behavior are also exercised by the sample builds.
    }

    [Theory]
    [InlineData(false, false)]
    [InlineData(false, true)]
    [InlineData(true, false)]
    [InlineData(true, true)]
    public void AllDefaultVariablesAndFragmentVariablesKeepClientAndTestContractsAligned(bool inline, bool fragments)
    {
        using var fixture = new Fixture(new()
        {
            ["inlineDefaultVariables"] = inline, ["omitUnusedVariables"] = true,
            ["generateTests"] = true, ["generateSample"] = true
        });
        File.WriteAllText(Path.Combine(fixture.Root, "ops", "query.graphql"), fragments
            ? "query Products($limit: Int = 5) { ...Root } fragment Root on Query { products(limit: $limit) { id } }"
            : "query Products($limit: Int = 5) { products(limit: $limit) { id } }");
        fixture.Generate();
        fixture.Compile();
    }

    [Theory]
    [InlineData("generateSchemaTypes")]
    [InlineData("generateUnionConverters")]
    public void RemovedArtifactSwitchesAreRejected(string option)
    {
        using var fixture = new Fixture(new() { [option] = false });
        Assert.Throws<ConfigurationException>(() => fixture.Generate());
        Assert.False(Directory.Exists(Path.Combine(fixture.Root, "Generated")));
    }

    [Theory]
    [InlineData("scalarMappings", "List<")]
    [InlineData("scalarSampleExpressions", "new decimal(")]
    public void InvalidConfiguredSyntaxFailsAtTheBoundary(string option, string value)
    {
        using var fixture = new Fixture(new()
        {
            [option] = new Dictionary<string, string> { ["Money"] = value },
            ["generateSample"] = true
        });
        File.WriteAllText(Path.Combine(fixture.Root, "schema.graphql"), "scalar Money type Query { price(amount: Money): Money }");
        File.WriteAllText(Path.Combine(fixture.Root, "ops", "query.graphql"), "query Price($amount: Money) { price(amount: $amount) }");
        var error = Assert.Throws<ConfigurationException>(() => fixture.Generate());
        Assert.Contains(value, error.Message);
    }

    [Fact]
    public async Task ConcurrentConfigurationsRemainIndependent()
    {
        await Task.WhenAll(Enumerable.Range(0, 4).Select(index => Task.Run(() =>
        {
            using var fixture = new Fixture(new()
            {
                ["clientClassName"] = "ConcurrentClient" + index,
                ["indentSize"] = index + 1,
                ["useHttpGet"] = index % 2 == 0,
                ["generateTests"] = true
            });
            var first = fixture.Generate().GeneratedFiles.Where(path => path.EndsWith(".cs", StringComparison.Ordinal))
                .ToDictionary(path => path, File.ReadAllText);
            fixture.Generate();
            foreach (var (path, source) in first) Assert.Equal(source, File.ReadAllText(path));
            fixture.Compile();
        }, TestContext.Current.CancellationToken)));
    }

    [Theory]
    [InlineData(1, "ExecuteAsync_Falls_Back_To_Post_For_Long_Query_Url")]
    [InlineData(2048, "ExecuteAsync_Uses_Get_For_Query")]
    public async Task GeneratedTransportTestsRespectTheClientsGetLengthPolicy(int limit, string method)
    {
        using var fixture = new Fixture(new() { ["useHttpGet"] = true, ["maxGetUrlLength"] = limit, ["generateTests"] = true });
        fixture.Generate();
        var assembly = System.Reflection.Assembly.Load(fixture.Compile());
        var type = assembly.GetType("Matrix.Tests.GraphQLClientPayloadTests", true)!;
        await (Task)type.GetMethod(method)!.Invoke(Activator.CreateInstance(type), null)!;
    }

    private sealed class Fixture : IDisposable
    {
        public string Root { get; } = Path.Combine(Path.GetTempPath(), "salep_roslyn_" + Guid.NewGuid().ToString("N"));
        public string ConfigPath => Path.Combine(Root, "salep.json");

        public Fixture(Dictionary<string, object> settings)
        {
            Directory.CreateDirectory(Path.Combine(Root, "ops"));
            File.WriteAllText(Path.Combine(Root, "schema.graphql"), """
                scalar DateTime
                scalar Money
                enum Status { IN_STOCK SOLD }
                input Filter { name: String, statuses: [Status!] }
                type Product { id: ID!, name: String, price: Money!, updated: DateTime, tags: [String] }
                type Query { products(filter: Filter, limit: Int): [Product!]! }
                type Mutation { rename(id: ID!, name: String!): Product }
                """);
            File.WriteAllText(Path.Combine(Root, "ops", "query.graphql"), """
                query Products($filter: Filter, $limit: Int = 5) { products(filter: $filter, limit: $limit) { id name price updated tags } }
                mutation Rename($id: ID!, $name: String!) { rename(id: $id, name: $name) { id name } }
                """);
            var config = new Dictionary<string, object>
            {
                ["schemaPath"] = "schema.graphql", ["operationsPath"] = "ops",
                ["outputDirectory"] = "Generated", ["testsOutputDirectory"] = "Tests",
                ["generatedNamespace"] = "Matrix.Generated", ["testsNamespace"] = "Matrix.Tests",
                ["generateTests"] = false, ["generateSample"] = false,
                ["emitAgentInstructions"] = false,
                ["scalarMappings"] = new Dictionary<string, string> { ["Money"] = "decimal" }
            };
            foreach (var (key, value) in settings) config[key] = value;
            FixtureConfiguration.Write(ConfigPath, JsonSerializer.Serialize(config));
        }

        public SalepGeneratorResult Generate() => FixtureConfiguration.Generate(new(ConfigPath, WorkingDirectory: Root));

        public byte[] Compile()
        {
            var options = RoslynEmitter.GetParseOptions(GeneratorConfig.Load(ConfigPath));
            var sources = Directory.GetFiles(Root, "*.cs", SearchOption.AllDirectories)
                .Select(path => CSharpSyntaxTree.ParseText(File.ReadAllText(path), options, path));
            var references = ((string)AppContext.GetData("TRUSTED_PLATFORM_ASSEMBLIES")!).Split(Path.PathSeparator)
                .Select(path => MetadataReference.CreateFromFile(path));
            var compilation = CSharpCompilation.Create("ConfigFixture" + Guid.NewGuid().ToString("N"), sources, references,
                new CSharpCompilationOptions(OutputKind.DynamicallyLinkedLibrary, nullableContextOptions: NullableContextOptions.Enable));
            using var stream = new MemoryStream();
            var result = compilation.Emit(stream);
            Assert.True(result.Success, string.Join(Environment.NewLine, result.Diagnostics));
            return stream.ToArray();
        }

        public void Dispose() => Directory.Delete(Root, true);
    }
}
