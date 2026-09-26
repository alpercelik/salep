using System.Text.Json;
using Xunit;

namespace Salep.GraphQLParser.Tests;

public sealed class PublicApiContractInventoryTests
{
    private static readonly string ContractPath = Path.Combine(AppContext.BaseDirectory, "Fixtures", "Contracts", "public-api-contract.json");
    private static readonly string[] collection0 = new[] { "constructor", "method", "staticMethod", "property", "field", "constant" };
    private static readonly string[] collection = new[] { "class", "abstractClass", "interface" };

    [Fact]
    public void ContractInventoryHasNormalizedIdentityAndSupportedTargetMetadata()
    {
        using var document = JsonDocument.Parse(File.ReadAllText(ContractPath));
        var root = document.RootElement;

        Assert.Equal(1, root.GetProperty("contractVersion").GetInt32());
        Assert.Equal("Salep.GraphQLParser", root.GetProperty("projectIdentity").GetProperty("namespaceRoot").GetString());
        Assert.Equal("Salep.GraphQLParser", root.GetProperty("projectIdentity").GetProperty("assembly").GetString());
        Assert.Equal(["net10.0", "net11.0"], root.GetProperty("projectIdentity").GetProperty("frameworks").EnumerateArray().Select(item => item.GetString()));
        Assert.Equal(["net10.0", "net11.0", "net8.0", "net9.0", "netstandard2.0"],
            root.GetProperty("sourceMetadata").GetProperty("frameworks").EnumerateArray().Select(item => item.GetString()).Order(StringComparer.Ordinal));
    }

    [Fact]
    public void ContractInventoryContainsUniqueProjectOwnedTypesAndWellFormedSignatures()
    {
        using var document = JsonDocument.Parse(File.ReadAllText(ContractPath));
        var types = document.RootElement.GetProperty("types").EnumerateArray().ToArray();
        var names = types.Select(item => item.GetProperty("name").GetString()!).ToArray();

        Assert.Equal(119, types.Length);
        Assert.Equal(names.Length, names.Distinct(StringComparer.Ordinal).Count());
        Assert.All(names, name => Assert.StartsWith("GraphQLParser.", name, StringComparison.Ordinal));

        foreach (var type in types)
        {
            Assert.Contains(type.GetProperty("kind").GetString(), collection);
            foreach (var member in type.GetProperty("members").EnumerateArray())
            {
                var kind = member.GetProperty("kind").GetString();
                Assert.Contains(kind, collection0);
                if (kind is "method" or "staticMethod")
                {
                    Assert.False(string.IsNullOrWhiteSpace(member.GetProperty("name").GetString()));
                    Assert.False(string.IsNullOrWhiteSpace(member.GetProperty("returnType").GetString()));
                }
                foreach (var parameter in member.TryGetProperty("parameters", out var parameters) ? parameters.EnumerateArray() : [])
                {
                    Assert.False(string.IsNullOrWhiteSpace(parameter.GetProperty("name").GetString()));
                    Assert.False(string.IsNullOrWhiteSpace(parameter.GetProperty("type").GetString()));
                }
            }
        }
    }

    [Fact]
    public void ContractIncludesParserSyntaxPrintingAndRewriteFamilies()
    {
        using var document = JsonDocument.Parse(File.ReadAllText(ContractPath));
        var names = document.RootElement.GetProperty("types").EnumerateArray()
            .Select(item => item.GetProperty("name").GetString()!)
            .ToArray();

        Assert.Contains("GraphQLParser.Utf8GraphQLParser", names);
        Assert.DoesNotContain("GraphQLParser.Utf8GraphQLOperationParser", names);
        Assert.Contains("GraphQLParser.DocumentNode", names);
        Assert.Contains("GraphQLParser.SyntaxKind", names);
        Assert.Contains("GraphQLParser.Utilities.SyntaxPrinter", names);
        Assert.Contains("GraphQLParser.Visitors.SyntaxRewriter", names);
        Assert.Contains("GraphQLParser.Visitors.ISyntaxRewriter`1", names);
    }
}
