using Xunit;

namespace Salep.Parser.Tests;

public sealed class SdlCopyMethodsTests
{
    private const string Schema = """
        schema @tag { query: Query }
        type Query implements Node @tag { id(arg: Int = 1): Int @tag }
        interface Node { id: Int }
        union Search @tag = Query | User
        enum Role @tag { ADMIN @tag }
        input Filter @tag { term: String @tag }
        scalar Date @tag
        directive @tag(if: Boolean!) repeatable on FIELD | FIELD_DEFINITION
        extend schema @tag
        extend scalar Date @tag
        extend type Query implements Node @tag { extra: Int }
        extend interface Node @tag { extra: Int }
        extend union Search @tag = User
        extend enum Role @tag { GUEST }
        extend input Filter @tag { active: Boolean }
        """;

    [Fact]
    public void Location_first_compatibility_constructors_accept_public_interface_collections()
    {
        var document = GraphQLParser.Parse(new SourceText(Schema.AsMemory()));
        var location = new Location(0, Schema.Length, 1, 1);
        var schema = document.Definitions.OfType<SchemaDefinitionNode>().Single();
        var schemaExtension = document.Definitions.OfType<SchemaExtensionNode>().Single();
        var enumDefinition = document.Definitions.OfType<EnumTypeDefinitionNode>().Single();
        var enumExtension = document.Definitions.OfType<EnumTypeExtensionNode>().Single();
        var input = document.Definitions.OfType<InputObjectTypeDefinitionNode>().Single();
        var inputExtension = document.Definitions.OfType<InputObjectTypeExtensionNode>().Single();
        var iface = document.Definitions.OfType<InterfaceTypeDefinitionNode>().Single();
        var ifaceExtension = document.Definitions.OfType<InterfaceTypeExtensionNode>().Single();
        var objectType = document.Definitions.OfType<ObjectTypeDefinitionNode>().Single();
        var objectExtension = document.Definitions.OfType<ObjectTypeExtensionNode>().Single();
        var union = document.Definitions.OfType<UnionTypeDefinitionNode>().Single();
        var unionExtension = document.Definitions.OfType<UnionTypeExtensionNode>().Single();
        var operation = Assert.IsType<OperationDefinitionNode>(GraphQLParser.Parse(new SourceText("query Q($id: ID) { item(id: $id) }".AsMemory())).Definitions[0]);

        Assert.Equal(schema.OperationTypes.Count, new SchemaDefinitionNode(location, schema.Description, schema.Directives, schema.OperationTypes).OperationTypes.Count);
        Assert.Equal(schemaExtension.Directives.Count, new SchemaExtensionNode(location, schemaExtension.Directives, schemaExtension.OperationTypes).Directives.Count);
        Assert.Equal(enumDefinition.Values.Count, new EnumTypeDefinitionNode(location, enumDefinition.Name, enumDefinition.Description, enumDefinition.Directives, enumDefinition.Values).Values.Count);
        Assert.Equal(enumExtension.Values.Count, new EnumTypeExtensionNode(location, enumExtension.Name, enumExtension.Directives, enumExtension.Values).Values.Count);
        Assert.Equal(input.Fields.Count, new InputObjectTypeDefinitionNode(location, input.Name, input.Description, input.Directives, input.Fields).Fields.Count);
        Assert.Equal(inputExtension.Fields.Count, new InputObjectTypeExtensionNode(location, inputExtension.Name, inputExtension.Directives, inputExtension.Fields).Fields.Count);
        Assert.Equal(iface.Fields.Count, new InterfaceTypeDefinitionNode(location, iface.Name, iface.Description, iface.Directives, iface.Interfaces, iface.Fields).Fields.Count);
        Assert.Equal(ifaceExtension.Fields.Count, new InterfaceTypeExtensionNode(location, ifaceExtension.Name, ifaceExtension.Directives, ifaceExtension.Interfaces, ifaceExtension.Fields).Fields.Count);
        Assert.Equal(objectType.Fields.Count, new ObjectTypeDefinitionNode(location, objectType.Name, objectType.Description, objectType.Directives, objectType.Interfaces, objectType.Fields).Fields.Count);
        Assert.Equal(objectExtension.Fields.Count, new ObjectTypeExtensionNode(location, objectExtension.Name, objectExtension.Directives, objectExtension.Interfaces, objectExtension.Fields).Fields.Count);
        Assert.Equal(union.Types.Count, new UnionTypeDefinitionNode(location, union.Name, union.Description, union.Directives, union.Types).Types.Count);
        Assert.Equal(unionExtension.Types.Count, new UnionTypeExtensionNode(location, unionExtension.Name, unionExtension.Directives, unionExtension.Types).Types.Count);
        Assert.Equal(location.Start, new OperationDefinitionNode(location, operation.Name, operation.Description, operation.Operation, operation.VariableDefinitions, operation.Directives, operation.SelectionSet).Location.Start);
    }

