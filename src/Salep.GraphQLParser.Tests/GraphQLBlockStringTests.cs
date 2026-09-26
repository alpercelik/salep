using Salep.GraphQLParser;
using Xunit;

namespace Salep.GraphQLParser.Tests;

public sealed class GraphQLBlockStringTests
{
    private static readonly string[] expected = new[] { " a", "b" };
    private static readonly string[] expectedArray = new[] { "a", " b" };
    private static readonly string[] expectedArray0 = new[] { " a", "b" };
    private static readonly string[] expectedArray1 = new[] { "a", " b" };
    private static readonly string[] expectedArray2 = new[] { " a", "b" };
    private static readonly string[] expectedArray3 = new[] { "a", "  b" };
    private static readonly string[] expectedArray4 = new[] { "a", "", "b" };
    private static readonly string[] expectedArray5 = new[] { "a", "         b" };
    private static readonly string[] expectedArray6 = new[] { "  a", " b", "c" };
    private static readonly string[] expectedArray7 = new[] { "Hello,", "  World!", "", "Yours,", "  GraphQL." };
    private static readonly string[] expectedArray8 = new[] { "Hello,", "  World!", "", "Yours,", "  GraphQL." };
    private static readonly string[] expectedArray9 = new[] { "    Hello,", "  World!", "", "Yours,", "  GraphQL." };
    private static readonly string[] expectedArray10 = new[] { "Hello,     ", "  World!   ", "           ", "Yours,     ", "  GraphQL. " };

    [Fact]
    public void DedentsCommonIndentationAndTrimsBlankEdges()
    {
        Assert.Empty(GraphQLBlockString.DedentBlockStringLines([""]));
        Assert.Equal(expected, GraphQLBlockString.DedentBlockStringLines([" a", "  b"]));
        Assert.Equal(expectedArray, GraphQLBlockString.DedentBlockStringLines(["", " a", "  b"]));
        Assert.Equal(expectedArray0, GraphQLBlockString.DedentBlockStringLines(["", "  a", " b"]));
        Assert.Equal(expectedArray1, GraphQLBlockString.DedentBlockStringLines(["", " a", "  b"]));
        Assert.Equal(expectedArray2, GraphQLBlockString.DedentBlockStringLines(["", "  a", " b"]));
        Assert.Equal(expectedArray3, GraphQLBlockString.DedentBlockStringLines(["  ", "    a", "      b", "  "]));
        Assert.Equal(expectedArray4, GraphQLBlockString.DedentBlockStringLines(["a", " ", "  b"]));
        Assert.Equal(expectedArray5, GraphQLBlockString.DedentBlockStringLines(["", "\ta", "          b"]));
        Assert.Equal(expectedArray6, GraphQLBlockString.DedentBlockStringLines(["", "  a", " b", "c"]));
        Assert.Equal(expectedArray7, GraphQLBlockString.DedentBlockStringLines(["", "", "    Hello,", "      World!", "", "    Yours,", "      GraphQL.", "", ""]));
        Assert.Equal(expectedArray8, GraphQLBlockString.DedentBlockStringLines(["  ", "        ", "    Hello,", "      World!", "", "    Yours,", "      GraphQL.", "        ", "  "]));
        Assert.Equal(expectedArray9, GraphQLBlockString.DedentBlockStringLines(["    Hello,", "      World!", "", "    Yours,", "      GraphQL."]));
        Assert.Equal(expectedArray10,
            GraphQLBlockString.DedentBlockStringLines(["               ", "    Hello,     ", "      World!   ", "               ", "    Yours,     ", "      GraphQL. ", "               "]));
    }

    [Theory]
    [InlineData("")]
    [InlineData(" a")]
    [InlineData("\t\"\n\"")]
    public void IdentifiesBlockStringPrintableValues(string value) => Assert.True(GraphQLBlockString.IsPrintableAsBlockString(value));

    [Theory]
    [InlineData(" ")]
    [InlineData("\t")]
    [InlineData(" \t")]
    [InlineData("\t ")]
    [InlineData("\n")]
    [InlineData("\n\n")]
    [InlineData("\n\n\n")]
    [InlineData(" \n  \n")]
    [InlineData("\t\n\t\t\n")]
    [InlineData(" \n a")]
    [InlineData("\t\na")]
    [InlineData("\n\na")]
    [InlineData("a\n")]
    [InlineData("a\n ")]
    [InlineData("a\n\t")]
    [InlineData("a\n\n")]
    [InlineData("\r")]
    [InlineData("\n\r")]
    [InlineData("\r\n")]
    [InlineData("a\rb")]
    [InlineData("\0")]
    [InlineData("a\0b")]
    public void RejectsValuesThatCannotRoundTripAsBlockStrings(string value) => Assert.False(GraphQLBlockString.IsPrintableAsBlockString(value));

