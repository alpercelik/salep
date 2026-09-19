using System.Runtime.InteropServices;
using GraphQLParser;
using Xunit;

namespace GraphQLParser.Tests;

public sealed class GraphQLLexerTests
{
    public static TheoryData<string, TokenKind> ValidNumericTokens => new()
    {
        { "0", TokenKind.Integer },
        { "-0", TokenKind.Integer },
        { "42", TokenKind.Integer },
        { "-731", TokenKind.Integer },
        { "0.0", TokenKind.Float },
        { "-12.375", TokenKind.Float },
        { "1e10", TokenKind.Float },
        { "2E-3", TokenKind.Float },
        { "-4.5E+6", TokenKind.Float },
    };

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

    [Theory]
    [InlineData("_")]
    [InlineData("A")]
    [InlineData("a9")]
    [InlineData("__typename")]
    [InlineData("query")]
    public void NamesUseAsciiGrammarAndPreserveTheirSourceSlice(string input)
    {
        var buffer = input.ToCharArray();
        var lexer = new GraphQLLexer(new SourceText(buffer.AsMemory()));

        var token = lexer.NextToken();

        Assert.Equal(TokenKind.Name, token.Kind);
        Assert.Equal(0, token.Start);
        Assert.Equal(buffer.Length, token.End);
        Assert.Equal(input, token.Value.ToString());
        Assert.True(MemoryMarshal.TryGetArray(token.Value, out ArraySegment<char> segment));
        Assert.Same(buffer, segment.Array);
    }

    [Theory]
    [MemberData(nameof(ValidNumericTokens))]
    public void ValidNumericFormsPreserveKindRawTextAndExactSpan(string input, TokenKind expectedKind)
    {
        var lexer = new GraphQLLexer(new SourceText(input.AsMemory()));

        var token = lexer.NextToken();

        Assert.Equal(expectedKind, token.Kind);
        Assert.Equal(0, token.Start);
        Assert.Equal(input.Length, token.End);
        Assert.Equal(input, token.Value.ToString());
        Assert.Equal(TokenKind.EndOfFile, lexer.NextToken().Kind);
    }

    [Theory]
    [InlineData("01", 1, 1)]
    [InlineData("-01", 2, 1)]
    [InlineData("-", 1, 0)]
    [InlineData("1.", 2, 0)]
    [InlineData("1.e2", 2, 1)]
    [InlineData("1e", 2, 0)]
    [InlineData("1e+", 3, 0)]
    [InlineData("1E-", 3, 0)]
    [InlineData("1abc", 1, 1)]
    [InlineData("1_2", 1, 1)]
    public void InvalidNumericFormsFailAtTheFirstInvalidOffset(string input, int position, int length)
    {
        var lexer = new GraphQLLexer(new SourceText(input.AsMemory()));

        var error = ReadUntilLexicalException(ref lexer);

        Assert.Equal(position, error.Position);
        Assert.Equal(length, error.Length);
    }

    [Fact]
    public void NonAsciiNameStartIsRejectedAtItsUtf16Offset()
    {
        var lexer = new GraphQLLexer(new SourceText("é".AsMemory()));

        var error = ReadUntilLexicalException(ref lexer);

        Assert.Equal(0, error.Position);
        Assert.Equal(1, error.Length);
    }

    [Fact]
    public void NonAsciiNameContinuationIsRejectedAfterTheValidAsciiName()
    {
        var lexer = new GraphQLLexer(new SourceText("aé".AsMemory()));

        var name = lexer.NextToken();
        var error = ReadUntilLexicalException(ref lexer);

        Assert.Equal(TokenKind.Name, name.Kind);
        Assert.Equal("a", name.Value.ToString());
        Assert.Equal(1, error.Position);
    }

