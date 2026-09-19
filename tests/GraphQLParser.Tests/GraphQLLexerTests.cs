using System.Runtime.InteropServices;
using GraphQLParser;
using Xunit;

namespace GraphQLParser.Tests;

public sealed class GraphQLLexerTests
{
    [Fact]
    public void PunctuatorsAreAdjacentAndKeepExactSourceSlices()
    {
        const string input = "!$&()...:=@[]{}|";
        TokenKind[] expectedKinds =
        [
            TokenKind.Bang,
            TokenKind.Dollar,
            TokenKind.Ampersand,
            TokenKind.ParenthesisLeft,
            TokenKind.ParenthesisRight,
            TokenKind.Spread,
            TokenKind.Colon,
            TokenKind.Equals,
            TokenKind.At,
            TokenKind.BracketLeft,
            TokenKind.BracketRight,
            TokenKind.BraceLeft,
            TokenKind.BraceRight,
            TokenKind.Pipe,
        ];
        string[] expectedValues = ["!", "$", "&", "(", ")", "...", ":", "=", "@", "[", "]", "{", "}", "|"];
        var buffer = input.ToCharArray();
        var source = new SourceText(buffer.AsMemory());
        var lexer = new GraphQLLexer(source);
        var offset = 0;

        for (var index = 0; index < expectedKinds.Length; index++)
        {
            var token = lexer.NextToken();
            Assert.Equal(expectedKinds[index], token.Kind);
            Assert.Equal(offset, token.Start);
            Assert.Equal(offset + expectedValues[index].Length, token.End);
            Assert.Equal(expectedValues[index], token.Value.ToString());
            Assert.True(MemoryMarshal.TryGetArray(token.Value, out ArraySegment<char> segment));
            Assert.Same(buffer, segment.Array);
            offset = token.End;
        }

        Assert.Equal(TokenKind.EndOfFile, lexer.NextToken().Kind);
    }

    [Fact]
    public void IgnoredInputCommentsAndCrlfDoNotProduceTokens()
    {
        const string input = "\uFEFF \t,\r\n# first comment\r\n!\uFEFF# tail comment\n$";
        var lexer = new GraphQLLexer(new SourceText(input.AsMemory()));

        var bang = lexer.NextToken();
        var dollar = lexer.NextToken();

        Assert.Equal(TokenKind.Bang, bang.Kind);
        Assert.Equal(input.IndexOf('!'), bang.Start);
        Assert.Equal(TokenKind.Dollar, dollar.Kind);
        Assert.Equal(input.IndexOf('$'), dollar.Start);
        Assert.Equal(TokenKind.EndOfFile, lexer.NextToken().Kind);
    }

    [Fact]
    public void IgnoredOnlyInputReturnsTheSameStableEofToken()
    {
        const string input = "\uFEFF \t,\r\n# comment at eof";
        var lexer = new GraphQLLexer(new SourceText(input.AsMemory()));

        var first = lexer.NextToken();
        var second = lexer.NextToken();

        Assert.Equal(TokenKind.EndOfFile, first.Kind);
        Assert.Equal(input.Length, first.Start);
        Assert.Equal(input.Length, first.End);
        Assert.Equal(first.Kind, second.Kind);
        Assert.Equal(first.Start, second.Start);
        Assert.Equal(first.End, second.End);
        Assert.Empty(second.Value.ToArray());
    }

    [Theory]
    [InlineData(".", 0, 1)]
    [InlineData("..", 0, 2)]
    [InlineData("....", 3, 1)]
    public void MalformedSpreadSequencesProduceSourceLocatedErrors(string input, int errorPosition, int errorLength)
    {
        var lexer = new GraphQLLexer(new SourceText(input.AsMemory()));

        if (input.Length == 4)
        {
            Assert.Equal(TokenKind.Spread, lexer.NextToken().Kind);
        }

        var error = ReadUntilLexicalException(ref lexer);

        Assert.Equal(errorPosition, error.Position);
        Assert.Equal(errorLength, error.Length);
    }

    [Fact]
    public void UnexpectedCharactersProduceSourceLocatedErrors()
    {
        var lexer = new GraphQLLexer(new SourceText("{?}".AsMemory()));
        Assert.Equal(TokenKind.BraceLeft, lexer.NextToken().Kind);

        var error = ReadUntilLexicalException(ref lexer);

        Assert.Equal(1, error.Position);
        Assert.Equal(1, error.Length);
        Assert.Contains("U+003F", error.Message, StringComparison.Ordinal);
    }

    private static GraphQLLexicalException ReadUntilLexicalException(ref GraphQLLexer lexer)
    {
        try
        {
            lexer.NextToken();
        }
        catch (GraphQLLexicalException error)
        {
            return error;
        }

        throw new Xunit.Sdk.XunitException("Expected the lexer to report an invalid character sequence.");
    }
}
