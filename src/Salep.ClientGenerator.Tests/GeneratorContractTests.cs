using System.Text.Json;
using System.Text.RegularExpressions;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Salep.ClientGenerator.Generation;
using Xunit;

namespace Salep.ClientGenerator.Tests;

public sealed partial class GeneratorContractTests
{
    [Theory]
    [InlineData("MinimalDependencies", "native", "builtin", "net11.0", "preview")]
    [InlineData("Opinionated", "dunet", "nodatime", "net10.0", "latest")]
    [InlineData("Opinionated", "dunet", "nodatime", "net11.0", "latest")]
    public void Client_module_and_test_public_surfaces_match_verified_contracts(
        string sample,
        string unionRepresentation,
        string scalarPreset,
        string targetFramework,
        string languageVersion)
    {
        using var fixture = new ContractFixture(sample, unionRepresentation, scalarPreset, targetFramework, languageVersion);
        fixture.Generate();
        AssertContract($"Sample-{sample}-{unionRepresentation}-{targetFramework}", fixture.Outputs.Select(output => (output.Scriban, output.Tests)));

        var dependentClient = fixture.Outputs[1];
        Assert.False(File.Exists(Path.Combine(dependentClient.Scriban, "SchemaTypes.cs")));
        Assert.False(File.Exists(Path.Combine(dependentClient.Scriban, "GraphQLSharedTypes.cs")));
    }

