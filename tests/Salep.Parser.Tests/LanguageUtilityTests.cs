using Salep.Parser;
using Xunit;

namespace Salep.Parser.Tests;

public sealed class LanguageUtilityTests
{
    [Fact]
    public void GraphQLQuotedStringFormattingEscapesControlCharactersAndPreservesUnicode()
    {
        Assert.Equal("\"hello world\"", GraphQLString.PrintString("hello world"));
        Assert.Equal("\"\\\"hello\\\"\"", GraphQLString.PrintString("\"hello\""));
        Assert.Equal("\"who's test\"", GraphQLString.PrintString("who's test"));
        Assert.Equal("\"escape: \\\\\"", GraphQLString.PrintString("escape: \\"));
        Assert.Equal("\"\\b\\f\\n\\r\\t\"", GraphQLString.PrintString("\b\f\n\r\t"));
        Assert.Equal("\"\\u0000\"", GraphQLString.PrintString("\0"));
        Assert.Equal("\"↻ 😀\"", GraphQLString.PrintString("↻ 😀"));

        var controls = new string(Enumerable.Range(0, 0xA0).Select(value => (char)value).ToArray());
        Assert.Equal(
            "\"\\u0000\\u0001\\u0002\\u0003\\u0004\\u0005\\u0006\\u0007\\b\\t\\n\\u000B\\f\\r\\u000E\\u000F"
            + "\\u0010\\u0011\\u0012\\u0013\\u0014\\u0015\\u0016\\u0017\\u0018\\u0019\\u001A\\u001B\\u001C\\u001D\\u001E\\u001F"
            + " !\\\"#$%&'()*+,-./0123456789:;<=>?@ABCDEFGHIJKLMNOPQRSTUVWXYZ[\\\\]^_`abcdefghijklmnopqrstuvwxyz{|}~\\u007F"
            + "\\u0080\\u0081\\u0082\\u0083\\u0084\\u0085\\u0086\\u0087\\u0088\\u0089\\u008A\\u008B\\u008C\\u008D\\u008E\\u008F"
            + "\\u0090\\u0091\\u0092\\u0093\\u0094\\u0095\\u0096\\u0097\\u0098\\u0099\\u009A\\u009B\\u009C\\u009D\\u009E\\u009F\"",
            GraphQLString.PrintString(controls));
    }

    [Fact]
    public void AstPredicatesClassifyNodesAndFindNestedVariables()
    {
        var source = "query Q { field(arg: { nested: [$value] }) }";
        var document = GraphQLParser.Parse(new SourceText(source.AsMemory()));
        var operation = Assert.IsType<OperationDefinitionNode>(Assert.Single(document.Definitions));
        var selection = Assert.IsType<FieldNode>(Assert.Single(operation.SelectionSet.Selections));
        var nestedValue = Assert.Single(selection.Arguments).Value;

        Assert.True(GraphQLAstPredicates.IsDefinitionNode(operation));
        Assert.True(GraphQLAstPredicates.IsExecutableDefinitionNode(operation));
        Assert.False(GraphQLAstPredicates.IsTypeSystemDefinitionNode(operation));
        Assert.True(GraphQLAstPredicates.IsSelectionNode(selection));
        Assert.True(GraphQLAstPredicates.IsValueNode(nestedValue));
        Assert.False(GraphQLAstPredicates.IsConstValueNode(nestedValue));

        var constantValue = GraphQLParser.Parse(new SourceText("query Q { field(arg: { nested: [1] }) }".AsMemory()));
        var constantField = Assert.IsType<FieldNode>(Assert.Single(Assert.IsType<OperationDefinitionNode>(Assert.Single(constantValue.Definitions)).SelectionSet.Selections));
        Assert.True(GraphQLAstPredicates.IsConstValueNode(Assert.Single(constantField.Arguments).Value));

        var sdl = GraphQLParser.Parse(new SourceText("type User { id: ID } extend type User { name: String }".AsMemory()));
        Assert.True(GraphQLAstPredicates.IsTypeSystemDefinitionNode(sdl.Definitions[0]));
        Assert.True(GraphQLAstPredicates.IsTypeDefinitionNode(sdl.Definitions[0]));
        Assert.True(GraphQLAstPredicates.IsTypeSystemExtensionNode(sdl.Definitions[1]));
        Assert.True(GraphQLAstPredicates.IsTypeExtensionNode(sdl.Definitions[1]));
        Assert.True(GraphQLAstPredicates.IsSchemaCoordinateNode(GraphQLParser.ParseSchemaCoordinate("User.id")));
    }

