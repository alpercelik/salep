using System.Text.Json;
using Salep.ClientGenerator.Model;
using Salep.ClientGenerator.TypeScript.Generation;
using Salep.GraphQLParser;
using Xunit;

namespace Salep.ClientGenerator.Tests;

public sealed class TypeScriptInputReviewRegressionTests
{
    [Fact]
    public void Integer_literals_and_nested_defaults_preserve_exact_text()
    {
        var schema = Schema("input Options { id: ID = 9007199254740993 } type Query { echo(id: ID = 9007199254740993, options: Options): ID }");
        var document = Executable(schema, "query Read($id: ID = 9007199254740993) { echo(id: 9007199254740993) variable: echo(id: $id) }");
        var planner = new TypeScriptClientDocumentPlanner(schema, document);
        var plan = JsonSerializer.SerializeToElement(planner.Plan(document.Operations[0]))[0];
        AssertInteger(plan.GetProperty("arguments").GetProperty("id"));
        AssertInteger(plan.GetProperty("argumentDefaults").GetProperty("id"));
        var inputs = JsonSerializer.SerializeToElement(planner.InputTypes());
        AssertInteger(inputs.GetProperty("Options").GetProperty("fields").GetProperty("id").GetProperty("defaultValue"));
        AssertInteger(JsonSerializer.SerializeToElement(TypeScriptClientDocumentPlanner.InputValue(document.Operations[0].Variables[0].DefaultValue!)));
    }

    [Fact]
    public void Float_literals_still_emit_numeric_values()
    {
        var schema = Schema("type Query { echo(value: Float): Float }");
        var document = Executable(schema, "{ echo(value: 1.25) }");
        var plan = JsonSerializer.SerializeToElement(new TypeScriptClientDocumentPlanner(schema, document).Plan(document.Operations[0]));
        var value = plan[0].GetProperty("arguments").GetProperty("value");
        Assert.Equal("literal", value.GetProperty("kind").GetString());
        Assert.Equal(1.25, value.GetProperty("value").GetDouble());
    }

    [Theory]
    [InlineData("Node", true, "Book,Writer")]
    [InlineData("Hit", true, "Book,Writer")]
    [InlineData("Book", false, "Book")]
    public void Output_metadata_preserves_wrappers_and_concrete_type_members(string output, bool abstractType, string expectedMembers)
    {
        var schema = Schema("interface Node { id: ID! } type Book implements Node { id: ID! } type Writer implements Node { id: ID! } union Hit = Book | Writer type Query { items: [" + output + "!]! }");
        var document = Executable(schema, "{ items { __typename } }");
        var plan = JsonSerializer.SerializeToElement(new TypeScriptClientDocumentPlanner(schema, document).Plan(document.Operations[0]));
        var type = plan[0].GetProperty("outputType");
        Assert.Equal("non-null", type.GetProperty("kind").GetString());
        type = type.GetProperty("ofType");
        Assert.Equal("list", type.GetProperty("kind").GetString());
        type = type.GetProperty("ofType");
        Assert.Equal("non-null", type.GetProperty("kind").GetString());
        type = type.GetProperty("ofType");
        Assert.Equal(output, type.GetProperty("name").GetString());
        Assert.Equal(abstractType, type.GetProperty("abstract").GetBoolean());
        Assert.Equal(expectedMembers.Split(','), type.GetProperty("possibleTypes").EnumerateArray().Select(member => member.GetString()).Order(StringComparer.Ordinal));
    }

    private static void AssertInteger(JsonElement value)
    {
        Assert.Equal("integer", value.GetProperty("kind").GetString());
        Assert.Equal("9007199254740993", value.GetProperty("value").GetString());
    }

    private static GraphQlSchemaModel Schema(string source) => GraphQlModelFactory.CreateSchema(global::Salep.GraphQLParser.GraphQLParser.Parse(new SourceText(source.AsMemory())));
    private static GraphQlExecutableDocument Executable(GraphQlSchemaModel schema, string source) => GraphQlModelFactory.CreateExecutable(schema, [global::Salep.GraphQLParser.GraphQLParser.Parse(new SourceText(source.AsMemory()))]);
}
