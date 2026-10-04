using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Salep.ClientGenerator.Model;
using Salep.ClientGenerator.Targets;
using Salep.ClientGenerator.Templates;
using Salep.GraphQLParser;

namespace Salep.ClientGenerator.Generation;

/// <summary>Filesystem generation options for the Scriban backend.</summary>
public sealed record ScribanGeneratorEnvironment(string? TargetFramework, string? LanguageVersion, IReadOnlyList<string>? ReferencedConfigurations = null);
public sealed record ScribanGeneratorOptions(string? ConfigPath = null, string? WorkingDirectory = null, ScribanGeneratorEnvironment? Environment = null, IReadOnlyList<string>? AllowedReadRoots = null, string? SolutionDirectory = null);

/// <summary>A diagnostic emitted while resolving or running a Scriban generation contract.</summary>
public sealed record ScribanDiagnostic(string Code, string ConfigurationPath, string Property, string Message, string Guidance);

/// <summary>A failure in a Scriban generation contract.</summary>
public sealed class ScribanConfigurationException(ScribanDiagnostic diagnostic)
    : InvalidOperationException($"{diagnostic.Code}: {diagnostic.ConfigurationPath} [{diagnostic.Property}]: {diagnostic.Message} {diagnostic.Guidance}")
{
    public ScribanDiagnostic Diagnostic { get; } = diagnostic;
}

/// <summary>Result of validating or generating a Scriban configuration.</summary>
public sealed record ScribanGeneratorResult(IReadOnlyList<string> GeneratedFiles, IReadOnlyList<string> Logs);

/// <summary>Resolves version-one configuration, renders Scriban-backed output, and publishes owned files.</summary>
public static class ScribanGenerator
{
    public const string ManifestFileName = ".salep.manifest.json";

    public static ScribanGeneratorResult Validate(ScribanGeneratorOptions options)
    {
        var plan = Prepare(options);
        if (plan.Configuration.Kind == "profile") return new([], [$"Validated profile '{GeneratorPathPolicy.Normalize(plan.Configuration.ConfigurationPath)}'."]);
        CheckOwnership(plan);
        return new([], [$"Validated {plan.Configuration.Kind} configuration '{GeneratorPathPolicy.Normalize(plan.Configuration.ConfigurationPath)}'."]);
    }

    public static ScribanGeneratorResult Generate(ScribanGeneratorOptions options)
    {
        var plan = Prepare(options);
        if (plan.Configuration.Kind == "profile") throw Error("SALEPS1004", plan.Configuration.ConfigurationPath, "kind", "Profiles provide defaults and cannot generate output.", "Select a client or tests configuration.");
        Publish(plan);
        return new(plan.Outputs.Keys.Select(name => GeneratorPathPolicy.Normalize(Path.Combine(plan.Configuration.Output, name))).ToArray(),
            [$"Generated {plan.Outputs.Count} files from '{GeneratorPathPolicy.Normalize(plan.Configuration.ConfigurationPath)}'.", $"Manifest: {GeneratorPathPolicy.Normalize(Path.Combine(plan.Configuration.Output, ManifestFileName))}"]);
    }

    /// <summary>Returns configuration, schema, operation, and currently owned output paths for build tracking.</summary>
    public static IReadOnlyList<string> GetInputFiles(ScribanGeneratorOptions options)
    {
        var plan = Prepare(options);
        var paths = plan.Inputs.Keys.ToHashSet(PathComparer);
        paths.Add(typeof(ScribanGenerator).Assembly.Location);
        paths.Add(typeof(Salep.GraphQLParser.GraphQLParser).Assembly.Location);
        var manifestPath = Path.Combine(plan.Configuration.Output, ManifestFileName);
        paths.Add(manifestPath);
        foreach (var file in plan.Outputs.Keys) paths.Add(Path.Combine(plan.Configuration.Output, file));
        if (File.Exists(manifestPath))
        {
            var manifest = ReadManifest(manifestPath, plan.Configuration.ConfigurationPath, plan.Paths, required: true, verifyOutputs: false)!;
            foreach (var file in manifest.Files.Keys) paths.Add(Path.Combine(plan.Configuration.Output, file));
        }
        return paths.Select(GeneratorPathPolicy.Normalize).Order(PathComparer).ToArray();
    }

    private static Plan Prepare(ScribanGeneratorOptions options)
        => Prepare(options, new HashSet<string>(PathComparer));

