using System.Diagnostics;
using System.Globalization;
using System.Runtime;
using System.Runtime.InteropServices;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using GraphQLParser;
using Parser = global::GraphQLParser.GraphQLParser;

CultureInfo.CurrentCulture = CultureInfo.InvariantCulture;
CultureInfo.CurrentUICulture = CultureInfo.InvariantCulture;

var positionalArguments = args.Where(argument => !argument.StartsWith("--", StringComparison.Ordinal)).ToArray();
var iterations = positionalArguments.Length > 0 && int.TryParse(positionalArguments[0], out var configuredIterations) && configuredIterations > 0
    ? configuredIterations
    : 1_000;
var repetitions = positionalArguments.Length > 1 && int.TryParse(positionalArguments[1], out var configuredRepetitions) && configuredRepetitions > 0
    ? configuredRepetitions
    : 3;
var outputPath = positionalArguments.Length > 2 ? positionalArguments[2] : null;
var checkBudgets = !args.Contains("--skip-budgets", StringComparer.Ordinal);
var warmupIterations = Math.Min(500, Math.Max(25, iterations / 10));

var corpusDirectory = Path.Combine(AppContext.BaseDirectory, "corpus", "v1");
var manifestPath = Path.Combine(corpusDirectory, "cases.json");
var manifestBytes = File.ReadAllBytes(manifestPath);
var jsonOptions = new JsonSerializerOptions { PropertyNameCaseInsensitive = true };
var corpus = JsonSerializer.Deserialize<CorpusManifest>(manifestBytes, jsonOptions)
    ?? throw new InvalidOperationException("The benchmark corpus manifest is empty or invalid.");
if (corpus.Version != 1) throw new InvalidOperationException($"Unsupported benchmark corpus version {corpus.Version}.");

var requiredCategories = corpus.RequiredCategories.ToHashSet(StringComparer.Ordinal);
var observedCategories = corpus.Cases.Select(item => item.Category).ToHashSet(StringComparer.Ordinal);
if (!requiredCategories.SetEquals(observedCategories))
    throw new InvalidOperationException("The benchmark corpus does not match the required versioned categories.");
if (corpus.Cases.Select(item => item.Id).Distinct(StringComparer.Ordinal).Count() != corpus.Cases.Length)
    throw new InvalidOperationException("The benchmark corpus contains duplicate case identifiers.");
foreach (var item in corpus.Cases)
{
    var expectedOperations = item.Valid ? new[] { "lexer", "strict-parse" } : new[] { "lexer", "diagnostic-parse" };
    if (!item.Operations.SequenceEqual(expectedOperations, StringComparer.Ordinal))
        throw new InvalidOperationException($"Benchmark operations do not match validity for corpus case {item.Id}.");
}

var loadedCases = new List<LoadedCase>(corpus.Cases.Length);
foreach (var definition in corpus.Cases)
{
    var fullPath = Path.GetFullPath(Path.Combine(corpusDirectory, definition.Path));
    if (!fullPath.StartsWith(corpusDirectory + Path.DirectorySeparatorChar, StringComparison.Ordinal))
        throw new InvalidOperationException($"Corpus path escapes the version directory: {definition.Path}");
    var sourceBytes = File.ReadAllBytes(fullPath);
    var actualHash = Convert.ToHexString(SHA256.HashData(sourceBytes)).ToLowerInvariant();
    if (!string.Equals(actualHash, definition.Sha256, StringComparison.Ordinal))
        throw new InvalidOperationException($"Benchmark corpus hash mismatch for {definition.Id}: expected {definition.Sha256}, found {actualHash}.");
    var source = new UTF8Encoding(false, true).GetString(sourceBytes);
    loadedCases.Add(new LoadedCase(definition, source, actualHash));
}

var rawSamples = new List<BenchmarkSample>();
foreach (var benchmarkCase in loadedCases)
{
    foreach (var operation in benchmarkCase.Definition.Operations)
    {
        for (var repetition = 1; repetition <= repetitions; repetition++)
            rawSamples.Add(Measure(benchmarkCase, operation, repetition));
    }
}

