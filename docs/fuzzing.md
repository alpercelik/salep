# Deterministic parser fuzzing

Run the committed generator and mutation suite with `./scripts/fuzz.sh [seed] [case-count]`. The default is seed `20260925` and 512 cases. The xorshift32 generator is implemented in the test project, so output does not depend on `System.Random` implementation changes. Repeating the same seed and count produces the same source cases.

The test alternates valid generated documents with one-character insertions, deletions, and replacements. Its templates cover executable operations and fragments, SDL definitions and extensions, quoted and block strings, numbers, nested values, selection nesting, and directives. Strict parsing may accept a mutation or report `GraphQLSyntaxException` / `GraphQLLexicalException`; diagnostic parsing must stay within the documented diagnostic limit and source bounds. Unexpected exceptions and valid-input rejection report the seed, case number, mode, failure, and a delta-reduced input.

Reproduce the recorded run with:

```sh
./scripts/fuzz.sh 20260925 512
```

The ordinary parser and oracle suites remain required after fuzz runs. This bounded in-process suite finds deterministic regressions; it is not a proof against process-level exhaustion on arbitrarily deep input, which is covered by the resource-bound task.