    [Fact]
    public void AstVisitorWalksInOrderSupportsSkippingAndStopsPredictably()
    {
        var document = GraphQLParser.Parse(new SourceText("query Q { first(arg: 1) second }".AsMemory()));
        var entered = new List<AstNodeKind>();
        var left = new List<AstNodeKind>();
        Assert.True(GraphQLAstVisitor.Visit(document, node =>
        {
            entered.Add(node.AstKind);
            return node is FieldNode field && field.Name.SourceValue.Span.SequenceEqual("second")
                ? GraphQLVisitControl.SkipChildren
                : GraphQLVisitControl.Continue;
        }, node =>
        {
            left.Add(node.AstKind);
            return GraphQLVisitControl.Continue;
        }));

        Assert.Equal(AstNodeKind.Document, entered[0]);
        Assert.Contains(AstNodeKind.Argument, entered);
        Assert.Equal(entered.Count, left.Count);
        Assert.Equal(AstNodeKind.Document, left[^1]);
        Assert.Equal(3, entered.Count(kind => kind == AstNodeKind.Name));

        var stopped = new List<AstNodeKind>();
        Assert.False(GraphQLAstVisitor.Visit(document, node =>
        {
            stopped.Add(node.AstKind);
            return stopped.Count == 3 ? GraphQLVisitControl.Stop : GraphQLVisitControl.Continue;
        }));
        Assert.Equal(3, stopped.Count);
    }

    [Fact]
    public void GraphQLPrinterFormatsExecutableAndSchemaDocumentsDeterministically()
    {
        var query = GraphQLParser.Parse(new SourceText("{trip(wheelchair:false arriveBy:false){dateTime}}".AsMemory()));
        Assert.Equal("{\n  trip(wheelchair: false, arriveBy: false) {\n    dateTime\n  }\n}", GraphQLPrinter.Print(query));

        var mutation = GraphQLParser.Parse(new SourceText("mutation ($foo: TestType) @testDirective { id, name }".AsMemory()));
        Assert.Equal("mutation ($foo: TestType) @testDirective {\n  id\n  name\n}", GraphQLPrinter.Print(mutation));

        var sdl = GraphQLParser.Parse(new SourceText("type User implements Node { id: ID! name(arg: String = \"default\"): String }".AsMemory()));
        var printedSdl = GraphQLPrinter.Print(sdl);
        Assert.Equal("type User implements Node {\n  id: ID!\n  name(arg: String = \"default\"): String\n}", printedSdl);
        Assert.Equal(printedSdl, GraphQLPrinter.Print(GraphQLParser.Parse(new SourceText(printedSdl.AsMemory()))));
    }

    [Fact]
    public void GraphQLPrinterMovesLongArgumentListsOntoSeparateLines()
    {
        const string source = "{trip(wheelchair:false arriveBy:false includePlannedCancellations:true transitDistanceReluctance:2000){dateTime}}";
        var printed = GraphQLPrinter.Print(GraphQLParser.Parse(new SourceText(source.AsMemory())));
        Assert.Equal("{\n  trip(\n    wheelchair: false\n    arriveBy: false\n    includePlannedCancellations: true\n    transitDistanceReluctance: 2000\n  ) {\n    dateTime\n  }\n}", printed);
    }

    [Theory]
    [InlineData("valid-executable-comprehensive.graphql")]
    [InlineData("valid-sdl-comprehensive.graphql")]
    public void PrinterOutputRoundTripsComprehensiveAstFixtures(string fileName)
    {
        var path = Path.Combine(AppContext.BaseDirectory, "Fixtures", "Oracle", fileName);
        var source = File.ReadAllText(path);
        var options = new GraphQLParserOptions(noLocation: true, allowLegacyFragmentVariables: true);
        var document = GraphQLParser.Parse(new SourceText(source.AsMemory()), options);
        var printed = GraphQLPrinter.Print(document);
        var reparsed = GraphQLParser.Parse(new SourceText(printed.AsMemory()), options);
        Assert.True(System.Text.Json.Nodes.JsonNode.DeepEquals(CanonicalAstJson.Project(document), CanonicalAstJson.Project(reparsed)),
            $"Printed {fileName} did not preserve the AST.\n{printed}");
    }
}
