using System.Security.Cryptography;
using System.Text;
using System.Text.Json;

namespace Salep.GraphQLParser.Benchmarks;

internal static class BenchmarkCorpus
{
    private static readonly JsonSerializerOptions ManifestJsonOptions = new()
    {
        PropertyNameCaseInsensitive = true,
    };

    private static readonly Lazy<CorpusSnapshot> Snapshot = new(Load);

    public static IReadOnlyList<LoadedCase> Cases => Snapshot.Value.Cases;
    public static int Version => Snapshot.Value.Version;
    public static string ManifestSha256 => Snapshot.Value.ManifestSha256;

    public static LoadedCase GetCase(string id) => Snapshot.Value.ById.TryGetValue(id, out var item)
        ? item
        : throw new InvalidOperationException($"Unknown benchmark corpus case '{id}'.");

    public static CorpusSnapshot LoadVerified() => Snapshot.Value;

    private static CorpusSnapshot Load()
    {
        var corpusDirectory = FindCorpusDirectory();
        var manifestPath = Path.Combine(corpusDirectory, "cases.json");
        var manifestBytes = File.ReadAllBytes(manifestPath);
        var manifest = JsonSerializer.Deserialize<CorpusManifest>(manifestBytes, ManifestJsonOptions)
                       ?? throw new InvalidOperationException("The benchmark corpus manifest is empty or invalid.");
        if (manifest.Version != 1)
            throw new InvalidOperationException($"Unsupported benchmark corpus version {manifest.Version}.");

        var requiredCategories = manifest.RequiredCategories.ToHashSet(StringComparer.Ordinal);
        var observedCategories = manifest.Cases.Select(item => item.Category).ToHashSet(StringComparer.Ordinal);
        if (!requiredCategories.SetEquals(observedCategories))
            throw new InvalidOperationException("The benchmark corpus does not match the required versioned categories.");
        if (manifest.Cases.Select(item => item.Id).Distinct(StringComparer.Ordinal).Count() != manifest.Cases.Length)
            throw new InvalidOperationException("The benchmark corpus contains duplicate case identifiers.");

        var loadedCases = new List<LoadedCase>(manifest.Cases.Length);
        foreach (var definition in manifest.Cases)
        {
            var expectedOperations = definition.Valid ? new[] { "lexer", "strict-parse" } : new[] { "lexer", "diagnostic-parse" };
            if (!definition.Operations.SequenceEqual(expectedOperations, StringComparer.Ordinal))
                throw new InvalidOperationException($"Benchmark operations do not match validity for corpus case {definition.Id}.");

            var fullPath = Path.GetFullPath(Path.Combine(corpusDirectory, definition.Path));
            if (!fullPath.StartsWith(corpusDirectory + Path.DirectorySeparatorChar, StringComparison.Ordinal))
                throw new InvalidOperationException($"Corpus path escapes the version directory: {definition.Path}");

            var sourceBytes = File.ReadAllBytes(fullPath);
            var actualHash = Convert.ToHexString(SHA256.HashData(sourceBytes)).ToLowerInvariant();
            if (!string.Equals(actualHash, definition.Sha256, StringComparison.Ordinal))
                throw new InvalidOperationException($"Benchmark corpus hash mismatch for {definition.Id}: expected {definition.Sha256}, found {actualHash}.");

            loadedCases.Add(new LoadedCase(definition, new UTF8Encoding(false, true).GetString(sourceBytes), actualHash));
        }

        return new CorpusSnapshot(
            manifest.Version,
            Convert.ToHexString(SHA256.HashData(manifestBytes)).ToLowerInvariant(),
            loadedCases,
            loadedCases.ToDictionary(item => item.Definition.Id, StringComparer.Ordinal));
    }

    private static string FindCorpusDirectory()
    {
        var candidates = new List<string>
        {
            Path.Combine(AppContext.BaseDirectory, "corpus", "v1"),
            Path.Combine(Path.GetDirectoryName(typeof(BenchmarkCorpus).Assembly.Location)!, "corpus", "v1"),
        };

        for (var directory = new DirectoryInfo(AppContext.BaseDirectory); directory is not null; directory = directory.Parent)
            candidates.Add(Path.Combine(directory.FullName, "benchmarks", "Salep.GraphQLParser.Benchmarks", "corpus", "v1"));

        var found = candidates.FirstOrDefault(candidate => File.Exists(Path.Combine(candidate, "cases.json")));
        return found is not null
            ? Path.GetFullPath(found)
            : throw new DirectoryNotFoundException("Could not locate the versioned benchmark corpus beside the benchmark output or repository checkout.");
    }
}

internal sealed record CorpusManifest(int Version, string SpecificationTarget, string[] RequiredCategories, CorpusCase[] Cases);
internal sealed record CorpusCase(string Id, string Category, string Path, bool Valid, string[] Operations, string Sha256);
internal sealed record LoadedCase(CorpusCase Definition, string Source, string SourceHash);
internal sealed record CorpusSnapshot(int Version, string ManifestSha256, IReadOnlyList<LoadedCase> Cases, IReadOnlyDictionary<string, LoadedCase> ById);
