using System.Collections.Immutable;
using System.Text.Json;
using Microsoft.CodeAnalysis.CSharp;

namespace Salep.ClientGenerator.Config;

internal sealed record ScalarDefinition(string Type, bool IsValueType, string? SampleExpression = null, string? SampleJson = null);

// Immutable emitter settings. Only ConfigurationResolver constructs these from user input.
internal sealed record GeneratorConfig
{
    public const string ConfigFileName = "salep.json";
    public string GeneratedNamespace { get; init; } = "Salep.Generated";
    public string TestsNamespace { get; init; } = "Salep.Generated.Tests";
    public string ClientClassName { get; init; } = "GraphQLClient";
    public bool UseNativeUnions { get; init; }
    public bool OmitUnusedVariables { get; init; }
    public bool InlineDefaultVariables { get; init; }
    public bool UseHttpGet { get; init; }
    public bool EnableBatching { get; init; }
    public int MaxGetUrlLength { get; init; } = 2048;
    public bool UseRawStrings { get; init; } = true;
    public int IndentSize { get; init; } = 4;
    public ImmutableDictionary<string, ScalarDefinition> Scalars { get; init; } = BuiltinScalars;
    public ImmutableDictionary<string, string> TypeOwners { get; init; } = ImmutableDictionary<string, string>.Empty;
    public ImmutableArray<string> ConverterRegistries { get; init; } = [];
    public string SharedNamespace { get; init; } = "Salep.Generated";
    public string UnionRepresentation => UseNativeUnions ? "native" : "dunet";
    public IReadOnlyDictionary<string, bool> ScalarValueTypes => Scalars.ToDictionary(pair => pair.Key, pair => pair.Value.IsValueType, StringComparer.Ordinal);
    public bool RequiresNodaTime() => Scalars.Values.Any(value => value.Type.StartsWith("NodaTime.", StringComparison.Ordinal) || value.Type.StartsWith("global::NodaTime.", StringComparison.Ordinal));
    public IEnumerable<string> GetAdditionalUsings() => RequiresNodaTime() ? ["NodaTime"] : [];
    public bool TryGetScalarMapping(string name, out string type, out bool isValueType)
    {
        var found = Scalars.TryGetValue(name, out var value);
        type = value?.Type ?? string.Empty;
        isValueType = value?.IsValueType ?? false;
        return found;
    }
    public bool TryGetScalarSampleExpression(string name, out string expression)
    {
        expression = Scalars.GetValueOrDefault(name)?.SampleExpression ?? string.Empty;
        return expression.Length > 0;
    }
    public bool TryGetScalarSampleJsonLiteral(string name, out string json)
    {
        json = Scalars.GetValueOrDefault(name)?.SampleJson ?? string.Empty;
        return json.Length > 0;
    }
    public static string ResolveConfigPath(string? path, string workingDirectory)
    {
        var full = Path.GetFullPath(path ?? ConfigFileName, workingDirectory);
        return Directory.Exists(full) ? Path.Combine(full, ConfigFileName) : full;
    }
    public static GeneratorConfig Load(string path) => ConfigurationResolver.Resolve(path).Settings;

    internal static readonly ImmutableDictionary<string, ScalarDefinition> BuiltinScalars = new Dictionary<string, ScalarDefinition>(StringComparer.Ordinal)
    {
        ["String"] = new("string", false, "\"sample\"", "\"sample\""),
        ["ID"] = new("string", false, "\"sample\"", "\"sample\""),
        ["URL"] = new("string", false, "\"https://example.com\"", "\"https://example.com\""),
        ["Int"] = new("int", true, "123", "123"),
        ["Float"] = new("double", true, "12.34", "12.34"),
        ["Boolean"] = new("bool", true, "true", "true"),
        ["Long"] = new("long", true, "123L", "123"),
        ["Decimal"] = new("decimal", true, "12.34m", "12.34"),
        ["DateTime"] = new("DateTime", true, "DateTime.UnixEpoch", "\"1970-01-01T00:00:00Z\""),
        ["Instant"] = new("DateTimeOffset", true, "DateTimeOffset.UnixEpoch", "\"1970-01-01T00:00:00Z\""),
        ["UUID"] = new("Guid", true, "Guid.Empty", "\"00000000-0000-0000-0000-000000000000\"")
    }.ToImmutableDictionary(StringComparer.Ordinal);
}

internal sealed record ResolvedConfiguration(
    string Path, string Kind, string Output, string Schema, string Operations,
    GeneratorConfig Settings, string? BaseClient, string? Client, bool EmitSample,
    bool EmitAgentInstructions, ImmutableArray<string> Suites, ImmutableArray<string> ConfigurationFiles,
    ImmutableDictionary<string, JsonElement> ProfileSettings);