    private static Plan Prepare(ScribanGeneratorOptions options, HashSet<string> configurationStack)
    {
        ArgumentNullException.ThrowIfNull(options);
        var working = Path.GetFullPath(options.WorkingDirectory ?? Environment.CurrentDirectory);
        var path = Path.GetFullPath(GeneratorPathPolicy.Normalize(options.ConfigPath ?? "salep.json"), working);
        if (!configurationStack.Add(path)) throw Error("SALEPS1002", path, "baseClient", "Client configuration reference cycle detected.");
        try
        {
        var paths = new GeneratorPathPolicy(options.SolutionDirectory ?? GeneratorPathPolicy.FindSolutionRoot(Path.GetDirectoryName(path)!), options.AllowedReadRoots);
        paths.Read(path);
        var resolved = Config.Resolve(path, new HashSet<string>(PathComparer), paths);
        paths.Write(resolved.Output, allowRoot: false);
        if (resolved.Kind == "profile")
        {
            var empty = new Dictionary<string, string>(StringComparer.Ordinal);
            var profileManifest = new Manifest { OutputDirectory = resolved.Output, Configuration = resolved.ConfigurationPath, ConfigurationIdentity = Path.GetRelativePath(resolved.Output, resolved.ConfigurationPath), Kind = resolved.Kind }.Seal();
            return new(resolved, empty, empty, profileManifest, paths);
        }
        Plan? baseClientPlan = null;
        Manifest? baseManifest = null;
        if (resolved.BaseClient is { } baseClientPath)
        {
            baseClientPlan = Prepare(new ScribanGeneratorOptions(baseClientPath, null, options.Environment, paths.ReadRoots), configurationStack);
            if (baseClientPlan.Configuration.Kind != "client") throw Error("SALEPS1004", path, "baseClient", "baseClient must reference a client configuration.");
            var parentManifestPath = Path.Combine(baseClientPlan.Configuration.Output, ManifestFileName);
            baseManifest = ReadManifest(parentManifestPath, baseClientPlan.Configuration.ConfigurationPath, baseClientPlan.Paths, required: true);
            if (baseManifest!.Fingerprint != CreateManifest(baseClientPlan).Fingerprint)
                throw Error("SALEPS2002", path, "baseClient", $"Base client '{GeneratorPathPolicy.Normalize(baseClientPlan.Configuration.ConfigurationPath)}' is stale.", "Regenerate the base client before generating this client.");
        }
        if (resolved.UseNativeUnions && options.Environment is { } environment
            && (environment.TargetFramework?.StartsWith("net11.", StringComparison.Ordinal) != true || environment.LanguageVersion != "preview"))
            throw Error("SALEPS3001", path, "targetFramework", "Native unions require net11.0 and LangVersion=preview.", "Update the consuming project or select Dunet in the Scriban configuration.");

        var schemaPath = resolved.Schema ?? throw Error("SALEPS1001", path, "schema", "A schema path is required for client generation.");
        if (!File.Exists(schemaPath)) throw Error("SALEPS1001", path, "schema", $"Schema '{GeneratorPathPolicy.Normalize(schemaPath)}' does not exist.");
        var operationFiles = ResolveOperationFiles(resolved.Operations, paths);
        if (operationFiles.Count == 0) throw Error("SALEPS1001", path, "operations", $"No GraphQL operation files matched '{GeneratorPathPolicy.Normalize(resolved.Operations)}'.");
        foreach (var input in operationFiles)
            if (!File.Exists(input)) throw Error("SALEPS1001", path, "operations", $"Operation input '{input}' does not exist.");

        try
        {
            var parserOptions = new ParserOptions(maxAllowedDirectives: 10_000);
            var schemaDocument = global::Salep.GraphQLParser.GraphQLParser.Parse(new SourceText(File.ReadAllText(schemaPath).AsMemory()), parserOptions);
            var operationDocuments = operationFiles.Select(file => global::Salep.GraphQLParser.GraphQLParser.Parse(new SourceText(File.ReadAllText(file).AsMemory()), parserOptions)).ToArray();
            var schema = GraphQlModelFactory.CreateSchema(schemaDocument);
            var executable = GraphQlModelFactory.CreateExecutable(schema, operationDocuments);
            var typeSignatures = SchemaSignatures(schemaDocument);
            var typeOwnersByGraphQlName = baseManifest is null
                ? new Dictionary<string, string>(StringComparer.Ordinal)
                : new Dictionary<string, string>(baseManifest.TypeOwners, StringComparer.Ordinal);
            var localTypeNames = new HashSet<string>(StringComparer.Ordinal);
            foreach (var type in typeSignatures)
            {
                if (baseManifest is not null && baseManifest.TypeSignatures.TryGetValue(type.Key, out var inheritedSignature))
                {
                    if (inheritedSignature != type.Value) throw Error("SALEPS2004", path, "schema." + type.Key, "Schema type conflicts with the type owned by the referenced base client.", "Keep shared schema definitions identical or use an independent client.");
                    typeOwnersByGraphQlName[type.Key] = baseManifest.TypeOwners.GetValueOrDefault(type.Key, baseClientPlan!.Configuration.Namespace);
                }
                else
                {
                    localTypeNames.Add(type.Key);
                    typeOwnersByGraphQlName[type.Key] = resolved.Namespace;
                }
            }
            var operationSignatures = baseManifest is null
                ? new Dictionary<string, string>(StringComparer.Ordinal)
                : new Dictionary<string, string>(baseManifest.OperationSignatures, StringComparer.Ordinal);
            var localOperations = new List<GraphQlOperationDefinition>();
            foreach (var operation in executable.Operations)
            {
                if (operation.Name is null) throw Error("SALEPS1005", path, "operations", "Generated operations must be named.");
                if (resolved.Kind == "tests")
                {
                    if (baseManifest!.OwnedOperationNames.Contains(operation.Name, StringComparer.Ordinal)) localOperations.Add(operation);
                    continue;
                }
                var signature = Hash(operation.ToString());
                if (operationSignatures.TryGetValue(operation.Name, out var inheritedSignature))
                {
                    if (inheritedSignature != signature) throw Error("SALEPS2004", path, "operations." + operation.Name, "Operation conflicts with a name owned by the referenced base client.", "Rename the operation or keep its definition identical.");
                    continue;
                }
                operationSignatures.Add(operation.Name, signature);
                localOperations.Add(operation);
            }
            executable = executable with { Operations = localOperations.ToArray() };
            var codeTypeOwners = new Dictionary<string, string>(StringComparer.Ordinal);
            var target = new CSharpCodeGenerationTarget(new CSharpCodeGenerationOptions { DefaultNamespace = resolved.Namespace });
            foreach (var owner in typeOwnersByGraphQlName)
            {
                codeTypeOwners[target.TypeName(owner.Key)] = owner.Value;
                if (schema.InterfaceTypes.Any(contract => contract.Name == owner.Key))
                {
                    codeTypeOwners[target.InterfaceName(owner.Key)] = owner.Value;
                    codeTypeOwners[target.TypeName(owner.Key) + "Result"] = owner.Value;
                }
            }
            var requiresNodaTime = resolved.Scalars.Values.Any(mapping => mapping.TypeName.StartsWith("NodaTime.", StringComparison.Ordinal)
                || mapping.TypeName.StartsWith("global::NodaTime.", StringComparison.Ordinal));
            target = new CSharpCodeGenerationTarget(new CSharpCodeGenerationOptions
            {
                DefaultNamespace = resolved.Namespace,
                UseNativeUnions = resolved.UseNativeUnions,
                ScalarMappings = resolved.Scalars,
                TypeOwners = codeTypeOwners,
                AdditionalImports = (baseManifest is null ? [] : new[] { baseManifest.SharedNamespace })
                    .Concat(requiresNodaTime ? ["NodaTime", "NodaTime.Serialization.SystemTextJson"] : [])
                    .ToArray()
            });
            if (baseManifest is not null && baseClientPlan!.Configuration.UseNativeUnions != resolved.UseNativeUnions)
                throw Error("SALEPS2004", path, "unionRepresentation", "Union representation conflicts with the referenced base client.", "Align both clients' union representation settings.");
            if (baseClientPlan is not null)
            {
                if (baseClientPlan.Configuration.OmitUnusedVariables != resolved.OmitUnusedVariables || baseClientPlan.Configuration.InlineDefaultVariables != resolved.InlineDefaultVariables)
                    throw Error("SALEPS2004", path, "operationVariables", "Operation variable policies conflict with the referenced base client.", "Align both clients' operation variable policies.");
                foreach (var scalar in baseClientPlan.Configuration.Scalars)
                    if (resolved.Scalars.TryGetValue(scalar.Key, out var actual) && actual != scalar.Value)
                        throw Error("SALEPS2004", path, "scalars." + scalar.Key, "Scalar mapping conflicts with the mapping owned by the referenced base client.", "Align shared scalar definitions or use an independent client.");
            }
            var converterRegistries = baseManifest is null
                ? new[] { "UnionJsonConverters" }
                : baseManifest.ConverterRegistries.Append("UnionJsonConverters").ToArray();
            var clientOptions = new CSharpClientGenerationOptions
            {
                ClientClassName = resolved.ClientName,
                OperationInterfaceTypeName = baseManifest is null ? "IGraphQLOperation" : "global::" + baseManifest.SharedNamespace + ".IGraphQLOperation",
                UseHttpGet = resolved.UseHttpGet,
                EnableBatching = resolved.EnableBatching,
                MaxGetUrlLength = resolved.MaxGetUrlLength,
                ConverterRegistries = converterRegistries,
                SerializerConfigurationStatements = requiresNodaTime
                    ? ["_jsonOptions.ConfigureForNodaTime(DateTimeZoneProviders.Tzdb);"]
                    : [],
                EmitOperationSample = resolved.EmitSample,
                EmitAgentInstructions = resolved.EmitAgentInstructions
            };
            var generator = new ScribanCSharpTemplateGenerator();
            var documentOptions = new GraphQlOperationDocumentOptions
            {
                OmitUnusedVariables = resolved.OmitUnusedVariables,
                InlineDefaultVariables = resolved.InlineDefaultVariables
            };
            var templateOverrides = resolved.TemplateOverrides.ToDictionary(
                pair => pair.Key,
                pair => File.ReadAllText(pair.Value),
                StringComparer.Ordinal);
            IReadOnlyList<CSharpGeneratedFile> generated;
            if (resolved.Kind == "tests")
            {
                generated = generator.GenerateTestSources(schema, executable, target,
                    clientOptions,
                    new CSharpTestGenerationOptions
                    {
                        Namespace = resolved.TestsNamespace,
                        Suites = resolved.Suites,
                        UseRawJsonLiterals = resolved.RawJsonLiterals,
                        EmitAgentInstructions = resolved.EmitAgentInstructions,
                        AdditionalImports = baseManifest is not null && baseManifest.SharedNamespace != resolved.Namespace ? [baseManifest.SharedNamespace] : []
                    },
                    documentOptions,
                    templateOverrides);
            }
            else
            {
                generated = generator.GenerateClientSources(schema, executable, target, clientOptions,
                    documentOptions, includeSharedTypes: baseClientPlan is null, localTypeNames: localTypeNames,
                    templateOverrides: templateOverrides);
            }
            var outputs = generated.ToDictionary(file => file.FileName, file => file.FileName.EndsWith(".cs", StringComparison.Ordinal) ? Reindent(file.Content, resolved.IndentSize) : file.Content, StringComparer.Ordinal);
            var inputPaths = operationFiles.Prepend(schemaPath).Concat(resolved.ConfigurationFiles)
                .Concat(resolved.TemplateOverrides.Values).Distinct(PathComparer).ToHashSet(PathComparer);
            var dependencies = baseManifest is null ? new Dictionary<string, string>(PathComparer) : new Dictionary<string, string>(baseManifest.Dependencies, PathComparer);
            if (baseManifest is not null)
            {
                dependencies[baseClientPlan!.Configuration.ConfigurationPath] = baseManifest.Fingerprint;
                foreach (var input in baseClientPlan.Inputs.Keys) inputPaths.Add(input);
                inputPaths.Add(Path.Combine(baseClientPlan.Configuration.Output, ManifestFileName));
                foreach (var file in baseManifest.Files.Keys) inputPaths.Add(Path.Combine(baseClientPlan.Configuration.Output, file));
            }
            foreach (var input in inputPaths) paths.Read(input);
            var inputs = inputPaths.ToDictionary(file => file, HashFile, PathComparer);
            var outputTypeSignatures = baseManifest is null ? new Dictionary<string, string>(StringComparer.Ordinal) : new Dictionary<string, string>(baseManifest.TypeSignatures, StringComparer.Ordinal);
            foreach (var localType in typeSignatures.Where(type => localTypeNames.Contains(type.Key))) outputTypeSignatures[localType.Key] = localType.Value;
            var outputOperationSignatures = baseManifest is null ? new Dictionary<string, string>(StringComparer.Ordinal) : new Dictionary<string, string>(baseManifest.OperationSignatures, StringComparer.Ordinal);
            if (resolved.Kind != "tests")
                foreach (var operation in localOperations)
                    if (operation.Name is { } name) outputOperationSignatures[name] = Hash(operation.ToString());
            var registryNames = baseManifest is null
                ? new[] { "global::" + resolved.Namespace + ".UnionJsonConverters" }
                : baseManifest.ConverterRegistries.Append("global::" + resolved.Namespace + ".UnionJsonConverters").Distinct(StringComparer.Ordinal).ToArray();
            var manifest = new Manifest
            {
                OutputDirectory = resolved.Output,
                Configuration = resolved.ConfigurationPath,
                ConfigurationIdentity = Path.GetRelativePath(resolved.Output, resolved.ConfigurationPath),
                Kind = resolved.Kind,
                Inputs = inputs,
                Dependencies = dependencies,
                TypeSignatures = outputTypeSignatures,
                TypeOwners = typeOwnersByGraphQlName,
                SharedNamespace = baseManifest?.SharedNamespace ?? resolved.Namespace,
                OperationSignatures = outputOperationSignatures,
                OwnedOperationNames = resolved.Kind == "tests" ? baseManifest?.OwnedOperationNames ?? [] : localOperations.Where(operation => operation.Name is not null).Select(operation => operation.Name!).ToArray(),
                ConverterRegistries = registryNames,
                Files = outputs.OrderBy(pair => pair.Key, StringComparer.Ordinal).ToDictionary(pair => pair.Key, pair => Hash(pair.Value), StringComparer.Ordinal)
            }.Seal();
            if (options.Environment?.ReferencedConfigurations is { } referenceConfigs)
            {
                var available = referenceConfigs.Select(reference => Path.GetFullPath(reference, working)).ToHashSet(PathComparer);
                foreach (var dependency in manifest.Dependencies.Keys)
                    if (!available.Contains(Path.GetFullPath(dependency)))
                        throw Error("SALEPS3002", path, "projectReference", $"Base client '{GeneratorPathPolicy.Normalize(dependency)}' is not available through the project-reference chain.", "Add the appropriate ProjectReference; Scriban does not modify project files.");
            }
            return new(resolved, outputs, inputs, manifest, paths);
        }
        catch (ScribanConfigurationException) { throw; }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException or ArgumentException or InvalidOperationException or FormatException or global::Scriban.Syntax.ScriptRuntimeException)
        {
            throw Error("SALEPS1006", path, "$", exception.Message, "Correct the reported input or output error and rerun 'salep validate'.");
        }
        }
        catch (InvalidDataException exception)
        {
            throw Error("SALEPS1007", path, "paths", exception.Message, "Keep paths within the solution; grant external read-only inputs using --read-root or SalepReadRoot.");
        }
        finally { configurationStack.Remove(path); }
    }

    private static void CheckOwnership(Plan plan, bool lockHeld = false)
    {
        var directory = plan.Configuration.Output;
        CheckOutputPaths(plan);
        using var guard = !lockHeld && Directory.Exists(directory)
            ? AcquireLock(Path.Combine(directory, ".salep.lock")) : null;
        var manifestPath = Path.Combine(directory, ManifestFileName);
        var current = ReadManifest(manifestPath, plan.Configuration.ConfigurationPath, plan.Paths, required: false, lockHeld: true, verifyOutputs: false);
        if (current is null && Directory.Exists(directory) && Directory.EnumerateFiles(directory, "*.cs").Any())
            throw Error("SALEPS2003", plan.Configuration.ConfigurationPath, "output", $"Output '{GeneratorPathPolicy.Normalize(directory)}' contains C# files without a Scriban ownership manifest.", "Choose an empty output directory or move unowned files before generating.");
        if (current is not null)
            foreach (var name in plan.Outputs.Keys)
                if (!current.Files.ContainsKey(name) && File.Exists(Path.Combine(directory, name)))
                    throw Error("SALEPS2003", plan.Configuration.ConfigurationPath, "output", $"Planned output '{name}' already exists without Scriban ownership.", "Move the unowned file or choose another output directory.");
    }

    private static void CheckOutputPaths(Plan plan)
    {
        try
        {
            var paths = plan.Paths;
            paths.Write(plan.Configuration.Output, allowRoot: false);
            foreach (var name in plan.Outputs.Keys)
            {
                if (!GeneratorPathPolicy.IsOutputName(name)) throw new InvalidDataException("Invalid generated output filename.");
                paths.Write(Path.Combine(plan.Configuration.Output, name));
            }
            foreach (var name in new[] { ManifestFileName, ".salep-scriban.manifest.json", ".salep.lock" })
                paths.Write(Path.Combine(plan.Configuration.Output, name));
        }
        catch (InvalidDataException exception)
        {
            throw Error("SALEPS1007", plan.Configuration.ConfigurationPath, "output", exception.Message);
        }
    }

    private static void Publish(Plan plan)
    {
        CheckOutputPaths(plan);
        Directory.CreateDirectory(plan.Configuration.Output);
        var lockPath = Path.Combine(plan.Configuration.Output, ".salep.lock");
        using var guard = AcquireLock(lockPath);
        CheckOwnership(plan, lockHeld: true);
        var manifestPath = Path.Combine(plan.Configuration.Output, ManifestFileName);
        var previous = ReadManifest(manifestPath, plan.Configuration.ConfigurationPath, plan.Paths, required: false, lockHeld: true, verifyOutputs: false);
        var manifest = CreateManifest(plan).Serialize();
        var stage = Path.Combine(plan.Configuration.Output, ".salep-stage-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(stage);
        try
        {
            foreach (var file in plan.Outputs) File.WriteAllText(Path.Combine(stage, file.Key), file.Value, new UTF8Encoding(false));
            File.WriteAllText(Path.Combine(stage, ManifestFileName), manifest, new UTF8Encoding(false));
            foreach (var file in plan.Outputs)
            {
                var destination = Path.Combine(plan.Configuration.Output, file.Key);
                if (!File.Exists(destination) || File.ReadAllText(destination) != file.Value)
                    File.Move(Path.Combine(stage, file.Key), destination, true);
            }
            if (previous is not null)
                foreach (var oldFile in previous.Files.Keys.Where(name => !plan.Outputs.ContainsKey(name)))
                    File.Delete(Path.Combine(plan.Configuration.Output, oldFile));
            if (!File.Exists(manifestPath) || File.ReadAllText(manifestPath) != manifest)
                File.Move(Path.Combine(stage, ManifestFileName), manifestPath, true);
            if (previous is not null && previous.SourceManifestPath != manifestPath)
                File.Delete(previous.SourceManifestPath);
        }
        finally { Directory.Delete(stage, true); }
    }

    private static Manifest? ReadManifest(string path, string configuration, GeneratorPathPolicy readPaths, bool required, bool lockHeld = false, bool verifyOutputs = true)
    {
        var directory = Path.GetDirectoryName(path)!;
        var outputPaths = readPaths;
        outputPaths.Write(path);
        outputPaths.Write(Path.Combine(directory, ".salep.lock"));
        outputPaths.Write(Path.Combine(directory, ".salep-scriban.manifest.json"));
        using var guard = !lockHeld && Directory.Exists(directory)
            ? AcquireLock(Path.Combine(directory, ".salep.lock")) : null;
        if (!File.Exists(path) && File.Exists(Path.Combine(directory, ".salep-scriban.manifest.json")))
            path = Path.Combine(directory, ".salep-scriban.manifest.json");
        if (!File.Exists(path))
        {
            if (required) throw Error("SALEPS2001", configuration, "manifest", $"Manifest '{GeneratorPathPolicy.Normalize(path)}' does not exist.");
            return null;
        }
        try
        {
            var text = File.ReadAllText(path);
            using var document = JsonDocument.Parse(text);
            var root = document.RootElement;
            Manifest? manifest;
            if (root.ValueKind == JsonValueKind.Object && root.TryGetProperty("Version", out var version)
                && version.ValueKind == JsonValueKind.Number && version.TryGetInt32(out var manifestVersion) && manifestVersion == 1)
            {
                // The released Roslyn package used this filename. Trust its ownership inventory
                // only after verifying the original sealed JSON, then replace it on publication.
                var unsigned = System.Text.Json.Nodes.JsonNode.Parse(text)!.AsObject();
                unsigned["Fingerprint"] = "";
                if (root.GetProperty("Fingerprint").GetString() != Hash(Manifest.Canonical(JsonSerializer.SerializeToElement(unsigned))))
                    throw new InvalidDataException("Legacy Roslyn manifest fingerprint is invalid.");
                manifest = new Manifest
                {
                    Version = 1, Configuration = root.GetProperty("Configuration").GetString()!,
                    ConfigurationIdentity = root.TryGetProperty("ConfigurationIdentity", out var identityValue) ? identityValue.GetString() ?? "" : "",
                    Fingerprint = root.GetProperty("Fingerprint").GetString()!,
                    Files = JsonSerializer.Deserialize<Dictionary<string, string>>(root.GetProperty("Files"))!
                };
            }
            else
            {
                manifest = JsonSerializer.Deserialize<Manifest>(text);
                if (manifest is null || manifest.Version != 2 || manifest.Fingerprint != manifest.Seal().Fingerprint)
                    throw new InvalidDataException("Manifest version or fingerprint is invalid.");
            }
            var identity = string.IsNullOrWhiteSpace(manifest.ConfigurationIdentity)
                ? Path.GetFullPath(GeneratorPathPolicy.Normalize(manifest.Configuration), directory)
                : Path.GetFullPath(GeneratorPathPolicy.Normalize(manifest.ConfigurationIdentity), Path.GetDirectoryName(path)!);
            if (!PathComparer.Equals(identity, Path.GetFullPath(configuration)))
                throw Error("SALEPS2003", configuration, "output", $"Output is already owned by '{GeneratorPathPolicy.Normalize(manifest.Configuration)}'.", "Use a separate output directory.");
            if (manifest.Files.Keys.Any(name => !GeneratorPathPolicy.IsOutputName(name)))
                throw new InvalidDataException("Manifest contains an invalid output path.");
            foreach (var name in manifest.Files.Keys) outputPaths.Write(Path.Combine(directory, name));
            foreach (var file in verifyOutputs ? manifest.Files : new Dictionary<string, string>())
                if (!File.Exists(Path.Combine(Path.GetDirectoryName(path)!, file.Key)) || HashFile(Path.Combine(Path.GetDirectoryName(path)!, file.Key)) != file.Value)
                    throw Error("SALEPS2002", configuration, "manifest", $"Owned output '{file.Key}' is missing or has been modified.", "Restore the generated file or clear the Scriban-owned output directory before regenerating.");
            var resolved = manifest.ResolvePaths(directory);
            readPaths.Read(resolved.Configuration);
            foreach (var input in resolved.Inputs.Keys.Concat(resolved.Dependencies.Keys)) readPaths.Read(input);
            return resolved with { SourceManifestPath = path };
        }
        catch (ScribanConfigurationException) { throw; }
        catch (Exception exception) when (exception is IOException or JsonException or InvalidDataException or ArgumentException or KeyNotFoundException or InvalidOperationException)
        {
            throw Error("SALEPS2001", configuration, "manifest", $"Cannot read verified manifest '{GeneratorPathPolicy.Normalize(path)}': {exception.Message}", "Generate from a clean output directory or restore the manifest and its files.");
        }
    }

    private static Manifest CreateManifest(Plan plan) => plan.Manifest;

    private static Dictionary<string, string> SchemaSignatures(DocumentNode document)
    {
        return document.Definitions
            .Select(definition => (Name: SchemaDefinitionName(definition), Definition: definition))
            .Where(item => item.Name is not null)
            .GroupBy(item => item.Name!, StringComparer.Ordinal)
            .ToDictionary(group => group.Key, group => Hash(string.Join("\n", group.Select(item => item.Definition.ToString()))), StringComparer.Ordinal);
    }

    private static string? SchemaDefinitionName(IDefinitionNode definition) => definition switch
    {
        ScalarTypeDefinitionNode item => item.Name.Value, ScalarTypeExtensionNode item => item.Name.Value,
        ObjectTypeDefinitionNode item => item.Name.Value, ObjectTypeExtensionNode item => item.Name.Value,
        InterfaceTypeDefinitionNode item => item.Name.Value, InterfaceTypeExtensionNode item => item.Name.Value,
        UnionTypeDefinitionNode item => item.Name.Value, UnionTypeExtensionNode item => item.Name.Value,
        EnumTypeDefinitionNode item => item.Name.Value, EnumTypeExtensionNode item => item.Name.Value,
        InputObjectTypeDefinitionNode item => item.Name.Value, InputObjectTypeExtensionNode item => item.Name.Value,
        _ => null
    };

    // Preserve raw literal contents and their closing delimiter indentation as one unit.
    private static string Reindent(string source, int indentSize)
    {
        if (indentSize == 4) return source;
        var lines = source.Split('\n');
        string? rawDelimiter = null;
        var preserveOtherRawLiteral = false;
        var rawIndent = 0;
        var rawOutputIndent = 0;
        for (var index = 0; index < lines.Length; index++)
        {
            var line = lines[index];
            var spaces = line.Length - line.TrimStart(' ').Length;
            var trimmed = line.Trim();
            if (rawDelimiter is not null)
            {
                // Move the common literal indentation without changing GraphQL indentation.
                if (spaces >= rawIndent)
                    lines[index] = new string(' ', rawOutputIndent) + line[rawIndent..];
                if (trimmed.StartsWith(rawDelimiter, StringComparison.Ordinal)) rawDelimiter = null;
                continue;
            }
            var quoteRuns = System.Text.RegularExpressions.Regex.Matches(line, "\"{3,}");
            if (preserveOtherRawLiteral)
            {
                if (quoteRuns.Count % 2 != 0) preserveOtherRawLiteral = false;
                continue;
            }
            if (quoteRuns.Count == 1 && trimmed == quoteRuns[0].Value)
            {
                rawDelimiter = trimmed;
                rawIndent = spaces;
                rawOutputIndent = spaces / 4 * indentSize + spaces % 4;
                lines[index] = new string(' ', rawOutputIndent) + line[spaces..];
            }
            else if (quoteRuns.Count % 2 != 0) preserveOtherRawLiteral = true;
            else if (quoteRuns.Count == 0)
                lines[index] = new string(' ', spaces / 4 * indentSize + spaces % 4) + line[spaces..];
        }
        return string.Join('\n', lines);
    }

    private static IReadOnlyList<string> ResolveOperationFiles(string path, GeneratorPathPolicy paths)
    {
        paths.ReadPattern(path);
        if (File.Exists(path)) return [paths.Read(path)];
        if (Directory.Exists(path)) return Directory.GetFiles(path, "*.graphql", SearchOption.TopDirectoryOnly).Order(StringComparer.Ordinal).Select(paths.Read).ToArray();
        var wildcard = path.IndexOfAny(['*', '?', '[', ']']);
        if (wildcard < 0) return [];
        var separator = path.LastIndexOfAny([Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar], wildcard);
        var baseDirectory = separator < 0 ? Environment.CurrentDirectory : path[..separator];
        var pattern = separator < 0 ? path : path[(separator + 1)..];
        if (string.IsNullOrWhiteSpace(baseDirectory) || string.IsNullOrWhiteSpace(pattern) || !Directory.Exists(baseDirectory)) return [];
        var matcher = new Microsoft.Extensions.FileSystemGlobbing.Matcher(StringComparison.OrdinalIgnoreCase);
        matcher.AddInclude(GeneratorPathPolicy.Normalize(pattern));
        CheckGlobTree(baseDirectory, paths);
        var result = matcher.Execute(new Microsoft.Extensions.FileSystemGlobbing.Abstractions.DirectoryInfoWrapper(new DirectoryInfo(baseDirectory)));
        return result.Files
            .Select(match => paths.Read(Path.Combine(baseDirectory, match.Path)))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .Order(StringComparer.Ordinal)
            .ToArray();
    }

    private static void CheckGlobTree(string directory, GeneratorPathPolicy paths)
    {
        foreach (var entry in Directory.EnumerateFileSystemEntries(directory))
        {
            paths.Read(entry);
            if (Directory.Exists(entry)) CheckGlobTree(entry, paths);
        }
    }

    private static FileStream AcquireLock(string path)
    {
        var until = DateTime.UtcNow.AddSeconds(30);
        while (true)
        {
            try { return new FileStream(path, FileMode.OpenOrCreate, FileAccess.ReadWrite, FileShare.None); }
            catch (IOException) when (DateTime.UtcNow < until) { Thread.Sleep(25); }
        }
    }

    private static string HashFile(string path) => Convert.ToHexString(SHA256.HashData(File.ReadAllBytes(path)));
    private static string Hash(string text) => Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(text)));
    private static StringComparer PathComparer => OperatingSystem.IsWindows() || OperatingSystem.IsMacOS() ? StringComparer.OrdinalIgnoreCase : StringComparer.Ordinal;
    private static ScribanConfigurationException Error(string code, string path, string property, string message, string guidance = "Correct the configuration and rerun 'salep validate'.") => new(new(code, GeneratorPathPolicy.Normalize(path), property, message, guidance));

    private sealed record Plan(Config Configuration, IReadOnlyDictionary<string, string> Outputs, IReadOnlyDictionary<string, string> Inputs, Manifest Manifest, GeneratorPathPolicy Paths);

    private sealed record Manifest
    {
        [System.Text.Json.Serialization.JsonIgnore]
        public string OutputDirectory { get; init; } = "";
        [System.Text.Json.Serialization.JsonIgnore]
        public string SourceManifestPath { get; init; } = "";
        public int Version { get; init; } = 2;
        public string Configuration { get; init; } = "";
        public string ConfigurationIdentity { get; init; } = "";
        public string Kind { get; init; } = "";
        public IReadOnlyDictionary<string, string> Inputs { get; init; } = new Dictionary<string, string>();
        public IReadOnlyDictionary<string, string> Dependencies { get; init; } = new Dictionary<string, string>();
        public string SharedNamespace { get; init; } = "";
        public IReadOnlyDictionary<string, string> TypeSignatures { get; init; } = new Dictionary<string, string>();
        public IReadOnlyDictionary<string, string> TypeOwners { get; init; } = new Dictionary<string, string>();
        public IReadOnlyDictionary<string, string> OperationSignatures { get; init; } = new Dictionary<string, string>();
        public IReadOnlyList<string> OwnedOperationNames { get; init; } = [];
        public IReadOnlyList<string> ConverterRegistries { get; init; } = [];
        public IReadOnlyDictionary<string, string> Files { get; init; } = new Dictionary<string, string>();
        public string Fingerprint { get; init; } = "";
        public Manifest Seal() => this with { Fingerprint = Hash(Canonical(JsonSerializer.SerializeToElement(ToPortable() with { Fingerprint = "" }))) };
        public string Serialize() => JsonSerializer.Serialize(ToPortable(), new JsonSerializerOptions { WriteIndented = true }) + "\n";
        private Manifest ToPortable()
        {
            if (string.IsNullOrEmpty(OutputDirectory)) return this;
            string Relative(string path)
            {
                var relative = Path.GetRelativePath(OutputDirectory, path);
                if (Path.IsPathRooted(relative))
                    throw new InvalidOperationException("Manifest paths must be on the same filesystem volume as the manifest output directory.");
                return relative.Replace('\\', '/');
            }
            return this with
            {
                OutputDirectory = "", Configuration = Relative(Configuration), ConfigurationIdentity = Relative(Configuration),
                Inputs = Inputs.OrderBy(pair => pair.Key, StringComparer.Ordinal).ToDictionary(pair => Relative(pair.Key), pair => pair.Value, StringComparer.Ordinal),
                Dependencies = Dependencies.OrderBy(pair => pair.Key, StringComparer.Ordinal).ToDictionary(pair => Relative(pair.Key), pair => pair.Value, StringComparer.Ordinal)
            };
        }
        public Manifest ResolvePaths(string directory)
        {
            string Absolute(string path) => Path.GetFullPath(GeneratorPathPolicy.Normalize(path), directory);
            return this with
            {
                OutputDirectory = directory, Configuration = Absolute(Configuration),
                Inputs = Inputs.ToDictionary(pair => Absolute(pair.Key), pair => pair.Value, PathComparer),
                Dependencies = Dependencies.ToDictionary(pair => Absolute(pair.Key), pair => pair.Value, PathComparer)
            };
        }
        public static string Canonical(JsonElement element) => element.ValueKind switch
        {
            JsonValueKind.Object => "{" + string.Join(",", element.EnumerateObject().OrderBy(property => property.Name, StringComparer.Ordinal).Select(property => JsonSerializer.Serialize(property.Name) + ":" + Canonical(property.Value))) + "}",
            JsonValueKind.Array => "[" + string.Join(",", element.EnumerateArray().Select(Canonical)) + "]",
            _ => element.GetRawText()
        };
    }

    private sealed record Config(string ConfigurationPath, string Kind, string? Schema, string Operations, string Output, string Namespace,
        string TestsNamespace, string ClientName, bool UseNativeUnions, bool OmitUnusedVariables, bool InlineDefaultVariables,
        bool UseHttpGet, bool EnableBatching, int MaxGetUrlLength, int IndentSize, bool RawJsonLiterals, bool EmitSample,
        bool EmitAgentInstructions, IReadOnlySet<string> Suites, IReadOnlyDictionary<string, CSharpScalarMapping> Scalars,
        IReadOnlyDictionary<string, CSharpScalarMapping> ScalarOverrides, string ScalarPreset,
        IReadOnlyList<string> ConfigurationFiles, IReadOnlyDictionary<string, string> TemplateOverrides,
        string? BaseClient = null)
    {
        public static Config Resolve(string path, HashSet<string> stack, GeneratorPathPolicy paths)
        {
            path = paths.Read(path);
            if (!stack.Add(path)) throw Error("SALEPS1002", path, "reference", "Configuration reference cycle detected.");
            try
            {
                if (!File.Exists(path)) throw Error("SALEPS1001", path, "$", "Configuration file does not exist.");
                using var document = ReadDocument(path);
                var root = document.RootElement;
                if (root.ValueKind != JsonValueKind.Object) throw Error("SALEPS1001", path, "$", "Expected a JSON object.");
                if (!root.TryGetProperty("version", out var version) || version.ValueKind != JsonValueKind.Number || !version.TryGetInt32(out var versionNumber) || versionNumber != 1)
                    throw Error("SALEPS1001", path, "version", "Expected configuration version 1.");
                var kind = GetString(root, "kind", path) ?? throw Error("SALEPS1001", path, "kind", "A kind is required.");
                if (kind is not ("profile" or "client" or "tests")) throw Error("SALEPS1001", path, "kind", $"Unknown kind '{kind}'.");
                var settingNames = new[] { "unionRepresentation", "scalarPreset", "scalars", "omitUnusedVariables", "inlineDefaultVariables", "useHttpGet", "enableBatching", "maxGetUrlLength", "indentSize", "templates" };
                var allowedNames = kind switch
                {
                    "profile" => new[] { "$schema", "version", "kind", "extends", "schema" }.Concat(settingNames),
                    "client" => new[] { "$schema", "version", "kind", "profile", "baseClient", "schema", "operations", "namespace", "clientName", "output", "emitSample", "emitAgentInstructions" }.Concat(settingNames),
                    _ => new[] { "$schema", "version", "kind", "client", "output", "namespace", "suites", "rawJsonLiterals", "indentSize", "emitAgentInstructions", "templates" }
                };
                var allowed = allowedNames.ToHashSet(StringComparer.Ordinal);
                CheckDuplicates(root, path, "$" );
                foreach (var property in root.EnumerateObject())
                {
                    if (!allowed.Contains(property.Name)) throw Error("SALEPS1003", path, property.Name, $"Property is not allowed for kind '{kind}'.", "Remove the unknown property or place it on its referenced client/profile.");
                }
                var directory = Path.GetDirectoryName(path)!;
                var localTemplateOverrides = ReadTemplateOverrides(root, path, directory, paths);
                Config? parent = null;
                var parentField = kind == "profile" ? "extends" : "profile";
                if (GetString(root, parentField, path) is { } reference)
                {
                    var parentPath = Path.GetFullPath(GeneratorPathPolicy.Normalize(reference), directory);
                    paths.Read(parentPath);
                    if (Directory.Exists(parentPath)) parentPath = Path.Combine(parentPath, "salep.json");
                    parent = Resolve(parentPath, stack, paths);
                    if (parent.Kind != "profile") throw Error("SALEPS1004", path, parentField, "The referenced configuration must have kind 'profile'.");
                }
                var schema = GetString(root, "schema", path) is { } schemaValue ? Path.GetFullPath(GeneratorPathPolicy.Normalize(schemaValue), directory) : parent?.Schema;
                var operations = GetString(root, "operations", path) is { } operationsValue ? Path.GetFullPath(GeneratorPathPolicy.Normalize(operationsValue), directory) : Path.Combine(directory, "graphql");
                var output = Path.GetFullPath(GeneratorPathPolicy.Normalize(GetString(root, "output", path) ?? (kind == "tests" ? "./GeneratedTests" : "./Generated")), directory);
                if (schema is not null) paths.Read(schema);
                paths.ReadPattern(operations);
                var clientName = GetString(root, "clientName", path) ?? parent?.ClientName ?? "GraphQLClient";
                var ns = GetString(root, "namespace", path) ?? parent?.Namespace ?? "Salep.Generated";
                var unionRepresentation = GetString(root, "unionRepresentation", path) ?? (parent?.UseNativeUnions == true ? "native" : "dunet");
                if (unionRepresentation is not ("native" or "dunet")) throw Error("SALEPS1001", path, "unionRepresentation", "Expected 'native' or 'dunet'.");
                ValidateNamespace(ns, path);
                ValidateIdentifier(clientName, path, "clientName");
                var emitSample = GetBool(root, "emitSample", false, path);
                var suites = root.TryGetProperty("suites", out var suiteElement)
                    ? suiteElement.ValueKind == JsonValueKind.Array
                        ? ReadSuites(suiteElement, path)
                        : throw Error("SALEPS1001", path, "suites", "Expected an array of suite names.")
                    : parent?.Suites ?? new HashSet<string>(emitSample ? ["transport", "operations", "unions", "samples"] : ["transport", "operations", "unions"], StringComparer.Ordinal);
                var scalarOverrides = new Dictionary<string, CSharpScalarMapping>(parent?.ScalarOverrides ?? new Dictionary<string, CSharpScalarMapping>(), StringComparer.Ordinal);
                var scalarPreset = GetString(root, "scalarPreset", path) ?? parent?.ScalarPreset ?? "builtin";
                if (scalarPreset is not ("builtin" or "nodatime")) throw Error("SALEPS1001", path, "scalarPreset", "Expected 'builtin' or 'nodatime'.");
                var scalars = new Dictionary<string, CSharpScalarMapping>(scalarOverrides, StringComparer.Ordinal);
                if (scalarPreset == "nodatime")
                {
                    var nodatime = new CSharpScalarMapping("NodaTime.Instant", true, "NodaTime.Instant.FromUtc(2020, 1, 1, 0, 0)", "\"2020-01-01T00:00:00Z\"");
                    if (!scalarOverrides.ContainsKey("DateTime")) scalars["DateTime"] = nodatime;
                    if (!scalarOverrides.ContainsKey("Instant")) scalars["Instant"] = nodatime;
                }
                if (root.TryGetProperty("scalars", out var scalarDefinitions))
                {
                    if (scalarDefinitions.ValueKind != JsonValueKind.Object) throw Error("SALEPS1001", path, "scalars", "Expected an object of scalar mappings.");
                    foreach (var scalar in scalarDefinitions.EnumerateObject())
                    {
                        var value = scalar.Value;
                        if (value.ValueKind != JsonValueKind.Object) throw Error("SALEPS1001", path, "scalars." + scalar.Name, "Expected a scalar mapping object.");
                        foreach (var field in value.EnumerateObject())
                            if (field.Name is not ("type" or "isValueType" or "sampleExpression" or "sampleJson")) throw Error("SALEPS1003", path, "scalars." + scalar.Name + "." + field.Name, "Unknown scalar mapping property.");
                        if (!value.TryGetProperty("isValueType", out _)) throw Error("SALEPS1001", path, "scalars." + scalar.Name, "isValueType is required.");
                        var type = GetString(value, "type", path) ?? throw Error("SALEPS1001", path, "scalars." + scalar.Name, "A C# type mapping is required.");
                        var valueType = GetBool(value, "isValueType", false, path);
                        var mapping = new CSharpScalarMapping(type, valueType, GetString(value, "sampleExpression", path), GetString(value, "sampleJson", path));
                        scalars[scalar.Name] = mapping;
                        scalarOverrides[scalar.Name] = mapping;
                    }
                }
                if (kind == "tests")
                {
                    var clientPathValue = GetString(root, "client", path) ?? throw Error("SALEPS1001", path, "client", "Tests configuration requires a client configuration path.");
                    var clientPath = Path.GetFullPath(GeneratorPathPolicy.Normalize(clientPathValue), directory);
                    paths.Read(clientPath);
                    if (Directory.Exists(clientPath)) clientPath = Path.Combine(clientPath, "salep.json");
                    var client = Resolve(clientPath, stack, paths);
                    if (client.Kind != "client") throw Error("SALEPS1004", path, "client", "Tests must reference a client configuration.");
                    var testsNamespace = GetString(root, "namespace", path) ?? client.Namespace + ".Tests";
                    ValidateNamespace(testsNamespace, path);
                    if (suites.Any(suite => suite is not ("transport" or "operations" or "unions" or "samples"))) throw Error("SALEPS1001", path, "suites", "Supported suites are transport, operations, unions, and samples.");
                    var testSuites = !root.TryGetProperty("suites", out _) && client.EmitSample
                        ? new HashSet<string>(suites.Append("samples"), StringComparer.Ordinal)
                        : suites;
                    if (testSuites.Contains("samples") && !client.EmitSample)
                        throw Error("SALEPS1005", path, "suites", "The client does not emit samples.");
                    return client with
                    {
                        ConfigurationPath = path,
                        Kind = "tests",
                        Output = output,
                        TestsNamespace = testsNamespace,
                        Suites = testSuites,
                        RawJsonLiterals = GetBool(root, "rawJsonLiterals", true, path),
                        IndentSize = GetInt(root, "indentSize", 4, path, 0, 16),
                        EmitAgentInstructions = GetBool(root, "emitAgentInstructions", true, path),
                        ConfigurationFiles = client.ConfigurationFiles.Append(path).Distinct(PathComparer).ToArray(),
                        TemplateOverrides = MergeTemplateOverrides(client.TemplateOverrides, localTemplateOverrides),
                        BaseClient = clientPath
                    };
                }
                var baseClient = GetString(root, "baseClient", path) is { } baseClientValue ? Path.GetFullPath(GeneratorPathPolicy.Normalize(baseClientValue), directory) : null;
                if (baseClient is not null) paths.Read(baseClient);
                if (baseClient is not null && Directory.Exists(baseClient)) baseClient = Path.Combine(baseClient, "salep.json");
                var resolved = new Config(path, kind, schema, operations, output, ns, ns + ".Tests", clientName,
                    unionRepresentation == "native",
                    GetBool(root, "omitUnusedVariables", parent?.OmitUnusedVariables ?? false, path), GetBool(root, "inlineDefaultVariables", parent?.InlineDefaultVariables ?? false, path),
                    GetBool(root, "useHttpGet", parent?.UseHttpGet ?? false, path), GetBool(root, "enableBatching", parent?.EnableBatching ?? false, path),
                    GetInt(root, "maxGetUrlLength", parent?.MaxGetUrlLength ?? 2048, path, 1, int.MaxValue),
                    GetInt(root, "indentSize", parent?.IndentSize ?? 4, path, 0, 16),
                    GetBool(root, "rawJsonLiterals", parent?.RawJsonLiterals ?? true, path), emitSample, GetBool(root, "emitAgentInstructions", true, path), suites, scalars,
                    scalarOverrides, scalarPreset,
                    (parent?.ConfigurationFiles ?? []).Append(path).Distinct(PathComparer).ToArray(),
                    MergeTemplateOverrides(parent?.TemplateOverrides, localTemplateOverrides), baseClient);
                if (kind == "client" && schema is null) throw Error("SALEPS1001", path, "schema", "Client configuration requires a schema path, locally or from a profile.");
                return resolved;
            }
            finally { stack.Remove(path); }
        }

        private static string? GetString(JsonElement element, string property, string path)
        {
            if (!element.TryGetProperty(property, out var value)) return null;
            if (value.ValueKind != JsonValueKind.String) throw Error("SALEPS1001", path, property, "Expected a string.");
            if (string.IsNullOrWhiteSpace(value.GetString())) throw Error("SALEPS1001", path, property, "Expected a non-empty string.");
            return value.GetString();
        }

        private static IReadOnlyDictionary<string, string> ReadTemplateOverrides(JsonElement root, string path, string directory, GeneratorPathPolicy paths)
        {
            if (!root.TryGetProperty("templates", out var templates)) return new Dictionary<string, string>(StringComparer.Ordinal);
            if (templates.ValueKind != JsonValueKind.Object)
                throw Error("SALEPS1001", path, "templates", "Expected an object mapping template names to file paths.");

            var result = new Dictionary<string, string>(StringComparer.Ordinal);
            foreach (var template in templates.EnumerateObject())
            {
                if (!ScribanTemplateNames.All.Contains(template.Name))
                    throw Error("SALEPS1003", path, "templates." + template.Name, "Unknown template name.", "Use a template key listed in the Scriban template customization guide.");
                if (template.Value.ValueKind != JsonValueKind.String || string.IsNullOrWhiteSpace(template.Value.GetString()))
                    throw Error("SALEPS1001", path, "templates." + template.Name, "Expected a non-empty template file path.");
                var templatePath = paths.Read(Path.GetFullPath(GeneratorPathPolicy.Normalize(template.Value.GetString()!), directory));
                if (!File.Exists(templatePath))
                    throw Error("SALEPS1001", path, "templates." + template.Name, $"Template file '{GeneratorPathPolicy.Normalize(templatePath)}' does not exist.");
                if (!result.TryAdd(template.Name, templatePath))
                    throw Error("SALEPS1001", path, "templates." + template.Name, "Template name is specified more than once.");
            }

            return result;
        }

        private static IReadOnlyDictionary<string, string> MergeTemplateOverrides(
            IReadOnlyDictionary<string, string>? inherited,
            IReadOnlyDictionary<string, string> local)
        {
            var result = inherited is null
                ? new Dictionary<string, string>(StringComparer.Ordinal)
                : new Dictionary<string, string>(inherited, StringComparer.Ordinal);
            foreach (var item in local) result[item.Key] = item.Value;
            return result;
        }

        private static JsonDocument ReadDocument(string path)
        {
            try { return JsonDocument.Parse(File.ReadAllText(path)); }
            catch (JsonException exception) { throw Error("SALEPS1001", path, "$", exception.Message); }
            catch (IOException exception) { throw Error("SALEPS1001", path, "$", exception.Message); }
        }

        private static IReadOnlySet<string> ReadSuites(JsonElement element, string path)
        {
            var values = element.EnumerateArray().Select(item =>
                item.ValueKind == JsonValueKind.String && !string.IsNullOrWhiteSpace(item.GetString())
                    ? item.GetString()!
                    : throw Error("SALEPS1001", path, "suites", "Suite names must be non-empty strings.")).ToArray();
            if (values.Distinct(StringComparer.Ordinal).Count() != values.Length)
                throw Error("SALEPS1001", path, "suites", "Duplicate suite.");
            return values.ToHashSet(StringComparer.Ordinal);
        }

        private static void CheckDuplicates(JsonElement value, string path, string property)
        {
            if (value.ValueKind == JsonValueKind.Object)
            {
                var names = new HashSet<string>(StringComparer.Ordinal);
                foreach (var item in value.EnumerateObject())
                {
                    if (!names.Add(item.Name)) throw Error("SALEPS1001", path, property + "." + item.Name, "Duplicate property.");
                    CheckDuplicates(item.Value, path, property + "." + item.Name);
                }
            }
            else if (value.ValueKind == JsonValueKind.Array)
            {
                foreach (var item in value.EnumerateArray()) CheckDuplicates(item, path, property);
            }
        }
        private static bool GetBool(JsonElement element, string property, bool fallback, string path) => element.TryGetProperty(property, out var value) ? value.ValueKind switch { JsonValueKind.True => true, JsonValueKind.False => false, _ => throw Error("SALEPS1001", path, property, "Expected a boolean.") } : fallback;
        private static int GetInt(JsonElement element, string property, int fallback, string path, int minimum = 0, int maximum = int.MaxValue)
        {
            if (!element.TryGetProperty(property, out var value)) return fallback;
            if (!value.TryGetInt32(out var number) || number < minimum || number > maximum) throw Error("SALEPS1001", path, property, $"Expected an integer between {minimum} and {maximum}.");
            return number;
        }
        private static void ValidateNamespace(string value, string path)
        {
            if (value.Split('.').Any(part => !System.Text.RegularExpressions.Regex.IsMatch(part, "^[A-Za-z_][A-Za-z0-9_]*$", System.Text.RegularExpressions.RegexOptions.CultureInvariant)))
                throw Error("SALEPS1001", path, "namespace", $"'{value}' is not a valid C# namespace.");
        }
        private static void ValidateIdentifier(string value, string path, string property)
        {
            if (!System.Text.RegularExpressions.Regex.IsMatch(value, "^[A-Za-z_][A-Za-z0-9_]*$", System.Text.RegularExpressions.RegexOptions.CultureInvariant))
                throw Error("SALEPS1001", path, property, $"'{value}' is not a valid C# identifier.");
        }
    }
}
