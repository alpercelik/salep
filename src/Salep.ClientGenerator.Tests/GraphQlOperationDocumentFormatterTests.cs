using Salep.ClientGenerator.Model;
using Salep.GraphQLParser;
using Xunit;

namespace Salep.ClientGenerator.Tests;

public sealed class GraphQlOperationDocumentFormatterTests
{
    [Fact]
    public void Format_IncludesReachableFragmentsAndAddsTypenameForAbstractTypes()
    {
        var schema = GraphQlModelFactory.CreateSchema(Parse("interface Node { id: ID! } type Product implements Node { id: ID! } type Query { node: Node }") );
        var executable = GraphQlModelFactory.CreateExecutable([
            Parse("query Read($unused: Int) { node { ...NodeFields } } fragment NodeFields on Node { id } fragment Unused on Node { id }")
        ]);

        var result = new GraphQlOperationDocumentFormatter(schema, executable).Format(executable.Operations.Single());

        Assert.NotNull(Parse(result));
        Assert.Contains("node {", result, StringComparison.Ordinal);
        Assert.Contains("__typename", result, StringComparison.Ordinal);
        Assert.Contains("fragment NodeFields", result, StringComparison.Ordinal);
        Assert.DoesNotContain("fragment Unused", result, StringComparison.Ordinal);
        Assert.Contains("fragment NodeFields on Node {" + Environment.NewLine + "  __typename", result, StringComparison.Ordinal);
    }

    [Fact]
    public void Format_AppliesVariablePoliciesWithoutInliningDefaultsThroughFragments()
    {
        var schema = GraphQlModelFactory.CreateSchema(Parse("type Query { product(id: ID, active: Boolean): String }") );
        var executable = GraphQlModelFactory.CreateExecutable([
            Parse("query Read($id: ID = \"p1\", $unused: Int, $active: Boolean = true) { product(id: $id, active: $active) }")
        ]);
        var operation = executable.Operations.Single();

        var result = new GraphQlOperationDocumentFormatter(schema, executable).Format(operation,
            new GraphQlOperationDocumentOptions { OmitUnusedVariables = true, InlineDefaultVariables = true, AddTypename = false });

        Assert.DoesNotContain("$id", result, StringComparison.Ordinal);
        Assert.DoesNotContain("$active", result, StringComparison.Ordinal);
        Assert.DoesNotContain("$unused", result, StringComparison.Ordinal);
        Assert.Contains("product(id: \"p1\", active: true)", result, StringComparison.Ordinal);
    }

    [Fact]
    public void Format_KeepsDefaultVariablesWhenFragmentSpreadIsPresent()
    {
        var schema = GraphQlModelFactory.CreateSchema(Parse("type Query { product(id: ID): String }") );
        var executable = GraphQlModelFactory.CreateExecutable([
            Parse("query Read($id: ID = \"p1\") { product(id: $id) ...Fields } fragment Fields on Query { product(id: $id) }")
        ]);

        var result = new GraphQlOperationDocumentFormatter(schema, executable).Format(executable.Operations.Single(),
            new GraphQlOperationDocumentOptions { InlineDefaultVariables = true, AddTypename = false });

        Assert.Contains("$id: ID = \"p1\"", result, StringComparison.Ordinal);
        Assert.Contains("product(id: $id)", result, StringComparison.Ordinal);
    }

    private static DocumentNode Parse(string source) => global::Salep.GraphQLParser.GraphQLParser.Parse(new SourceText(source.AsMemory()));
}
