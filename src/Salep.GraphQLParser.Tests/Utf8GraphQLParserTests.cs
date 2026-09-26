using System.Buffers;
using System.Text;
using Xunit;

namespace Salep.GraphQLParser.Tests;

public sealed class Utf8GraphQLParserTests
{
    [Fact]
    public void Utf8EntryPointsParseSpansSequencesAndStringsEquivalently()
    {
        const string source = "query Q { field(arg: \"ø\") }";
        var bytes = Encoding.UTF8.GetBytes(source);
        var fromSpan = Utf8GraphQLParser.Parse(bytes.AsSpan());
        var fromSequence = Utf8GraphQLParser.Parse(new ReadOnlySequence<byte>(bytes));
        var fromString = Utf8GraphQLParser.Parse(source);

        Assert.Equal(fromString.ToString(), fromSpan.ToString());
        Assert.Equal(fromString.ToString(), fromSequence.ToString());

        var parser = new Utf8GraphQLParser(bytes.AsSpan());
        Assert.False(parser.IsEndOfFile);
        Assert.Equal(0, parser.ParsedSyntaxNodes);
        _ = parser.Parse();
        Assert.True(parser.IsEndOfFile);
        Assert.True(parser.ParsedSyntaxNodes >= 5);
    }

    [Fact]
    public void Utf8EntryPointRejectsMalformedEncoding()
    {
        byte[] invalid = [0xC3, 0x28];
        Assert.Throws<Utf8EncodingException>(() => Utf8GraphQLParser.Parse(invalid.AsSpan()));
    }

    [Fact]
    public void ParserOptionsApplyLocationAndResourceLimits()
    {
        var source = new SourceText("{ a b }".AsMemory());
        var noLocations = GraphQLParser.Parse(source, ParserOptions.Default.NoLocation);
        Assert.False(noLocations.HasLocation);

        Assert.Throws<GraphQLResourceLimitException>(() => GraphQLParser.Parse(source, new ParserOptions(maxAllowedFields: 1)));
        Assert.Throws<GraphQLResourceLimitException>(() => GraphQLParser.Parse(source, new ParserOptions(maxAllowedNodes: 2)));
        Assert.Throws<GraphQLResourceLimitException>(() => GraphQLParser.Parse(new SourceText("{ a @one @two }".AsMemory()), new ParserOptions(maxAllowedDirectives: 1)));
        Assert.Throws<ArgumentOutOfRangeException>(() => new ParserOptions(maxAllowedTokens: 0));
    }

    [Fact]
    public void ParserOptionsKeepDocumentedDefaultsAndAllowConfiguredRecursionDepth()
    {
        Assert.Equal(int.MaxValue, ParserOptions.Default.MaxAllowedNodes);
        Assert.Equal(int.MaxValue, ParserOptions.Default.MaxAllowedTokens);
        Assert.Equal(2048, ParserOptions.Default.MaxAllowedFields);
        Assert.Equal(4, ParserOptions.Default.MaxAllowedDirectives);
        Assert.Equal(200, ParserOptions.Default.MaxAllowedRecursionDepth);
        Assert.Equal(int.MaxValue, ParserOptions.Trusted.MaxAllowedRecursionDepth);

        var depth = 140;
        var source = "query { field(arg: " + new string('[', depth) + "0" + new string(']', depth) + ") }";
        _ = GraphQLParser.Parse(new SourceText(source.AsMemory()), ParserOptions.Default);
    }

    [Fact]
    public void Utf8ReaderPreservesByteOffsetsCommentsValuesAndStableEof()
    {
        const string source = "# greet ø\nname 1.5e2 \"a\\n\"";
        var bytes = Encoding.UTF8.GetBytes(source);
        var reader = new Utf8GraphQLReader(bytes.AsSpan());

        Assert.Equal(3, reader.Count());
        Assert.True(reader.Read());
        Assert.Equal(TokenKind.Name, reader.Kind);
        Assert.Equal("name", reader.GetName());
        Assert.Equal(" greet ø", reader.GetComment());
        Assert.Equal(Encoding.UTF8.GetByteCount("# greet ø\n"), reader.Start);
        Assert.True(reader.Read());
        Assert.Equal("1.5e2", Encoding.UTF8.GetString(reader.Value));
        Assert.Equal(FloatFormat.Exponential, reader.FloatFormat);
        Assert.True(reader.Read());
        Assert.Equal("a\n", reader.GetString());
        Assert.False(reader.Read());
        Assert.Equal(TokenKind.EndOfFile, reader.Kind);
        Assert.False(reader.Read());
        Assert.Equal(TokenKind.EndOfFile, reader.Kind);
    }

