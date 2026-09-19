using System.Text.Json;
using Xunit;

namespace GraphQLParser.Tests;

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
}