var summaries = rawSamples
    .GroupBy(sample => (sample.CaseId, sample.Operation))
    .Select(group => new BenchmarkSummary(
        group.Key.CaseId,
        group.Key.Operation,
        Median(group.Select(sample => sample.BytesPerOperation)),
        Median(group.Select(sample => sample.OperationsPerSecond))))
    .ToArray();

var report = new BenchmarkReport(
    DateTimeOffset.UtcNow,
    1,
    corpus.SpecificationTarget,
    Convert.ToHexString(SHA256.HashData(manifestBytes)).ToLowerInvariant(),
    Environment.Version.ToString(),
    RuntimeInformation.FrameworkDescription,
    RuntimeInformation.OSDescription,
    RuntimeInformation.OSArchitecture.ToString(),
    RuntimeInformation.ProcessArchitecture.ToString(),
    Environment.ProcessorCount,
    Environment.GetEnvironmentVariable("PROCESSOR_IDENTIFIER"),
    GCSettings.IsServerGC,
    GCSettings.LatencyMode.ToString(),
    "Release",
    iterations,
    warmupIterations,
    repetitions,
    loadedCases.Select(item => new CorpusCaseResult(
        item.Definition.Id,
        item.Definition.Category,
        item.Source.Length,
        item.SourceHash,
        item.Definition.Operations)).ToArray(),
    rawSamples,
    summaries);

var serializedReport = JsonSerializer.Serialize(report, new JsonSerializerOptions { WriteIndented = true });
if (outputPath is null)
{
    Console.WriteLine(serializedReport);
}
else
{
    var absoluteOutputPath = Path.GetFullPath(outputPath);
    Directory.CreateDirectory(Path.GetDirectoryName(absoluteOutputPath)!);
    File.WriteAllText(absoluteOutputPath, serializedReport + Environment.NewLine, new UTF8Encoding(false));
    Console.WriteLine($"Wrote {rawSamples.Count} raw benchmark samples to {absoluteOutputPath}");
}

if (checkBudgets)
    VerifyAllocationBudgets(rawSamples, jsonOptions);

Console.WriteLine($"Benchmark corpus v{corpus.Version} passed integrity checks ({loadedCases.Count} cases, {rawSamples.Count} raw samples).");

BenchmarkSample Measure(LoadedCase benchmarkCase, string operation, int repetition)
{
    var checksum = 0;
    for (var index = 0; index < warmupIterations; index++)
        checksum += Execute(operation, benchmarkCase.Source);

    GC.Collect();
    GC.WaitForPendingFinalizers();
    GC.Collect();
    var allocatedBefore = GC.GetAllocatedBytesForCurrentThread();
    var startedAt = Stopwatch.GetTimestamp();
    for (var index = 0; index < iterations; index++)
        checksum += Execute(operation, benchmarkCase.Source);
    var elapsed = Stopwatch.GetElapsedTime(startedAt);
    var allocatedBytes = GC.GetAllocatedBytesForCurrentThread() - allocatedBefore;

    return new BenchmarkSample(
        benchmarkCase.Definition.Id,
        operation,
        repetition,
        benchmarkCase.Source.Length,
        iterations,
        elapsed.TotalMilliseconds,
        (double)allocatedBytes / iterations,
        iterations / elapsed.TotalSeconds,
        checksum);
}

static int Execute(string operation, string source) => operation switch
{
    "lexer" => Lex(source),
    "strict-parse" => Parser.Parse(new SourceText(source.AsMemory())).Definitions.Count,
    "diagnostic-parse" => DiagnosticChecksum(Parser.ParseWithDiagnostics(new SourceText(source.AsMemory()))),
    _ => throw new InvalidOperationException($"Unknown benchmark operation '{operation}'."),
};

static int DiagnosticChecksum(GraphQLParseResult result) => result.Diagnostics.Count + (result.Document?.Definitions.Count ?? 0);

