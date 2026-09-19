using GraphQLParser;
using Xunit;

namespace GraphQLParser.Tests;

public sealed class SdlParserTests
{
    [Fact]
    public void ParsesSchemaAndEveryTypeDefinitionWithDescriptionsAndOrderedChildren()
    {
        const string source = "\"\"\"schema docs\"\"\" schema @schemaTag { query: Query } \"scalar docs\" scalar Date @specifiedBy(url: \"https://example.test/date\") type Query implements & Node & Resource @key(fields: \"id\") { \"field docs\" id(arg: Int = 1 @bound): ID! @deprecated(reason: \"old\") } interface Node implements Resource { id: ID! } interface Resource { id: ID! } union Search = | Query | Product enum Color { \"red\" RED BLUE @tag } input Filter @oneOf { term: String ids: [ID!] = [] }";
        var document = GraphQLParser.Parse(new SourceText(source.AsMemory()));

        var schema = Assert.IsType<SchemaDefinitionNode>(document.Definitions[0]);
        Assert.Equal(OperationType.Query, Assert.Single(schema.OperationTypes).Operation);
        Assert.Equal("schema docs", schema.Description!.Value.ToString());
        Assert.IsType<ScalarTypeDefinitionNode>(document.Definitions[1]);
        var scalar = Assert.IsType<ScalarTypeDefinitionNode>(document.Definitions[1]);
        Assert.Equal("scalar docs", scalar.Description!.Value.ToString());

        var objectType = Assert.IsType<ObjectTypeDefinitionNode>(document.Definitions[2]);
        Assert.Equal(new[] { "Node", "Resource" }, objectType.Interfaces.Select(type => type.Name.Value.ToString()));
        var field = Assert.IsType<FieldDefinitionNode>(Assert.Single(objectType.Fields));
        Assert.Equal("field docs", field.Description!.Value.ToString());
        Assert.Equal("arg", Assert.Single(field.Arguments).Name.Value.ToString());
        Assert.Equal("deprecated", Assert.Single(field.Directives).Name.Value.ToString());
        Assert.IsType<InterfaceTypeDefinitionNode>(document.Definitions[3]);
        Assert.IsType<InterfaceTypeDefinitionNode>(document.Definitions[4]);
        Assert.Equal(new[] { "Query", "Product" }, Assert.IsType<UnionTypeDefinitionNode>(document.Definitions[5]).Types.Select(type => type.Name.Value.ToString()));

        var enumType = Assert.IsType<EnumTypeDefinitionNode>(document.Definitions[6]);
        Assert.Equal(new[] { "RED", "BLUE" }, enumType.Values.Select(value => value.Name.Value.ToString()));
        var inputType = Assert.IsType<InputObjectTypeDefinitionNode>(document.Definitions[7]);
        Assert.Equal("oneOf", Assert.Single(inputType.Directives).Name.Value.ToString());
        Assert.Equal(2, inputType.Fields.Count);
        Assert.IsType<ListValueNode>(inputType.Fields[1].DefaultValue);
    }

    [Theory]
    [InlineData("schema { }")]
    [InlineData("scalar { }")]
    [InlineData("type Query { }")]
    [InlineData("type Query { field(arg:): String }")]
    [InlineData("interface { field: String }")]
    [InlineData("union Search = | Query |")]
    [InlineData("enum Color { true }")]
    [InlineData("input Filter { field: }")]
    [InlineData("\"description\" query { field }")]
    public void RejectsMalformedTypeSystemDefinitionsAtTheOffendingToken(string source)
    {
        var error = Assert.Throws<GraphQLSyntaxException>(() => GraphQLParser.Parse(new SourceText(source.AsMemory())));
        Assert.InRange(error.Position, 0, source.Length);
    }
}
