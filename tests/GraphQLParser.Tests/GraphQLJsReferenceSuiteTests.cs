using System.Text.Json;
using System.Text.Json.Nodes;
using GraphQLParser;
using Xunit;
using Parser = GraphQLParser.GraphQLParser;

namespace GraphQLParser.Tests;

public sealed class GraphQLJsReferenceSuiteTests
{
    private const string SourceCommit = "57b385b288150960acd09337adf2fc778abb32ab";
    private const int UpstreamTestCount = 220;
    private const int ParserCaseCount = 166;
    private const int LexerCaseCount = 341;
    private const int BlockStringCaseCount = 16;
    private static readonly string CorpusPath = Path.Combine(AppContext.BaseDirectory, "Fixtures", "ReferenceSuite", "corpus.json");

    public static IEnumerable<object[]> ParserCases => Cases("parse");
    public static IEnumerable<object[]> LexerCases => Cases("lexer");
    public static IEnumerable<object[]> BlockStringCases => Cases("blockString");
    public static IEnumerable<object[]> UtilityCases => Cases("utility");
    public static IEnumerable<object[]> SchemaCoordinateCases => Cases("schemaCoordinate");
    public static IEnumerable<object[]> PrinterCases => Cases("printer");

    [Fact]
    public void CorpusHasPinnedProvenanceAndAccountsForEveryUpstreamTest()
    {
        using var document = JsonDocument.Parse(File.ReadAllText(CorpusPath));
        var root = document.RootElement;
        var provenance = root.GetProperty("provenance");
        Assert.Equal("16.14.0", provenance.GetProperty("graphqlJsVersion").GetString());
        Assert.Equal(SourceCommit, provenance.GetProperty("graphqlJsCommit").GetString());
        Assert.Equal(UpstreamTestCount, provenance.GetProperty("upstreamTestsPassing").GetInt32());
        Assert.Equal(ParserCaseCount, provenance.GetProperty("caseCounts").GetProperty("parse").GetInt32());
        Assert.Equal(LexerCaseCount, provenance.GetProperty("caseCounts").GetProperty("lexer").GetInt32());
        Assert.Equal(BlockStringCaseCount, provenance.GetProperty("caseCounts").GetProperty("blockString").GetInt32());

        var cases = root.GetProperty("cases").EnumerateArray().ToArray();
        var provenanceCounts = provenance.GetProperty("caseCounts");
        Assert.Equal(ParserCaseCount, provenanceCounts.GetProperty("parse").GetInt32());
        Assert.Equal(provenanceCounts.EnumerateObject()
            .Where(item => item.Name != "excludedTestCases" && item.Name != "excludedTestNames")
            .Sum(item => item.Value.GetInt32()), cases.Length);
        Assert.Equal(cases.Length, cases.Select(item => item.GetProperty("id").GetString()).Distinct(StringComparer.Ordinal).Count());

        var accountedTests = cases
            .Select(item => TestKey(item.GetProperty("origin")))
            .Concat(root.GetProperty("exclusions").EnumerateArray().Select(TestKey))
            .ToHashSet(StringComparer.Ordinal);
        foreach (var test in root.GetProperty("upstreamTests").EnumerateArray())
        {
            Assert.Contains(TestKey(test), accountedTests);
        }

        Assert.Equal(UpstreamTestCount, root.GetProperty("upstreamTests").GetArrayLength());
    }

