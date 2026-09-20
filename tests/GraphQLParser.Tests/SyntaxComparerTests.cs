using Xunit;

namespace GraphQLParser.Tests;

public sealed class SyntaxComparerTests
{
    private static DocumentNode Parse(string source) => GraphQLParser.Parse(new SourceText(source.AsMemory()));

    [Fact]
    public void SyntaxComparerIgnoresSourceLocationsButRetainsSyntaxValues()
    {
        var first = Parse("{ item }");
        var sameSyntaxAtDifferentLocations = Parse("# offset\n{ item }");
        var differentSyntax = Parse("{ other }");

        Assert.False(SyntaxComparer.ByReference.Equals(first, sameSyntaxAtDifferentLocations));
        Assert.True(SyntaxComparer.BySyntax.Equals(first, sameSyntaxAtDifferentLocations));
        Assert.Equal(SyntaxComparer.BySyntax.GetHashCode(first), SyntaxComparer.BySyntax.GetHashCode(sameSyntaxAtDifferentLocations));
        Assert.False(SyntaxComparer.BySyntax.Equals(first, differentSyntax));
    }

    [Fact]
    public void DescriptionIgnoringComparerIgnoresNestedDescriptionsOnly()
    {
        var described = Parse("\"\"\"description\"\"\" type Thing { field: String }");
        var undescribed = Parse("type Thing { field: String }");
        var differentName = Parse("type Other { field: String }");

        Assert.False(SyntaxComparer.BySyntax.Equals(described, undescribed));
        Assert.True(SyntaxComparer.BySyntaxIgnoreDescriptions.Equals(described, undescribed));
        Assert.Equal(SyntaxComparer.BySyntaxIgnoreDescriptions.GetHashCode(described),
            SyntaxComparer.BySyntaxIgnoreDescriptions.GetHashCode(undescribed));
        Assert.False(SyntaxComparer.BySyntaxIgnoreDescriptions.Equals(described, differentName));
    }
}