    [Fact]
    public void Utf8ReaderEnforcesTokenLimitsAndDecodesStringPayloads()
    {
        var reader = new Utf8GraphQLReader(Encoding.UTF8.GetBytes("one two").AsSpan(), maxAllowedTokens: 1);
        Assert.True(reader.Read());
        try
        {
            reader.Read();
            Assert.Fail("The reader should enforce its token limit.");
        }
        catch (GraphQLResourceLimitException)
        {
        }

        var escaped = Encoding.UTF8.GetBytes("line\\nø");
        Assert.Equal("line\nø", Utf8GraphQLReader.GetString(escaped, isBlockString: false));
        Assert.Equal("unescaped", Utf8GraphQLReader.GetString(Encoding.UTF8.GetBytes("unescaped")));
        var writer = new ArrayBufferWriter<byte>();
        var written = Utf8GraphQLReader.GetRawString(escaped, isBlockString: false, writer);
        Assert.Equal(written, writer.WrittenCount);
        Assert.Equal("\"line\\nø\"", Encoding.UTF8.GetString(writer.WrittenSpan));

        var payload = Encoding.UTF8.GetBytes("a\\n");
        var valueBuffer = new byte[payload.Length];
        payload.CopyTo(valueBuffer);
        Span<byte> value = valueBuffer;
        var length = value.Length;
        var helperReader = new Utf8GraphQLReader(ReadOnlySpan<byte>.Empty);
        helperReader.UnescapeValue(ref value);
        Assert.Equal("a\n", Encoding.UTF8.GetString(value));
        Assert.True(value.Length < length);
    }

    private static readonly string[] expected = new[] { "query", "{", "name", "}" };

    [Fact]
    public void Utf8ReaderFlattensSegmentedSequencesAndRejectsInvalidUtf8()
    {
        var first = Encoding.UTF8.GetBytes("query { na");
        var second = Encoding.UTF8.GetBytes("me } ");
        var sequence = CreateSequence(first, second);
        var reader = new Utf8GraphQLReader(sequence);
        var tokens = new List<string>();
        while (reader.Read()) tokens.Add(Encoding.UTF8.GetString(reader.Value));
        Assert.Equal(expected, tokens);

        byte[] invalid = [0xC3, 0x28];
        Assert.Throws<Utf8EncodingException>(() => new Utf8GraphQLReader(invalid.AsSpan()));
    }

    [Fact]
    public void Utf8SyntaxHelpersParseEachSupportedConstructFromStringsSpansAndReaders()
    {
        var fieldString = Utf8GraphQLParser.Syntax.ParseField("alias: field(id: 1)");
        var fieldBytes = Utf8GraphQLParser.Syntax.ParseField(Encoding.UTF8.GetBytes("alias: field(id: 1)").AsSpan());
        Assert.Equal(fieldString.ToString(), fieldBytes.ToString());
        var fieldReader = new Utf8GraphQLReader(Encoding.UTF8.GetBytes("alias: field(id: 1)").AsSpan());
        Assert.Equal(fieldString.ToString(), Utf8GraphQLParser.Syntax.ParseField(fieldReader).ToString());

        Assert.IsType<DirectiveDefinitionNode>(Utf8GraphQLParser.Syntax.ParseDirectiveDefinition("directive @tag on FIELD"));
        Assert.IsType<FieldDefinitionNode>(Utf8GraphQLParser.Syntax.ParseFieldDefinition("value(arg: Int): String"));
        Assert.IsType<FragmentDefinitionNode>(Utf8GraphQLParser.Syntax.ParseFragmentDefinition("fragment F on T { value }"));
        Assert.IsType<ObjectValueNode>(Utf8GraphQLParser.Syntax.ParseObjectLiteral("{ value: [1, 2] }"));
        Assert.IsType<ObjectTypeDefinitionNode>(Utf8GraphQLParser.Syntax.ParseObjectTypeDefinition("type Query { value: String }"));
        Assert.IsType<MemberCoordinateNode>(Utf8GraphQLParser.Syntax.ParseSchemaCoordinate("Query.value"));
        Assert.Single(Utf8GraphQLParser.Syntax.ParseSelectionSet("{ value }").Selections);
        Assert.Equal(SyntaxKind.NonNullType, Utf8GraphQLParser.Syntax.ParseTypeReference("String!").Kind);
        Assert.IsType<IntValueNode>(Utf8GraphQLParser.Syntax.ParseValueLiteral(Encoding.UTF8.GetBytes("42").AsSpan()));
        Assert.Throws<ArgumentException>(() => Utf8GraphQLParser.Syntax.ParseValueLiteral("$variable"));
        Assert.Throws<FormatException>(() => Utf8GraphQLParser.Syntax.ParseFragmentDefinition("fragment F on T { value } fragment G on T { other }"));
    }

    private static ReadOnlySequence<byte> CreateSequence(byte[] first, byte[] second)
    {
        var firstSegment = new TestSequenceSegment(first);
        var lastSegment = firstSegment.Append(second);
        return new ReadOnlySequence<byte>(firstSegment, 0, lastSegment, lastSegment.Memory.Length);
    }

    private sealed class TestSequenceSegment : ReadOnlySequenceSegment<byte>
    {
        public TestSequenceSegment(byte[] memory) => Memory = memory;

        public TestSequenceSegment Append(byte[] memory)
        {
            var segment = new TestSequenceSegment(memory) { RunningIndex = RunningIndex + Memory.Length };
            Next = segment;
            return segment;
        }
    }
}
