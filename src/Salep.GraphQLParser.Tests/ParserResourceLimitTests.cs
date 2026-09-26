using Salep.GraphQLParser;
using System.Runtime.InteropServices;
using Xunit;
using Parser = Salep.GraphQLParser.GraphQLParser;

namespace Salep.GraphQLParser.Tests;

public sealed class ParserResourceLimitTests
{
    [Fact]
    public void SourceLengthLimitReportsObservedLengthAndExceededSpan()
    {
        var options = new GraphQLParserOptions(maximumSourceLength: 5);
        var source = new SourceText("query {".AsMemory());
        var error = Assert.Throws<GraphQLResourceLimitException>(() => GraphQLParser.Parse(source, options));

        Assert.Equal("source length", error.Resource);
        Assert.Equal(5, error.Limit);
        Assert.Equal(7, error.Observed);
        Assert.Equal(new SourceLocation(5, 7), error.Location);

        var exactBoundary = new GraphQLParserOptions(maximumSourceLength: 5);
        Assert.Single(GraphQLParser.Parse(new SourceText("{ f }".AsMemory()), exactBoundary).Definitions);
    }

    [Fact]
    public void DefaultSourceLimitRejectsLargeInputBeforeTokenization()
    {
        var source = new SourceText(new string(' ', GraphQLParserOptions.Default.MaximumSourceLength + 1).AsMemory());
        var error = Assert.Throws<GraphQLResourceLimitException>(() => GraphQLParser.Parse(source));

        Assert.Equal("source length", error.Resource);
        Assert.Equal(GraphQLParserOptions.Default.MaximumSourceLength + 1, error.Observed);
    }

    [Fact]
    public void TokenLimitAllowsTheBoundaryAndReportsTheFirstExcessToken()
    {
        var source = new SourceText("{ f }".AsMemory());
        var exact = new GraphQLParserOptions(maximumTokenCount: 3);
        Assert.Single(GraphQLParser.Parse(source, exact).Definitions);

        var limited = new GraphQLParserOptions(maximumTokenCount: 2);
        var error = Assert.Throws<GraphQLResourceLimitException>(() => GraphQLParser.Parse(source, limited));
        Assert.Equal("token count", error.Resource);
        Assert.Equal(3, error.Observed);
        Assert.Equal(new SourceLocation(4, 5), error.Location);
    }

    [Fact]
    public void NestingPreflightRejectsDeepInputBeforeRecursiveParsing()
    {
        var deeplyNested = "query { field " + new string('{', 10_000) + " leaf " + new string('}', 10_000) + " }";
        var options = new GraphQLParserOptions(maximumNestingDepth: 64);
        var error = Assert.Throws<GraphQLResourceLimitException>(() => GraphQLParser.Parse(new SourceText(deeplyNested.AsMemory()), options));

        Assert.Equal("nesting depth", error.Resource);
        Assert.Equal(65, error.Observed);
        Assert.Equal(64, error.Limit);
        Assert.InRange(error.Location.Start, 0, deeplyNested.Length);
    }

    [Fact]
    public void DiagnosticLimitCanBeConfiguredAndIsReportedAsTruncated()
    {
        var malformed = string.Join(' ', Enumerable.Range(0, 8).Select(index => $"query Bad{index} {{ }}"));
        var options = new GraphQLParserOptions(maximumDiagnosticCount: 2);
        var result = GraphQLParser.ParseWithDiagnostics(new SourceText(malformed.AsMemory()), options);

        Assert.Equal(2, result.Diagnostics.Count);
        Assert.True(result.DiagnosticsTruncated);
    }

    [Fact]
    public void DiagnosticModeReportsEmptyAndWhitespaceOnlyDocuments()
    {
        foreach (var source in new[] { string.Empty, " \n\t" })
        {
            var result = GraphQLParser.ParseWithDiagnostics(new SourceText(source.AsMemory()));
            var diagnostic = Assert.Single(result.Diagnostics);
            Assert.Null(result.Document);
            Assert.Equal("syntax", diagnostic.Category);
            Assert.Equal(source.Length, diagnostic.Location.Start);
        }
    }

    [Fact]
    public void DefaultLimitsAndOptionsValidationAreExplicit()
    {
        Assert.Equal(1_048_576, GraphQLParserOptions.Default.MaximumSourceLength);
        Assert.Equal(250_000, GraphQLParserOptions.Default.MaximumTokenCount);
        Assert.Equal(128, GraphQLParserOptions.Default.MaximumNestingDepth);
        Assert.Equal(100, GraphQLParserOptions.Default.MaximumDiagnosticCount);
        Assert.Throws<ArgumentOutOfRangeException>(() => new GraphQLParserOptions(maximumTokenCount: 0));
        Assert.Throws<ArgumentOutOfRangeException>(() => new GraphQLParserOptions(maximumDiagnosticCount: 10_001));
    }

