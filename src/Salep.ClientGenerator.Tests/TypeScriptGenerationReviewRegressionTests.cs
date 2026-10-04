using Salep.ClientGenerator.Model;
using Salep.ClientGenerator.TypeScript.Generation;
using Salep.ClientGenerator.TypeScript.Targets;
using Salep.GraphQLParser;
using Xunit;

namespace Salep.ClientGenerator.Tests;

public sealed class TypeScriptGenerationReviewRegressionTests
{
    [Fact]
    public void Jsx_text_attributes_fragments_and_nested_elements_do_not_become_documents()
    {
        const string source = """"
            const view = <>
              <div title="Don't parse gql`query Attribute { bad }`">
                Don't parse graphql(`query Text { bad }`) here.
                <span>gql`query NestedText { bad }`</span>
                {graphql(`query Expression { value }`)}
                <Component title="a > b" visible={a > b} document={gql`query AttributeExpression { value }`} />
                {true && <section>{gql`query NestedExpression { value }`}</section>}
                {{ nested: { document: gql`query ObjectExpression { value }` } }.nested.document}
              </div>
            </>;
            const document = gql`query Outside { value }`;
            """";
        Assert.Equal(["query Expression { value }", "query AttributeExpression { value }", "query NestedExpression { value }", "query ObjectExpression { value }", "query Outside { value }"], TypeScriptDocumentExtractor.Extract(source));
        var located = TypeScriptDocumentExtractor.ExtractWithLocations(source);
        Assert.All(located, document => Assert.Equal(source.IndexOf(document.Text, StringComparison.Ordinal), document.Offsets[0]));
    }

    [Fact]
    public void Type_parameters_assertions_and_comparisons_preserve_javascript_documents()
    {
        const string source = """"
            const identity = <T,>(value: T) => value;
            function read<T extends object>(value: T) { return gql`query Generic { value }`; }
            const asserted = <Record<string, unknown>>{};
            const compared = a < b && c > d;
            const document = graphql(`query Actual { value }`);
            """";
        Assert.Equal(["query Generic { value }", "query Actual { value }"], TypeScriptDocumentExtractor.Extract(source));
    }

    [Fact]
    public void Typescript_angle_assertions_ignore_later_closing_tag_strings_and_comments()
    {
        const string source = """"
            const result = <T>gql`query Read { value }`;
            const marker = "</T>";
            const other = <U>graphql(`query Other { value }`);
            // </U>
            """";
        Assert.Equal(["query Read { value }", "query Other { value }"], TypeScriptDocumentExtractor.Extract(source, jsx: false));
        var located = TypeScriptDocumentExtractor.ExtractWithLocations(source, jsx: false);
        Assert.All(located, document => Assert.Equal(source.IndexOf(document.Text, StringComparison.Ordinal), document.Offsets[0]));
    }

    [Fact]
    public void Generator_selects_jsx_lexing_only_for_tsx_files()
    {
        var root = Path.Combine(Path.GetTempPath(), "salep-ts-lexing-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(root);
        try
        {
            File.WriteAllText(Path.Combine(root, "schema.graphql"), "type Query { value: String }");
            File.WriteAllText(Path.Combine(root, "assertion.ts"), """const result = <T>gql`query Read { value }`; const marker = "</T>";""");
            File.WriteAllText(Path.Combine(root, "view.tsx"), """const view = <div>Don't parse gql`query Fake { bad }` {gql`query Inside { value }`}</div>;""");
            var config = Path.Combine(root, "salep.json");
            File.WriteAllText(config, """{"version":1,"target":"typescript","schema":"schema.graphql","documents":"*.{ts,tsx}"}""");
            TypeScriptGenerator.Generate(new(config));
            var generated = File.ReadAllText(Path.Combine(root, "Generated", "gql", "graphql.ts"));
            Assert.Contains("ReadDocument", generated);
            Assert.Contains("InsideDocument", generated);
            Assert.DoesNotContain("FakeDocument", generated);
        }
        finally { Directory.Delete(root, true); }
    }

    [Theory]
    [InlineData("extends")]
    [InlineData("new")]
    [InlineData("delete")]
    [InlineData("void")]
    [InlineData("false")]
    [InlineData("implements")]
    [InlineData("symbol")]
    [InlineData("bigint")]
    public void Graphql_type_names_that_are_typescript_keywords_are_escaped(string name)
    {
        var schema = GraphQlModelFactory.CreateSchema(Parse("enum " + name + " { VALUE } type Query { value: " + name + " }"));
        var target = new TypeScriptCodeGenerationTarget();
        target.PrepareNames(schema, new([], []));
        Assert.Equal("Gql" + name, target.TypeName(name));
    }

    [Fact]
    public void Resolver_and_imported_runtime_names_are_reserved_without_colliding_with_preescaped_names()
    {
        string[] names = ["ResolverParentTypes", "LocalResolvers", "Promise", "Readonly", "LocalSelection", "LocalValue", "LocalType", "LocalInputDefinitions", "GqlPromise", "Gqlextends", "extends"];
        var schema = GraphQlModelFactory.CreateSchema(Parse(string.Join("\n", names.Select(name => "enum " + name + " { VALUE }")) + "\ntype Query { value: Promise }"));
        var target = new TypeScriptCodeGenerationTarget();
        target.PrepareNames(schema, new([], []));
        Assert.Equal(names.Length, names.Select(target.TypeName).Distinct(StringComparer.Ordinal).Count());
        foreach (var name in names.Take(8)) Assert.NotEqual(name, target.TypeName(name));
    }

    [Fact]
    public void Client_schema_generation_keeps_schema_aliases_distinct_from_resolver_helpers()
    {
        var root = Path.Combine(Path.GetTempPath(), "salep-ts-names-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(root);
        try
        {
            File.WriteAllText(Path.Combine(root, "schema.graphql"), "enum extends { VALUE } enum Promise { VALUE } input ResolverParentTypes { value: String } input LocalResolvers { value: String } enum LocalSelection { VALUE } type Query { value(parent: ResolverParentTypes, resolver: LocalResolvers): extends }");
            File.WriteAllText(Path.Combine(root, "client.graphql"), "extend type Query { local: Promise }");
            File.WriteAllText(Path.Combine(root, "operations.graphql"), "query Read($parent: ResolverParentTypes, $resolver: LocalResolvers) { value(parent: $parent, resolver: $resolver) local @client }");
            var config = Path.Combine(root, "salep.json");
            File.WriteAllText(config, """{"version":1,"target":"typescript","schema":"schema.graphql","clientSchema":"client.graphql","documents":"operations.graphql"}""");
            TypeScriptGenerator.Generate(new(config));
            var generated = File.ReadAllText(Path.Combine(root, "Generated", "gql", "resolvers.ts"));
            Assert.Contains("export type Gqlextends", generated);
            Assert.Contains("export type GqlPromise", generated);
            Assert.Contains("export type GqlResolverParentTypes", generated);
            Assert.Contains("export type GqlLocalResolvers", generated);
            Assert.Contains("export type GqlLocalSelection", generated);
            Assert.Contains("export type ResolverParentTypes =", generated);
            Assert.Contains("export type LocalResolvers<TContext", generated);
            Assert.Contains("Promise<GqlPromise | null>", generated);
        }
        finally { Directory.Delete(root, true); }
    }

    private static DocumentNode Parse(string source) => global::Salep.GraphQLParser.GraphQLParser.Parse(new SourceText(source.AsMemory()));
}
