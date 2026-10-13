using Salep.ClientGenerator.Model;
using Salep.GraphQLParser;
using Xunit;

namespace Salep.ClientGenerator.Tests;

public sealed class GraphQlValidationIterationTwoTests
{
    [Theory]
    [InlineData("query Q { value } fragment F on Query { value } fragment F on Query { value }", "Duplicate fragment 'F'")]
    [InlineData("query Q { value } query Q { value }", "Duplicate operation 'Q'")]
    [InlineData("{ value } query Q { value }", "anonymous operation")]
    public void Executable_uniqueness_errors_use_mapped_source(string query, string expected)
    {
        var error = Assert.Throws<InvalidDataException>(() => Validate("type Query { value: String }", query));
        Assert.Contains("operations/source.graphql(", error.Message);
        Assert.Contains(expected, error.Message);
    }

    [Theory]
    [InlineData("type Root { value: String }", "query object root")]
    [InlineData("directive @d on FIELD directive @d on FIELD type Query { value: String }", "Duplicate directive definition")]
    [InlineData("schema { query: Query mutation: Query } type Query { value: String }", "Duplicate root object type")]
    [InlineData("schema { query: Query } extend schema { query: Query } type Query { value: String }", "Duplicate schema root")]
    [InlineData("schema { query: Query } schema { mutation: Mutation } type Query { value: String } type Mutation { value: String }", "Multiple schema definitions")]
    public void Global_schema_errors_have_real_mapped_locations(string schema, string expected)
    {
        var error = Assert.Throws<InvalidDataException>(() => Validate(schema, "query Q { value }"));
        Assert.Contains("schemas/source.graphql(", error.Message);
        Assert.DoesNotContain("(0,0)", error.Message);
        Assert.Contains(expected, error.Message);
    }

    [Theory]
    [InlineData("subscription S { update @skip(if: false) { value } }")]
    [InlineData("subscription S($show: Boolean!) { update @include(if: $show) { value } }")]
    [InlineData("subscription S { ...Root @skip(if: false) } fragment Root on Subscription { update { value } }")]
    [InlineData("subscription S { ... on Subscription @include(if: true) { update { value } } }")]
    [InlineData("subscription S { ...Root } fragment Root on Subscription { update @skip(if: false) { value } }")]
    public void Subscription_root_conditions_are_rejected_through_fragments(string query)
    {
        var error = Assert.Throws<InvalidDataException>(() => Validate(SubscriptionSchema, query));
        Assert.Contains("Subscription root selections cannot use", error.Message);
        Assert.Contains("operations/source.graphql(", error.Message);
    }

    [Theory]
    [InlineData("subscription S { ...Root } fragment Root on Subscription { update { value @skip(if: false) } }")]
    [InlineData("subscription S($show: Boolean!) { update { value @include(if: $show) } }")]
    [InlineData("subscription S { update { value } update { other: value } }")]
    public void Subscription_child_conditions_and_merged_root_fields_are_allowed(string query)
        => Validate(SubscriptionSchema, query);

    [Fact]
    public void Distinct_operations_can_share_fragments_and_validator_can_be_reused()
    {
        var validator = CreateValidator("type Query { value: String }", "query A { ...F } query B { ...F } fragment F on Query { value }");
        validator.Validate();
        validator.Validate();
    }

    private const string SubscriptionSchema = "type Query { value: String } type Result { value: String } type Subscription { update: Result }";

    private static void Validate(string schema, string query) => CreateValidator(schema, query).Validate();
    private static GraphQlDocumentValidator CreateValidator(string schemaSource, string query)
    {
        var schema = GraphQlModelFactory.CreateSchema(global::Salep.GraphQLParser.GraphQLParser.Parse(new SourceText(schemaSource.AsMemory())), source => source with { SourceName = "schemas/source.graphql" });
        var document = GraphQlModelFactory.CreateExecutable([global::Salep.GraphQLParser.GraphQLParser.Parse(new SourceText(query.AsMemory()))], source => source with { SourceName = "operations/source.graphql" });
        return new(schema, document);
    }
}
