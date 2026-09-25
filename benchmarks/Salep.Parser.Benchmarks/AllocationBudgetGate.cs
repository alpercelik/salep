using System.Runtime;
using System.Runtime.InteropServices;
using System.Text.Json;

internal static class AllocationBudgetGate
{
    public static int Run(CorpusSnapshot corpus, string[] args)
    {
        var (iterations, repetitions) = ParseOptions(args);
        var budgetsPath = Path.Combine(AppContext.BaseDirectory, "allocation-budgets.v1.json");
        var budgetDocument = JsonSerializer.Deserialize<AllocationBudgetDocument>(File.ReadAllText(budgetsPath),
            new JsonSerializerOptions { PropertyNameCaseInsensitive = true })
            ?? throw new InvalidOperationException("The allocation budget file is empty or invalid.");
        if (budgetDocument.Version != 1 || budgetDocument.CorpusVersion != corpus.Version)
            throw new InvalidOperationException("The allocation budgets do not match benchmark corpus version 1.");
        if (budgetDocument.Budgets.Length == 0)
            throw new InvalidOperationException("Allocation budgets are not initialized.");

        var budgetMap = budgetDocument.Budgets.ToDictionary(item => (item.CaseId, item.Operation));
        var failures = new List<string>();
        var observed = new HashSet<(string CaseId, string Operation)>();
        var warmupIterations = Math.Min(500, Math.Max(25, iterations / 10));

        foreach (var item in corpus.Cases)
        {
            foreach (var operation in item.Definition.Operations)
            {
                observed.Add((item.Definition.Id, operation));
                if (!budgetMap.TryGetValue((item.Definition.Id, operation), out var budget))
                {
                    failures.Add($"No allocation budget for {item.Definition.Id}/{operation}.");
                    continue;
                }

                for (var repetition = 0; repetition < repetitions; repetition++)
                {
                    var checksum = 0;
                    for (var index = 0; index < warmupIterations; index++)
                        checksum += BenchmarkOperations.Execute(operation, item.Source);

                    GC.Collect();
                    GC.WaitForPendingFinalizers();
                    GC.Collect();
                    var allocatedBefore = GC.GetAllocatedBytesForCurrentThread();
                    for (var index = 0; index < iterations; index++)
                        checksum += BenchmarkOperations.Execute(operation, item.Source);
                    var allocatedBytes = GC.GetAllocatedBytesForCurrentThread() - allocatedBefore;
                    var bytesPerOperation = (double)allocatedBytes / iterations;
                    Console.WriteLine($"{item.Definition.Id}/{operation} repetition {repetition + 1}: {bytesPerOperation:F1} B/op (budget {budget.MaximumBytesPerOperation} B/op), checksum {checksum}.");
                    if (bytesPerOperation > budget.MaximumBytesPerOperation)
                        failures.Add($"{item.Definition.Id}/{operation} repetition {repetition + 1} allocated {bytesPerOperation:F1} B/op; budget is {budget.MaximumBytesPerOperation} B/op.");
                }
            }
        }

        if (budgetMap.Keys.Except(observed).Any())
            failures.Add("The allocation budget contains cases absent from the selected benchmark corpus.");

        Console.WriteLine($"Allocation gate environment: {RuntimeInformation.FrameworkDescription}; {RuntimeInformation.OSDescription}; {RuntimeInformation.ProcessArchitecture}; server GC={GCSettings.IsServerGC}; iterations={iterations}; warmup={warmupIterations}; repetitions={repetitions}.");
        if (failures.Count == 0)
        {
            Console.WriteLine($"Allocation budgets passed for {observed.Count} corpus operations. Corpus manifest SHA-256: {corpus.ManifestSha256}.");
            return 0;
        }

        Console.Error.WriteLine("Benchmark allocation regression:\n" + string.Join("\n", failures));
        return 1;
    }

    private static (int Iterations, int Repetitions) ParseOptions(string[] args)
    {
        var iterations = 1_000;
        var repetitions = 3;
        for (var index = 0; index < args.Length; index++)
        {
            var name = args[index];
            if (index + 1 >= args.Length || !int.TryParse(args[++index], out var value) || value <= 0)
                throw new ArgumentException($"Expected a positive integer after {name}.");
            switch (name)
            {
                case "--iterations":
                    iterations = value;
                    break;
                case "--repetitions":
                    repetitions = value;
                    break;
                default:
                    throw new ArgumentException($"Unknown allocation gate option '{name}'.");
            }
        }
        return (iterations, repetitions);
    }

    private sealed record AllocationBudgetDocument(int Version, int CorpusVersion, string Method, AllocationBudget[] Budgets);
    private sealed record AllocationBudget(string CaseId, string Operation, int MaximumBytesPerOperation);
}
