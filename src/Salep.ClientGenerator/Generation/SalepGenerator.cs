using System.Collections.Immutable;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Salep.GraphQLParser;
using Salep.ClientGenerator.Config;
using Salep.ClientGenerator.Diagnostics;
using Salep.ClientGenerator.Emission;
using Salep.ClientGenerator.Model;
using Salep.ClientGenerator.Operations;
using Salep.ClientGenerator.Utilities;

namespace Salep.ClientGenerator.Generation;

public static class SalepGenerator
{
    public static SalepGeneratorResult Generate(SalepGeneratorOptions options)
    {
        var plan = Prepare(options);
        plan.Publish();
        return Result(plan);
    }

    public static SalepGeneratorResult Validate(SalepGeneratorOptions options)
    {
        var path = GeneratorConfig.ResolveConfigPath(options.ConfigPath, options.WorkingDirectory ?? Environment.CurrentDirectory);
        var config = ConfigurationResolver.Resolve(path);
        if (config.Kind == "profile") return new([], [], [$"Validated profile '{path}'."]);
        var plan = Prepare(options);
        plan.CheckOwnership();
        return Result(plan);
    }

    internal static GenerationPlan Prepare(SalepGeneratorOptions options)
    {
        var path = GeneratorConfig.ResolveConfigPath(options.ConfigPath, options.WorkingDirectory ?? Environment.CurrentDirectory);
        var config = ConfigurationResolver.Resolve(path);
        var plan = config.Kind switch
        {
            "client" => PrepareClient(config),
            "tests" => PrepareTests(config),
            _ => throw ConfigurationResolver.Error("SALEP1004", path, "kind", "Profiles supply defaults and cannot generate output.", "Select a client or tests configuration.")
        };
        if (options.Environment is { } environment)
        {
            if (plan.Manifest.Settings.UseNativeUnions && (environment.TargetFramework?.StartsWith("net11.", StringComparison.Ordinal) != true || environment.LanguageVersion != "preview"))
                throw ConfigurationResolver.Error("SALEP3001", path, "targetFramework", "Native unions require net11.0 and LangVersion=preview.", "Update the consuming project or select Dunet in the client configuration.");
            var available = environment.ReferencedConfigurations.Select(p => Path.GetFullPath(p, options.WorkingDirectory ?? System.Environment.CurrentDirectory)).ToHashSet(ConfigurationResolver.PathComparer);
            foreach (var dependency in plan.Manifest.Dependencies.Keys)
                if (!available.Contains(dependency)) throw ConfigurationResolver.Error("SALEP3002", path, "projectReference", $"Client '{dependency}' is not available through the project-reference chain.", "Add the appropriate ProjectReference; Salep does not modify project files.");
        }
        return plan;
    }

    private static SalepGeneratorResult Result(GenerationPlan plan) => new(
        plan.Outputs.Keys.Select(name => Path.Combine(plan.Configuration.Output, name)).ToArray(), plan.Warnings,
        [$"Validated {plan.Configuration.Kind} contract '{plan.Configuration.Path}'.", $"Manifest: {Path.Combine(plan.Configuration.Output, GenerationManifest.FileName)}"]);

