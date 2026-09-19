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

    [Fact]
    public void ParsesDirectiveDefinitionsAndEverySchemaOrTypeExtension()
    {
        const string source = "extend schema @schemaTag extend schema { mutation: Mutation } extend scalar Date @specifiedBy(url: \"x\") extend type Query implements Node @key(fields: \"id\") { extra: String } extend interface Node @tag { version: Int } extend union Search @tag = Product extend enum Color @tag { GREEN @deprecated } extend input Filter @oneOf { enabled: Boolean } \"directive docs\" directive @audit(reason: String = \"recorded\") repeatable on | FIELD | OBJECT";
        var document = GraphQLParser.Parse(new SourceText(source.AsMemory()));

        Assert.Equal(9, document.Definitions.Count);
        Assert.IsType<SchemaExtensionNode>(document.Definitions[0]);
        Assert.Single(Assert.IsType<SchemaExtensionNode>(document.Definitions[1]).OperationTypes);
        Assert.IsType<ScalarTypeExtensionNode>(document.Definitions[2]);
        Assert.Single(Assert.IsType<ObjectTypeExtensionNode>(document.Definitions[3]).Interfaces);
        Assert.Single(Assert.IsType<InterfaceTypeExtensionNode>(document.Definitions[4]).Fields);
        Assert.Equal("Product", Assert.Single(Assert.IsType<UnionTypeExtensionNode>(document.Definitions[5]).Types).Name.Value.ToString());
        Assert.Equal("GREEN", Assert.Single(Assert.IsType<EnumTypeExtensionNode>(document.Definitions[6]).Values).Name.Value.ToString());
        Assert.Single(Assert.IsType<InputObjectTypeExtensionNode>(document.Definitions[7]).Fields);

        var directive = Assert.IsType<DirectiveDefinitionNode>(document.Definitions[8]);
        Assert.True(directive.Repeatable);
        Assert.Equal("directive docs", directive.Description!.Value.ToString());
        Assert.Equal(new[] { "FIELD", "OBJECT" }, directive.Locations.Select(location => location.Value.ToString()));
        Assert.Equal("recorded", Assert.Single(directive.Arguments).DefaultValue is StringValueNode value ? value.Value.ToString() : null);
    }

    [Theory]
    [InlineData("extend schema")]
    [InlineData("extend schema { }")]
    [InlineData("extend schema @tag { }")]
    [InlineData("extend scalar Date")]
    [InlineData("extend type Query")]
    [InlineData("extend type Query { }")]
    [InlineData("extend interface Node")]
    [InlineData("extend union Search")]
    [InlineData("extend union Search =")]
    [InlineData("extend enum Color")]
    [InlineData("extend enum Color { }")]
    [InlineData("extend input Filter")]
    [InlineData("extend input Filter { }")]
    [InlineData("directive @bad on UNKNOWN")]
    [InlineData("directive @bad() on FIELD")]
    [InlineData("directive @bad on FIELD |")]
    [InlineData("enum Empty { }")]
    [InlineData("input Empty { }")]
    public void RejectsExtensionsWithoutRequiredContentAndInvalidDirectiveLocations(string source)
    {
        var error = Assert.Throws<GraphQLSyntaxException>(() => GraphQLParser.Parse(new SourceText(source.AsMemory())));
        Assert.InRange(error.Position, 0, source.Length);
    }

    [Fact]
    public void ParsesCustomDirectiveNamesAsGenericSyntax()
    {
        const string source = "scalar URL @specifiedBy(url: \"https://example.test\") input Choice @oneOf { first: String second: Int }";
        var document = GraphQLParser.Parse(new SourceText(source.AsMemory()));
        Assert.Equal("specifiedBy", Assert.Single(Assert.IsType<ScalarTypeDefinitionNode>(document.Definitions[0]).Directives).Name.Value.ToString());
        Assert.Equal("oneOf", Assert.Single(Assert.IsType<InputObjectTypeDefinitionNode>(document.Definitions[1]).Directives).Name.Value.ToString());
    }

    [Fact]
    public void InvalidDirectiveLocationReportsTheInvalidNameSpan()
    {
        const string source = "directive @bad on UNKNOWN";
        var error = Assert.Throws<GraphQLSyntaxException>(() => GraphQLParser.Parse(new SourceText(source.AsMemory())));
        Assert.Equal(source.IndexOf("UNKNOWN", StringComparison.Ordinal), error.Position);
        Assert.Equal("UNKNOWN".Length, error.Length);
    }
}
