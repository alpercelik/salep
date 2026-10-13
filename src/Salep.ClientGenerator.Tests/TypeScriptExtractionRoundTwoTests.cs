using Salep.ClientGenerator.TypeScript.Generation;
using Xunit;

namespace Salep.ClientGenerator.Tests;

public sealed class TypeScriptExtractionRoundTwoTests
{
    [Theory]
    [InlineData("const outer = `${gql`query Nested { value }`}`;")]
    [InlineData("const outer = `${`nested ${graphql(`query Nested { value }`)}`}`;")]
    [InlineData("const outer = `${ /}/.test(x) ? gql`query Nested { value }` : '' }`;")]
    [InlineData("const outer = `${<div>{gql`query Nested { value }`}</div>}`;")]
    public void Static_documents_inside_unrelated_template_expressions_are_extracted(string source)
    {
        var document = Assert.Single(TypeScriptDocumentExtractor.ExtractWithLocations(source));
        Assert.Equal("query Nested { value }", document.Text);
        Assert.Equal(source.IndexOf("query Nested", StringComparison.Ordinal), document.Offsets[0]);
        Assert.Equal(source.IndexOf('`', document.Offsets[0]), document.Offsets[^1]);
    }

    [Theory]
    [InlineData("if (enabled)")]
    [InlineData("while (enabled)")]
    [InlineData("for (; enabled;)")]
    [InlineData("if (check(')'))")]
    [InlineData("if (check(/* ) */ true))")]
    [InlineData("if (/[(]/.test(text))")]
    public void Regex_statement_after_control_header_does_not_expose_fake_documents(string header)
    {
        var source = header + " /gql`query Fake { bad }`/.test(text); const d = gql`query Real { value }`;";
        Assert.Equal("query Real { value }", Assert.Single(TypeScriptDocumentExtractor.Extract(source, jsx: false)));
    }

    [Theory]
    [InlineData("const n = (value) / 2; const d = gql`query Real { value }`;")]
    [InlineData("const n = calculate(value) / 2; const d = gql`query Real { value }`;")]
    [InlineData("const n = calculator.if(value) / 2; const d = gql`query Real { value }`;")]
    [InlineData("const n = calculator./* comment */if(value) / 2; const d = gql`query Real { value }`;")]
    [InlineData("const outer = `${calculate(value) / 2} ${gql`query Real { value }`}`;")]
    public void Division_after_group_or_call_remains_division(string source)
        => Assert.Equal("query Real { value }", Assert.Single(TypeScriptDocumentExtractor.Extract(source, jsx: false)));
}