internal static class ConfigurationResolver
{
    private static readonly string[] SettingsKeys = ["unionRepresentation", "scalarPreset", "scalars", "omitUnusedVariables", "inlineDefaultVariables", "useHttpGet", "enableBatching", "maxGetUrlLength", "indentSize"];
    private static readonly string[] CommonKeys = ["$schema", "version", "kind"];

    public static ResolvedConfiguration Resolve(string path) => Resolve(Path.GetFullPath(path), new HashSet<string>(PathComparer));
    internal static StringComparer PathComparer => OperatingSystem.IsWindows() || OperatingSystem.IsMacOS() ? StringComparer.OrdinalIgnoreCase : StringComparer.Ordinal;
    internal static ConfigurationException Error(string code, string path, string property, string message, string guidance = "Correct the configuration and run salep validate.")
        => new(new(code, path, property, message, guidance));

    private static ResolvedConfiguration Resolve(string path, HashSet<string> stack)
    {
        if (!stack.Add(path)) throw Error("SALEP1002", path, "reference", "Configuration reference cycle detected.");
        try
        {
            using var doc = Read(path);
            var root = doc.RootElement;
            if (root.ValueKind != JsonValueKind.Object) throw Error("SALEP1001", path, "$", "Expected a JSON object.");
            CheckDuplicates(root, path, "$");
            if (!root.TryGetProperty("version", out var version) || version.ValueKind != JsonValueKind.Number || !version.TryGetInt32(out var number) || number != 1)
                throw Error("SALEP1001", path, "version", "Expected version 1. Legacy flat configuration is not supported.");
            var kind = String(root, "kind", path, required: true)!;
            var roleKeys = kind switch
            {
                "profile" => new[] { "extends", "schema" }.Concat(SettingsKeys),
                "client" => new[] { "profile", "baseClient", "schema", "operations", "namespace", "clientName", "output", "emitSample", "emitAgentInstructions" }.Concat(SettingsKeys),
                "tests" => ["client", "output", "namespace", "suites", "indentSize", "rawJsonLiterals", "emitAgentInstructions"],
                _ => throw Error("SALEP1001", path, "kind", $"Unknown kind '{kind}'; expected profile, client, or tests.")
            };
            var allowed = CommonKeys.Concat(roleKeys).ToHashSet(StringComparer.Ordinal);
            foreach (var property in root.EnumerateObject())
                if (!allowed.Contains(property.Name))
                    throw Error("SALEP1003", path, property.Name, $"Property is not allowed for kind '{kind}'.",
                        kind == "tests" ? $"Client behavior is owned by '{String(root, "client", path)}'; remove the override." : "Remove the unknown or obsolete property.");

            var directory = System.IO.Path.GetDirectoryName(path)!;
            string? Reference(string name) => String(root, name, path) is { } value ? GeneratorConfig.ResolveConfigPath(value, directory) : null;
            var files = ImmutableArray.CreateBuilder<string>(); files.Add(path);
            var inherited = new Dictionary<string, JsonElement>(StringComparer.Ordinal);
            string? schema = null;
            var profile = Reference(kind == "profile" ? "extends" : "profile");
            if (profile is not null) LoadProfile(profile, stack, inherited, files, ref schema);
            foreach (var key in SettingsKeys) if (root.TryGetProperty(key, out var value)) MergeSetting(inherited, key, value);
            if (String(root, "schema", path) is { } schemaPath) schema = System.IO.Path.GetFullPath(schemaPath, directory);
            var settings = ResolveSettings(inherited, path);
            var client = Reference("client");
            var baseClient = Reference("baseClient");
            if (kind == "client" && baseClient is not null)
            {
                var resolvedBase = Resolve(baseClient, stack);
                if (resolvedBase.Kind != "client") throw Error("SALEP1004", path, "baseClient", $"'{baseClient}' is not a client configuration.");
            }
            var emitSample = Bool(root, "emitSample", false, path);
            var outputDefault = kind == "tests" ? "./GeneratedTests" : "./Generated";
            var output = System.IO.Path.GetFullPath(String(root, "output", path) ?? outputDefault, directory);
            var operations = System.IO.Path.GetFullPath(String(root, "operations", path) ?? "./graphql", directory);
            ImmutableArray<string> suites = [];
            if (kind == "tests")
            {
                if (client is null) throw Error("SALEP1001", path, "client", "A tests configuration requires a concrete client configuration.");
                var target = Resolve(client, stack);
                if (target.Kind != "client") throw Error("SALEP1004", path, "client", $"'{client}' is not a client configuration.");
                settings = target.Settings with
                {
                    TestsNamespace = String(root, "namespace", path) ?? target.Settings.GeneratedNamespace + ".Tests",
                    IndentSize = Integer(root, "indentSize", 4, path, 0, 16),
                    UseRawStrings = Bool(root, "rawJsonLiterals", true, path)
                };
                schema = target.Schema; operations = target.Operations; emitSample = target.EmitSample;
                suites = ReadSuites(root, path, emitSample);
            }
            else
            {
                settings = settings with
                {
                    GeneratedNamespace = String(root, "namespace", path) ?? "Salep.Generated",
                    ClientClassName = String(root, "clientName", path) ?? "GraphQLClient"
                };
                settings = settings with { SharedNamespace = settings.GeneratedNamespace };
                if (kind == "client" && schema is null) throw Error("SALEP1001", path, "schema", "A client requires a schema path, locally or from a profile.");
            }
            ValidateName(settings.GeneratedNamespace, true, path, "namespace");
            ValidateName(settings.TestsNamespace, true, path, "namespace");
            ValidateName(settings.ClientClassName, false, path, "clientName");
            return new(path, kind, output, schema ?? "", operations, settings, baseClient, client, emitSample,
                Bool(root, "emitAgentInstructions", true, path), suites, files.ToImmutable(), inherited.ToImmutableDictionary(StringComparer.Ordinal));
        }
        finally { stack.Remove(path); }
    }

