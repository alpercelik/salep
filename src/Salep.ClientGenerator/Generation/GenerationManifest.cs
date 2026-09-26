using System.Collections.Immutable;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Salep.ClientGenerator.Config;

namespace Salep.ClientGenerator.Generation;

internal sealed record SymbolContract(string Kind, string Name, string Namespace, string Owner, string Signature);

internal sealed record GenerationManifest
{
    public const string FileName = ".salep.manifest.json";
    public const int CurrentVersion = 1;
    public int Version { get; init; } = CurrentVersion;
    public string Producer { get; init; } = typeof(SalepGenerator).Assembly.GetName().Version!.ToString();
    public required string Configuration { get; init; }
    public string ConfigurationIdentity { get; init; } = "";
    public required string Kind { get; init; }
    public required string Output { get; init; }
    public required GeneratorConfig Settings { get; init; }
    public bool EmitsSample { get; init; }
    public string RequiredFramework { get; init; } = "net10.0";
    public string RequiredLanguage { get; init; } = "14";
    public ImmutableSortedDictionary<string, string> Inputs { get; init; } = ImmutableSortedDictionary<string, string>.Empty;
    public ImmutableSortedDictionary<string, string> Dependencies { get; init; } = ImmutableSortedDictionary<string, string>.Empty;
    public ImmutableSortedDictionary<string, SymbolContract> Symbols { get; init; } = ImmutableSortedDictionary<string, SymbolContract>.Empty;
    public ImmutableSortedDictionary<string, string> Files { get; init; } = ImmutableSortedDictionary<string, string>.Empty;
    public string Fingerprint { get; init; } = "";

    public GenerationManifest Seal() => this with { Fingerprint = Hash(Canonical(JsonSerializer.SerializeToElement(this with { Fingerprint = "" }, JsonOptions))) };
    internal static readonly JsonSerializerOptions JsonOptions = new() { WriteIndented = true };
    public string Serialize()
    {
        // Immutable dictionary traversal can differ between processes; publish sorted JSON too.
        using var document = JsonDocument.Parse(Canonical(JsonSerializer.SerializeToElement(this, JsonOptions)));
        return JsonSerializer.Serialize(document.RootElement, JsonOptions) + "\n";
    }
    public static string Hash(string value) => Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(value)));
    public static string HashFile(string path) => Convert.ToHexString(SHA256.HashData(File.ReadAllBytes(path)));
    private static string Canonical(JsonElement element) => element.ValueKind switch
    {
        JsonValueKind.Object => "{" + string.Join(",", element.EnumerateObject().OrderBy(p => p.Name, StringComparer.Ordinal).Select(p => JsonSerializer.Serialize(p.Name) + ":" + Canonical(p.Value))) + "}",
        JsonValueKind.Array => "[" + string.Join(",", element.EnumerateArray().Select(Canonical)) + "]",
        _ => element.GetRawText()
    };

    public static GenerationManifest Read(string output, string config)
    {
        var path = Path.Combine(output, FileName);
        try
        {
            var manifest = JsonSerializer.Deserialize<GenerationManifest>(File.ReadAllText(path), JsonOptions);
            if (manifest is null || manifest.Version != CurrentVersion || manifest.Fingerprint != manifest.Seal().Fingerprint)
                throw new InvalidDataException("Unsupported or incomplete manifest.");
            var owner = string.IsNullOrEmpty(manifest.ConfigurationIdentity)
                ? manifest.Configuration : Path.GetFullPath(manifest.ConfigurationIdentity, output);
            if (!ConfigurationResolver.PathComparer.Equals(owner, config))
                throw ConfigurationResolver.Error("SALEP2003", config, "output", $"Output '{output}' belongs to '{manifest.Configuration}'.", "Use a separate output directory.");
            if (manifest.Files.Keys.Any(name => Path.GetFileName(name) != name || name == FileName)) throw new InvalidDataException("Invalid output file inventory.");
            return manifest;
        }
        catch (ConfigurationException) { throw; }
        catch (Exception e) when (e is IOException or InvalidDataException or JsonException or NotSupportedException or InvalidOperationException or ArgumentException)
        {
            throw ConfigurationResolver.Error("SALEP2001", config, "manifest", $"Cannot read verified contract '{path}': {e.Message}", $"Generate '{config}' first; remove obsolete pre-v1 generated artifacts when migrating.");
        }
    }
    public void Verify(GenerationManifest expected)
    {
        if (Fingerprint != expected.Fingerprint || Producer != expected.Producer || Files.Any(file => !File.Exists(Path.Combine(Output, file.Key)) || HashFile(Path.Combine(Output, file.Key)) != file.Value))
            throw ConfigurationResolver.Error("SALEP2002", Configuration, "manifest", "Client contract is stale or generated files are missing/changed.", $"Regenerate '{Configuration}' before generating dependents.");
    }
}
