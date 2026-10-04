using Salep.ClientGenerator.Model;
using Salep.ClientGenerator.TypeScript.Generation;
using Xunit;

namespace Salep.ClientGenerator.Tests;

public sealed class TypeScriptExtractionIterationFiveTests
{
    [Theory]
    [InlineData("async function load() { const result = await /gql`query Fake { bad }`/.exec(text); return gql`query Real { value }`; }")]
    [InlineData("const load = async () => await /graphql(`query Fake { bad }`)/.exec(text); const q = gql`query Real { value }`;")]
    [InlineData("async function load() { return `${await /gql`query Fake { bad }`/.exec(text)} ${gql`query Real { value }`}`; }")]
    public void Regex_operands_of_await_do_not_expose_fake_documents(string source)
        => Assert.Equal("query Real { value }", Assert.Single(TypeScriptDocumentExtractor.Extract(source, jsx: false)));

    [Theory]
    [InlineData("const ratio = obj.await / 2; const q = gql`query Real { value }`;")]
    [InlineData("const ratio = obj.await() / 2; const q = gql`query Real { value }`;")]
    [InlineData("const ratio = obj./* comment */await / 2; const q = gql`query Real { value }`;")]
    public void Await_member_names_preserve_division(string source)
        => Assert.Equal("query Real { value }", Assert.Single(TypeScriptDocumentExtractor.Extract(source, jsx: false)));

    [Theory]
    [InlineData("graphql(`query Real { value }`,)")]
    [InlineData("graphql(`query Real { value }`, /* trailing */ )")]
    [InlineData("gql(`query Real { value }`, // trailing\n)")]
    public void One_argument_call_can_have_trailing_comma(string source)
        => Assert.Equal("query Real { value }", Assert.Single(TypeScriptDocumentExtractor.Extract(source, jsx: false)));

    [Theory]
    [InlineData("graphql(`query Real { value }`, other)")]
    [InlineData("graphql(`query Real { value }`,,)")]
    public void Actual_second_arguments_and_elisions_are_rejected(string source)
        => Assert.Throws<InvalidDataException>(() => TypeScriptDocumentExtractor.Extract(source, jsx: false));

    [Fact]
    public void Trailing_comma_preserves_cooked_text_and_original_offsets()
    {
        const string source = "\r\nconst q = graphql(`query Real {\\n value\\n}`, /* trailing */);";
        var document = Assert.Single(TypeScriptDocumentExtractor.ExtractWithLocations(source, jsx: false));
        Assert.Equal("query Real {\n value\n}", document.Text);
        Assert.Equal(source.IndexOf("query Real", StringComparison.Ordinal), document.Offsets[0]);
        Assert.Equal(source.IndexOf("value", StringComparison.Ordinal), document.Offsets[document.Text.IndexOf("value", StringComparison.Ordinal)]);
        var mapped = new TypeScriptSourceDocument("operations.ts", source, document.Text, document.Offsets)
            .Map(new GraphQlSourceSpan(document.Text.IndexOf("value", StringComparison.Ordinal), document.Text.Length, 2, 2, true));
        Assert.Equal("operations.ts", mapped.SourceName);
        Assert.Equal(2, mapped.Line);
        Assert.Equal(source.IndexOf("value", StringComparison.Ordinal) - 1, mapped.Column);
    }

    [Fact]
    public void Generation_ignores_await_regex_and_accepts_single_argument_trailing_comma()
    {
        var root = Path.Combine(Path.GetTempPath(), "salep-extraction-five-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(root);
        try
        {
            File.WriteAllText(Path.Combine(root, "schema.graphql"), "type Query { value: String }");
            File.WriteAllText(Path.Combine(root, "operations.ts"), "async function load(){ const match = await /gql`query Fake { bad }`/.exec(text); return graphql(`query Real {\\n value\\n}`,); }");
            var config = Path.Combine(root, "salep.json");
            File.WriteAllText(config, """{"version":1,"target":"typescript","schema":"schema.graphql","documents":"operations.ts","output":"Generated"}""");
            TypeScriptGenerator.Generate(new(config));
            var generated = File.ReadAllText(Path.Combine(root, "Generated", "gql", "graphql.ts"));
            Assert.Contains("RealDocument", generated);
            Assert.DoesNotContain("FakeDocument", generated);
        }
        finally { Directory.Delete(root, true); }
    }
}
