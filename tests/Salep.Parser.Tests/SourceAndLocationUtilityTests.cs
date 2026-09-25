using Salep.Parser;
using Xunit;

namespace Salep.Parser.Tests;

public sealed class SourceAndLocationUtilityTests
{
    [Fact]
    public void SourceValidatesAndPreservesNameAndOffsets()
    {
        Assert.Throws<ArgumentNullException>(() => new Source(null!));
        Assert.Throws<ArgumentOutOfRangeException>(() => new Source("", "Test", default));
        Assert.Throws<ArgumentOutOfRangeException>(() => new SourceLocationOffset(0, 1));
        Assert.Throws<ArgumentOutOfRangeException>(() => new SourceLocationOffset(1, 0));

        var source = new Source("{ id }", "query.graphql", new SourceLocationOffset(12, 4));
        var document = GraphQLParser.Parse(source);
        Assert.Same(source, document.SourceInfo);
        Assert.Equal(source.Body, document.Source.Content.ToString());
    }

    [Fact]
    public void PrintsLocationsWithLineColumnOffsetsAndNeighboringLines()
    {
        var single = GraphQLPrintLocation.PrintSourceLocation(
            new Source("*", "Test", new SourceLocationOffset(9, 1)),
            new GraphQLSourceLocation(1, 1));
        Assert.Equal("Test:9:1\n9 | *\n  | ^", single);

        var followingEmptyLine = GraphQLPrintLocation.PrintSourceLocation(
            new Source("*\n", "Test", new SourceLocationOffset(9, 1)),
            new GraphQLSourceLocation(1, 1));
        Assert.Equal("Test:9:1\n 9 | *\n   | ^\n10 |", followingEmptyLine);

        var paddedFirstLine = GraphQLPrintLocation.PrintSourceLocation(
            new Source("first\nsecond", "Test", new SourceLocationOffset(3, 7)),
            new GraphQLSourceLocation(1, 3));
        Assert.Equal("Test:3:9\n3 |       first\n  |         ^\n4 | second", paddedFirstLine);
    }

    [Fact]
    public void RejectsLineLocationsOutsideTheSource()
    {
        Assert.Throws<ArgumentOutOfRangeException>(() => GraphQLPrintLocation.PrintSourceLocation(
            new Source("one line"), new GraphQLSourceLocation(2, 1)));
    }

    [Fact]
    public void PrintsHelpfulWindowsAroundMinifiedSourceLocations()
    {
        var body = "query SomeMinifiedQueryWithErrorInside($foo:String!=FIRST_ERROR_HERE$bar:String){someField(foo:$foo bar:$bar baz:SECOND_ERROR_HERE){fieldA fieldB{fieldC fieldD...on THIRD_ERROR_HERE}}}";
        var source = new Source(body);
        var first = GraphQLPrintLocation.PrintSourceLocation(source, new GraphQLSourceLocation(1, body.IndexOf("FIRST_ERROR_HERE", StringComparison.Ordinal) + 1));
        var second = GraphQLPrintLocation.PrintSourceLocation(source, new GraphQLSourceLocation(1, body.IndexOf("SECOND_ERROR_HERE", StringComparison.Ordinal) + 1));
        var third = GraphQLPrintLocation.PrintSourceLocation(source, new GraphQLSourceLocation(1, body.IndexOf("THIRD_ERROR_HERE", StringComparison.Ordinal) + 1));

        Assert.Contains("GraphQL request:1:53", first, StringComparison.Ordinal);
        Assert.Contains("^", first, StringComparison.Ordinal);
        Assert.Contains("GraphQL request:1:114", second, StringComparison.Ordinal);
        Assert.Contains("GraphQL request:1:166", third, StringComparison.Ordinal);
        Assert.Contains("THIRD_ERROR_HERE", third, StringComparison.Ordinal);
    }
}