    [Theory]
    [MemberData(nameof(ParserCases))]
    public void EveryApplicableUpstreamParserCaseMatchesAcceptanceAstAndOffset(string caseId, string serializedCase)
    {
        using var caseDocument = JsonDocument.Parse(serializedCase);
        var testCase = caseDocument.RootElement;
        var source = ReadSource(testCase);
        var expected = testCase.GetProperty("expected");
        GraphQLParserOptions? options = null;
        if (testCase.TryGetProperty("parserOptions", out var parserOptions))
        {
            options = new GraphQLParserOptions(
                maximumTokenCount: parserOptions.TryGetProperty("maximumTokenCount", out var maximumTokens) ? maximumTokens.GetInt32() : GraphQLParserOptions.Default.MaximumTokenCount,
                noLocation: parserOptions.TryGetProperty("noLocation", out var noLocation) && noLocation.GetBoolean(),
                allowLegacyFragmentVariables: parserOptions.TryGetProperty("allowLegacyFragmentVariables", out var legacyVariables) && legacyVariables.GetBoolean());
        }

        DocumentNode? actualDocument = null;
        Exception? actualError = null;
        try
        {
            var sourceText = new SourceText(source.AsMemory());
            actualDocument = options is null ? Parser.Parse(sourceText) : Parser.Parse(sourceText, options);
        }
        catch (Exception exception)
        {
            actualError = exception;
        }

        if (expected.GetProperty("outcome").GetString() == "valid")
        {
            Assert.True(actualError is null, $"{caseId}: expected valid parse, got {actualError?.GetType().Name} at {(actualError as GraphQLSyntaxException)?.Position}: {actualError?.Message}");
            var expectedAst = JsonNode.Parse(expected.GetProperty("ast").GetRawText());
            var actualAst = CanonicalAstJson.Project(actualDocument!);
            Assert.True(JsonNode.DeepEquals(expectedAst, actualAst), $"{caseId}: canonical AST mismatch. Expected {expectedAst}; actual {actualAst}");
            return;
        }

        Assert.True(actualError is not null, $"{caseId}: expected rejection but the C# parser accepted the source.");
        var expectedCategory = expected.GetProperty("failureCategory").GetString();
        var actualCategory = actualError switch
        {
            GraphQLLexicalException => "lexical",
            GraphQLSyntaxException => "syntax",
            GraphQLResourceLimitException => "resource",
            _ => $"unexpected:{actualError!.GetType().Name}",
        };
        Assert.Equal(expectedCategory, actualCategory);

        var expectedPosition = expected.GetProperty("position");
        if (expectedPosition.ValueKind == JsonValueKind.Number)
        {
            var actualPosition = actualError switch
            {
                GraphQLLexicalException lexical => lexical.Position,
                GraphQLSyntaxException syntax => syntax.Position,
                GraphQLResourceLimitException limit => limit.Location.Start,
                _ => -1,
            };
            Assert.True(expectedPosition.GetInt32() == actualPosition,
                $"{caseId}: failure offset mismatch; expected {expectedPosition.GetInt32()}, actual {actualPosition}.");
        }
    }

    [Theory]
    [MemberData(nameof(LexerCases))]
    public void EveryApplicableUpstreamLexerCallMatchesTokenValuesAndSpans(string caseId, string serializedCase)
    {
        using var caseDocument = JsonDocument.Parse(serializedCase);
        var testCase = caseDocument.RootElement;
        var source = ReadSource(testCase);
        var expected = testCase.GetProperty("expected");
        var tokens = expected.GetProperty("tokens").EnumerateArray().ToArray();
        var errorPosition = expected.GetProperty("errorPosition");
        var hasError = errorPosition.ValueKind == JsonValueKind.Number;
        Assert.Equal(expected.GetProperty("advanceCalls").GetInt32(), tokens.Length + (hasError ? 1 : 0));

        var lexer = new GraphQLLexer(new SourceText(source.AsMemory()));
        foreach (var expectedToken in tokens)
        {
            Token actual;
            try
            {
                actual = lexer.NextToken();
            }
            catch (Exception exception)
            {
                throw new Xunit.Sdk.XunitException($"{caseId}: lexer threw before the expected token: {exception.GetType().Name}: {exception.Message}");
            }

            Assert.Equal(MapTokenKind(expectedToken.GetProperty("kind").GetString()!), actual.Kind);
            Assert.Equal(expectedToken.GetProperty("value").GetString(), actual.Value.ToString());
            Assert.Equal(expectedToken.GetProperty("start").GetInt32(), actual.Start);
            Assert.Equal(expectedToken.GetProperty("end").GetInt32(), actual.End);
        }

        if (hasError)
        {
            GraphQLLexicalException? exception = null;
            try
            {
                _ = lexer.NextToken();
            }
            catch (GraphQLLexicalException lexicalError)
            {
                exception = lexicalError;
            }
            Assert.True(exception is not null, $"{caseId}: expected a lexical failure after the captured tokens.");
            Assert.Equal(errorPosition.GetInt32(), exception!.Position);
        }
    }

