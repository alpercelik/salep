using System.Text;
using Salep.GraphQLParser;
using Salep.GraphQLParser.Utilities;
using Xunit;

namespace Salep.GraphQLParser.Tests;

public sealed class SyntaxUtilitiesTests
{
    [Fact]
    public async Task SerializerAndPrinterWriteGraphQLTextAndUtf8()
    {
        var document = GraphQLParser.Parse(new SourceText("{ user(id: 1) { name } }".AsMemory()));
        var expected = GraphQLPrinter.Print(document);
        Assert.Equal(expected, SyntaxPrinter.Print(document));
        var compact = SyntaxPrinter.Print(document, indented: false);
        Assert.DoesNotContain('\n', compact);
        var reparsed = GraphQLParser.Parse(new SourceText(compact.AsMemory()));
        Assert.True(SyntaxComparer.BySyntax.Equals(document, reparsed));

        var writer = new StringSyntaxWriter();
        new SyntaxSerializer().Serialize(document, writer);
        Assert.Equal(expected, writer.ToString());
        Assert.Equal(expected, Encoding.UTF8.GetString(await PrintToBytes(document)));
    }

    [Fact]
    public void StringSyntaxWriterTracksColumnsAndPoolClearsState()
    {
        var writer = new StringSyntaxWriter();
        writer.Indent();
        writer.WriteLine();
        writer.WriteIndent();
        writer.Write("abc");
        Assert.Equal(5, writer.Column);
        Assert.Equal("\n  abc", writer.ToString());

        StringSyntaxWriter.Return(writer);
        var reused = StringSyntaxWriter.Rent();
        Assert.Empty(reused.ToString());
        Assert.Equal(0, reused.Column);
        StringSyntaxWriter.Return(reused);
    }

    [Fact]
    public void WriteManyUsesProvidedSeparatorAndValidatesArguments()
    {
        var writer = new StringSyntaxWriter();
        writer.WriteMany(["a", "b", "c"], static (value, output) => output.Write(value), ", ");
        Assert.Equal("a, b, c", writer.ToString());
        Assert.Throws<ArgumentNullException>(() => SyntaxPrinter.Print(null!));
    }

    [Fact]
    public void CompactPrintingPreservesBlockStringNewlinesAndEscapedTripleQuotes()
    {
        var blockValue = new StringValueNode(new Location(0, 0, 1, 1), "first line\ntriple quote: \"\"\"\nlast line", block: true);
        var source = "{ field(value: " + GraphQLPrinter.Print(blockValue) + ") }";
        var document = GraphQLParser.Parse(new SourceText(source.AsMemory()));

        var compact = SyntaxPrinter.Print(document, indented: false);
        Assert.Contains("triple quote: \\\"\"\"", compact);
        var reparsed = GraphQLParser.Parse(new SourceText(compact.AsMemory()));
        Assert.True(SyntaxComparer.BySyntax.Equals(document, reparsed));
    }

    private static async Task<byte[]> PrintToBytes(ISyntaxNode node)
    {
        using var stream = new MemoryStream();
        await SyntaxPrinter.PrintToAsync(node, stream);
        return stream.ToArray();
    }
}