    private static void LoadProfile(string path, HashSet<string> stack, Dictionary<string, JsonElement> values, ImmutableArray<string>.Builder files, ref string? schema)
    {
        // Resolve first so every profile is validated with exactly the same role and cycle rules.
        var resolved = Resolve(path, stack);
        if (resolved.Kind != "profile") throw Error("SALEP1004", path, "profile", "Expected a profile configuration.");
        files.AddRange(resolved.ConfigurationFiles);
        foreach (var pair in resolved.ProfileSettings) MergeSetting(values, pair.Key, pair.Value);
        if (!string.IsNullOrEmpty(resolved.Schema)) schema = resolved.Schema;
    }

    private static void MergeSetting(Dictionary<string, JsonElement> values, string key, JsonElement value)
    {
        if (key == "scalars" && value.ValueKind == JsonValueKind.Object && values.TryGetValue(key, out var previous) && previous.ValueKind == JsonValueKind.Object)
        {
            var entries = previous.EnumerateObject().ToDictionary(p => p.Name, p => p.Value.Clone(), StringComparer.Ordinal);
            foreach (var property in value.EnumerateObject()) entries[property.Name] = property.Value.Clone();
            values[key] = JsonSerializer.SerializeToElement(entries);
        }
        else values[key] = value.Clone();
    }

    private static GeneratorConfig ResolveSettings(Dictionary<string, JsonElement> values, string path)
    {
        var root = JsonSerializer.SerializeToElement(values);
        var representation = String(root, "unionRepresentation", path) ?? "dunet";
        if (representation is not ("dunet" or "native")) throw Error("SALEP1001", path, "unionRepresentation", "Expected dunet or native.");
        var preset = String(root, "scalarPreset", path) ?? "builtin";
        if (preset is not ("builtin" or "nodatime")) throw Error("SALEP1001", path, "scalarPreset", "Expected builtin or nodatime.");
        var scalars = GeneratorConfig.BuiltinScalars.ToBuilder();
        if (preset == "nodatime")
            foreach (var name in new[] { "DateTime", "Instant" }) scalars[name] = new("NodaTime.Instant", true, "NodaTime.Instant.FromUtc(2020, 1, 1, 0, 0)", "\"2020-01-01T00:00:00Z\"");
        if (root.TryGetProperty("scalars", out var scalarMap))
        {
            if (scalarMap.ValueKind != JsonValueKind.Object) throw Error("SALEP1001", path, "scalars", "Expected an object.");
            foreach (var entry in scalarMap.EnumerateObject())
            {
                var scalar = entry.Value;
                if (scalar.ValueKind != JsonValueKind.Object) throw Error("SALEP1001", path, "scalars." + entry.Name, "Expected a complete scalar definition.");
                foreach (var field in scalar.EnumerateObject())
                    if (field.Name is not ("type" or "isValueType" or "sampleExpression" or "sampleJson")) throw Error("SALEP1003", path, "scalars." + entry.Name + "." + field.Name, "Unknown scalar property.");
                var type = String(scalar, "type", path, true)!;
                if (!scalar.TryGetProperty("isValueType", out _)) throw Error("SALEP1001", path, "scalars." + entry.Name, "isValueType is required for explicit scalar definitions.");
                var expression = String(scalar, "sampleExpression", path);
                var json = String(scalar, "sampleJson", path);
                if (SyntaxFactory.ParseTypeName(type).ContainsDiagnostics || (expression is not null && SyntaxFactory.ParseExpression(expression).ContainsDiagnostics))
                    throw Error("SALEP1001", path, "scalars." + entry.Name, $"Invalid C# type or sample expression syntax in scalar definition: {type}; sampleExpression={expression}.");
                if (json is not null) { try { using var sample = JsonDocument.Parse(json); } catch (JsonException) { throw Error("SALEP1001", path, "scalars." + entry.Name + ".sampleJson", "Expected a string containing one JSON value."); } }
                scalars[entry.Name] = new(type, Bool(scalar, "isValueType", false, path), expression, json);
            }
        }
        return new()
        {
            UseNativeUnions = representation == "native", Scalars = scalars.ToImmutable(),
            UseHttpGet = Bool(root, "useHttpGet", false, path), EnableBatching = Bool(root, "enableBatching", false, path),
            OmitUnusedVariables = Bool(root, "omitUnusedVariables", false, path), InlineDefaultVariables = Bool(root, "inlineDefaultVariables", false, path),
            MaxGetUrlLength = Integer(root, "maxGetUrlLength", 2048, path, 1, int.MaxValue), IndentSize = Integer(root, "indentSize", 4, path, 0, 16)
        };
    }