    [Fact]
    public void PrintsReadableAndMinimizedFormsForPinnedExamples()
    {
        AssertPrint("one liner", "\"\"\"one liner\"\"\"");
        AssertPrint("no indent\n with indent", "\"\"\"\nno indent\n with indent\n\"\"\"", "\"\"\"\nno indent\n with indent\"\"\"");
        AssertPrint("    space-led string", "\"\"\"    space-led string\"\"\"");
        AssertPrint("    space-led value \"quoted string\"", "\"\"\"    space-led value \"quoted string\"\n\"\"\"");
        AssertPrint("backslash \\", "\"\"\"\nbackslash \\\n\"\"\"", "\"\"\"backslash \\\n\"\"\"");
        AssertPrint("triple quotation \"\"\"", "\"\"\"\ntriple quotation \\\"\"\"\n\"\"\"", "\"\"\"triple quotation \\\"\"\"" + "\"\"\"");
        var longLine = new string('x', 71);
        AssertPrint(longLine, "\"\"\"\n" + longLine + "\n\"\"\"", "\"\"\"" + longLine + "\"\"\"");
        const string indentedLines = "    first  \n  line     \nindentation\n     string";
        AssertPrint(indentedLines, "\"\"\"\n" + indentedLines + "\n\"\"\"", "\"\"\"" + indentedLines + "\"\"\"");

        var controls = "\" \\ / \b \f \n \r \t";
        AssertPrint(controls, "\"\"\"\n" + controls + "\n\"\"\"", "\"\"\"\n" + controls + "\"\"\"");
    }

    [Theory]
    [InlineData("simple text")]
    [InlineData("a\"\"\"b")]
    [InlineData("line one\n  line two")]
    [InlineData("backslash \\")]
    [InlineData("quoted at end\"")]
    [InlineData("supplementary 😀 text")]
    public void ReadableAndMinimizedOutputParseBackToTheOriginalValue(string value)
    {
        foreach (var options in new[] { default(BlockStringPrintOptions), new BlockStringPrintOptions(Minimize: true) })
        {
            var printed = GraphQLBlockString.PrintBlockString(value, options);
            var lexer = new GraphQLLexer(new SourceText(printed.AsMemory()));
            var token = lexer.NextToken();
            Assert.Equal(TokenKind.BlockString, token.Kind);
            Assert.Equal(value, token.Value.ToString());
            Assert.Equal(TokenKind.EndOfFile, lexer.NextToken().Kind);
        }
    }

    [Fact]
    public void ExhaustivelyRoundTripsPrintableStringsUpToFiveCharacters()
    {
        ReadOnlySpan<char> alphabet = ['\n', '\t', ' ', '"', 'a', '\\'];
        var buffer = new char[5];
        for (var length = 0; length <= buffer.Length; length++)
        {
            var count = (int)Math.Pow(alphabet.Length, length);
            for (var encoded = 0; encoded < count; encoded++)
            {
                var remaining = encoded;
                for (var index = 0; index < length; index++)
                {
                    buffer[index] = alphabet[remaining % alphabet.Length];
                    remaining /= alphabet.Length;
                }

                var value = new string(buffer, 0, length);
                if (!GraphQLBlockString.IsPrintableAsBlockString(value)) continue;
                foreach (var options in new[] { default(BlockStringPrintOptions), new BlockStringPrintOptions(Minimize: true) })
                {
                    var printed = GraphQLBlockString.PrintBlockString(value, options);
                    var lexer = new GraphQLLexer(new SourceText(printed.AsMemory()));
                    var token = lexer.NextToken();
                    Assert.Equal(TokenKind.BlockString, token.Kind);
                    Assert.True(value.AsSpan().SequenceEqual(token.Value.Span), $"Round-trip failed for value length {value.Length}.");
                    Assert.Equal(TokenKind.EndOfFile, lexer.NextToken().Kind);
                }
            }
        }
    }

    private static void AssertPrint(string value, string readable, string? minimized = null)
    {
        Assert.Equal(readable, GraphQLBlockString.PrintBlockString(value));
        Assert.Equal(minimized ?? readable, GraphQLBlockString.PrintBlockString(value, new BlockStringPrintOptions(Minimize: true)));
    }
}
