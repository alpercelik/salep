using Salep.ClientGenerator.TypeScript.Generation;
using Xunit;

namespace Salep.ClientGenerator.Tests;

public sealed class TypeScriptExtractionIterationFourTests
{
    [Theory]
    [InlineData("if (ok) {}")]
    [InlineData("if (ok) { read(); }")]
    [InlineData("while (ok) { read(); }")]
    [InlineData("if (ok) read(); else")]
    [InlineData("do")]
    [InlineData("function declaration() { return {}; }")]
    [InlineData("class Declaration { method() {} }")]
    [InlineData("if (ok) {} /* comment */")]
    [InlineData("if (ok) read(); else // comment\n")]
    public void Regex_statements_do_not_expose_documents(string prefix)
    {
        var source = prefix + " /gql`query Bogus { missing }`/.test(text);" + (prefix == "do" ? " while (ok);" : "") + " const real=gql`query Real { value }`;";
        Assert.Equal(["query Real { value }"], TypeScriptDocumentExtractor.Extract(source, jsx: false));
    }

    [Theory]
    [InlineData("const value = {}")]
    [InlineData("const value = { nested: {} }")]
    [InlineData("const value = function () { return {}; }")]
    [InlineData("const value = function named() { return {}; }")]
    [InlineData("const value = async function () { return {}; }")]
    [InlineData("const value = () => {}")]
    [InlineData("const value = class { method() {} }")]
    [InlineData("const value = ({})")]
    public void Division_after_object_and_function_expressions_preserves_documents(string prefix)
        => Assert.Equal(["query Operand { value }"], TypeScriptDocumentExtractor.Extract(prefix + " / gql`query Operand { value }`;", jsx: false));

    [Fact]
    public void Nested_blocks_comments_and_template_expression_offsets_remain_correct()
    {
        const string source = """"
            const callback = () => { { } /gql`query BlockText { missing }`/.test(text); return {}; };
            const outer = `${ /}/.test(text) ? gql`query Nested { value }` : null }`;
            if (ok) { const object = {}; } /* comment */ /gql`query TailText { missing }`/.test(text);
            """";
        var document = Assert.Single(TypeScriptDocumentExtractor.ExtractWithLocations(source, jsx: false));
        Assert.Equal("query Nested { value }", document.Text);
        Assert.Equal(source.IndexOf(document.Text, StringComparison.Ordinal), document.Offsets[0]);
    }
    [Theory]
    [InlineData("const outer = `${ {} / gql`query Operand { value }` }`;")]
    [InlineData("const outer = `${ function () {} / gql`query Operand { value }` }`;")]
    [InlineData("const view = <div>{ {} / gql`query Operand { value }` }</div>;")]
    public void Expression_regions_start_with_object_or_function_expressions(string source)
        => Assert.Equal(["query Operand { value }"], TypeScriptDocumentExtractor.Extract(source));

}
