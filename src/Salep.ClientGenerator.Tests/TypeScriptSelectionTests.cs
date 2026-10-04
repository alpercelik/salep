using Salep.ClientGenerator.Model;
using Salep.ClientGenerator.TypeScript.Targets;
using Salep.ClientGenerator.TypeScript.Templates;
using Salep.GraphQLParser;
using Xunit;

namespace Salep.ClientGenerator.Tests;

/// <summary>Covers TypeScript type wrappers, scalar mappings and selection merging.</summary>
public sealed class TypeScriptSelectionTests
{
    [Theory]
    [InlineData("String", "string | null")]
    [InlineData("String!", "string")]
    [InlineData("[String]", "ReadonlyArray<string | null> | null")]
    [InlineData("[String]!", "ReadonlyArray<string | null>")]
    [InlineData("[String!]", "ReadonlyArray<string> | null")]
    [InlineData("[String!]!", "ReadonlyArray<string>")]
    [InlineData("[[String!]!]!", "ReadonlyArray<ReadonlyArray<string>>")]
    [InlineData("[[String]]", "ReadonlyArray<ReadonlyArray<string | null> | null> | null")]
    [InlineData("[[Genre!]!]!", "ReadonlyArray<ReadonlyArray<Genre>>")]
    public void List_and_element_nullability_are_independent(string graphQlType, string expected)
    {
        var schema = Schema("enum Genre { NOVEL HISTORY } input Options { value: " + graphQlType + " } type Query { value: String }");
        var field = Assert.Single(Assert.Single(schema.Types.OfType<GraphQlInputObjectType>()).Fields);
        Assert.Equal(expected, new TypeScriptCodeGenerationTarget().Render(field.Type, schema, true));
    }

    [Fact]
    public void Repeated_parent_selections_merge_children_and_preserve_aliases()
    {
        var source = Render("type Query { book: Book! } type Book { id: ID! title: String! }",
            "query Books { book { key: id } book { title } }");
        Assert.Contains("\"key\": string", source);
        Assert.Contains("\"title\": string", source);
        Assert.DoesNotContain("\"id\":", source);
    }

    [Fact]
    public void Transitive_fragments_and_inline_conditions_select_concrete_union_fields()
    {
        var source = Render("type Query { hit: Hit! } union Hit = Book | Writer type Book { id: ID! title: String! } type Writer { id: ID! name: String! }",
            "query Search { hit { ...BookFields ... on Writer { name } } } fragment BookFields on Book { ...BookKey title } fragment BookKey on Book { key: id }");
        Assert.Contains("\"__typename\": \"Book\"", source);
        Assert.Contains("\"__typename\": \"Writer\"", source);
        Assert.Contains("\"key\": string", source);
        Assert.Contains("\"title\": string", source);
        Assert.Contains("\"name\": string", source);
    }

    [Theory]
    [InlineData("query Book($show: Boolean!) { book { title @include(if: $show) } }", true)]
    [InlineData("query Book($show: Boolean!) { book { ... on Book @include(if: $show) { title } } }", true)]
    [InlineData("query Book($show: Boolean!) { book { ...Title @include(if: $show) } } fragment Title on Book { title }", true)]
    [InlineData("query Book($show: Boolean!) { book { title title @include(if: $show) } }", false)]
    public void Variable_conditions_propagate_and_unconditional_duplicates_remain_required(string query, bool optional)
    {
        var source = Render("type Query { book: Book! } type Book { title: String! }", query);
        Assert.Contains(optional ? "\"title\"?: string" : "\"title\": string", source);
    }

    [Fact]
    public void Scalar_mappings_distinguish_input_from_output_and_unknown_scalars_stay_unknown()
    {
        var schema = Schema("scalar Timestamp scalar Extra input Options { value: Timestamp! extra: Extra! } type Query { value: Timestamp! }");
        var target = new TypeScriptCodeGenerationTarget(new Dictionary<string, TypeScriptScalar> { ["Timestamp"] = new("string", "number") });
        var fields = Assert.Single(schema.Types.OfType<GraphQlInputObjectType>()).Fields;
        Assert.Equal("string", target.Render(fields[0].Type, schema, true));
        Assert.Equal("number", target.Render(fields[0].Type, schema, false));
        Assert.Equal("unknown", target.Render(fields[1].Type, schema, true));
    }

    private static GraphQlSchemaModel Schema(string source)
        => GraphQlModelFactory.CreateSchema(global::Salep.GraphQLParser.GraphQLParser.Parse(new SourceText(source.AsMemory())));

    private static string Render(string schemaSource, string operationSource)
    {
        var schema = Schema(schemaSource);
        var document = GraphQlModelFactory.CreateExecutable(schema, [global::Salep.GraphQLParser.GraphQLParser.Parse(new SourceText(operationSource.AsMemory()))]);
        return new TypeScriptSourceRenderer(schema, document, new TypeScriptCodeGenerationTarget(), new TypeScriptTemplateCatalog(), fragmentMasking: false).Operations();
    }
}
