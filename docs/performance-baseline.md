# Lexer and parser performance baseline

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

The hardened build adds about 8 allocated bytes per parse in these cases. Throughput is mixed: executable and SDL strict parsing improved in these runs, lexer and diagnostic throughput varied in opposite directions. The small fixed corpus and Stopwatch harness do not identify a safe source-pooling, interning, or slicing change with a repeatable net benefit. No such optimization was retained; the lexer remains allocation-free for these inputs and source-backed names already use `ReadOnlyMemory<char>`. Avoiding speculative pooling also preserves the AST's straightforward caller-memory ownership contract.

These local measurements are evidence for the tested inputs only, not an SLA or a whole-workload performance claim. See [release readiness](release-readiness.md) for the conformance scope and remaining limitations.
