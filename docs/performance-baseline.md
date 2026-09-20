# Lexer and parser performance baseline

The original embedded-input results below are retained as historical comparisons. The current reproducible benchmark uses the versioned corpus and repeated raw measurements described in [Versioned benchmark corpus](#versioned-benchmark-corpus).

Recorded 2026-09-20 before optimization. The dependency-free harness is `benchmarks/GraphQLParser.Benchmarks`; run it from the repository root with `./scripts/benchmark.sh 10000`. It performs 500 warmup operations, forces a full collection, then measures 10,000 operations using `Stopwatch` and `GC.GetAllocatedBytesForCurrentThread`. Three consecutive Release runs were used; throughput below is the median. Runtime, operating system, GC mode, iteration count, input character count, and checksums are printed on every run.

Environment: .NET 10.0.11 (`net10.0`), macOS 27.0.0, workstation GC. No BenchmarkDotNet or other package is used, so the harness runs without adding NuGet dependencies.

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

`benchmarks/corpus/v1` is the checked-in benchmark corpus for the September 2025 GraphQL specification target. Its manifest pins each UTF-8 file with SHA-256 and defines the operation used for each case. Run `python3 benchmarks/generate-corpus.py` to deterministically regenerate the files and manifest. The benchmark rejects hash mismatches, duplicate identifiers, missing required categories, unsupported corpus versions, and strict/diagnostic operation mismatches.

The eight cases cover small and large executable documents, small and large SDL documents, escaped and block strings, fragments, malformed input, and 120 levels of nested list values. Valid cases run lexer and strict parse operations; malformed input runs lexer and diagnostic parse. The report records raw samples and medians, corpus and manifest hashes, UTF-16 input lengths, checksums, runtime/framework, OS and architecture, processor count, GC mode, configuration, warmup, iteration, and repetition counts. The CPU identifier is included when the runtime exposes it.

Reproduce the checked-in local capture with `./scripts/benchmark.sh 1000 3 benchmarks/results/2026-09-20.json`. Output includes three separate timing and allocation samples per case/operation. Allocation ceilings in `benchmarks/GraphQLParser.Benchmarks/allocation-budgets.v1.json` use `ceil(three-repetition median bytes/op * 1.15 + 64 bytes)`. They are deterministic allocation-only guardrails; throughput and the recorded local rates are informational because machine load affects Stopwatch measurements. A throughput change never suppresses or weakens parsing, conformance, or test-suite failures.

The checked-in report at `benchmarks/results/2026-09-20.json` is one local capture, not a performance guarantee. Compare captures only when the corpus version, runtime, configuration, and workload are compatible. Keep correctness tests and differential checks as independent mandatory gates.