    [Theory]
    [MemberData(nameof(BlockStringCases))]
    public void EveryApplicableUpstreamBlockStringNormalizationCaseMatches(string caseId, string serializedCase)
    {
        using var caseDocument = JsonDocument.Parse(serializedCase);
        var testCase = caseDocument.RootElement;
        var source = ReadSource(testCase);
        var lexer = new GraphQLLexer(new SourceText(source.AsMemory()));
        var token = lexer.NextToken();

        Assert.True(token.Kind == TokenKind.BlockString, $"{caseId}: expected a block string token, got {token.Kind}.");
        Assert.True(testCase.GetProperty("expected").GetProperty("value").GetString() == token.Value.ToString(), $"{caseId}: normalized block string value differs.");
        Assert.True(source.Length == token.End, $"{caseId}: block string source span differs.");
        Assert.True(lexer.NextToken().Kind == TokenKind.EndOfFile, $"{caseId}: expected end of input after the block string.");
    }

    [Theory]
    [MemberData(nameof(SchemaCoordinateCases))]
    public void EveryCapturedSchemaCoordinateCaseMatches(string caseId, string serializedCase)
    {
        using var caseDocument = JsonDocument.Parse(serializedCase);
        var testCase = caseDocument.RootElement;
        var source = new string(testCase.GetProperty("sourceUtf16").EnumerateArray().Select(item => (char)item.GetUInt16()).ToArray());
        var expected = testCase.GetProperty("expected");
        if (expected.GetProperty("outcome").GetString() == "valid")
        {
            var actual = CanonicalAstJson.Project(Parser.ParseSchemaCoordinate(source));
            Assert.True(JsonNode.DeepEquals(JsonNode.Parse(expected.GetProperty("ast").GetRawText()), actual), $"{caseId}: schema-coordinate AST differs.");
            return;
        }

        var exception = Assert.Throws<GraphQLSyntaxException>(() => Parser.ParseSchemaCoordinate(source));
        var expectedPosition = expected.GetProperty("position");
        if (expectedPosition.ValueKind == JsonValueKind.Number) Assert.Equal(expectedPosition.GetInt32(), exception.Position);
    }

    [Theory]
    [MemberData(nameof(UtilityCases))]
    public void EveryCapturedLanguageUtilityCaseMatches(string caseId, string serializedCase)
    {
        using var caseDocument = JsonDocument.Parse(serializedCase);
        var testCase = caseDocument.RootElement;
        var utility = testCase.GetProperty("utility");
        var expected = testCase.GetProperty("expected");
        var method = utility.GetProperty("method").GetString();
        switch (method)
        {
            case "dedentBlockStringLines":
                var lines = utility.GetProperty("input").EnumerateArray().Select(item => item.GetString()!).ToArray();
                var actualLines = GraphQLBlockString.DedentBlockStringLines(lines);
                Assert.Equal(expected.EnumerateArray().Select(item => item.GetString()), actualLines);
                break;
            case "isPrintableAsBlockString":
                Assert.Equal(expected.GetBoolean(), GraphQLBlockString.IsPrintableAsBlockString(utility.GetProperty("input").GetString()!));
                break;
            case "printBlockString":
                var options = utility.GetProperty("options");
                var minimize = options.ValueKind == JsonValueKind.Object && options.TryGetProperty("minimize", out var minimizeValue) && minimizeValue.GetBoolean();
                Assert.Equal(expected.GetString(), GraphQLBlockString.PrintBlockString(utility.GetProperty("input").GetString()!, new BlockStringPrintOptions(minimize)));
                break;
            case "printString":
                Assert.Equal(expected.GetString(), GraphQLString.PrintString(utility.GetProperty("input").GetString()!));
                break;
            case "printSourceLocation":
                var input = utility.GetProperty("input");
                var offset = input.GetProperty("locationOffset");
                var source = new Source(input.GetProperty("body").GetString()!, input.GetProperty("name").GetString()!,
                    new SourceLocationOffset(offset.GetProperty("line").GetInt32(), offset.GetProperty("column").GetInt32()));
                var location = input.GetProperty("location");
                Assert.Equal(expected.GetString(), GraphQLPrintLocation.PrintSourceLocation(source,
                    new GraphQLSourceLocation(location.GetProperty("line").GetInt32(), location.GetProperty("column").GetInt32())));
                break;
            case "Source":
                var sourceInput = utility.GetProperty("input");
                if (expected.GetProperty("outcome").GetString() == "valid")
                {
                    var body = sourceInput.GetProperty("body").GetString()!;
                    var name = sourceInput.GetProperty("name").GetString() ?? "GraphQL request";
                    var loc = sourceInput.GetProperty("locationOffset");
                    var created = loc.ValueKind == JsonValueKind.Object
                        ? new Source(body, name, new SourceLocationOffset(loc.GetProperty("line").GetInt32(), loc.GetProperty("column").GetInt32()))
                        : new Source(body, name);
                    Assert.Equal(name, created.Name);
                }
                else
                {
                    var loc = sourceInput.GetProperty("locationOffset");
                    Assert.Throws<ArgumentOutOfRangeException>(() => new Source(
                        sourceInput.GetProperty("body").GetString()!,
                        sourceInput.GetProperty("name").GetString() ?? string.Empty,
                        new SourceLocationOffset(loc.GetProperty("line").GetInt32(), loc.GetProperty("column").GetInt32())));
                }
                break;
            default:
                throw new InvalidDataException($"{caseId}: unsupported captured utility method '{method}'.");
        }
    }

