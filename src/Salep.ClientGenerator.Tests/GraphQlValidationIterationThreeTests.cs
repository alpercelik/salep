using Salep.ClientGenerator.Model;
using Salep.GraphQLParser;
using Xunit;

namespace Salep.ClientGenerator.Tests;

public sealed class GraphQlValidationIterationThreeTests
{
    [Theory]
    [InlineData("{ x: 1, x: 2 }")]
    [InlineData("{ outer: { x: 1, x: 2 } }")]
    [InlineData("[{ x: 1, x: 2 }]")]
    public void Custom_scalar_composites_reject_duplicate_object_fields(string value)
    {
        var error = Assert.Throws<InvalidDataException>(() => Validate("scalar JSON type Query { echo(value: JSON): String }", "query Q { echo(value: " + value + ") }"));
        Assert.Contains("Duplicate input object field 'x'", error.Message);
        Assert.Contains("operations.graphql(", error.Message);
    }

    [Fact]
    public void Custom_scalar_defaults_also_reject_duplicate_fields()
        => Assert.Contains("Duplicate input object field", Assert.Throws<InvalidDataException>(() => Validate("scalar JSON type Query { echo(value: JSON = { x: 1, x: 2 }): String }")).Message);

    [Theory]
    [InlineData("directive @d(arg: String @d) on ARGUMENT_DEFINITION")]
    [InlineData("directive @d(arg: String @other) on ARGUMENT_DEFINITION directive @other(arg: String @d) on ARGUMENT_DEFINITION")]
    [InlineData("directive @d(arg: Options) on INPUT_OBJECT input Options @d { value: String }")]
    [InlineData("directive @d(arg: Options) on INPUT_FIELD_DEFINITION input Options { value: String @d }")]
    [InlineData("directive @d(arg: Options) on INPUT_FIELD_DEFINITION input Options { nested: Other } input Other { nested: Options value: String @d }")]
    [InlineData("directive @d(arg: Choice) on ENUM_VALUE enum Choice { VALUE @d }")]
    [InlineData("directive @d(arg: Options) on INPUT_OBJECT input Options { value: String } extend input Options @d { other: String }")]
    public void Directive_reference_cycles_are_rejected_through_directives_and_types(string definitions)
    {
        var error = Assert.Throws<InvalidDataException>(() => Validate(definitions + " type Query { value: String }"));
        Assert.Contains("Directive reference cycle", error.Message);
        Assert.Contains("schema.graphql(", error.Message);
    }

    [Theory]
    [InlineData("directive @d(arg: Options) on FIELD input Options { nested: Options }")]
    [InlineData("directive @d(arg: Options) on FIELD input Options { nested: Other } input Other { nested: Options }")]
    [InlineData("directive @d(arg: String @other) on FIELD directive @other on ARGUMENT_DEFINITION")]
    [InlineData("directive @d(left: Options, right: Options) on FIELD input Options { value: String }")]
    public void Recursive_types_and_acyclic_directive_dependencies_are_allowed(string definitions)
        => Validate(definitions + " type Query { value: String }");

    [Theory]
    [InlineData("type Query { value: String @deprecated(reason: null) }")]
    [InlineData("enum Choice { VALUE @deprecated(reason: null) } type Query { value: Choice }")]
    [InlineData("input Options { value: String @deprecated(reason: null) } type Query { value: String }")]
    public void Builtin_deprecation_reasons_reject_explicit_null(string schema)
        => Assert.Contains("Null is not allowed", Assert.Throws<InvalidDataException>(() => Validate(schema)).Message);

    [Theory]
    [InlineData("type Query { value: String @deprecated }")]
    [InlineData("type Query { value: String @deprecated(reason: \"\") }")]
    [InlineData("type Query { value: String @deprecated(reason: \"Use other\") }")]
    public void Omitted_and_string_deprecation_reasons_are_allowed(string schema) => Validate(schema);

    [Theory]
    [InlineData("Int")]
    [InlineData("Float")]
    [InlineData("String")]
    [InlineData("Boolean")]
    [InlineData("ID")]
    public void Builtin_scalars_cannot_use_specification_urls(string scalar)
        => Assert.Contains("@specifiedBy cannot appear", Assert.Throws<InvalidDataException>(() => Validate("scalar " + scalar + " @specifiedBy(url: \"https://example.test\") type Query { value: " + scalar + " }")).Message);

    [Fact]
    public void Custom_scalar_can_use_specification_url()
        => Validate("scalar Custom @specifiedBy(url: \"https://example.test\") type Query { value: Custom }");

    private static void Validate(string schemaSource, string operation = "query Q { value }")
    {
        var schema = GraphQlModelFactory.CreateSchema(global::Salep.GraphQLParser.GraphQLParser.Parse(new SourceText(schemaSource.AsMemory())), source => source with { SourceName = "schema.graphql" });
        var document = GraphQlModelFactory.CreateExecutable([global::Salep.GraphQLParser.GraphQLParser.Parse(new SourceText(operation.AsMemory()))], source => source with { SourceName = "operations.graphql" });
        new GraphQlDocumentValidator(schema, document).Validate();
    }
}