    private static GenerationPlan PrepareClient(ResolvedConfiguration config)
    {
        GenerationPlan? parent = null;
        if (config.BaseClient is not null)
        {
            parent = PrepareClient(ConfigurationResolver.Resolve(config.BaseClient));
            parent.VerifyExisting();
            CheckOutputSeparation(config, parent);
        }
        var inputs = Inputs(config);
        var document = ParseSchema(config);
        var loaded = OperationLoader.Load(config.Operations);
        var settings = config.Settings;
        var inherited = parent?.Manifest.Symbols ?? ImmutableSortedDictionary<string, SymbolContract>.Empty;
        if (parent is not null)
        {
            if (settings.UseNativeUnions != parent.Manifest.Settings.UseNativeUnions)
                throw Conflict(config, "unionRepresentation", settings.UnionRepresentation, parent.Manifest.Settings.UnionRepresentation, parent.Configuration.Path);
            foreach (var name in settings.Scalars.Keys.Union(parent.Manifest.Settings.Scalars.Keys, StringComparer.Ordinal))
            {
                var actual = settings.Scalars.GetValueOrDefault(name) ?? new ScalarDefinition("string", false);
                var expected = parent.Manifest.Settings.Scalars.GetValueOrDefault(name) ?? new ScalarDefinition("string", false);
                if (actual.Type != expected.Type || actual.IsValueType != expected.IsValueType)
                    throw Conflict(config, "scalars." + name, $"{actual.Type} (isValueType={actual.IsValueType})", $"{expected.Type} (isValueType={expected.IsValueType})", parent.Configuration.Path);
            }
        }
        var symbols = inherited.ToBuilder();
        var localTypes = new HashSet<string>(StringComparer.Ordinal);
        var typeOwners = settings.TypeOwners.ToBuilder();
        var rawSchema = SchemaModel.FromDocument(document, settings);
        foreach (var definition in document.Definitions)
        {
            var name = DefinitionName(definition);
            if (name is null) continue;
            var key = "schema:" + name;
            var signature = GenerationManifest.Hash(definition.ToString());
            if (definition is InterfaceTypeDefinitionNode)
                signature = GenerationManifest.Hash(signature + string.Join("|", rawSchema.GetInterfaceImplementations(name).Select(t => t.Name.Value).Order(StringComparer.Ordinal)));
            if (inherited.TryGetValue(key, out var owner))
            {
                if (owner.Signature != signature) throw Conflict(config, key, signature, owner.Signature, owner.Owner);
                var csName = definition is InterfaceTypeDefinitionNode ? CSharpNaming.ToInterfaceName(name) : CSharpNaming.ToTypeName(name);
                typeOwners[csName] = owner.Namespace;
                if (definition is InterfaceTypeDefinitionNode) typeOwners[SchemaModel.GetInterfaceResultTypeName(name)] = owner.Namespace;
            }
            else
            {
                localTypes.Add(name);
                symbols[key] = new(definition.Kind.ToString(), name, settings.GeneratedNamespace, config.Path, signature);
            }
        }
        var sharedNamespace = parent?.Manifest.Settings.SharedNamespace ?? settings.GeneratedNamespace;
        var registries = parent?.Manifest.Settings.ConverterRegistries ?? ImmutableArray<string>.Empty;
        // Each client owns a registry for its own converters; reference registries are registered explicitly.
        registries = registries.Add("global::" + settings.GeneratedNamespace + ".UnionJsonConverters");
        settings = settings with { TypeOwners = typeOwners.ToImmutable(), SharedNamespace = sharedNamespace, ConverterRegistries = registries };
        var schema = SchemaModel.FromDocument(document, settings);
        var operations = new List<OperationDefinitionNode>();
        foreach (var operation in loaded.Operations)
        {
            var name = operation.Name?.Value;
            if (string.IsNullOrWhiteSpace(name)) throw ConfigurationResolver.Error("SALEP1005", config.Path, "operations", "Generated operations must be named.");
            var key = "operation:" + name;
            var signature = GenerationManifest.Hash(new OperationVariablePolicies(settings, loaded.Fragments).Apply(operation).ToString()
                + string.Join("\n", loaded.Fragments.Select(f => f.ToString()).Order(StringComparer.Ordinal)));
            if (symbols.TryGetValue(key, out var owner))
            {
                if (owner.Signature != signature) throw Conflict(config, key, signature, owner.Signature, owner.Owner);
                continue;
            }
            symbols[key] = new("operation", name, settings.GeneratedNamespace, config.Path, signature);
            operations.Add(operation);
        }
        if (parent is null)
            foreach (var name in new[] { "IGraphQLOperation", "GraphQLRequest", "GraphQLResponse", "GraphQLError", "GraphQLErrorLocation" })
                symbols["shared:" + name] = new("shared", name, settings.GeneratedNamespace, config.Path, "v1");
        var contract = "global::" + sharedNamespace + ".IGraphQLOperation";
        var emitter = new GraphQlClientEmitter(settings, contract, loaded.Fragments, true);
        if (config.EmitSample) ValidateSamples(config, schema, operations, loaded.Fragments);
        var outputs = ImmutableSortedDictionary.CreateBuilder<string, string>(StringComparer.Ordinal);
        if (localTypes.Count > 0) outputs["SchemaTypes.cs"] = new SchemaTypesEmitter(schema, localTypes).Generate();
        outputs["Operations.cs"] = new OperationsEmitter(schema, operations, loaded.Fragments, contract).Generate();
        outputs["GraphQLClient.cs"] = emitter.GenerateClient(operations);
        outputs["UnionJsonConverters.cs"] = new UnionJsonConvertersEmitter(schema, localTypes).Generate();
        if (parent is null) outputs["GraphQLSharedTypes.cs"] = emitter.GenerateSharedTypes(true, true);
        if (config.EmitSample) outputs["Operations.Sample.cs"] = emitter.GenerateSample(operations, schema);
        if (config.EmitAgentInstructions) outputs["agents.md"] = AgentInstructionsEmitter.GenerateClientInstructions(settings);
        var dependencies = parent is null ? ImmutableSortedDictionary<string, string>.Empty : parent.Manifest.Dependencies.SetItem(parent.Configuration.Path, parent.Manifest.Fingerprint);
        // Inventory the same rendered syntax that will be published, including registries,
        // operation wrappers, and transport types, so ownership never predicts absent files.
        foreach (var source in outputs.Where(pair => pair.Key.EndsWith(".cs", StringComparison.Ordinal)))
        {
            var root = CSharpSyntaxTree.ParseText(source.Value, RoslynEmitter.GetParseOptions(settings)).GetCompilationUnitRoot();
            foreach (var declaration in root.Members.OfType<BaseNamespaceDeclarationSyntax>().SelectMany(n => n.Members))
            {
                var name = declaration switch
                {
                    BaseTypeDeclarationSyntax type => type.Identifier.ValueText,
                    _ => null
                };
                if (name is null) continue;
                var key = "symbol:" + settings.GeneratedNamespace + "." + name;
                if (symbols.TryGetValue(key, out var existing))
                    throw Conflict(config, key, "duplicate generated symbol", existing.Signature, existing.Owner);
                symbols[key] = new("CSharp", name, settings.GeneratedNamespace, config.Path, GenerationManifest.Hash(declaration.ToString()));
            }
        }
        var manifest = Manifest(config, settings, inputs, dependencies, symbols.ToImmutable(), outputs.ToImmutable());
        return new(config, manifest, outputs.ToImmutable(), DiagnosticsReporter.CollectWarnings(schema, operations).ToImmutableArray());
    }

