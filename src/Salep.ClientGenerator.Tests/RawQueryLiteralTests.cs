using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Salep.ClientGenerator.Model;
using Salep.ClientGenerator.Targets;
using Salep.GraphQLParser;
using Xunit;

namespace Salep.ClientGenerator.Tests;

public sealed class RawQueryLiteralTests
{
    [Theory]
    [InlineData("\n")]
    [InlineData("\r\n")]
    [InlineData("\r")]
    public void Raw_queries_preserve_formatted_documents_and_fragment_values(string newline)
    {
        var schema = GraphQlModelFactory.CreateSchema(Parse("type Query { echo(text: String): String }"));
        var document = "query Echo {\n  ...Fields\n}\nfragment Fields on Query {\n  echo(text: \"\\\"\\\"\\\" path \\\\ café\")\n}".ReplaceLineEndings(newline);
        var executable = GraphQlModelFactory.CreateExecutable(schema, [Parse(document)]);
        var source = new ScribanCSharpTemplateGenerator().GenerateOperations(schema, executable, new CSharpCodeGenerationTarget());
        var tree = CSharpSyntaxTree.ParseText(source, cancellationToken: TestContext.Current.CancellationToken);
        Assert.DoesNotContain(tree.GetDiagnostics(TestContext.Current.CancellationToken), item => item.Severity == DiagnosticSeverity.Error);
        var property = tree.GetRoot(TestContext.Current.CancellationToken).DescendantNodes().OfType<PropertyDeclarationSyntax>().Single(item => item.Identifier.ValueText == "Query");
        var literal = Assert.IsType<LiteralExpressionSyntax>(property.ExpressionBody!.Expression);
        Assert.Equal(Microsoft.CodeAnalysis.CSharp.SyntaxKind.MultiLineRawStringLiteralToken, literal.Token.Kind());
        Assert.Equal(new GraphQlOperationDocumentFormatter(schema, executable).Format(executable.Operations.Single()), literal.Token.ValueText);
        var alternateTree = CSharpSyntaxTree.ParseText(source.ReplaceLineEndings(newline), cancellationToken: TestContext.Current.CancellationToken);
        Assert.DoesNotContain(alternateTree.GetDiagnostics(TestContext.Current.CancellationToken), item => item.Severity == DiagnosticSeverity.Error);
        var alternateLiteral = alternateTree.GetRoot(TestContext.Current.CancellationToken).DescendantNodes().OfType<PropertyDeclarationSyntax>()
            .Single(item => item.Identifier.ValueText == "Query").ExpressionBody!.Expression as LiteralExpressionSyntax;
        Assert.Equal(literal.Token.ValueText.ReplaceLineEndings(newline), alternateLiteral!.Token.ValueText);
        Assert.Equal(source, source.ReplaceLineEndings(Environment.NewLine));
        Assert.Contains("        query Echo {", source.ReplaceLineEndings("\n").Split('\n'));
        Assert.Contains("        fragment Fields on Query {", source, StringComparison.Ordinal);
    }

    private static DocumentNode Parse(string source) => global::Salep.GraphQLParser.GraphQLParser.Parse(new SourceText(source.AsMemory()));
}
