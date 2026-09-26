using System.Text.Json;
using System.Text.Json.Nodes;
using Xunit;

namespace Salep.GraphQLParser.Tests;

public sealed class OracleCorpusTests
{
    [Fact]
    public void EveryManifestFixtureHasSourceAndVersionedOracleOutput()
    {
        var oracleDirectory = Path.Combine(AppContext.BaseDirectory, "Fixtures", "Oracle");
        using var manifestDocument = JsonDocument.Parse(File.ReadAllText(Path.Combine(oracleDirectory, "manifest.json")));

        foreach (var fixture in manifestDocument.RootElement.EnumerateArray())
        {
            var id = fixture.GetProperty("id").GetString()!;
            var file = fixture.GetProperty("file").GetString()!;
            var expectedOutcome = fixture.GetProperty("expect").GetString();
            Assert.True(File.Exists(Path.Combine(oracleDirectory, file)), $"{id}: source fixture is missing");

            var expectedPath = Path.Combine(oracleDirectory, "expected", $"{id}.json");
            Assert.True(File.Exists(expectedPath), $"{id}: oracle snapshot is missing");
            using var expectedDocument = JsonDocument.Parse(File.ReadAllText(expectedPath));
            var expected = expectedDocument.RootElement;
            Assert.Equal(id, expected.GetProperty("fixtureId").GetString());
            Assert.Equal("16.14.0", expected.GetProperty("graphqlJsVersion").GetString());
            Assert.Equal(expectedOutcome, expected.GetProperty("outcome").GetString());
        }
    }

    [Fact]
    public void EveryValidFixtureHasTheSameCanonicalAstAsGraphqlJs()
    {
        var oracleDirectory = Path.Combine(AppContext.BaseDirectory, "Fixtures", "Oracle");
        using var manifestDocument = JsonDocument.Parse(File.ReadAllText(Path.Combine(oracleDirectory, "manifest.json")));

        foreach (var fixture in manifestDocument.RootElement.EnumerateArray().Where(item => item.GetProperty("expect").GetString() == "valid"))
        {
            var id = fixture.GetProperty("id").GetString()!;
            var file = fixture.GetProperty("file").GetString()!;
            var source = File.ReadAllText(Path.Combine(oracleDirectory, file));
            using var expectedDocument = JsonDocument.Parse(File.ReadAllText(Path.Combine(oracleDirectory, "expected", $"{id}.json")));
            var expected = JsonNode.Parse(expectedDocument.RootElement.GetProperty("ast").GetRawText())!;
            DocumentNode parsed;
            try
            {
                parsed = GraphQLParser.Parse(new SourceText(source.AsMemory()));
            }
            catch (Exception exception)
            {
                throw new Xunit.Sdk.XunitException($"{id}: parser rejected a graphql-js-valid fixture; grammar gap or parser failure: {exception.GetType().Name}: {exception.Message}");
            }

            JsonObject actual;
            try
            {
                actual = CanonicalAstJson.Project(parsed);
            }
            catch (Exception exception)
            {
                throw new Xunit.Sdk.XunitException($"{id}: canonical serializer failed for a parsed document: {exception.GetType().Name}: {exception.Message}");
            }

            var mismatch = FirstMismatch(expected, actual, "$" );
            Assert.True(mismatch is null, $"{id}: canonical AST structural/value/location mismatch {mismatch}");
        }
    }

    [Fact]
    public void EveryInvalidFixtureMatchesOracleRejectionCategoryAndLocation()
    {
        var oracleDirectory = Path.Combine(AppContext.BaseDirectory, "Fixtures", "Oracle");
        using var manifestDocument = JsonDocument.Parse(File.ReadAllText(Path.Combine(oracleDirectory, "manifest.json")));

        foreach (var fixture in manifestDocument.RootElement.EnumerateArray().Where(item => item.GetProperty("expect").GetString() == "invalid"))
        {
            var id = fixture.GetProperty("id").GetString()!;
            var file = fixture.GetProperty("file").GetString()!;
            var expectedCategory = fixture.GetProperty("failureCategory").GetString()!;
            var source = File.ReadAllText(Path.Combine(oracleDirectory, file));
            using var expectedDocument = JsonDocument.Parse(File.ReadAllText(Path.Combine(oracleDirectory, "expected", $"{id}.json")));
            var errorJson = expectedDocument.RootElement.GetProperty("errors")[0];
            var oraclePosition = errorJson.GetProperty("positions")[0].GetInt32();
            var oracleLocation = errorJson.GetProperty("locations")[0];

            string actualCategory;
            int actualPosition;
            try
            {
                _ = GraphQLParser.Parse(new SourceText(source.AsMemory()));
                throw new Xunit.Sdk.XunitException($"{id}: acceptance mismatch; parser accepted oracle-invalid input ({expectedCategory})");
            }
            catch (GraphQLLexicalException exception)
            {
                actualCategory = "lexical";
                actualPosition = exception.Position;
            }
            catch (GraphQLSyntaxException exception)
            {
                actualCategory = "syntax";
                actualPosition = exception.Position;
            }

            Assert.True(actualCategory == expectedCategory, $"{id}: failure category mismatch; expected {expectedCategory}, actual {actualCategory}");
            Assert.True(actualPosition == oraclePosition, $"{id}: {actualCategory} source offset mismatch; expected {oraclePosition}, actual {actualPosition}");
            var (line, column) = GetGraphqlJsLocation(source, oraclePosition);
            Assert.True(oracleLocation.GetProperty("line").GetInt32() == line && oracleLocation.GetProperty("column").GetInt32() == column,
                $"{id}: oracle location convention mismatch at offset {oraclePosition}; expected line {line}, column {column}");
        }
    }

    private static string? FirstMismatch(JsonNode? expected, JsonNode? actual, string path)
    {
        if (JsonNode.DeepEquals(expected, actual)) return null;
        if (expected is JsonObject expectedObject && actual is JsonObject actualObject)
        {
            foreach (var pair in expectedObject)
            {
                var childPath = $"{path}.{pair.Key}";
                if (!actualObject.TryGetPropertyValue(pair.Key, out var actualValue)) return $"{childPath} is missing (expected {pair.Value})";
                var mismatch = FirstMismatch(pair.Value, actualValue, childPath);
                if (mismatch is not null) return mismatch;
            }

            foreach (var pair in actualObject)
            {
                if (!expectedObject.ContainsKey(pair.Key)) return $"{path}.{pair.Key} is unexpected (actual {pair.Value})";
            }
        }
        else if (expected is JsonArray expectedArray && actual is JsonArray actualArray)
        {
            if (expectedArray.Count != actualArray.Count) return $"{path} has length {actualArray.Count}, expected {expectedArray.Count}";
            for (var index = 0; index < expectedArray.Count; index++)
            {
                var mismatch = FirstMismatch(expectedArray[index], actualArray[index], $"{path}[{index}]");
                if (mismatch is not null) return mismatch;
            }
        }
        else
        {
            return $"{path} expected {expected}, actual {actual}";
        }

        return $"{path} differs";
    }

    private static (int Line, int Column) GetGraphqlJsLocation(string source, int offset)
    {
        var line = 1;
        var lineStart = 0;
        for (var index = 0; index < offset; index++)
        {
            if (source[index] == '\n')
            {
                line++;
                lineStart = index + 1;
            }
        }

        return (line, offset - lineStart + 1);
    }
}
