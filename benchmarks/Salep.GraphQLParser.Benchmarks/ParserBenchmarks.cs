using BenchmarkDotNet.Attributes;

namespace Salep.GraphQLParser.Benchmarks;

public abstract class CorpusOperationBenchmark
{
    private string _source = string.Empty;

    [ParamsSource(nameof(CaseIds))]
    public string CaseId { get; set; } = string.Empty;

    public abstract IEnumerable<string> CaseIds { get; }

    [GlobalSetup]
    public void Setup() => _source = BenchmarkCorpus.GetCase(CaseId).Source;

    protected string Source => _source;
}

[MemoryDiagnoser]
public class LexerBenchmarks : CorpusOperationBenchmark
{
    public override IEnumerable<string> CaseIds => BenchmarkCorpus.Cases.Select(item => item.Definition.Id);

    [Benchmark]
    public int Lex() => BenchmarkOperations.Lex(Source);
}

[MemoryDiagnoser]
public class StrictParseBenchmarks : CorpusOperationBenchmark
{
    public override IEnumerable<string> CaseIds => BenchmarkCorpus.Cases
        .Where(item => item.Definition.Operations.Contains("strict-parse", StringComparer.Ordinal))
        .Select(item => item.Definition.Id);

    [Benchmark]
    public int StrictParse() => GraphQLParser.Parse(new SourceText(Source.AsMemory())).Definitions.Count;
}

[MemoryDiagnoser]
public class DiagnosticParseBenchmarks : CorpusOperationBenchmark
{
    public override IEnumerable<string> CaseIds => BenchmarkCorpus.Cases
        .Where(item => item.Definition.Operations.Contains("diagnostic-parse", StringComparer.Ordinal))
        .Select(item => item.Definition.Id);

    [Benchmark]
    public int DiagnosticParse() => BenchmarkOperations.DiagnosticChecksum(GraphQLParser.ParseWithDiagnostics(new SourceText(Source.AsMemory())));
}