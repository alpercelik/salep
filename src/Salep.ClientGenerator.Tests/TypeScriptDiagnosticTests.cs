using Salep.ClientGenerator.TypeScript.Generation;
using Xunit;

namespace Salep.ClientGenerator.Tests;

public sealed class TypeScriptDiagnosticTests
{
    [Theory]
    [InlineData("\nconst q = request`query Read {\n missing\n}`;", "operations.tsx(3,2)")]
    [InlineData("\r\nconst q = request`query Read {\\n missing\\n}`;", "operations.tsx(2,34)")]
    [InlineData("const q = request`query Read {`;", "operations.tsx(1,31)")]
    public void Embedded_document_failures_point_to_original_file_lines_and_columns(string text, string expected)
    {
        var root = Path.Combine(Path.GetTempPath(), "salep-diagnostics-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(root);
        try
        {
            File.WriteAllText(Path.Combine(root, "schema.graphql"), "type Query { title: String }");
            File.WriteAllText(Path.Combine(root, "operations.tsx"), text);
            var config = Path.Combine(root, "salep.json");
            File.WriteAllText(config, """{"version":1,"target":"typescript","schema":"schema.graphql","documents":"operations.tsx","documentTags":["request"],"output":"Generated"}""");
            var error = Assert.Throws<InvalidDataException>(() => TypeScriptGenerator.Generate(new(config)));
            Assert.Contains(expected, error.Message);
            Assert.False(Directory.Exists(Path.Combine(root, "Generated")));
        }
        finally { Directory.Delete(root, true); }
    }
}