    [Fact]
    public void Type_definition_copies_replace_each_declared_component()
    {
        var document = GraphQLParser.Parse(new SourceText(Schema.AsMemory()));
        var tag = Assert.IsType<DirectiveNode>(GraphQLParser.Parse(new SourceText("{ f @tag }".AsMemory()))
            .Definitions.OfType<OperationDefinitionNode>().Single().SelectionSet.Selections.OfType<FieldNode>().Single().Directives.Single());
        var replacementName = new NameNode("Replacement");
        var location = new Location(1, 3, 1, 2);

        var scalar = document.Definitions.OfType<ScalarTypeDefinitionNode>().Single();
        Assert.Equal("Replacement", scalar.WithName(replacementName).Name.Value);
        Assert.Same(tag, scalar.WithDirectives([tag]).Directives[0]);
        Assert.Equal("Date", scalar.WithLocation(location).Name.Value);
        Assert.Equal("Date", scalar.WithDescription(null).Name.Value);

        var objectType = document.Definitions.OfType<ObjectTypeDefinitionNode>().Single();
        Assert.Same(objectType.Fields[0], objectType.WithFields(objectType.Fields).Fields[0]);
        Assert.Same(objectType.Interfaces[0], objectType.WithInterfaces(objectType.Interfaces).Interfaces[0]);
        Assert.Same(objectType.Fields[0], objectType.WithDirectives([tag]).Fields[0]);
        Assert.Equal("Replacement", objectType.WithName(replacementName).Name.Value);
        Assert.Equal("Query", objectType.WithDescription(null).Name.Value);
        Assert.Equal("Query", objectType.WithLocation(location).Name.Value);

        var iface = document.Definitions.OfType<InterfaceTypeDefinitionNode>().Single();
        Assert.Same(iface.Fields[0], iface.WithFields(iface.Fields).Fields[0]);
        Assert.Empty(iface.WithInterfaces(iface.Interfaces).Interfaces);
        Assert.Equal("Node", iface.WithDirectives([tag]).Name.Value);
        Assert.Equal("Node", iface.WithDescription(null).Name.Value);
        Assert.Equal("Node", iface.WithLocation(location).Name.Value);
        Assert.Equal("Replacement", iface.WithName(replacementName).Name.Value);

        var union = document.Definitions.OfType<UnionTypeDefinitionNode>().Single();
        Assert.Same(union.Types[0], union.WithTypes(union.Types).Types[0]);
        Assert.Equal("Search", union.WithDirectives([tag]).Name.Value);
        Assert.Equal("Search", union.WithDescription(null).Name.Value);
        Assert.Equal("Search", union.WithLocation(location).Name.Value);
        Assert.Equal("Replacement", union.WithName(replacementName).Name.Value);

        var enumeration = document.Definitions.OfType<EnumTypeDefinitionNode>().Single();
        Assert.Same(enumeration.Values[0], enumeration.WithValues(enumeration.Values).Values[0]);
        Assert.Equal("Role", enumeration.WithDirectives([tag]).Name.Value);
        Assert.Equal("Role", enumeration.WithDescription(null).Name.Value);
        Assert.Equal("Role", enumeration.WithLocation(location).Name.Value);
        Assert.Equal("Replacement", enumeration.WithName(replacementName).Name.Value);

        var input = document.Definitions.OfType<InputObjectTypeDefinitionNode>().Single();
        Assert.Same(input.Fields[0], input.WithFields(input.Fields).Fields[0]);
        Assert.Equal("Filter", input.WithDirectives([tag]).Name.Value);
        Assert.Equal("Filter", input.WithDescription(null).Name.Value);
        Assert.Equal("Filter", input.WithLocation(location).Name.Value);
        Assert.Equal("Replacement", input.WithName(replacementName).Name.Value);
    }