    [Fact]
    public void PlainQuotedStringUsesSourceMemoryForDecodedContentsAndKeepsTheRawLexeme()
    {
        var buffer = "\"plain text\"".ToCharArray();
        var lexer = new GraphQLLexer(new SourceText(buffer.AsMemory()));

        var token = lexer.NextToken();

        Assert.Equal(TokenKind.String, token.Kind);
        Assert.Equal(0, token.Start);
        Assert.Equal(buffer.Length, token.End);
        Assert.Equal("\"plain text\"", token.RawValue.ToString());
        Assert.Equal("plain text", token.Value.ToString());
        Assert.True(MemoryMarshal.TryGetArray(token.Value, out ArraySegment<char> valueSegment));
        Assert.Same(buffer, valueSegment.Array);
    }

    public static TheoryData<string, string> ValidQuotedStrings => new()
    {
        { "\"plain\"", "plain" },
        { "\"\\\"\"", "\"" },
        { "\"\\\\\"", "\\" },
        { "\"\\/\"", "/" },
        { "\"\\b\"", "\b" },
        { "\"\\f\"", "\f" },
        { "\"\\n\"", "\n" },
        { "\"\\r\"", "\r" },
        { "\"\\t\"", "\t" },
        { "\"\\u0041\"", "A" },
        { "\"\\u0000\"", "\0" },
        { "\"\\uD83D\\uDE00\"", "😀" },
        { "\"\\u{1F600}\"", "😀" },
        { "\"\\u{10FFFF}\"", "\U0010FFFF" },
    };

    [Theory]
    [MemberData(nameof(ValidQuotedStrings))]
    public void QuotedStringsDecodeEscapesAndRetainTheRawSpan(string input, string expectedValue)
    {
        var lexer = new GraphQLLexer(new SourceText(input.AsMemory()));

        var token = lexer.NextToken();

        Assert.Equal(TokenKind.String, token.Kind);
        Assert.Equal(0, token.Start);
        Assert.Equal(input.Length, token.End);
        Assert.Equal(input, token.RawValue.ToString());
        Assert.Equal(expectedValue, token.Value.ToString());
        Assert.Equal(TokenKind.EndOfFile, lexer.NextToken().Kind);
    }

    [Theory]
    [InlineData("\"\\x\"", 1)]
    [InlineData("\"\\u12G4\"", 1)]
    [InlineData("\"\\u123\"", 1)]
    [InlineData("\"\\u{}\"", 1)]
    [InlineData("\"\\u{D800}\"", 1)]
    [InlineData("\"\\u{110000}\"", 1)]
    [InlineData("\"\\u{1234567}\"", 1)]
    [InlineData("\"\\uD800\"", 1)]
    [InlineData("\"\\uDC00\"", 1)]
    [InlineData("\"\\uD83D\\u0041\"", 1)]
    [InlineData("\"unterminated", 13)]
    public void InvalidQuotedStringsReportTheEscapeOrEofOffset(string input, int expectedPosition)
    {
        var lexer = new GraphQLLexer(new SourceText(input.AsMemory()));

        var error = ReadUntilLexicalException(ref lexer);

        Assert.Equal(expectedPosition, error.Position);
        Assert.Equal(expectedPosition == input.Length ? 0 : 1, Math.Sign(error.Length));
    }

    [Theory]
    [InlineData("\"a\nb\"", 2)]
    [InlineData("\"a\rb\"", 2)]
    public void RawLineTerminatorsAreRejected(string input, int expectedPosition)
    {
        var newlineLexer = new GraphQLLexer(new SourceText(input.AsMemory()));
        var newlineError = ReadUntilLexicalException(ref newlineLexer);
        Assert.Equal(expectedPosition, newlineError.Position);
    }

    [Fact]
    public void RawControlsAndUnpairedSurrogatesAreRejected()
    {
        var controlInput = new string(['"', '\u0001', '"']);
        var controlLexer = new GraphQLLexer(new SourceText(controlInput.AsMemory()));
        Assert.Equal(1, ReadUntilLexicalException(ref controlLexer).Position);

        var surrogateLexer = new GraphQLLexer(new SourceText("\"\uD800\"".AsMemory()));
        var surrogateError = ReadUntilLexicalException(ref surrogateLexer);
        Assert.Equal(1, surrogateError.Position);
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
