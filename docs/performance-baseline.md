# Lexer and parser performance baseline

The embedded-input results below are historical comparisons from the dependency-free harness. Current timings use BenchmarkDotNet over the versioned corpus; the separate allocation-budget gate remains deterministic and machine-local. See [Current BenchmarkDotNet workflow](#current-benchmarkdotnet-workflow).

Recorded 2026-09-25 before optimization. The dependency-free harness was then at `benchmarks/GraphQLParser.Benchmarks`; these numbers are retained as a historical baseline. It used 500 warmup operations, a full collection, `Stopwatch`, and `GC.GetAllocatedBytesForCurrentThread` for 10,000 operations. Three consecutive Release runs were used; throughput below is the median.

Environment: .NET 10.0.11 (`net10.0`), macOS 27.0.0, workstation GC. This historical harness did not use BenchmarkDotNet or another package.

| Input | Mode | Characters | Allocated bytes/op | Median ops/sec |
| --- | --- | ---: | ---: | ---: |
| Executable query and fragment | Lexer | 225 | 0.0 | 152,436 |
| SDL schema and type definitions | Lexer | 470 | 0.0 | 81,340 |
| Executable query and fragment | Strict parse | 225 | 11,760.5 | 93,152 |
| SDL schema and type definitions | Strict parse | 470 | 21,248.8 | 54,464 |
| Malformed executable document | Diagnostic parse | 58 | 5,953.6 | 83,099 |

The inputs are embedded in `Program.cs` and cover variables, defaults, directives, fragments, nested fields, schema and type definitions, interfaces, unions, enums, input objects, and one malformed argument. The checksums ensure the work is consumed and make accidental benchmark behavior changes visible.

## Allocation notes

The lexer path measured zero managed bytes per operation for these plain-name and punctuator-heavy inputs. Strict parse allocations are dominated by the parser's eager `List<Token>` materialization and the AST object graph. Each AST child list is first accumulated in a mutable `List<T>` and then copied into the immutable `AstNodeList<T>` array, so transient list storage and final child arrays are expected contributors. Source-backed names themselves do not copy their characters. Diagnostic parse additionally allocates diagnostic objects and the read-only result wrapper.

These attribution notes come from code inspection rather than an allocation profiler. The numbers are a local baseline, not a performance guarantee: the simple Stopwatch harness is sensitive to machine load and is intended for before/after comparisons on the same machine and runtime. Correctness and oracle checks remain independent gates.

## Release-candidate comparison

Three additional Release runs were collected after TASK-0026 on the same runtime and machine, using the same inputs and 10,000 iterations. The medians below compare that hardened parser with the original three-run baseline above. Allocation is bytes per operation; throughput is operations per second.

| Input | Mode | Baseline allocated | Release-candidate allocated | Baseline ops/sec | Release-candidate ops/sec |
| --- | --- | ---: | ---: | ---: | ---: |
| Executable query and fragment | Lexer | 0.0 | 0.0 | 152,436 | 152,046 |
| SDL schema and type definitions | Lexer | 0.0 | 0.0 | 81,340 | 84,037 |
| Executable query and fragment | Strict parse | 11,760.5 | 11,768.5 | 93,152 | 99,937 |
| SDL schema and type definitions | Strict parse | 21,248.8 | 21,256.8 | 54,464 | 57,020 |
| Malformed executable document | Diagnostic parse | 5,953.6 | 5,961.5 | 83,099 | 78,842 |

The hardened build adds about 8 allocated bytes per parse in these cases. Throughput is mixed: executable and SDL strict parsing improved in these runs, lexer and diagnostic throughput varied in opposite directions. The small fixed corpus and Stopwatch harness do not identify a safe source-pooling, interning, or slicing change with a repeatable net benefit. No such optimization was retained; the lexer remains allocation-free for these inputs and source-backed names already use `ReadOnlyMemory<char>`. The default parser's immutable source snapshot and explicit borrowed APIs keep ownership behavior clear.

These local measurements are evidence for the tested inputs only, not an SLA or a whole-workload performance claim. See [release readiness](release-readiness.md) for the conformance scope and remaining limitations.

## Versioned benchmark corpus

`benchmarks/corpus/v1` is the checked-in benchmark corpus for the September 2025 GraphQL specification target. Its manifest pins each UTF-8 file with SHA-256 and defines the operation used for each case. Run `python3 benchmarks/generate-corpus.py` to deterministically regenerate the files and manifest. The benchmark rejects hash mismatches, duplicate identifiers, missing required categories, unsupported corpus versions, and strict/diagnostic operation mismatches before measuring.

The eight cases cover small and large executable documents, small and large SDL documents, escaped and block strings, fragments, malformed input, and 120 levels of nested list values. Valid cases run lexer and strict parse operations; malformed input runs lexer and diagnostic parse. The report records raw samples and medians, corpus and manifest hashes, UTF-16 input lengths, checksums, runtime/framework, OS and architecture, processor count, GC mode, configuration, warmup, iteration, and repetition counts. The CPU identifier is included when the runtime exposes it.

Allocation ceilings in `benchmarks/Salep.Parser.Benchmarks/allocation-budgets.v1.json` were set to `ceil(three-repetition median bytes/op * 1.15 + 64 bytes)`. The gate uses a repeatable per-thread allocated-bytes measurement across each case and operation. BenchmarkDotNet's MemoryDiagnoser provides richer allocation diagnostics alongside timing results; the separate ceilings remain the pass/fail allocation guard. Throughput is informational and never suppresses or weakens parsing, conformance, or test-suite failures.

The checked-in report at `benchmarks/results/2026-09-25.json` is one local capture from the earlier custom harness, not a BenchmarkDotNet report or a performance guarantee. Compare BenchmarkDotNet captures only when the corpus version, runtime, configuration, and workload are compatible. Keep correctness tests and differential checks as independent mandatory gates.

## Current BenchmarkDotNet workflow

Run `./scripts/benchmark.sh` from the repository root. The script first runs the allocation-budget gate over all corpus operations, then launches the process-isolated BenchmarkDotNet suite in Release mode. BenchmarkDotNet reports lexer, strict-parse, and diagnostic-parse results separately, with corpus case IDs as parameters and `MemoryDiagnoser` enabled. Its generated reports and raw measurements are local artifacts; they are not checked in as a machine-independent baseline.

The benchmark project uses BenchmarkDotNet 0.16.0-preview.2 because the stable 0.15.4 release builds for `net11.0` but does not recognize the .NET 11 preview runtime. The selected release is a prerelease; move back to a stable version once it supports this runtime. The script forwards BenchmarkDotNet command-line options, for example `./scripts/benchmark.sh --filter '*Lexer*' --job Dry`. Use `BENCHMARK_TFM=net10.0` (the default) or `BENCHMARK_TFM=net11.0` to select the benchmark host framework. The .NET 11 target currently uses a preview SDK/runtime, so results should be compared only with runs using the same runtime version. `BENCHMARK_ALLOCATION_ITERATIONS` and `BENCHMARK_ALLOCATION_REPETITIONS` configure the separate allocation gate; its defaults are 1,000 measured operations and 3 repetitions.

The allocation gate checks each repetition against the existing per-case ceiling and reports the case and operation for any failure. BenchmarkDotNet timing results are for analysis, not CI thresholds: compare runs on the same machine, runtime, corpus, and configuration, and review the statistical spread before attributing a change to parser code. The process-isolated default BenchmarkDotNet toolchain is the source of timing comparisons; in-process jobs should not be used as performance baselines.