    [Fact]
    public void Type_extension_copies_preserve_unchanged_members_and_replace_contents()
    {
        var document = GraphQLParser.Parse(new SourceText(Schema.AsMemory()));
        var tag = Assert.IsType<DirectiveNode>(GraphQLParser.Parse(new SourceText("{ f @tag }".AsMemory()))
            .Definitions.OfType<OperationDefinitionNode>().Single().SelectionSet.Selections.OfType<FieldNode>().Single().Directives.Single());
        var location = new Location(1, 3, 1, 2);
        var name = new NameNode("Renamed");

        var scalar = document.Definitions.OfType<ScalarTypeExtensionNode>().Single();
        Assert.Equal("Renamed", scalar.WithName(name).Name.Value);
        Assert.Equal("Date", scalar.WithLocation(location).Name.Value);
        Assert.Same(tag, scalar.WithDirectives([tag]).Directives[0]);

        var obj = document.Definitions.OfType<ObjectTypeExtensionNode>().Single();
        Assert.Same(obj.Fields[0], obj.WithFields(obj.Fields).Fields[0]);
        Assert.Same(obj.Interfaces[0], obj.WithInterfaces(obj.Interfaces).Interfaces[0]);
        Assert.Equal("Query", obj.WithDirectives([tag]).Name.Value);
        Assert.Equal("Query", obj.WithLocation(location).Name.Value);
        Assert.Equal("Renamed", obj.WithName(name).Name.Value);

        var iface = document.Definitions.OfType<InterfaceTypeExtensionNode>().Single();
        Assert.Same(iface.Fields[0], iface.WithFields(iface.Fields).Fields[0]);
        Assert.Empty(iface.WithInterfaces(iface.Interfaces).Interfaces);
        Assert.Equal("Node", iface.WithDirectives([tag]).Name.Value);
        Assert.Equal("Node", iface.WithLocation(location).Name.Value);
        Assert.Equal("Renamed", iface.WithName(name).Name.Value);

        var union = document.Definitions.OfType<UnionTypeExtensionNode>().Single();
        Assert.Same(union.Types[0], union.WithTypes(union.Types).Types[0]);
        Assert.Equal("Search", union.WithDirectives([tag]).Name.Value);
        Assert.Equal("Search", union.WithLocation(location).Name.Value);
        Assert.Equal("Renamed", union.WithName(name).Name.Value);

        var enumeration = document.Definitions.OfType<EnumTypeExtensionNode>().Single();
        Assert.Same(enumeration.Values[0], enumeration.WithValues(enumeration.Values).Values[0]);
        Assert.Equal("Role", enumeration.WithDirectives([tag]).Name.Value);
        Assert.Equal("Role", enumeration.WithLocation(location).Name.Value);
        Assert.Equal("Renamed", enumeration.WithName(name).Name.Value);

        var input = document.Definitions.OfType<InputObjectTypeExtensionNode>().Single();
        Assert.Same(input.Fields[0], input.WithFields(input.Fields).Fields[0]);
        Assert.Equal("Filter", input.WithDirectives([tag]).Name.Value);
        Assert.Equal("Filter", input.WithLocation(location).Name.Value);
        Assert.Equal("Renamed", input.WithName(name).Name.Value);
    }