    [Theory]
    [InlineData("native", "net11.0", "preview")]
    [InlineData("dunet", "net10.0", "latest")]
    public void Full_spec_coverage_client_and_operation_surfaces_match_verified_contracts(string unionRepresentation, string targetFramework, string languageVersion)
    {
        var repositoryRoot = Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, "../../../../"));
        var coverage = Path.Combine(repositoryRoot, "src/samples/Scriban/Opinionated/Client/schema.coverage.graphql");
        using var fixture = new ContractFixture("Coverage", unionRepresentation, "builtin", targetFramework, languageVersion, coverage, coverage);
        fixture.Generate();
        AssertContract($"Coverage-{unionRepresentation}-{targetFramework}", fixture.Outputs.Select(output => (output.Scriban, output.Tests)));


    }

    [Fact]
    public void Dependent_clients_reject_changed_schema_extensions_without_writing_outputs()
    {
        using var fixture = new ContractFixture("Opinionated", "dunet", "nodatime", "net10.0", "latest");
        var result = fixture.GenerateModulesAfterChangingSchemaExtension();
        Assert.EndsWith("2004", result.ScribanDiagnosticCode, StringComparison.Ordinal);
        Assert.False(Directory.Exists(result.ScribanModuleOutput));
    }

    [Fact]
    public void Dependent_clients_reject_modified_base_generated_files_without_writing_outputs()
    {
        using var fixture = new ContractFixture("Opinionated", "dunet", "nodatime", "net10.0", "latest");
        var result = fixture.GenerateModulesAfterModifyingBaseSchemaTypes();

        Assert.EndsWith("2002", result.ScribanDiagnosticCode, StringComparison.Ordinal);
        Assert.False(Directory.Exists(result.ScribanModuleOutput));
    }

    private static void AssertContract(string name, IEnumerable<(string Directory, bool Tests)> outputs)
    {
        var values = outputs.Select(output => new
        {
            Files = OwnedFileNames(output.Directory),
            Surface = output.Tests ? CSharpTestSurface(output.Directory) : CSharpSurface(output.Directory),
            Queries = output.Tests ? new SortedDictionary<string, string>(StringComparer.Ordinal)
                : new SortedDictionary<string, string>(OperationQueries(output.Directory).ToDictionary(item => item.Key, item => item.Value.ReplaceLineEndings("\n")), StringComparer.Ordinal)
        }).ToArray();
        var path = Path.Combine(AppContext.BaseDirectory, "Fixtures", "Contracts", name + ".json");
        var actual = JsonSerializer.SerializeToElement(values);
        using var expected = JsonDocument.Parse(File.ReadAllText(path));
        Assert.True(JsonElement.DeepEquals(expected.RootElement, actual),
            $"Generated contract differs from reviewed baseline '{name}'. Review API, query, inventory and generated test changes before updating the fixture.\nActual: {actual}");
    }

    private static SortedDictionary<string, string> OperationQueries(string directory)
    {
        var queries = new SortedDictionary<string, string>(StringComparer.Ordinal);
        foreach (var path in Directory.EnumerateFiles(directory, "*.cs", SearchOption.AllDirectories))
        {
            var root = CSharpSyntaxTree.ParseText(File.ReadAllText(path), cancellationToken: TestContext.Current.CancellationToken)
                .GetCompilationUnitRoot(TestContext.Current.CancellationToken);
            foreach (var operation in root.DescendantNodes().OfType<RecordDeclarationSyntax>()
                         .Where(type => type.Identifier.ValueText.EndsWith("Operation", StringComparison.Ordinal)))
            {
                var queryProperty = operation.Members.OfType<PropertyDeclarationSyntax>()
                    .SingleOrDefault(property => property.Identifier.ValueText == "Query");
                var expression = queryProperty?.ExpressionBody?.Expression ?? queryProperty?.Initializer?.Value;
                if (expression is LiteralExpressionSyntax { Token.ValueText: { } query } literal
                    && literal.IsKind(SyntaxKind.StringLiteralExpression))
                    queries[operation.Identifier.ValueText[..^"Operation".Length]] = query;
            }
        }

        return queries;
    }

    private static SortedSet<string> CSharpSurface(string directory)
    {
        var surface = new SortedSet<string>(StringComparer.Ordinal);
        foreach (var path in Directory.EnumerateFiles(directory, "*.cs", SearchOption.AllDirectories))
        {
            var source = File.ReadAllText(path);
            var declaredNamespace = Regex.Match(source, @"namespace\s+([A-Za-z_][A-Za-z0-9_.]*)").Groups[1].Value;
            if (declaredNamespace.Length > 0) source = source.Replace("global::" + declaredNamespace + ".", "", StringComparison.Ordinal);
            var root = CSharpSyntaxTree.ParseText(source, new CSharpParseOptions(LanguageVersion.Preview), cancellationToken: TestContext.Current.CancellationToken).GetCompilationUnitRoot(TestContext.Current.CancellationToken);
            foreach (var type in root.DescendantNodes().OfType<TypeDeclarationSyntax>().Where(type => type.Modifiers.Any(modifier => modifier.IsKind(SyntaxKind.PublicKeyword))))
            {
                var signature = new List<string>
                {
                    $"{type.Kind()}:{type.Identifier.ValueText}<{string.Join(",", type.TypeParameterList?.Parameters.Select(parameter => parameter.Identifier.ValueText) ?? [])}>:{string.Join(",", type.Modifiers.Where(token => token.IsKind(SyntaxKind.SealedKeyword) || token.IsKind(SyntaxKind.AbstractKeyword) || token.IsKind(SyntaxKind.StaticKeyword)).Select(token => token.ValueText))}:{string.Join(",", (type.BaseList?.Types.Select(item => NormalizeType(item.Type.ToString())) ?? []).Order(StringComparer.Ordinal))}:{NormalizeAttributes(type.AttributeLists)}:{NormalizeSource(string.Join(" ", type.ConstraintClauses))}"
                };
                if (type is RecordDeclarationSyntax { ParameterList: not null } record)
                {
                    signature.AddRange(record.ParameterList.Parameters.Select(parameter => $"property:{parameter.Identifier.ValueText}:{NormalizeType(parameter.Type?.ToString() ?? "")}:{NormalizeAttributes(parameter.AttributeLists)}:False:get,init:False"));
                    signature.Add($"constructor({string.Join(",", record.ParameterList.Parameters.Select(ParameterSignature))})");
                }

                if (type is not InterfaceDeclarationSyntax && type is not RecordDeclarationSyntax { ParameterList: not null }
                    && !type.Members.OfType<ConstructorDeclarationSyntax>().Any()) signature.Add("constructor()");
                foreach (var member in type.Members)
                {
                    switch (member)
                    {
                        case ConstructorDeclarationSyntax constructor when constructor.Modifiers.Any(modifier => modifier.IsKind(SyntaxKind.PublicKeyword)):
                            signature.Add($"constructor({string.Join(",", constructor.ParameterList.Parameters.Select(ParameterSignature))})");
                            break;
                        case PropertyDeclarationSyntax property when type is InterfaceDeclarationSyntax || property.Modifiers.Any(modifier => modifier.IsKind(SyntaxKind.PublicKeyword)):
                            signature.Add($"property:{property.Identifier.ValueText}:{NormalizeType(property.Type.ToString())}:{NormalizeAttributes(property.AttributeLists)}:{property.Modifiers.Any(token => token.IsKind(SyntaxKind.RequiredKeyword))}:{AccessorSignature(property)}:{property.Modifiers.Any(token => token.IsKind(SyntaxKind.StaticKeyword))}");
                            break;
                        case MethodDeclarationSyntax method when type is InterfaceDeclarationSyntax || method.Modifiers.Any(modifier => modifier.IsKind(SyntaxKind.PublicKeyword)):
                            signature.Add($"method:{NormalizeMethodName(method.Identifier.ValueText)}<{string.Join(",", method.TypeParameterList?.Parameters.Select(parameter => parameter.Identifier.ValueText) ?? [])}>({string.Join(",", method.ParameterList.Parameters.Select(parameter => ParameterSignature(parameter)))}):{NormalizeType(method.ReturnType.ToString())}:{NormalizeSource(string.Join(" ", method.ConstraintClauses))}:{NormalizeAttributes(method.AttributeLists)}:{method.Modifiers.Any(token => token.IsKind(SyntaxKind.StaticKeyword))}");
                            break;
                        case MemberDeclarationSyntax other when other.Modifiers.Any(modifier => modifier.IsKind(SyntaxKind.PublicKeyword))
                            && other is not BaseTypeDeclarationSyntax:
                            Assert.Fail($"Public member kind {other.Kind()} needs an explicit compatibility fingerprint: {other}");
                            break;
                    }
                }

                signature.Sort(StringComparer.Ordinal);
                surface.Add(string.Join("|", signature));
            }

            foreach (var enumType in root.DescendantNodes().OfType<EnumDeclarationSyntax>().Where(type => type.Modifiers.Any(modifier => modifier.IsKind(SyntaxKind.PublicKeyword))))
                surface.Add($"enum:{enumType.Identifier.ValueText}:{string.Join(",", enumType.Members.Select(member => $"{member.Identifier.ValueText}:{NormalizeAttributes(member.AttributeLists)}"))}");

            foreach (Match union in Regex.Matches(source, @"public union (?<name>[A-Za-z_][A-Za-z0-9_]*)\((?<cases>[^)]*)\)"))
                surface.Add($"union:{union.Groups["name"].Value}:{Regex.Replace(union.Groups["cases"].Value, @"\s+", "")}");
        }

        return surface;
    }

    private static SortedSet<string> CSharpTestSurface(string directory)
    {
        var surface = new SortedSet<string>(StringComparer.Ordinal);
        foreach (var path in Directory.EnumerateFiles(directory, "*.cs", SearchOption.AllDirectories))
        {
            var source = File.ReadAllText(path);
            foreach (Match method in Regex.Matches(source, @"public\s+(?:async\s+)?(?:Task|void)\s+(?<name>[A-Za-z_][A-Za-z0-9_]*)\s*\("))
                surface.Add(NormalizeMethodName(method.Groups["name"].Value));
        }

        return surface;
    }

    private static string[] OwnedFileNames(string directory)
        => Directory.EnumerateFiles(directory, "*", SearchOption.AllDirectories)
            .Select(path => Path.GetRelativePath(directory, path).Replace(Path.DirectorySeparatorChar, '/'))
            .Where(path => !path.EndsWith(".manifest.json", StringComparison.Ordinal)
                && !path.EndsWith(".lock", StringComparison.Ordinal))
            .Order(StringComparer.Ordinal)
            .ToArray();

    private static string ParameterSignature(ParameterSyntax parameter)
        => $"{NormalizeType(parameter.Type?.ToString() ?? "")}:{parameter.Identifier.ValueText}:{parameter.Modifiers}:{NormalizeSource(parameter.Default?.ToString() ?? "")}:{NormalizeAttributes(parameter.AttributeLists)}";

    private static string AccessorSignature(PropertyDeclarationSyntax property)
        => property.ExpressionBody is not null ? "get" : string.Join(",", property.AccessorList?.Accessors
            .Select(accessor => $"{accessor.Modifiers}{accessor.Keyword.ValueText}") ?? []);

    private static string NormalizeAttributes(SyntaxList<AttributeListSyntax> attributes)
        => string.Join(",", attributes.Select(attribute => NormalizeSource(attribute.ToString())));

    private static string NormalizeSource(string source)
        => Regex.Replace(source, @"\s+", " ").Trim();

    private static string NormalizeMethodName(string name)
        => name.Replace("Operation_Exposes_Operation_Metadata", "_Operation_Metadata", StringComparison.Ordinal)
            .Replace("Operation_Deserializes_Response", "_Deserializes_Response", StringComparison.Ordinal)
            .Replace("ExecuteAsync_Uses_Get_For_Query_Or_Falls_Back_To_Post", "ExecuteAsync_Uses_Get_For_Query", StringComparison.Ordinal)
            .Replace("GraphQLResponse_Allows_Null_List_Items", "Response_Allows_Null_List_Items", StringComparison.Ordinal);

    private static string NormalizeType(string type) => Regex.Replace(type, @"global::[A-Za-z_][A-Za-z0-9_.]*\.Parity\.Client\.", "");

    private sealed class ContractFixture : IDisposable
    {
        private readonly string root = Path.Combine(Path.GetTempPath(), "salep-contract-" + Guid.NewGuid().ToString("N"));
        private readonly string sample;
        private readonly string unionRepresentation;
        private readonly string scalarPreset;
        private readonly string targetFramework;
        private readonly string languageVersion;
        private readonly string schema;
        private readonly string clientOperations;
        private readonly string moduleOperations;
        private readonly string namespaceRoot;
        private readonly string scribanClientConfig;
        private readonly string scribanModuleConfig;
        private readonly string scribanClientTestsConfig;
        private readonly string scribanModuleTestsConfig;
        private readonly bool native;

        public IReadOnlyList<(string Scriban, bool Tests)> Outputs { get; }

        public ContractFixture(string sample, string unionRepresentation, string scalarPreset, string targetFramework, string languageVersion,
            string? schemaOverride = null, string? operationsOverride = null)
        {
            this.sample = sample;
            this.unionRepresentation = unionRepresentation;
            this.scalarPreset = scalarPreset;
            this.targetFramework = targetFramework;
            this.languageVersion = languageVersion;
            native = unionRepresentation == "native";
            Directory.CreateDirectory(root);
            var samples = Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, "../../../../src/samples"));
            schema = schemaOverride ?? Path.Combine(samples, "Salep.Samples.GraphQLServer/Generated/schema.graphql");
            var sampleDirectory = Path.Combine(samples, "Scriban", sample);
            clientOperations = operationsOverride ?? Path.Combine(sampleDirectory, "Client/graphql");
            moduleOperations = operationsOverride ?? Path.Combine(sampleDirectory, "Module/graphql");
            namespaceRoot = $"Salep.Samples.{sample}.Parity";
            scribanClientConfig = WriteConfig("scriban-client.json");
            scribanModuleConfig = WriteConfig("scriban-module.json");
            scribanClientTestsConfig = WriteConfig("scriban-client-tests.json");
            scribanModuleTestsConfig = WriteConfig("scriban-module-tests.json");
            Outputs =
            [
                (Path.Combine(root, "Scriban/Client"), false),
                (Path.Combine(root, "Scriban/Module"), false),
                (Path.Combine(root, "Scriban/ClientTests"), true),
                (Path.Combine(root, "Scriban/ModuleTests"), true)
            ];
        }

        public void Generate()
        {
            var scribanEnvironment = new ScribanGeneratorEnvironment(targetFramework, languageVersion, [scribanClientConfig]);
            ScribanGenerator.Generate(new(scribanClientConfig, root, native ? scribanEnvironment with { ReferencedConfigurations = [] } : null));

            ScribanGenerator.Generate(new(scribanModuleConfig, root, new(targetFramework, languageVersion, [scribanClientConfig])));

            ScribanGenerator.Generate(new(scribanClientTestsConfig, root));
            ScribanGenerator.Generate(new(scribanModuleTestsConfig, root));

            var scribanSnapshot = Snapshot(Path.Combine(root, "Scriban"));
            ScribanGenerator.Generate(new(scribanClientConfig, root, native ? scribanEnvironment with { ReferencedConfigurations = [] } : null));
            ScribanGenerator.Generate(new(scribanModuleConfig, root, new(targetFramework, languageVersion, [scribanClientConfig])));
            ScribanGenerator.Generate(new(scribanClientTestsConfig, root));
            ScribanGenerator.Generate(new(scribanModuleTestsConfig, root));
            Assert.Equal(scribanSnapshot, Snapshot(Path.Combine(root, "Scriban")));
        }

        public (string ScribanDiagnosticCode, string ScribanModuleOutput)
            GenerateModulesAfterChangingSchemaExtension()
        {
            ScribanGenerator.Generate(new(scribanClientConfig, root));
            var changedSchema = Path.Combine(root, "child-schema.graphql");
            File.WriteAllText(changedSchema, File.ReadAllText(schema) + "\nextend type User { parityAdded: String }\n");
            foreach (var configPath in new[] { scribanModuleConfig })
            {
                var config = JsonSerializer.Deserialize<Dictionary<string, object?>>(File.ReadAllText(configPath))!;
                config["schema"] = changedSchema;
                File.WriteAllText(configPath, JsonSerializer.Serialize(config));
            }
            var scribanError = Assert.Throws<ScribanConfigurationException>(() =>
                ScribanGenerator.Generate(new(scribanModuleConfig, root, new(targetFramework, languageVersion, [scribanClientConfig]))));
            return (scribanError.Diagnostic.Code, Outputs[1].Scriban);
        }

        public (string ScribanDiagnosticCode, string ScribanModuleOutput)
            GenerateModulesAfterModifyingBaseSchemaTypes()
        {
            var scribanEnvironment = new ScribanGeneratorEnvironment(targetFramework, languageVersion, [scribanClientConfig]);
            ScribanGenerator.Generate(new(scribanClientConfig, root, native ? scribanEnvironment with { ReferencedConfigurations = [] } : null));

            File.AppendAllText(Path.Combine(Outputs[0].Scriban, "SchemaTypes.cs"), "// modified after manifest creation");

            var scribanError = Assert.Throws<ScribanConfigurationException>(() =>
                ScribanGenerator.Generate(new(scribanModuleConfig, root, new(targetFramework, languageVersion, [scribanClientConfig]))));
            return (scribanError.Diagnostic.Code, Outputs[1].Scriban);
        }

        private static SortedDictionary<string, string> Snapshot(string directory)
            => new(Directory.EnumerateFiles(directory, "*", SearchOption.AllDirectories)
                .ToDictionary(path => Path.GetRelativePath(directory, path), File.ReadAllText, StringComparer.Ordinal), StringComparer.Ordinal);

        private string WriteConfig(string fileName)
        {
            var isModule = fileName.Contains("module", StringComparison.Ordinal);
            var isTests = fileName.Contains("tests", StringComparison.Ordinal);
            const string backend = "Scriban";
            var label = isModule ? "Module" : "Client";
            var configuration = new Dictionary<string, object?>(StringComparer.Ordinal)
            {
                ["version"] = 1,
                ["kind"] = isTests ? "tests" : "client",
                ["output"] = Path.Combine(backend, label + (isTests ? "Tests" : ""))
            };
            if (isTests)
            {
                configuration["client"] = isModule ? scribanModuleConfigPlaceholder : scribanClientConfigPlaceholder;
                configuration["namespace"] = $"{namespaceRoot}.{label}Tests";
            }
            else
            {
                configuration["schema"] = schema;
                configuration["operations"] = isModule ? moduleOperations : clientOperations;
                configuration["namespace"] = $"{namespaceRoot}.{label}";
                configuration["clientName"] = isModule ? "GraphQLModuleClient" : "GraphQLClient";
                configuration["emitAgentInstructions"] = false;
                configuration["unionRepresentation"] = unionRepresentation;
                configuration["scalarPreset"] = scalarPreset;
                configuration["omitUnusedVariables"] = true;
                configuration["inlineDefaultVariables"] = false;
                configuration["useHttpGet"] = true;
                configuration["enableBatching"] = true;
                configuration["maxGetUrlLength"] = 2048;
                if (isModule)
                    configuration["baseClient"] = scribanClientConfig;
            }

            var path = Path.Combine(root, fileName);
            File.WriteAllText(path, JsonSerializer.Serialize(configuration));
            return path;
        }

        private string scribanClientConfigPlaceholder => Path.Combine(root, "scriban-client.json");
        private string scribanModuleConfigPlaceholder => Path.Combine(root, "scriban-module.json");

        public void Dispose() => Directory.Delete(root, recursive: true);
    }
}
