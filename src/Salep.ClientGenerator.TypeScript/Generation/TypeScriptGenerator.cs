using Microsoft.Extensions.FileSystemGlobbing;
using Salep.ClientGenerator.Generation;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Salep.ClientGenerator.Model;
using Salep.ClientGenerator.TypeScript.Targets;
using Salep.GraphQLParser;
using Salep.ClientGenerator.TypeScript.Templates;

namespace Salep.ClientGenerator.TypeScript.Generation;

/// <summary>Caller-controlled roots for TypeScript generation.</summary>
public sealed record TypeScriptGeneratorOptions(string ConfigurationPath, string? SolutionDirectory = null, IReadOnlyList<string>? ReadRoots = null, System.Net.Http.HttpClient? SchemaHttpClient = null);

/// <summary>Generates a standalone TypeScript client from local GraphQL documents.</summary>
public static class TypeScriptGenerator
{
    public static IReadOnlyList<string> Generate(TypeScriptGeneratorOptions options, bool validateOnly = false)
    {
        var configPath = Path.GetFullPath(GeneratorPathPolicy.Normalize(options.ConfigurationPath));
        var directory = Path.GetDirectoryName(configPath)!;
        var paths = new GeneratorPathPolicy(options.SolutionDirectory ?? GeneratorPathPolicy.FindSolutionRoot(directory), options.ReadRoots);
        paths.Read(configPath);
        using var json = JsonDocument.Parse(File.ReadAllText(configPath));
        var config = json.RootElement;
        CheckProperties(config, ["version", "target", "schema", "documents", "output", "scalars", "templates", "fragmentMasking", "documentTags", "persistedOperations", "outputLayout", "clientSchema"]);
        if (config.GetProperty("version").GetInt32() != 1 || config.GetProperty("target").GetString() != "typescript")
            throw new InvalidDataException("Expected version 1 and target 'typescript'.");
        string Resolve(string path) => GeneratorPathPolicy.ResolvePath(path, directory);
        var schemaSources = TypeScriptSchemaLoader.Load(config.GetProperty("schema"), directory, paths, options.SchemaHttpClient);
        var clientSources = config.TryGetProperty("clientSchema", out var clientSchemaValue)
            ? TypeScriptSchemaLoader.Load(clientSchemaValue, directory, paths, options.SchemaHttpClient) : [];
        var serverSourceCount = schemaSources.Count;
        clientSources = clientSources.Select(source => source.RemoteId is null ? source : source with { RemoteId = "client/" + source.RemoteId }).ToArray();
        schemaSources = schemaSources.Concat(clientSources).ToArray();
        var output = paths.Write(Resolve(config.TryGetProperty("output", out var outputValue) ? outputValue.GetString()! : "./Generated"), false);
        var inputs = new List<string> { configPath };
        inputs.AddRange(schemaSources.Where(source => source.LocalPath is not null).Select(source => source.LocalPath!));
        var patterns = config.GetProperty("documents");
        var documents = new List<string>();
        foreach (var pattern in patterns.ValueKind == JsonValueKind.Array ? patterns.EnumerateArray().Select(item => item.GetString()!) : [patterns.GetString()!])
            foreach (var expanded in ExpandBraces(pattern)) documents.AddRange(Discover(Resolve(expanded), paths));
        documents = documents.Distinct(StringComparer.Ordinal).Order(StringComparer.Ordinal).ToList();
        if (documents.Count == 0) throw new InvalidDataException("No operation documents matched.");
        inputs.AddRange(documents);
        var scalarMap = new Dictionary<string, TypeScriptScalar>(StringComparer.Ordinal);
        if (config.TryGetProperty("scalars", out var scalarValues))
            foreach (var scalar in scalarValues.EnumerateObject())
            {
                CheckProperties(scalar.Value, ["input", "output"]);
                if (string.IsNullOrWhiteSpace(scalar.Value.GetProperty("input").GetString()) || string.IsNullOrWhiteSpace(scalar.Value.GetProperty("output").GetString()))
                    throw new InvalidDataException("Scalar input/output mappings cannot be empty.");
                scalarMap.Add(scalar.Name, new(scalar.Value.GetProperty("input").GetString()!, scalar.Value.GetProperty("output").GetString()!));
            }
        var parserOptions = new ParserOptions(maxAllowedDirectives: 10_000);
        DocumentNode Parse(string source) => global::Salep.GraphQLParser.GraphQLParser.Parse(new SourceText(source.AsMemory()), parserOptions);
        var schemaParts = schemaSources.Select(source =>
        {
            var name = source.LocalPath is null ? source.Name : GeneratorPathPolicy.Normalize(Path.GetRelativePath(directory, source.LocalPath));
            var input = new TypeScriptSourceDocument(source.Converted ? name + " [converted SDL]" : name, source.Text, source.Text);
            return GraphQlModelFactory.CreateSchema(input.ValidateDocumentKind(input.Parse(parserOptions), schema: true), input.Map);
        }).ToArray();
        var schema = new GraphQlSchemaModel(schemaParts.SelectMany(part => part.Types).ToArray(), schemaParts.SelectMany(part => part.SchemaDefinitions).ToArray(), schemaParts.SelectMany(part => part.DirectiveDefinitions).ToArray());
        if (clientSources.Count > 0)
        {
            var clientDirective = GraphQlModelFactory.CreateSchema(Parse("directive @client on FIELD | FRAGMENT_SPREAD | INLINE_FRAGMENT")).DirectiveDefinitions.Single();
            if (schema.DirectiveDefinitions.Any(directive => directive.Name == "client")) throw new InvalidDataException("client is reserved when clientSchema is configured.");
            schema = schema with { DirectiveDefinitions = schema.DirectiveDefinitions.Append(clientDirective).ToArray() };
        }
        var tags = config.TryGetProperty("documentTags", out var tagValues) ? tagValues.EnumerateArray().Select(value => value.GetString()!).ToArray() : new[] { "gql", "graphql" };
        if (tags.Length == 0 || tags.Distinct(StringComparer.Ordinal).Count() != tags.Length || tags.Any(tag => string.IsNullOrEmpty(tag) || !(char.IsLetter(tag[0]) || tag[0] is '_' or '$') || tag.Any(character => !(char.IsLetterOrDigit(character) || character is '_' or '$'))))
            throw new InvalidDataException("documentTags must contain unique, nonempty JavaScript identifiers.");
        var sourceDocuments = documents.SelectMany(file => ReadDocuments(file, directory, tags)).ToArray();
        var rawSources = sourceDocuments.Select(source => source.Text).Distinct(StringComparer.Ordinal).ToArray();
        var parts = sourceDocuments.Select(source => GraphQlModelFactory.CreateExecutable([source.ValidateDocumentKind(source.Parse(parserOptions), schema: false)], source.Map)).ToArray();
        var executable = new GraphQlExecutableDocument(parts.SelectMany(part => part.Operations).ToArray(), parts.SelectMany(part => part.Fragments).ToArray());
        if (executable.Operations.Count == 0 && executable.Fragments.Count == 0) throw new InvalidDataException("No named operations were found in the document inputs.");
        var operations = new List<GraphQlOperationDefinition>();
        foreach (var group in executable.Operations.GroupBy(operation => operation.Name, StringComparer.Ordinal))
        {
            if (string.IsNullOrEmpty(group.Key)) throw new InvalidDataException("Operations require unique names.");
            if (group.Select(operation => TypeScriptDocumentAst.Operation(new([operation], []), "")).Distinct(StringComparer.Ordinal).Count() > 1)
                throw new InvalidDataException("Operations require unique names; repeated names must describe identical documents.");
            operations.Add(group.First());
        }
        var fragments = new List<GraphQlFragmentDefinition>();
        foreach (var group in executable.Fragments.GroupBy(fragment => fragment.Name, StringComparer.Ordinal))
        {
            if (group.Select(fragment => TypeScriptDocumentAst.FragmentDocument(fragment, [])).Distinct(StringComparer.Ordinal).Count() > 1)
                throw new InvalidDataException("Repeated fragment names must describe identical documents.");
            fragments.Add(group.First());
        }
        executable = executable with { Operations = operations, Fragments = fragments };
        foreach (var fragment in executable.Fragments) ValidateSpreads(fragment.SelectionSet, executable, new HashSet<string>(StringComparer.Ordinal) { fragment.Name });
        foreach (var operation in executable.Operations) ValidateSpreads(operation.SelectionSet, executable, new HashSet<string>(StringComparer.Ordinal));
        new GraphQlDocumentValidator(schema, executable).Validate();
        if (clientSources.Count > 0)
            new TypeScriptClientDocumentPlanner(schema, executable).ValidateLocalSelections(schemaParts.Skip(serverSourceCount).SelectMany(part => part.Types).ToArray());
        var overrides = new Dictionary<string, string>(StringComparer.Ordinal);
        if (config.TryGetProperty("templates", out var templateMappings))
        {
            CheckProperties(templateMappings, TypeScriptTemplateCatalog.Files.Keys.ToArray());
            foreach (var entry in templateMappings.EnumerateObject())
            {
                var file = paths.Read(Resolve(entry.Value.GetString()!));
                inputs.Add(file);
                overrides.Add(entry.Name, File.ReadAllText(file));
            }
        }
        var templates = new TypeScriptTemplateCatalog(overrides);
        var target = new TypeScriptCodeGenerationTarget(scalarMap);
        target.PrepareNames(schema, executable);
        var persistedOperations = config.TryGetProperty("persistedOperations", out var persisted) && persisted.GetBoolean();
        var renderer = new TypeScriptSourceRenderer(schema, executable, target, templates, !config.TryGetProperty("fragmentMasking", out var masking) || masking.GetBoolean(), persistedOperations, clientSources.Count > 0);
        var lookupEntries = rawSources.Select(source => new { source, parsed = GraphQlModelFactory.CreateExecutable(new[] { Parse(source) }) })
            .Where(entry => entry.parsed.Operations.Count > 0 || entry.parsed.Fragments.Count > 0).Select(entry =>
        {
            var parsed = entry.parsed;
            var operationName = parsed.Operations.FirstOrDefault()?.Name;
            var name = operationName is null ? target.FragmentName(parsed.Fragments.First().Name) : target.OperationName(operationName);
            return new { source = target.StringLiteral(entry.source), name };
        }).ToArray();
        var sources = new Dictionary<string, string>(StringComparer.Ordinal)
        {
            ["gql/graphql.ts"] = templates.Render("graphql", new { schema_types = renderer.SchemaTypes(), operations = renderer.Operations(), fragments = renderer.Fragments() }),
            ["gql/gql.ts"] = templates.Render("lookup", new { entries = lookupEntries }),
            ["gql/local-execution.ts"] = templates.Render("local-execution", new { }),
            ["gql/client.ts"] = templates.Render("client", new { }),
            ["gql/fragment-masking.ts"] = templates.Render("fragment-masking", new { }),
            ["gql/index.ts"] = templates.Render("index", new { }),
            ["schema.graphql"] = string.Join(Environment.NewLine + Environment.NewLine, schemaSources.Select(source => source.Text))
        };
        if (clientSources.Count > 0)
            sources["gql/resolvers.ts"] = templates.Render("graphql", new { schema_types = renderer.SchemaTypes(), operations = renderer.ResolverTypes(schemaParts.Skip(serverSourceCount).SelectMany(part => part.Types).ToArray()), fragments = "" });
        if (persistedOperations)
        {
            var formatter = new GraphQlOperationDocumentFormatter(schema, executable);
            var persistedPlanner = new TypeScriptClientDocumentPlanner(schema, executable);
            var persistedEntries = executable.Operations.Select(operation => new { operation, query = (clientSources.Count > 0 && persistedPlanner.HasLocalSelections(operation) ? persistedPlanner.RemoteQuery(operation) : formatter.Format(operation))?.Replace("\r\n", "\n", StringComparison.Ordinal).Replace('\r', '\n') }).Where(entry => entry.query is not null)
                .ToDictionary(entry => TypeScriptSourceRenderer.OperationId(entry.query!), entry => new { name = entry.operation.Name, type = entry.operation.OperationType, query = entry.query }, StringComparer.Ordinal);
            sources["operations.json"] = JsonSerializer.Serialize(new { version = 1, algorithm = "sha256", operations = persistedEntries }, new JsonSerializerOptions { WriteIndented = true }) + Environment.NewLine;
        }
        var layout = config.TryGetProperty("outputLayout", out var layoutValue) ? layoutValue.GetString() : "combined";
        if (layout is not ("combined" or "per-operation")) throw new InvalidDataException("outputLayout must be combined or per-operation.");
        if (layout == "per-operation")
        {
            sources["gql/shared.ts"] = templates.Render("graphql", new { schema_types = renderer.SchemaTypes(), operations = "", fragments = renderer.Fragments() });
            var modules = new List<string>();
            var filenameGroups = executable.Operations.GroupBy(operation => target.OperationName(operation.Name!), StringComparer.OrdinalIgnoreCase).ToDictionary(group => group.Key, group => group.Count(), StringComparer.OrdinalIgnoreCase);
            var sharedNames = schema.Types.Concat(GraphQlIntrospection.Types.OfType<GraphQlEnumType>()).Where(type => type is GraphQlEnumType or GraphQlInputObjectType).Select(type => target.TypeName(type.Name))
                .Concat(executable.Fragments.Select(fragment => target.FragmentName(fragment.Name))).Append("TypedDocument").Append("Scalars").Distinct(StringComparer.Ordinal).ToArray();
            foreach (var operation in executable.Operations)
            {
                var module = "operation-" + target.OperationName(operation.Name!);
                if (filenameGroups[target.OperationName(operation.Name!)] > 1) module += "-" + TypeScriptSourceRenderer.OperationId(operation.Name!)[..12];
                var declarations = renderer.Operations([operation]);
                var typeDeclarations = System.Text.RegularExpressions.Regex.Replace(declarations, @"export const [^\r\n]*", "");
                typeDeclarations = System.Text.RegularExpressions.Regex.Replace(typeDeclarations, @"/\*[\s\S]*?\*/", "");
                typeDeclarations = System.Text.RegularExpressions.Regex.Replace(typeDeclarations, @"""(?:\\.|[^""\\])*""", "");
                var imports = sharedNames.Where(name => System.Text.RegularExpressions.Regex.IsMatch(typeDeclarations, @"(?<![\w$])" + System.Text.RegularExpressions.Regex.Escape(name) + @"(?![\w$])")).Append("TypedDocument").Distinct(StringComparer.Ordinal).ToArray();
                sources["gql/operations/" + module + ".ts"] = templates.Render("partition.operation", new { imports = string.Join(", ", imports), declarations });
                modules.Add(module);
            }
            sources["gql/graphql.ts"] = templates.Render("partition.barrel", new { modules });
        }
        var destinations = sources.ToDictionary(entry => entry.Key, entry => paths.Write(Path.Combine(output, entry.Key)), StringComparer.Ordinal);
        foreach (var name in sources.Keys.Where(name => name.EndsWith(".ts", StringComparison.Ordinal)).ToArray())
            sources[name] = sources[name].TrimEnd('\r', '\n') + Environment.NewLine;
        var manifestPath = paths.Write(Path.Combine(output, ".salep.manifest.json"));
        // Resolve and fingerprint the complete plan before touching the publication directory.
        var inputHashes = inputs.Distinct(StringComparer.Ordinal).Order(StringComparer.Ordinal)
            .ToDictionary(file => Relative(output, file), Hash);
        var lockPath = paths.Write(Path.Combine(output, ".salep.lock"));
        var inputPaths = new HashSet<string>(inputs.Select(GeneratorPathPolicy.PhysicalIdentity), StringComparer.OrdinalIgnoreCase);
        void RejectInputOverlap(IEnumerable<string> ownedPaths)
        {
            if (ownedPaths.Any(file => inputPaths.Contains(GeneratorPathPolicy.PhysicalIdentity(file))))
                throw new InvalidDataException("Generator inputs cannot overlap owned output paths.");
        }
        RejectInputOverlap(destinations.Values.Append(manifestPath).Append(lockPath));
        // Stale outputs are still owned until publication completes. Check them before
        // creating the directory/lock, then recheck the inventory under the lock.
        if (File.Exists(manifestPath))
        {
            using var previous = JsonDocument.Parse(File.ReadAllText(manifestPath));
            RejectInputOverlap(previous.RootElement.GetProperty("Outputs").EnumerateObject()
                .Select(entry => paths.Write(Path.Combine(output, entry.Name))));
        }
        var sourceHashes = sources.ToDictionary(entry => entry.Key, entry => Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(entry.Value))), StringComparer.Ordinal);
        if (!validateOnly) Directory.CreateDirectory(output);
        using var publicationLock = validateOnly ? null : new FileStream(lockPath, FileMode.OpenOrCreate, FileAccess.ReadWrite, FileShare.None);
        var previousOutputs = new HashSet<string>(StringComparer.Ordinal);
        if (File.Exists(manifestPath))
        {
            using var previous = JsonDocument.Parse(File.ReadAllText(manifestPath));
            var inventory = previous.RootElement;
            CheckProperties(inventory, ["Version", "Target", "Configuration", "Inputs", "Outputs", "RemoteInputs"]);
            if (inventory.GetProperty("Version").GetInt32() != 1 || inventory.GetProperty("Target").GetString() != "typescript" || inventory.GetProperty("Configuration").GetString() != Relative(output, configPath))
                throw new InvalidDataException("Output belongs to another generator or configuration.");
            var owned = inventory.GetProperty("Outputs");
            var previousNames = owned.EnumerateObject().Select(entry => entry.Name).ToArray();
            RejectInputOverlap(previousNames.Select(name => paths.Write(Path.Combine(output, name))));
            var partitionNames = previousNames.Where(name => name == "gql/shared.ts" || System.Text.RegularExpressions.Regex.IsMatch(name, @"^gql/operations/operation-[A-Za-z_][A-Za-z0-9_]*(?:-[a-f0-9]{12})?\.ts$"));
            CheckProperties(owned, sources.Keys.Concat(new[] { "graphql.ts", "operations.json", "gql/resolvers.ts" }).Concat(partitionNames).ToArray());
            foreach (var entry in owned.EnumerateObject())
            {
                previousOutputs.Add(entry.Name);
                var file = paths.Write(Path.Combine(output, entry.Name));
                if (File.Exists(file) && Hash(file) != entry.Value.GetString()) throw new InvalidDataException("Owned output has been modified; restore it or move it out before regeneration.");
            }
            if (previousOutputs.Count == 0) throw new InvalidDataException("Manifest lacks the output contract.");
            var inputNames = new HashSet<string>(StringComparer.Ordinal);
            foreach (var entry in inventory.GetProperty("Inputs").EnumerateObject())
            {
                if (!inputNames.Add(entry.Name) || entry.Name != GeneratorPathPolicy.Normalize(entry.Name) || Path.IsPathRooted(entry.Name)) throw new InvalidDataException("Manifest inputs must be unique relative paths using '/'.");
                paths.Read(Path.GetFullPath(entry.Name, output));
            }
        }
        foreach (var entry in destinations)
            if (File.Exists(entry.Value) && !previousOutputs.Contains(entry.Key)) throw new InvalidDataException($"Output contains unowned '{entry.Key}'.");
        if (validateOnly) return destinations.Values.Select(GeneratorPathPolicy.Normalize).ToArray();
        var manifest = new
        {
            Version = 1, Target = "typescript", Configuration = Relative(output, configPath),
            Inputs = inputHashes, Outputs = sourceHashes,
            RemoteInputs = schemaSources.Where(source => source.RemoteId is not null).ToDictionary(source => source.RemoteId!, source => Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(source.Text))), StringComparer.Ordinal)
        };
        var manifestText = JsonSerializer.Serialize(manifest, new JsonSerializerOptions { WriteIndented = true }) + Environment.NewLine;
        foreach (var entry in sources)
        {
            Directory.CreateDirectory(Path.GetDirectoryName(destinations[entry.Key])!);
            Publish(destinations[entry.Key], entry.Value, paths);
        }
        foreach (var stale in previousOutputs.Except(sources.Keys, StringComparer.Ordinal))
        {
            var file = paths.Write(Path.Combine(output, stale));
            if (File.Exists(file)) File.Delete(file);
        }
        Publish(manifestPath, manifestText, paths);
        return destinations.Values.Select(GeneratorPathPolicy.Normalize).ToArray();
    }

    public static string EmbeddedTemplate(string key = "client") => TypeScriptTemplateCatalog.ReadDefault(key);

    private static void ValidateSpreads(GraphQlSelectionSet selections, GraphQlExecutableDocument document, HashSet<string> active)
    {
        foreach (var selection in selections.Selections)
        {
            switch (selection)
            {
                case GraphQlFieldSelection { SelectionSet: { } nested }: ValidateSpreads(nested, document, active); break;
                case GraphQlInlineFragmentSelection inline: ValidateSpreads(inline.SelectionSet, document, active); break;
                case GraphQlFragmentSpreadSelection spread:
                    var fragment = document.Fragments.SingleOrDefault(item => item.Name == spread.Name)
                        ?? throw new InvalidDataException(spread.Source.Diagnostic($"Unknown fragment '{spread.Name}'."));
                    if (!active.Add(spread.Name)) throw new InvalidDataException(spread.Source.Diagnostic($"Fragment cycle at '{spread.Name}'."));
                    ValidateSpreads(fragment.SelectionSet, document, active);
                    active.Remove(spread.Name);
                    break;
            }
        }
    }

    private static void Publish(string destination, string content, GeneratorPathPolicy paths)
    {
        if (File.Exists(destination) && File.ReadAllText(destination) == content) return;
        var temporary = paths.Write(destination + "." + Guid.NewGuid().ToString("N") + ".tmp");
        try
        {
            File.WriteAllText(temporary, content, new UTF8Encoding(false));
            paths.Write(destination);
            File.Move(temporary, destination, true);
        }
        finally { if (File.Exists(temporary)) File.Delete(temporary); }
    }

    private static string Hash(string file) => Convert.ToHexString(SHA256.HashData(File.ReadAllBytes(file)));
    private static string Relative(string folder, string file)
    {
        var path = Path.GetRelativePath(folder, file);
        if (Path.IsPathRooted(path)) throw new InvalidDataException("Manifest inputs must share the output filesystem volume.");
        return GeneratorPathPolicy.Normalize(path);
    }

    private static void CheckProperties(JsonElement element, string[] allowed)
    {
        var found = new HashSet<string>(StringComparer.Ordinal);
        foreach (var property in element.EnumerateObject())
            if (!found.Add(property.Name) || !allowed.Contains(property.Name, StringComparer.Ordinal))
                throw new InvalidDataException($"Unknown or duplicate property '{property.Name}'.");
    }

    private static IReadOnlyList<TypeScriptSourceDocument> ReadDocuments(string file, string directory, IReadOnlyCollection<string> tags)
    {
        var name = GeneratorPathPolicy.Normalize(Path.GetRelativePath(directory, file));
        var text = File.ReadAllText(file);
        if (Path.GetExtension(file) is not (".ts" or ".tsx")) return [new(name, text, text)];
        try { return TypeScriptDocumentExtractor.ExtractWithLocations(text, tags, jsx: Path.GetExtension(file) == ".tsx").Select(source => new TypeScriptSourceDocument(name, text, source.Text, source.Offsets)).ToArray(); }
        catch (InvalidDataException error)
        {
            var input = new TypeScriptSourceDocument(name, text, text);
            var (line, column) = input.Coordinates(error.Data["SourcePosition"] is int offset ? Math.Clamp(offset, 0, text.Length) : 0);
            throw new InvalidDataException($"{name}({line},{column}): {error.Message}", error);
        }
    }

    private static IEnumerable<string> ExpandBraces(string pattern)
    {
        var start = pattern.IndexOf('{', StringComparison.Ordinal);
        if (start < 0) { yield return pattern; yield break; }
        var end = pattern.IndexOf('}', start + 1);
        if (end < 0) throw new InvalidDataException("Unclosed brace in a document pattern.");
        var choices = pattern[(start + 1)..end].Split(',');
        if (choices.Length < 2 || choices.Any(string.IsNullOrEmpty)) throw new InvalidDataException("Brace patterns require comma-separated alternatives.");
        foreach (var choice in choices)
            foreach (var expanded in ExpandBraces(pattern[..start] + choice + pattern[(end + 1)..])) yield return expanded;
    }

    private static IEnumerable<string> Discover(string pattern, GeneratorPathPolicy paths)
    {
        pattern = GeneratorPathPolicy.Normalize(pattern);
        paths.ReadPattern(pattern);
        if (File.Exists(pattern)) return [paths.Read(pattern)];
        if (Directory.Exists(pattern)) return Walk(pattern, paths).Where(file => Path.GetExtension(file) is ".graphql" or ".ts" or ".tsx");
        var wildcard = pattern.IndexOfAny(['*', '?']);
        if (wildcard < 0) throw new FileNotFoundException("Document input does not exist.", pattern);
        var separator = pattern.LastIndexOf('/', wildcard);
        var root = pattern[..(separator + 1)];
        var matcher = new Microsoft.Extensions.FileSystemGlobbing.Matcher(StringComparison.Ordinal);
        matcher.AddInclude(pattern[(separator + 1)..]);
        return Walk(root, paths).Where(file => matcher.Match(GeneratorPathPolicy.Normalize(Path.GetRelativePath(root, file))).HasMatches);
    }

    private static IEnumerable<string> Walk(string root, GeneratorPathPolicy paths)
    {
        paths.Read(root);
        foreach (var file in Directory.EnumerateFiles(root).Order(StringComparer.Ordinal)) yield return paths.Read(file);
        foreach (var child in Directory.EnumerateDirectories(root).Order(StringComparer.Ordinal))
            foreach (var file in Walk(paths.Read(child), paths)) yield return file;
    }
}