    private static ImmutableArray<string> ReadSuites(JsonElement root, string path, bool samples)
    {
        if (!root.TryGetProperty("suites", out var suites)) return samples ? ["transport", "operations", "unions", "samples"] : ["transport", "operations", "unions"];
        if (suites.ValueKind != JsonValueKind.Array) throw Error("SALEP1001", path, "suites", "Expected an array.");
        var result = ImmutableArray.CreateBuilder<string>();
        foreach (var item in suites.EnumerateArray())
        {
            if (item.ValueKind != JsonValueKind.String || item.GetString() is not ("transport" or "operations" or "unions" or "samples")) throw Error("SALEP1001", path, "suites", "Unknown test suite.");
            var name = item.GetString()!;
            if (result.Contains(name)) throw Error("SALEP1001", path, "suites", "Duplicate suite.");
            if (name == "samples" && !samples) throw Error("SALEP1005", path, "suites", "The client does not emit samples.");
            result.Add(name);
        }
        return result.ToImmutable();
    }
    private static JsonDocument Read(string path)
    {
        if (!File.Exists(path)) throw Error("SALEP1001", path, "$", "Configuration file does not exist.");
        try { return JsonDocument.Parse(File.ReadAllText(path)); }
        catch (JsonException e) { throw Error("SALEP1001", path, "$", e.Message); }
    }
    private static void CheckDuplicates(JsonElement value, string path, string property)
    {
        if (value.ValueKind == JsonValueKind.Object)
        {
            var names = new HashSet<string>(StringComparer.Ordinal);
            foreach (var item in value.EnumerateObject())
            {
                if (!names.Add(item.Name)) throw Error("SALEP1001", path, property + "." + item.Name, "Duplicate property.");
                CheckDuplicates(item.Value, path, property + "." + item.Name);
            }
        }
        else if (value.ValueKind == JsonValueKind.Array) foreach (var item in value.EnumerateArray()) CheckDuplicates(item, path, property);
    }
    private static string? String(JsonElement root, string key, string path, bool required = false)
    {
        if (!root.TryGetProperty(key, out var value)) return required ? throw Error("SALEP1001", path, key, "Property is required.") : null;
        if (value.ValueKind != JsonValueKind.String || string.IsNullOrWhiteSpace(value.GetString())) throw Error("SALEP1001", path, key, "Expected a non-empty string.");
        return value.GetString();
    }
    private static bool Bool(JsonElement root, string key, bool fallback, string path)
    {
        if (!root.TryGetProperty(key, out var value)) return fallback;
        return value.ValueKind switch { JsonValueKind.True => true, JsonValueKind.False => false, _ => throw Error("SALEP1001", path, key, "Expected a boolean.") };
    }
    private static int Integer(JsonElement root, string key, int fallback, string path, int min, int max)
    {
        if (!root.TryGetProperty(key, out var value)) return fallback;
        if (value.ValueKind != JsonValueKind.Number || !value.TryGetInt32(out var number) || number < min || number > max) throw Error("SALEP1001", path, key, $"Expected an integer between {min} and {max}.");
        return number;
    }
    private static void ValidateName(string name, bool dotted, string path, string property)
    {
        if (!(dotted ? name.Split('.') : [name]).All(SyntaxFacts.IsValidIdentifier)) throw Error("SALEP1001", path, property, $"'{name}' is not a valid C# identifier or namespace.");
    }
}
