using Salep.GraphQLParser;
using Xunit;

namespace Salep.GraphQLParser.Tests;

public sealed class DeterministicParserFuzzTests
{
    private const int DefaultSeed = 20_260_925;
    private static readonly string[] value = new[] { "?", "!", "...", "@", "\"", "0" };

    [Fact]
    public void SeededGeneratedAndMutatedInputsDoNotCrashOrLoseDiagnostics()
    {
        var seed = ReadSetting("GRAPHQL_FUZZ_SEED", DefaultSeed);
        var caseCount = ReadSetting("GRAPHQL_FUZZ_CASES", 512);
        Assert.InRange(caseCount, 1, 20_000);
        var random = new DeterministicRandom(unchecked((uint)seed));
        var exercisedKinds = new HashSet<int>();

        for (var index = 0; index < caseCount; index++)
        {
            var kind = (index / 2) % 8;
            exercisedKinds.Add(kind);
            var validSource = GenerateValidSource(random, index, kind);
            var shouldBeValid = index % 2 == 0;
            var source = shouldBeValid ? validSource : Mutate(random, validSource);

            CheckCase(source, shouldBeValid, seed, index, "strict", () => ParseStrict(source, shouldBeValid));
            CheckCase(source, shouldBeValid, seed, index, "diagnostic", () => ParseDiagnostic(source, shouldBeValid));
        }

        Assert.Equal(8, exercisedKinds.Count);
    }

    private static string? ParseStrict(string source, bool shouldBeValid)
    {
        try
        {
            _ = GraphQLParser.Parse(new SourceText(source.AsMemory()));
            return null;
        }
        catch (GraphQLSyntaxException) when (!shouldBeValid)
        {
            return null;
        }
        catch (GraphQLLexicalException) when (!shouldBeValid)
        {
            return null;
        }
        catch (Exception exception)
        {
            return $"{exception.GetType().Name}: {exception.Message}";
        }
    }

    private static string? ParseDiagnostic(string source, bool shouldBeValid)
    {
        try
        {
            var result = GraphQLParser.ParseWithDiagnostics(new SourceText(source.AsMemory()));
            if (shouldBeValid && !result.Success) return "valid generated input produced diagnostics";
            if (!shouldBeValid && !result.Success && result.Diagnostics.Count == 0) return "invalid input produced no diagnostic";
            if (result.Diagnostics.Count > GraphQLParser.MaximumDiagnosticCount) return "diagnostic limit exceeded";
            foreach (var diagnostic in result.Diagnostics)
            {
                if (diagnostic.Location.Start < 0 || diagnostic.Location.End > source.Length)
                    return $"diagnostic location out of bounds: {diagnostic.Location}";
            }

            return null;
        }
        catch (Exception exception)
        {
            return $"{exception.GetType().Name}: {exception.Message}";
        }
    }

    private static void CheckCase(string source, bool shouldBeValid, int seed, int index, string mode, Func<string?> check)
    {
        var failure = check();
        if (failure is null) return;
        var reduced = Reduce(source, candidate => FailureRemains(candidate, shouldBeValid, mode));
        throw new Xunit.Sdk.XunitException($"Fuzz failure: seed={seed}, case={index}, mode={mode}, expectedValid={shouldBeValid}, failure={failure}\nReduced input:\n{reduced}");
    }

    private static bool FailureRemains(string source, bool shouldBeValid, string mode)
    {
        var failure = mode == "strict" ? ParseStrict(source, shouldBeValid) : ParseDiagnostic(source, shouldBeValid);
        return failure is not null;
    }

    private static string Reduce(string input, Func<string, bool> stillFails)
    {
        var current = input;
        var granularity = 2;
        var attempts = 0;
        while (current.Length > 1 && attempts++ < 2_000)
        {
            var chunkSize = (int)Math.Ceiling((double)current.Length / granularity);
            var reduced = false;
            for (var start = 0; start < current.Length; start += chunkSize)
            {
                var length = Math.Min(chunkSize, current.Length - start);
                var candidate = current.Remove(start, length);
                if (candidate.Length == 0 || !stillFails(candidate)) continue;
                current = candidate;
                granularity = Math.Max(2, granularity - 1);
                reduced = true;
                break;
            }

            if (reduced) continue;
            if (granularity >= current.Length) break;
            granularity = Math.Min(current.Length, granularity * 2);
        }

        return current;
    }

    private static string GenerateValidSource(DeterministicRandom random, int index, int kind)
    {
        var suffix = $"{index}_{random.Next(10_000)}".Replace('_', 'N');
        var number = random.Next(1_000);
        var executable = kind switch
        {
            0 => $"{{ field{suffix}(value: -{number}.25e+2) {{ child {{ leaf }} }} }}",
            1 => $"query Q{suffix}($v: [Int!]! = [{number}, 2.5e-1]) @trace {{ alias: field(value: {{ text: \"s{number}\\n\", values: [$v, null, ENUM] }}) {{ ...F{suffix} ... on T {{ id }} }} }} fragment F{suffix} on T {{ id name }}",
            2 => $"query S{suffix} {{ field(text: \"\"\"block {number}\"\"\", flag: true, missing: null) }}",
            3 => $"query N{suffix}($x: String = \"escaped\\t{number}\") {{ field(arg: $x) }}",
            4 => $"type T{suffix} implements & I{suffix} @tag {{ field(arg: Int = [{number}, {number + 1}] @bound): [String!]! @deprecated }} interface I{suffix} {{ id: ID! }}",
            5 => $"union U{suffix} = T{suffix} | Other enum E{suffix} {{ FIRST SECOND }} input In{suffix} @oneOf {{ text: String ids: [ID!] }}",
            6 => $"directive @d{suffix}(value: String = \"{number}\") repeatable on | FIELD | OBJECT schema {{ query: Q{suffix} mutation: M{suffix} }}",
            _ => $"scalar Date{suffix} @specifiedBy(url: \"https://example.test/{number}\") extend type T{suffix} @tag {{ added: Boolean }} extend union U{suffix} = Added{suffix}",
        };
        return executable;
    }

    private static string Mutate(DeterministicRandom random, string source)
    {
        var index = random.Next(source.Length);
        return random.Next(3) switch
        {
            0 => source.Remove(index, 1),
            1 => source.Insert(index, value[random.Next(6)]),
            _ => source[..index] + new[] { "?", "!", ":", "{", "}", "\\" }[random.Next(6)] + source[(index + 1)..],
        };
    }

    private static int ReadSetting(string name, int fallback) => int.TryParse(Environment.GetEnvironmentVariable(name), out var value) ? value : fallback;

    private sealed class DeterministicRandom(uint state)
    {
        private uint _state = state == 0 ? 0x9E3779B9 : state;

        public int Next(int exclusiveMaximum)
        {
            _state ^= _state << 13;
            _state ^= _state >> 17;
            _state ^= _state << 5;
            return (int)(_state % (uint)exclusiveMaximum);
        }
    }
}