    [Fact]
    public void Schema_and_member_definition_copies_keep_ast_components()
    {
        var document = GraphQLParser.Parse(new SourceText(Schema.AsMemory()));
        var schema = document.Definitions.OfType<SchemaDefinitionNode>().Single();
        var schemaExtension = document.Definitions.OfType<SchemaExtensionNode>().Single();
        var location = new Location(1, 3, 1, 2);
        var queryType = new NamedTypeNode(new NameNode("Query"), default);
        var queryOperation = schema.OperationTypes[0];

        Assert.Same(queryOperation, schema.WithOperationTypes(schema.OperationTypes).OperationTypes[0]);
        Assert.Same(schema.OperationTypes[0], schema.WithLocation(location).OperationTypes[0]);
        Assert.Same(schema.OperationTypes[0], schema.WithDirectives(schema.Directives).OperationTypes[0]);
        Assert.Empty(schemaExtension.WithOperationTypes(schemaExtension.OperationTypes).OperationTypes);
        Assert.Equal((SourceLocation)location, (SourceLocation)schemaExtension.WithLocation(location).Location);
        Assert.Same(schemaExtension.Directives[0], schemaExtension.WithDirectives(schemaExtension.Directives).Directives[0]);

        var operation = queryOperation;
        Assert.Equal(operation.Operation, operation.WithOperation(operation.Operation).Operation);
        Assert.Same(queryType, operation.WithType(queryType).Type);
        Assert.Same(operation.Type, operation.WithLocation(location).Type);

        var type = document.Definitions.OfType<ObjectTypeDefinitionNode>().Single();
        var field = type.Fields[0];
        Assert.Same(field.Arguments[0], field.WithArguments(field.Arguments).Arguments[0]);
        Assert.Same(field.Directives[0], field.WithDirectives(field.Directives).Directives[0]);
        Assert.Same(field.Type, field.WithType(field.Type).Type);
        Assert.Equal("id", field.WithName(field.Name).Name.Value);
        Assert.Equal("id", field.WithDescription(null).Name.Value);
        Assert.Equal("id", field.WithLocation(location).Name.Value);

        var input = field.Arguments[0];
        Assert.Same(input.Type, input.WithType(input.Type).Type);
        Assert.Empty(input.WithDirectives(input.Directives).Directives);
        Assert.Equal("arg", input.WithName(input.Name).Name.Value);
        Assert.Equal("arg", input.WithDescription(null).Name.Value);
        Assert.Null(input.WithDefaultValue(null).DefaultValue);
        Assert.Equal("arg", input.WithLocation(location).Name.Value);

        var enumValue = document.Definitions.OfType<EnumTypeDefinitionNode>().Single().Values[0];
        Assert.Same(enumValue.Directives[0], enumValue.WithDirectives(enumValue.Directives).Directives[0]);
        Assert.Equal("ADMIN", enumValue.WithName(enumValue.Name).Name.Value);
        Assert.Equal("ADMIN", enumValue.WithDescription(null).Name.Value);
        Assert.Equal("ADMIN", enumValue.WithLocation(location).Name.Value);

        var directive = document.Definitions.OfType<DirectiveDefinitionNode>().Single();
        Assert.True(directive.IsRepeatable);
        Assert.Same(directive, directive.AsRepeatable());
        Assert.False(directive.AsRepeatable(false).Repeatable);
        Assert.Same(directive, directive.AsRepeatable(true));
        Assert.Same(directive.Arguments[0], directive.WithArguments(directive.Arguments).Arguments[0]);
        Assert.Same(directive.Locations[0], directive.WithLocations(directive.Locations).Locations[0]);
        Assert.Equal("tag", directive.WithName(directive.Name).Name.Value);
        Assert.Equal("tag", directive.WithDescription(null).Name.Value);
        Assert.Equal("tag", directive.WithLocation(location).Name.Value);

        var applied = Assert.IsType<DirectiveDefinitionNode>(directive.WithDirectives([document.Definitions.OfType<ObjectTypeDefinitionNode>().Single().Directives[0]]));
        Assert.Single(applied.Directives);
    }
}