static int Lex(string source)
{
    var lexer = new GraphQLLexer(new SourceText(source.AsMemory()));
    var count = 0;
    while (lexer.NextToken().Kind != TokenKind.EndOfFile) count++;
    return count;
}

static double Median(IEnumerable<double> values)
{
    var ordered = values.Order().ToArray();
    var middle = ordered.Length / 2;
    return ordered.Length % 2 == 0 ? (ordered[middle - 1] + ordered[middle]) / 2 : ordered[middle];
}

static void VerifyAllocationBudgets(List<BenchmarkSample> samples, JsonSerializerOptions jsonOptions)
{
    var budgetsPath = Path.Combine(AppContext.BaseDirectory, "allocation-budgets.v1.json");
    var budgets = JsonSerializer.Deserialize<AllocationBudgetDocument>(File.ReadAllText(budgetsPath), jsonOptions)
        ?? throw new InvalidOperationException("The allocation budget file is empty or invalid.");
    if (budgets.Version != 1 || budgets.CorpusVersion != 1)
        throw new InvalidOperationException("The allocation budgets do not match benchmark corpus version 1.");
    if (budgets.Budgets.Length == 0)
        throw new InvalidOperationException("Allocation budgets are not initialized. Run the benchmark with --skip-budgets to capture a baseline first.");

    var budgetMap = budgets.Budgets.ToDictionary(item => (item.CaseId, item.Operation));
    var failures = new List<string>();
    foreach (var sample in samples)
    {
        if (!budgetMap.TryGetValue((sample.CaseId, sample.Operation), out var budget))
        {
            failures.Add($"No allocation budget for {sample.CaseId}/{sample.Operation}.");
            continue;
        }
        if (sample.BytesPerOperation > budget.MaximumBytesPerOperation)
            failures.Add($"{sample.CaseId}/{sample.Operation} repetition {sample.Repetition} allocated {sample.BytesPerOperation:F1} B/op; budget is {budget.MaximumBytesPerOperation} B/op.");
    }

    if (budgetMap.Keys.Except(samples.Select(sample => (sample.CaseId, sample.Operation))).Any())
        failures.Add("The allocation budget contains cases absent from the selected benchmark corpus.");
    if (failures.Count != 0)
        throw new InvalidOperationException("Benchmark allocation regression:\n" + string.Join("\n", failures));
}

internal sealed record CorpusManifest(int Version, string SpecificationTarget, string[] RequiredCategories, CorpusCase[] Cases);
internal sealed record CorpusCase(string Id, string Category, string Path, bool Valid, string[] Operations, string Sha256);
internal sealed record LoadedCase(CorpusCase Definition, string Source, string SourceHash);
internal sealed record CorpusCaseResult(string Id, string Category, int Utf16Characters, string Sha256, string[] Operations);
internal sealed record BenchmarkSample(string CaseId, string Operation, int Repetition, int Utf16Characters, int Iterations, double ElapsedMilliseconds, double BytesPerOperation, double OperationsPerSecond, int Checksum);
internal sealed record BenchmarkSummary(string CaseId, string Operation, double MedianBytesPerOperation, double MedianOperationsPerSecond);
internal sealed record BenchmarkReport(DateTimeOffset CapturedAtUtc, int CorpusVersion, string SpecificationTarget, string CorpusManifestSha256, string RuntimeVersion, string Framework, string OperatingSystem, string OperatingSystemArchitecture, string ProcessArchitecture, int LogicalProcessorCount, string? ProcessorIdentifier, bool ServerGc, string GcLatencyMode, string Configuration, int IterationsPerSample, int WarmupIterations, int Repetitions, CorpusCaseResult[] Cases, List<BenchmarkSample> Samples, BenchmarkSummary[] Summary);
internal sealed record AllocationBudgetDocument(int Version, int CorpusVersion, string Method, AllocationBudget[] Budgets);
internal sealed record AllocationBudget(string CaseId, string Operation, int MaximumBytesPerOperation);
