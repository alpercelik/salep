using Salep.ClientGenerator.Model;
using Salep.GraphQLParser;
using Xunit;

namespace Salep.ClientGenerator.Tests;

public sealed class GraphQlValidationIterationOneTests
{
    private const string ScalarSchema = "scalar JSON type Query { echo(value: JSON): String int: Int }";

    [Theory]
    [InlineData("{ x: $v }")]
    [InlineData("[$v]")]
    [InlineData("{ x: [{ inner: $v }] }")]
    public void Variables_in_custom_scalar_composites_are_used_without_field_type_inference(string value)
        => Validate(ScalarSchema, "query Q($v: Int) { echo(value: " + value + ") }");

    [Theory]
    [InlineData("{ x: $missing }")]
    [InlineData("[$missing]")]
    [InlineData("{ x: [{ inner: $missing }] }")]
    public void Undefined_variables_in_custom_scalar_composites_are_rejected(string value)
        => Assert.Contains("Undefined variable '$missing'", Assert.Throws<InvalidDataException>(() => Validate(ScalarSchema, "{ echo(value: " + value + ") }")).Message);

    [Fact]
    public void Top_level_scalar_variable_keeps_type_compatibility_check()
        => Assert.Contains("incompatible type", Assert.Throws<InvalidDataException>(() => Validate(ScalarSchema, "query Q($v: Int) { echo(value: $v) }")).Message);

    [Theory]
    [InlineData("scalar Mutation")]
    [InlineData("input Mutation { value: String }")]
    [InlineData("interface Subscription { value: String }")]
    [InlineData("enum Subscription { VALUE }")]
    public void Present_implicit_optional_roots_must_be_objects(string optionalRoot)
        => Assert.Contains("must be an object", Assert.Throws<InvalidDataException>(() => Validate(optionalRoot + " type Query { value: String }", "{ value }")).Message);

    [Fact]
    public void Explicit_schema_does_not_infer_omitted_optional_roots()
        => Validate("schema { query: Root } scalar Mutation type Root { value: String }", "{ value }");

    [Fact]
    public void Implicit_optional_object_roots_are_allowed()
        => Validate("type Mutation { set: String } type Subscription { update: String } type Query { value: String }", "{ value }");

    [Theory]
    [InlineData("scalar S @specifiedBy(url: \"https://a\") extend scalar S @specifiedBy(url: \"https://b\") type Query { value: S }")]
    [InlineData("directive @marker on OBJECT type Query @marker { value: String } extend type Query @marker { other: String }")]
    [InlineData("directive @marker on SCHEMA schema @marker { query: Query } extend schema @marker type Query { value: String }")]
    public void Nonrepeatable_directives_cannot_repeat_across_extensions(string schema)
        => Assert.Contains("not repeatable", Assert.Throws<InvalidDataException>(() => Validate(schema, "{ value }")).Message);

    [Fact]
    public void Repeatable_directives_can_repeat_across_extensions()
        => Validate("directive @marker repeatable on OBJECT type Query @marker { value: String } extend type Query @marker { other: String }", "{ value }");

    [Fact]
    public void Unused_variable_policy_is_explicit_and_other_validation_stays_enabled()
    {
        Assert.Contains("Unused variable", Assert.Throws<InvalidDataException>(() => Validate(ScalarSchema, "query Q($v: Int) { int }")).Message);
        Validate(ScalarSchema, "query Q($v: Int) { int }", allowUnusedVariables: true);
        Assert.Contains("Undefined variable", Assert.Throws<InvalidDataException>(() => Validate(ScalarSchema, "query Q($v: Int) { echo(value: { x: $missing }) }", allowUnusedVariables: true)).Message);
    }

    private static void Validate(string schemaSource, string query, bool allowUnusedVariables = false)
    {
        var schema = GraphQlModelFactory.CreateSchema(global::Salep.GraphQLParser.GraphQLParser.Parse(new SourceText(schemaSource.AsMemory())));
        var document = GraphQlModelFactory.CreateExecutable(schema, [global::Salep.GraphQLParser.GraphQLParser.Parse(new SourceText(query.AsMemory()))]);
        new GraphQlDocumentValidator(schema, document, allowUnusedVariables).Validate();
    }
}
