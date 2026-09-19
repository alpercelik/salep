using GraphQLParser;
using Xunit;

namespace GraphQLParser.Tests;

public sealed class ParserDiagnosticTests
{
    [Fact]
    public void DiagnosticModeReturnsSuccessForAValidDocument()
    {
        const string source = "query Q { field }";
        var result = GraphQLParser.ParseWithDiagnostics(new SourceText(source.AsMemory()));

        Assert.True(result.Success);
        Assert.NotNull(result.Document);
        Assert.Empty(result.Diagnostics);
    }

    [Fact]
    public void DiagnosticModeCollectsMultipleDefinitionErrorsAndKeepsLaterValidDefinitions()
    {
        const string source = "query BadA { } query BadB { } query Good { field }";
        var result = GraphQLParser.ParseWithDiagnostics(new SourceText(source.AsMemory()));

        Assert.False(result.Success);
        Assert.Equal(2, result.Diagnostics.Count);
        Assert.NotNull(result.Document);
        var good = Assert.IsType<OperationDefinitionNode>(Assert.Single(result.Document.Definitions));
        Assert.Equal("Good", good.Name!.Value.ToString());
        Assert.All(result.Diagnostics, diagnostic =>
        {
            Assert.Equal("syntax", diagnostic.Category);
            Assert.Contains("selection", diagnostic.Expected, StringComparison.OrdinalIgnoreCase);
            Assert.Contains("BraceRight", diagnostic.Actual);
        });

        var strict = Assert.Throws<GraphQLSyntaxException>(() => GraphQLParser.Parse(new SourceText(source.AsMemory())));
        Assert.Equal(result.Diagnostics[0].Location.Start, strict.Position);
    }

    [Fact]
    public void DiagnosticsExposeLexicalFailureAndTokenContextWithoutLosingEarlierDefinitions()
    {
        const string source = "query Good { field } query Bad { field(value: 1e+) }";
        var result = GraphQLParser.ParseWithDiagnostics(new SourceText(source.AsMemory()));

        var diagnostic = Assert.Single(result.Diagnostics);
        Assert.Equal("lexical", diagnostic.Category);
        Assert.Equal("a valid GraphQL token", diagnostic.Expected);
        Assert.Equal(")", diagnostic.Actual);
        Assert.Equal(source.IndexOf(')', source.IndexOf("1e+", StringComparison.Ordinal)), diagnostic.Location.Start);
        Assert.Equal("Good", Assert.IsType<OperationDefinitionNode>(Assert.Single(result.Document!.Definitions)).Name!.Value.ToString());
        Assert.False(result.Success);

        var strict = Assert.Throws<GraphQLLexicalException>(() => GraphQLParser.Parse(new SourceText(source.AsMemory())));
        Assert.Equal(diagnostic.Location.Start, strict.Position);
    }

    [Fact]
    public void DiagnosticResultHasNoDocumentWhenNoDefinitionCanBeRecovered()
    {
        var result = GraphQLParser.ParseWithDiagnostics(new SourceText("query Q { }".AsMemory()));

        Assert.Null(result.Document);
        Assert.Single(result.Diagnostics);
        Assert.False(result.Success);
    }
}