    private static GenerationPlan PrepareTests(ResolvedConfiguration config)
    {
        var client = PrepareClient(ConfigurationResolver.Resolve(config.Client!));
        client.VerifyExisting();
        CheckOutputSeparation(config, client);
        var settings = client.Manifest.Settings with { TestsNamespace = config.Settings.TestsNamespace, IndentSize = config.Settings.IndentSize, UseRawStrings = config.Settings.UseRawStrings };
        var schema = SchemaModel.FromDocument(ParseSchema(config), settings);
        var loaded = OperationLoader.Load(config.Operations);
        var operations = loaded.Operations.Where(op => op.Name is not null && client.Manifest.Symbols.TryGetValue("operation:" + op.Name.Value, out var symbol) && ConfigurationResolver.PathComparer.Equals(symbol.Owner, client.Configuration.Path)).ToArray();
        ValidateSamples(config, schema, operations, loaded.Fragments);
        var emitter = new TestsEmitter(settings, "global::" + settings.SharedNamespace + ".IGraphQLOperation", loaded.Fragments);
        var outputs = ImmutableSortedDictionary.CreateBuilder<string, string>(StringComparer.Ordinal);
        if (config.Suites.Contains("transport") || config.Suites.Contains("operations") || config.Suites.Contains("samples")) outputs["TestHttpMessageHandler.cs"] = emitter.GenerateHttpHandler();
        if (config.Suites.Contains("transport")) outputs["GraphQLClientPayloadTests.cs"] = emitter.GeneratePayloadTests();
        if (config.Suites.Contains("operations"))
        {
            outputs["OperationsResponseTests.cs"] = emitter.GenerateOperationsResponseTests(operations, schema);
            outputs["OperationsMetadataTests.cs"] = emitter.GenerateOperationsMetadataTests(operations);
        }
        if (config.Suites.Contains("unions")) outputs["UnionConverterTests.cs"] = emitter.GenerateUnionConverterTests(schema);
        if (config.Suites.Contains("samples")) outputs["OperationsSampleTests.cs"] = emitter.GenerateOperationsSampleTests();
        if (config.EmitAgentInstructions) outputs["agents.md"] = AgentInstructionsEmitter.GenerateTestInstructions(settings);
        var dependencies = client.Manifest.Dependencies.SetItem(client.Configuration.Path, client.Manifest.Fingerprint);
        var manifest = Manifest(config, settings, Inputs(config), dependencies, ImmutableSortedDictionary<string, SymbolContract>.Empty, outputs.ToImmutable());
        return new(config, manifest, outputs.ToImmutable(), DiagnosticsReporter.CollectWarnings(schema, operations).ToImmutableArray());
    }