    [Fact]
    public void RepeatedAndConcurrentIndependentParsesProduceIdenticalTrees()
    {
        const string source = "query Q($id: ID!) { alias: user(id: $id) { id name } } type Query { value: [String!]! }";
        var sourceText = new SourceText(source.AsMemory());
        var expected = CanonicalAstJson.Project(GraphQLParser.Parse(sourceText)).ToJsonString();

        for (var index = 0; index < 100; index++)
        {
            Assert.Equal(expected, CanonicalAstJson.Project(GraphQLParser.Parse(sourceText)).ToJsonString());
        }

        var results = new string[64];
        Parallel.For(0, results.Length, index =>
        {
            results[index] = CanonicalAstJson.Project(GraphQLParser.Parse(sourceText)).ToJsonString();
        });
        Assert.All(results, result => Assert.Equal(expected, result));
    }

    [Fact]
    public void DefaultParsingSnapshotsMutableCallerMemoryIntoImmutableStorage()
    {
        var document = ParseFromTemporaryBuffer(out var bufferReference);

        GC.Collect();
        GC.WaitForPendingFinalizers();
        GC.Collect();

        Assert.False(bufferReference.TryGetTarget(out _));
        Assert.False(MemoryMarshal.TryGetArray(document.Source.Content, out _));
        Assert.Equal("{ retainedField }", document.Source.Content.ToString());
        var operation = Assert.IsType<OperationDefinitionNode>(document.Definitions[0]);
        var field = Assert.IsType<FieldNode>(operation.SelectionSet.Selections[0]);
        Assert.Equal("retainedField", field.Name.Value.ToString());
        Assert.Equal(new Location(2, 15, 1, 3), field.Name.Location);
    }

    [Fact]
    public void BorrowedParsingIsExplicitAndReflectsTheRetainedCallerBuffer()
    {
        var buffer = "{ borrowedName }".ToCharArray();
        var document = GraphQLParser.ParseBorrowed(new SourceText(buffer.AsMemory()));

        Assert.True(MemoryMarshal.TryGetArray(document.Source.Content, out ArraySegment<char> sourceSegment));
        Assert.Same(buffer, sourceSegment.Array);
        buffer[2] = 'B';

        var operation = Assert.IsType<OperationDefinitionNode>(document.Definitions[0]);
        var field = Assert.IsType<FieldNode>(operation.SelectionSet.Selections[0]);
        Assert.Equal("BorrowedName", field.Name.Value);
        Assert.Equal(new Location(2, 14, 1, 3), field.Name.Location);
    }

    [Fact]
    public void BorrowedDiagnosticAndCoordinateEntriesRetainTheirInput()
    {
        var diagnosticBuffer = "{ diagnosticField }".ToCharArray();
        var diagnosticResult = GraphQLParser.ParseWithDiagnosticsBorrowed(new SourceText(diagnosticBuffer.AsMemory()));
        Assert.True(diagnosticResult.Success);
        Assert.True(MemoryMarshal.TryGetArray(diagnosticResult.Document!.Source.Content, out var diagnosticSource));
        Assert.Same(diagnosticBuffer, diagnosticSource.Array);

        var coordinateBuffer = "TypeName.field".ToCharArray();
        var coordinate = GraphQLParser.ParseSchemaCoordinateBorrowed(new SourceText(coordinateBuffer.AsMemory()));
        Assert.True(MemoryMarshal.TryGetArray(coordinate.Name.SourceValue, out var coordinateName));
        Assert.Same(coordinateBuffer, coordinateName.Array);
    }

    [Fact]
    public void DefaultSchemaCoordinateParsingOwnsNamesFromMutableCallerMemory()
    {
        var buffer = "TypeName.field".ToCharArray();
        var coordinate = GraphQLParser.ParseSchemaCoordinate(new SourceText(buffer.AsMemory()));
        buffer[0] = 'X';

        Assert.Equal("TypeName", coordinate.Name.Value);
        Assert.Equal("TypeName.field", coordinate.ToString());
        Assert.Equal(new Location(0, 14, 1, 1), coordinate.Location);
    }

    [Fact]
    public void ConcurrentReadsOfOneOwnedDocumentRemainStable()
    {
        var document = GraphQLParser.Parse(new SourceText("query Q { user { id name } }".AsMemory()));
        var expected = GraphQLPrinter.Print(document);
        var results = new string[64];

        Parallel.For(0, results.Length, index => results[index] = GraphQLPrinter.Print(document));

        Assert.All(results, result => Assert.Equal(expected, result));
    }

    private static DocumentNode ParseFromTemporaryBuffer(out WeakReference<char[]> bufferReference)
    {
        var buffer = "{ retainedField }".ToCharArray();
        bufferReference = new WeakReference<char[]>(buffer);
        return GraphQLParser.Parse(new SourceText(buffer.AsMemory()));
    }

    [Fact]
    public void ResourceFailureIsConsistentInStrictAndDiagnosticEntryPoints()
    {
        const string source = "query { a { b { c } } }";
        var options = new GraphQLParserOptions(maximumNestingDepth: 2);

        var strict = Assert.Throws<GraphQLResourceLimitException>(() => GraphQLParser.Parse(new SourceText(source.AsMemory()), options));
        var diagnostic = Assert.Throws<GraphQLResourceLimitException>(() => GraphQLParser.ParseWithDiagnostics(new SourceText(source.AsMemory()), options));
        Assert.Equal(strict.Resource, diagnostic.Resource);
        Assert.Equal(strict.Location, diagnostic.Location);
    }
}
