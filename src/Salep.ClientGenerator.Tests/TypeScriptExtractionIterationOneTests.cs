using Salep.ClientGenerator.TypeScript.Generation;
using Xunit;

namespace Salep.ClientGenerator.Tests;

public sealed class TypeScriptExtractionIterationOneTests
{
    [Theory]
    [InlineData("if (ok) /* comment */ ")]
    [InlineData("while (ok) // comment\n")]
    [InlineData("for (;;) /* first */ // second\n")]
    [InlineData("return /* comment */ ")]
    [InlineData("throw // comment\n")]
    [InlineData("const pattern = /* comment */ ")]
    [InlineData("const pattern = value + /* comment */ ")]
    public void Regex_contents_after_trivia_are_not_documents(string prefix)
    {
        var source = prefix + "/gql`query Bogus { nope }`/.test(text); const real = gql`query Real { value }`;";
        var documents = TypeScriptDocumentExtractor.ExtractWithLocations(source);
        var document = Assert.Single(documents);
        Assert.Equal("query Real { value }", document.Text);
        Assert.Equal(source.IndexOf(document.Text, StringComparison.Ordinal), document.Offsets[0]);
    }

    [Theory]
    [InlineData("read() /* comment */")]
    [InlineData("counter++ /* comment */")]
    [InlineData("object.if() /* comment */")]
    [InlineData("object.while() // comment\n")]
    public void Division_after_calls_preserves_tagged_operands(string prefix)
    {
        var source = prefix + " / gql`query Operand { value }`;";
        Assert.Equal(["query Operand { value }"], TypeScriptDocumentExtractor.Extract(source));
    }

    [Fact]
    public void Comment_aware_regex_context_preserves_jsx_and_literal_offsets()
    {
        const string source = """"
            if (read(")", /[(]/)) /* context */ /gql`query Bogus { nope }`/.test(text);
            const view = <section>gql`query Text { nope }`{graphql(`query Actual { value }`)}</section>;
            """";
        var document = Assert.Single(TypeScriptDocumentExtractor.ExtractWithLocations(source));
        Assert.Equal("query Actual { value }", document.Text);
        Assert.Equal(source.IndexOf(document.Text, StringComparison.Ordinal), document.Offsets[0]);
    }
    [Fact]
    public void Regex_braces_in_template_expressions_preserve_nested_documents()
    {
        const string source = "const message = `prefix ${ /}/.test(value) ? gql`query Nested { value }` : null } suffix`;";
        var document = Assert.Single(TypeScriptDocumentExtractor.ExtractWithLocations(source));
        Assert.Equal("query Nested { value }", document.Text);
        Assert.Equal(source.IndexOf(document.Text, StringComparison.Ordinal), document.Offsets[0]);
    }

}