    [Theory]
    [MemberData(nameof(PrinterCases))]
    public void EveryCapturedSourceBackedPrinterCaseMatches(string caseId, string serializedCase)
    {
        using var caseDocument = JsonDocument.Parse(serializedCase);
        var testCase = caseDocument.RootElement;
        var source = ReadSource(testCase);
        AstNode node;
        if (testCase.GetProperty("printer").GetProperty("nodeKind").GetString() is "TypeCoordinate" or "MemberCoordinate" or "ArgumentCoordinate" or "DirectiveCoordinate" or "DirectiveArgumentCoordinate")
        {
            node = Parser.ParseSchemaCoordinate(source);
        }
        else
        {
            var parserOptions = testCase.TryGetProperty("parserOptions", out var options)
                ? new GraphQLParserOptions(allowLegacyFragmentVariables: options.TryGetProperty("allowLegacyFragmentVariables", out var legacy) && legacy.GetBoolean())
                : GraphQLParserOptions.Default;
            node = Parser.Parse(new SourceText(source.AsMemory()), parserOptions);
        }
        var actual = GraphQLPrinter.Print(node);
        var expectedOutput = testCase.GetProperty("expected").GetProperty("output").GetString();
        Assert.True(expectedOutput == actual, $"{caseId}: expected [{expectedOutput}], actual [{actual}].");
    }

    private static IEnumerable<object[]> Cases(string kind)
    {
        using var document = JsonDocument.Parse(File.ReadAllText(CorpusPath));
        foreach (var item in document.RootElement.GetProperty("cases").EnumerateArray())
        {
            if (item.GetProperty("kind").GetString() == kind)
            {
                yield return [item.GetProperty("id").GetString()!, item.GetRawText()];
            }
        }
    }

    private static string TestKey(JsonElement item) =>
        $"{(item.TryGetProperty("file", out var file) ? file.GetString() : item.GetProperty("sourceFile").GetString())}\0{item.GetProperty("test").GetString()}";

    private static string ReadSource(JsonElement item) =>
        new(item.GetProperty("sourceUtf16").EnumerateArray().Select(codeUnit => (char)codeUnit.GetUInt16()).ToArray());

    private static TokenKind MapTokenKind(string kind) => kind switch
    {
        "<EOF>" => TokenKind.EndOfFile,
        "!" => TokenKind.Bang,
        "$" => TokenKind.Dollar,
        "&" => TokenKind.Ampersand,
        "(" => TokenKind.ParenthesisLeft,
        ")" => TokenKind.ParenthesisRight,
        "..." => TokenKind.Spread,
        ":" => TokenKind.Colon,
        "=" => TokenKind.Equals,
        "@" => TokenKind.At,
        "[" => TokenKind.BracketLeft,
        "]" => TokenKind.BracketRight,
        "{" => TokenKind.BraceLeft,
        "|" => TokenKind.Pipe,
        "}" => TokenKind.BraceRight,
        "Name" => TokenKind.Name,
        "Int" => TokenKind.Integer,
        "Float" => TokenKind.Float,
        "String" => TokenKind.String,
        "BlockString" => TokenKind.BlockString,
        _ => throw new Xunit.Sdk.XunitException($"Unknown graphql-js token kind {kind} in generated corpus."),
    };
}