    private static GenerationManifest Manifest(ResolvedConfiguration config, GeneratorConfig settings,
        ImmutableSortedDictionary<string, string> inputs, ImmutableSortedDictionary<string, string> dependencies,
        ImmutableSortedDictionary<string, SymbolContract> symbols, ImmutableSortedDictionary<string, string> outputs)
        => new GenerationManifest
        {
            Configuration = config.Path, ConfigurationIdentity = Path.GetRelativePath(config.Output, config.Path), Kind = config.Kind, Output = config.Output, Settings = settings, EmitsSample = config.EmitSample,
            RequiredFramework = settings.UseNativeUnions ? "net11.0" : "net10.0", RequiredLanguage = settings.UseNativeUnions ? "preview" : "14",
            Inputs = inputs, Dependencies = dependencies, Symbols = symbols,
            Files = outputs.ToImmutableSortedDictionary(pair => pair.Key, pair => GenerationManifest.Hash(pair.Value), StringComparer.Ordinal)
        }.Seal();

    private static ImmutableSortedDictionary<string, string> Inputs(ResolvedConfiguration config)
    {
        var files = config.ConfigurationFiles.Add(config.Schema).AddRange(OperationLoader.ResolveOperationFiles(config.Operations));
        var inputs = ImmutableSortedDictionary.CreateBuilder<string, string>(StringComparer.Ordinal);
        foreach (var file in files.Distinct(ConfigurationResolver.PathComparer))
        {
            if (!File.Exists(file)) throw ConfigurationResolver.Error("SALEP1001", config.Path, "input", $"Input '{file}' does not exist.");
            inputs[file] = GenerationManifest.HashFile(file);
        }
        return inputs.ToImmutable();
    }
    private static DocumentNode ParseSchema(ResolvedConfiguration config)
    {
        if (!File.Exists(config.Schema)) throw ConfigurationResolver.Error("SALEP1001", config.Path, "schema", $"Schema '{config.Schema}' does not exist.");
        return Utf8GraphQLParser.Parse(File.ReadAllText(config.Schema), new ParserOptions(maxAllowedDirectives: 10_000));
    }
    private static string? DefinitionName(IDefinitionNode definition) => definition switch
    {
        ObjectTypeDefinitionNode d => d.Name.Value, InputObjectTypeDefinitionNode d => d.Name.Value,
        InterfaceTypeDefinitionNode d => d.Name.Value, UnionTypeDefinitionNode d => d.Name.Value,
        EnumTypeDefinitionNode d => d.Name.Value, _ => null
    };
    private static ConfigurationException Conflict(ResolvedConfiguration config, string property, string actual, string expected, string owner)
        => ConfigurationResolver.Error("SALEP2004", config.Path, property, $"Value '{actual}' conflicts with '{expected}' owned by '{owner}'.", "Align the shared contract or use an independent client.");

