using GraphQLParser;
using Xunit;

namespace GraphQLParser.Tests;

public sealed class SdlAstTests
{
    private static readonly SourceLocation Location = new(0, 1);

    [Fact]
    public void SDLDefinitionsKeepDescriptionsChildrenAndDirectiveMetadata()
    {
        var description = new StringValueNode("description".AsMemory(), false, Location);
        var name = Name("Query");
        var directive = new DirectiveNode(Name("deprecated"), [], Location);
        var input = new InputValueDefinitionNode(Name("limit"), new NonNullTypeNode(new NamedTypeNode(Name("Int"), Location), Location), new IntValueNode("10".AsMemory(), Location), [directive], Location, description);
        var field = new FieldDefinitionNode(Name("search"), [input], new ListTypeNode(new NamedTypeNode(Name("Product"), Location), Location), [directive], Location, description);
        var definition = new ObjectTypeDefinitionNode(name, [new NamedTypeNode(Name("Node"), Location)], [directive], [field], Location, description);
        var directiveDefinition = new DirectiveDefinitionNode(Name("audit"), [input], true, [Name("FIELD"), Name("OBJECT")], Location, description);

        Assert.Equal(AstNodeKind.ObjectTypeDefinition, definition.Kind);
        Assert.Same(description, definition.Description);
        Assert.Same(directive, Assert.Single(definition.Directives));
        Assert.Same(field, Assert.Single(definition.Fields));
        Assert.Same(input, Assert.Single(field.Arguments));
        Assert.Same(description, input.Description);
        Assert.Same(input.DefaultValue, new InputValueDefinitionNode(Name("x"), new NamedTypeNode(Name("Int"), Location), input.DefaultValue, [], Location).DefaultValue);
        Assert.True(directiveDefinition.Repeatable);
        Assert.Equal(new[] { "FIELD", "OBJECT" }, directiveDefinition.Locations.Select(item => item.Value.ToString()));
    }

    [Fact]
    public void EverySDLDefinitionAndExtensionHasItsOwnKindAndOrderedChildren()
    {
        var named = new NamedTypeNode(Name("T"), Location);
        var field = new FieldDefinitionNode(Name("f"), [], named, [], Location);
        var input = new InputValueDefinitionNode(Name("i"), named, null, [], Location);
        var enumValue = new EnumValueDefinitionNode(Name("V"), [], Location);
        var directive = new DirectiveNode(Name("d"), [], Location);
        var op = new OperationTypeDefinitionNode(OperationType.Query, named, Location);

        AstNode[] definitions =
        [
            new SchemaDefinitionNode([op], [], Location), new SchemaExtensionNode([], [directive], Location),
            new ScalarTypeDefinitionNode(Name("S"), [], Location), new ScalarTypeExtensionNode(Name("S"), [directive], Location),
            new ObjectTypeDefinitionNode(Name("O"), [named], [], [field], Location), new ObjectTypeExtensionNode(Name("O"), [], [], [field], Location),
            new InterfaceTypeDefinitionNode(Name("I"), [], [], [field], Location), new InterfaceTypeExtensionNode(Name("I"), [], [directive], [], Location),
            new UnionTypeDefinitionNode(Name("U"), [], [named], Location), new UnionTypeExtensionNode(Name("U"), [], [named], Location),
            new EnumTypeDefinitionNode(Name("E"), [], [enumValue], Location), new EnumTypeExtensionNode(Name("E"), [directive], [], Location),
            new InputObjectTypeDefinitionNode(Name("Input"), [], [input], Location), new InputObjectTypeExtensionNode(Name("Input"), [], [input], Location),
            new DirectiveDefinitionNode(Name("d"), [], false, [Name("FIELD")], Location),
        ];

        Assert.Equal(definitions.Length, definitions.Select(node => node.Kind).Distinct().Count());
        Assert.Equal(AstNodeKind.SchemaDefinition, definitions[0].Kind);
        Assert.Equal(AstNodeKind.DirectiveDefinition, definitions[^1].Kind);
    }

    [Fact]
    public void SchemaAndExtensionsEnforceGrammarRequiredContent()
    {
        Assert.Throws<ArgumentException>(() => new SchemaDefinitionNode([], [], Location));
        Assert.Throws<ArgumentException>(() => new SchemaExtensionNode([], [], Location));
        Assert.Throws<ArgumentException>(() => new ScalarTypeExtensionNode(Name("S"), [], Location));
        Assert.Throws<ArgumentException>(() => new ObjectTypeExtensionNode(Name("O"), [], [], [], Location));
        Assert.Throws<ArgumentException>(() => new InterfaceTypeExtensionNode(Name("I"), [], [], [], Location));
        Assert.Throws<ArgumentException>(() => new UnionTypeExtensionNode(Name("U"), [], [], Location));
        Assert.Throws<ArgumentException>(() => new EnumTypeExtensionNode(Name("E"), [], [], Location));
        Assert.Throws<ArgumentException>(() => new InputObjectTypeExtensionNode(Name("Input"), [], [], Location));
        Assert.Throws<ArgumentException>(() => new DirectiveDefinitionNode(Name("d"), [], false, [], Location));
    }

    private static NameNode Name(string value) => new(value.AsMemory(), new SourceLocation(0, value.Length));
}
