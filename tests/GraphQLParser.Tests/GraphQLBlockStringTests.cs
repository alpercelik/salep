using GraphQLParser;
using Xunit;

namespace GraphQLParser.Tests;

public sealed class GraphQLBlockStringTests
{
    [Fact]
    public void DedentsCommonIndentationAndTrimsBlankEdges()
    {
        Assert.Empty(GraphQLBlockString.DedentBlockStringLines([""]));
        Assert.Equal(new[] { " a", "b" }, GraphQLBlockString.DedentBlockStringLines([" a", "  b"]));
        Assert.Equal(new[] { "a", " b" }, GraphQLBlockString.DedentBlockStringLines(["", " a", "  b"]));
        Assert.Equal(new[] { " a", "b" }, GraphQLBlockString.DedentBlockStringLines(["", "  a", " b"]));
        Assert.Equal(new[] { "a", " b" }, GraphQLBlockString.DedentBlockStringLines(["", " a", "  b"]));
        Assert.Equal(new[] { " a", "b" }, GraphQLBlockString.DedentBlockStringLines(["", "  a", " b"]));
        Assert.Equal(new[] { "a", "  b" }, GraphQLBlockString.DedentBlockStringLines(["  ", "    a", "      b", "  "]));
        Assert.Equal(new[] { "a", "", "b" }, GraphQLBlockString.DedentBlockStringLines(["a", " ", "  b"]));
        Assert.Equal(new[] { "a", "         b" }, GraphQLBlockString.DedentBlockStringLines(["", "\ta", "          b"]));
        Assert.Equal(new[] { "  a", " b", "c" }, GraphQLBlockString.DedentBlockStringLines(["", "  a", " b", "c"]));
        Assert.Equal(new[] { "Hello,", "  World!", "", "Yours,", "  GraphQL." }, GraphQLBlockString.DedentBlockStringLines(["", "", "    Hello,", "      World!", "", "    Yours,", "      GraphQL.", "", ""]));
        Assert.Equal(new[] { "Hello,", "  World!", "", "Yours,", "  GraphQL." }, GraphQLBlockString.DedentBlockStringLines(["  ", "        ", "    Hello,", "      World!", "", "    Yours,", "      GraphQL.", "        ", "  "]));
        Assert.Equal(new[] { "    Hello,", "  World!", "", "Yours,", "  GraphQL." }, GraphQLBlockString.DedentBlockStringLines(["    Hello,", "      World!", "", "    Yours,", "      GraphQL."]));
        Assert.Equal(new[] { "Hello,     ", "  World!   ", "           ", "Yours,     ", "  GraphQL. " },
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
