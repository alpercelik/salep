using Xunit;

namespace GraphQLParser.Tests;

public sealed class FixtureHarnessTests
{
    [Fact]
    public void GraphQLFixturesAreCopiedToTheTestOutputDirectory()
    {
        var fixturePath = Path.Combine(AppContext.BaseDirectory, "Fixtures", "smoke.graphql");

        Assert.True(File.Exists(fixturePath), $"Expected test fixture at {fixturePath}.");
        Assert.Equal("query Smoke { health }", File.ReadAllText(fixturePath).Trim());
    }
}
