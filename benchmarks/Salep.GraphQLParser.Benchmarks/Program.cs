using System.Globalization;
using BenchmarkDotNet.Running;

namespace Salep.GraphQLParser.Benchmarks;

internal static class Program
{
    private static int Main(string[] args)
    {
        CultureInfo.CurrentCulture = CultureInfo.InvariantCulture;
        CultureInfo.CurrentUICulture = CultureInfo.InvariantCulture;

        var corpus = BenchmarkCorpus.LoadVerified();
        if (args.Length > 0 && string.Equals(args[0], "--allocation-gate", StringComparison.Ordinal))
            return AllocationBudgetGate.Run(corpus, args[1..]);

        Console.WriteLine($"Verified benchmark corpus v{corpus.Version}: {corpus.Cases.Count} cases, manifest {corpus.ManifestSha256}.");
        var summaries = BenchmarkSwitcher.FromAssembly(typeof(Program).Assembly).Run(args);
        return summaries.Any(summary => summary.HasCriticalValidationErrors || summary.Reports.Any(report => !report.Success))
            ? 1
            : 0;
    }
}