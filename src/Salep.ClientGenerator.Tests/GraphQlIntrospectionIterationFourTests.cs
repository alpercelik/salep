using Salep.ClientGenerator.Model;
using Salep.GraphQLParser;
using Xunit;

namespace Salep.ClientGenerator.Tests;

public sealed class GraphQlIntrospectionIterationFourTests
{
    [Theory]
    [InlineData("__Type", "fields")]
    [InlineData("__Type", "enumValues")]
    [InlineData("__Type", "inputFields")]
    [InlineData("__Field", "args")]
    [InlineData("__Directive", "args")]
    public void Deprecated_inclusion_argument_is_nonnull_with_false_default(string parent, string fieldName)
    {
        var schema = Schema();
        var field = new GraphQlSchemaSemantics(schema).Fields(parent).Single(field => field.Name == fieldName);
        var argument = Assert.Single(field.Arguments);
        Assert.IsType<GraphQlNonNullTypeReference>(argument.Type);
        Assert.Equal("Boolean", GraphQlSchemaSemantics.NamedType(argument.Type));
        Assert.Equal(GraphQlValueKind.BooleanLiteral, argument.DefaultValue!.Kind);
        Assert.False(argument.DefaultValue.Boolean);
    }

    [Theory]
    [InlineData("__type(name: \"Query\") { fields(includeDeprecated: null) { name } }")]
    [InlineData("__type(name: \"Query\") { enumValues(includeDeprecated: null) { name } }")]
    [InlineData("__type(name: \"Query\") { inputFields(includeDeprecated: null) { name } }")]
    [InlineData("__type(name: \"Query\") { fields { args(includeDeprecated: null) { name } } }")]
    [InlineData("__schema { directives { args(includeDeprecated: null) { name } } }")]
    public void Explicit_null_deprecation_flag_is_rejected(string selection)
    {
        var error = Assert.Throws<InvalidDataException>(() => Validate("query Q { " + selection + " }"));
        Assert.Contains("Null is not allowed", error.Message);
        Assert.Contains("introspection.graphql(", error.Message);
    }

    [Theory]
    [InlineData("__type(name: \"Query\") { fields { name } }")]
    [InlineData("__type(name: \"Query\") { enumValues { name } }")]
    [InlineData("__type(name: \"Query\") { inputFields { name } }")]
    [InlineData("__type(name: \"Query\") { fields { args { name } } }")]
    [InlineData("__schema { directives { args { name } } }")]
    public void Omitted_deprecation_flag_uses_default(string selection) => Validate("query Q { " + selection + " }");

    [Theory]
    [InlineData("__type(name: \"Query\") { fields(includeDeprecated: $show) { name } }")]
    [InlineData("__type(name: \"Query\") { enumValues(includeDeprecated: $show) { name } }")]
    [InlineData("__type(name: \"Query\") { inputFields(includeDeprecated: $show) { name } }")]
    [InlineData("__type(name: \"Query\") { fields { args(includeDeprecated: $show) { name } } }")]
    [InlineData("__schema { directives { args(includeDeprecated: $show) { name } } }")]
    public void Nullable_variable_is_compatible_because_argument_has_default(string selection)
        => Validate("query Q($show: Boolean) { " + selection + " }");

    [Theory]
    [InlineData("true")]
    [InlineData("false")]
    public void Explicit_boolean_flags_remain_allowed(string value)
        => Validate("query Q { __type(name: \"Query\") { fields(includeDeprecated: " + value + ") { name } } }");

    private static GraphQlSchemaModel Schema() => GraphQlModelFactory.CreateSchema(global::Salep.GraphQLParser.GraphQLParser.Parse(new SourceText("type Query { value: String }".AsMemory())));
    private static void Validate(string operation)
    {
        var document = GraphQlModelFactory.CreateExecutable([global::Salep.GraphQLParser.GraphQLParser.Parse(new SourceText(operation.AsMemory()))], source => source with { SourceName = "introspection.graphql" });
        new GraphQlDocumentValidator(Schema(), document).Validate();
    }
}
