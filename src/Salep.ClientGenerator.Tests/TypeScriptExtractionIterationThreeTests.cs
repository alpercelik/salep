using System.Text.Json;
using Salep.ClientGenerator.TypeScript.Generation;
using Xunit;

namespace Salep.ClientGenerator.Tests;

public sealed class TypeScriptExtractionIterationThreeTests
{
    [Theory]
    [InlineData("\r\n")]
    [InlineData("\r")]
    [InlineData("\n")]
    public void Raw_template_line_endings_match_javascript_cooked_text_and_source_offsets(string newline)
    {
        var source = "const document=graphql(`query Read {" + newline + " title" + newline + "}`);";
        var extracted = Assert.Single(TypeScriptDocumentExtractor.ExtractWithLocations(source));
        Assert.Equal("query Read {\n title\n}", extracted.Text);
        Assert.Equal(source.IndexOf("title", StringComparison.Ordinal), extracted.Offsets[extracted.Text.IndexOf("title", StringComparison.Ordinal)]);
        Assert.Equal(source.IndexOf('}', StringComparison.Ordinal), extracted.Offsets[extracted.Text.IndexOf('}', StringComparison.Ordinal)]);
    }

    [Theory]
    [InlineData("\\r", "\r")]
    [InlineData("\\n", "\n")]
    [InlineData("\\\r\n", "")]
    [InlineData("\\\r", "")]
    [InlineData("\\\n", "")]
    public void Explicit_escapes_and_continuations_preserve_javascript_cooking(string encoded, string cooked)
        => Assert.Equal("query Read {" + cooked + " title }", Assert.Single(TypeScriptDocumentExtractor.Extract("graphql(`query Read {" + encoded + " title }`)")));

    [Theory]
    [InlineData("\r\n")]
    [InlineData("\r")]
    public void Generated_lookup_keys_use_cooked_values(string newline)
    {
        WithInputs("const document=graphql(`query Read {" + newline + " title" + newline + "}`);", config =>
        {
            TypeScriptGenerator.Generate(new(config));
            var lookup = File.ReadAllText(Path.Combine(Path.GetDirectoryName(config)!, "Generated", "gql", "gql.ts"));
            Assert.Contains(JsonSerializer.Serialize("query Read {\n title\n}"), lookup);
            Assert.DoesNotContain(JsonSerializer.Serialize("query Read {" + newline + " title" + newline + "}"), lookup);
        });
    }

    [Theory]
    [InlineData("\r\n")]
    [InlineData("\r")]
    public void Diagnostics_after_normalized_newlines_keep_original_coordinates(string newline)
    {
        WithInputs("const document=graphql(`query Read {" + newline + " title" + newline + " missing" + newline + "}`);", config =>
        {
            var error = Assert.Throws<InvalidDataException>(() => TypeScriptGenerator.Generate(new(config)));
            Assert.Contains("operations.ts(3,2)", error.Message);
            Assert.False(Directory.Exists(Path.Combine(Path.GetDirectoryName(config)!, "Generated")));
        });
    }

    private static void WithInputs(string source, Action<string> action)
    {
        var root = Path.Combine(Path.GetTempPath(), "salep-ts-cooked-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(root);
        try
        {
            File.WriteAllText(Path.Combine(root, "schema.graphql"), "type Query { title: String }");
            File.WriteAllText(Path.Combine(root, "operations.ts"), source);
            var config = Path.Combine(root, "salep.json");
            File.WriteAllText(config, """{"version":1,"target":"typescript","schema":"schema.graphql","documents":"operations.ts","output":"Generated"}""");
            action(config);
        }
        finally { Directory.Delete(root, true); }
    }
}