    private static void CheckOutputSeparation(ResolvedConfiguration config, GenerationPlan dependency)
    {
        foreach (var path in dependency.Manifest.Dependencies.Keys.Append(dependency.Configuration.Path))
        {
            var other = ConfigurationResolver.Resolve(path);
            var a = Path.GetFullPath(config.Output).TrimEnd(Path.DirectorySeparatorChar) + Path.DirectorySeparatorChar;
            var b = Path.GetFullPath(other.Output).TrimEnd(Path.DirectorySeparatorChar) + Path.DirectorySeparatorChar;
            var comparison = OperatingSystem.IsWindows() || OperatingSystem.IsMacOS() ? StringComparison.OrdinalIgnoreCase : StringComparison.Ordinal;
            if (a.StartsWith(b, comparison) || b.StartsWith(a, comparison))
                throw ConfigurationResolver.Error("SALEP2003", config.Path, "output", $"Output '{config.Output}' overlaps '{other.Output}' owned by '{path}'.", "Choose separate, non-nested output directories.");
        }
    }

    private static void ValidateSamples(ResolvedConfiguration config, SchemaModel schema, IReadOnlyList<OperationDefinitionNode> operations, IReadOnlyList<FragmentDefinitionNode> fragments)
    {
        var names = new HashSet<string>(StringComparer.Ordinal);
        var visited = new HashSet<string>(StringComparer.Ordinal);
        void VisitType(ITypeNode type, bool input = false) => VisitName(SyntaxNodeExtensions.NamedType(type).Name.Value, input);
        void VisitName(string name, bool input = false)
        {
            if (!visited.Add((input ? "input:" : "output:") + name)) return;
            names.Add(name);
            if (input && schema.InputTypes.TryGetValue(name, out var inputType))
                foreach (var field in inputType.Fields) VisitType(field.Type, true);
            if (!input && schema.ObjectTypes.TryGetValue(name, out var objectType))
                foreach (var field in objectType.Fields.Where(f => f.Type is NonNullTypeNode)) VisitType(field.Type);
            if (!input && schema.UnionTypes.TryGetValue(name, out var union))
                foreach (var member in union.Types) VisitName(member.Name.Value);
            if (!input && schema.InterfaceTypes.ContainsKey(name))
                foreach (var member in schema.GetInterfaceImplementations(name)) VisitName(member.Name.Value);
        }
        if (config.Kind == "client" || config.Suites.Contains("operations") || config.Suites.Contains("samples"))
            foreach (var operation in operations)
            {
                foreach (var variable in new OperationVariablePolicies(schema.Config, fragments).Apply(operation).VariableDefinitions) VisitType(variable.Type, true);
                if (config.Kind == "tests" && config.Suites.Contains("operations"))
                    foreach (var field in operation.SelectionSet.Selections.OfType<FieldNode>())
                        if (schema.GetFieldTypeNode(schema.GetRootTypeName(operation.Operation), field.Name.Value) is { } type) VisitType(type);
            }
        if (config.Kind == "tests" && config.Suites.Contains("unions"))
            foreach (var name in schema.UnionTypes.Keys.Concat(schema.InterfaceTypes.Keys)) VisitName(name);
        foreach (var name in names)
            if (schema.Config.Scalars.TryGetValue(name, out var scalar) && (scalar.SampleExpression is null || scalar.SampleJson is null))
                throw ConfigurationResolver.Error("SALEP1005", config.Path, "scalars." + name, "Generated samples/tests require sampleExpression and sampleJson for this scalar.", $"Complete the scalar definition in client '{config.Client ?? config.Path}'.");
    }
}
