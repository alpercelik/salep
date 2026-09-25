using Xunit;
using Salep.Parser.Visitors;

namespace Salep.Parser.Tests;

public sealed class AstCopyMethodsTests
{
    [Fact]
    public void Executable_nodes_copy_names_values_types_and_locations()
    {
        var location = new SourceLocation(0, 8);
        var replacementLocation = new Location(2, 10, 1, 3);
        var name = new NameNode("item");
        var integer = new IntValueNode(3);
        var argument = new ArgumentNode(name, integer, location);

        Assert.Same(name, argument.WithValue(integer).Name);
        Assert.Same(integer, argument.WithName(name).Value);
        Assert.Equal(new SourceLocation(2, 10), (SourceLocation)argument.WithLocation(replacementLocation).Location);

        var directive = new DirectiveNode(name, [argument], location);
        Assert.Same(argument, directive.WithArguments([argument]).Arguments[0]);
        Assert.Same(name, directive.WithName(name).Name);

        var nullableType = new NamedTypeNode(name, location);
        var variable = new VariableNode(name, location);
        var variableDefinition = new VariableDefinitionNode(variable, nullableType, integer, [directive], location);
        Assert.Same(variable, variableDefinition.WithVariable(variable).Variable);
        Assert.Same(nullableType, variableDefinition.WithType(nullableType).Type);
        Assert.Same(integer, variableDefinition.WithDefaultValue(integer).DefaultValue);
        Assert.Same(directive, variableDefinition.WithDirectives([directive]).Directives[0]);
        Assert.Equal(new SourceLocation(2, 10), (SourceLocation)variableDefinition.WithLocation(replacementLocation).Location);
        Assert.Same(name, nullableType.WithName(name).Name);
        Assert.Same(nullableType.Name, nullableType.WithLocation(replacementLocation).Name);

        var listType = new ListTypeNode(nullableType, location);
        Assert.Same(nullableType, listType.WithType(nullableType).Type);
        var nonNullType = new NonNullTypeNode(nullableType, location);
        Assert.Same(nullableType, nonNullType.WithType(nullableType).Type);
        Assert.Same(nullableType, nonNullType.WithLocation(replacementLocation).Type);
    }

    [Fact]
    public void Fragment_spread_arguments_survive_copy_traversal_rewrite_and_printing()
    {
        var name = new NameNode("details");
        var argument = new ArgumentNode(new NameNode("id"), new IntValueNode(7), default);
        var spread = new FragmentSpreadNode(new Location(0, 20, 1, 1), name, [argument], []);

        Assert.Same(argument, spread.Arguments[0]);
        Assert.Same(argument, spread.WithArguments([argument]).Arguments[0]);
        Assert.Equal("...details(id: 7)", spread.ToString());
        Assert.Contains(spread.GetNodes(), node => ReferenceEquals(node, argument));

        var document = GraphQLParser.Parse(new SourceText("{ ...details }".AsMemory()));
        var spreadRewriter = SyntaxRewriter.Create<object>((node, _) => node is ArgumentNode current && current.Name.Value == "id"
            ? current.WithValue(new IntValueNode(8))
            : node);
        var rewrittenSpread = Assert.IsType<FragmentSpreadNode>(spreadRewriter.Rewrite(spread));
        Assert.Equal("...details(id: 8)", rewrittenSpread.ToString());
    }

    [Fact]
    public void Fragment_definition_copies_preserve_unchanged_members()
    {
        var document = GraphQLParser.Parse(new SourceText("fragment details on User { id }".AsMemory()));
        var fragment = Assert.IsType<FragmentDefinitionNode>(document.Definitions[0]);
        var newName = new NameNode("summary");

        Assert.Same(fragment.SelectionSet, fragment.WithName(newName).SelectionSet);
        Assert.Same(fragment.TypeCondition, fragment.WithTypeCondition(fragment.TypeCondition).TypeCondition);
        Assert.Equal(fragment.Directives.Count, fragment.WithDirectives(fragment.Directives).Directives.Count);
        Assert.Equal(fragment.VariableDefinitions.Count, fragment.WithVariableDefinitions(fragment.VariableDefinitions).VariableDefinitions.Count);
    }

    [Fact]
    public void Public_convenience_constructors_create_nodes_with_consistent_children()
    {
        Location location = new(0, 12, 1, 1);
        var name = new NameNode("field");
        IValueNode value = new IntValueNode(4);
        var argument = new ArgumentNode(location, new NameNode("id"), value);
        Assert.Same(value, argument.Value);
        Assert.Equal("true", GraphQLPrinter.Print(new ArgumentNode("enabled", true).Value));
        Assert.Equal("4", GraphQLPrinter.Print(new ArgumentNode("count", 4).Value));
        Assert.Equal("text", Assert.IsType<StringValueNode>(new ArgumentNode("label", "text").Value).Value);
        Assert.Equal("4", new ArgumentNode("count", value).Value.ToString());

        var directiveByInterface = new DirectiveNode(location, new NameNode("include"), [argument]);
        var directiveByName = new DirectiveNode("include", new[] { argument });
        Assert.Same(argument, directiveByInterface.Arguments[0]);
        Assert.Same(argument, directiveByName.Arguments[0]);
        Assert.Same(argument, new DirectiveNode(new NameNode("include"), [argument]).Arguments[0]);

        var fieldWithChildren = new FieldNode(location, name, null, [directiveByInterface], [argument], null);
        Assert.Same(argument, fieldWithChildren.Arguments[0]);
        Assert.Same(directiveByInterface, fieldWithChildren.Directives[0]);
        var fieldWithoutLocation = new FieldNode(null, name, null, [], [], null);
        Assert.False(fieldWithoutLocation.HasLocation);
        Assert.Equal(0, fieldWithoutLocation.Location.Start);
        Assert.Equal(0, fieldWithoutLocation.Location.End);
        Assert.Equal("field", new FieldNode("field").Name.Value);
        Assert.Equal("field", new FieldNode("field", null).Name.Value);
        Assert.Same(argument, new FieldNode(name, null, [], [argument], null).Arguments[0]);

        Assert.Equal("User", new NamedTypeNode("User").Name.Value);
        Assert.Equal("User", new NamedTypeNode(new NameNode("User")).Name.Value);
        Assert.Equal("[User]", new ListTypeNode((ITypeNode)new NamedTypeNode("User")).ToString());
        Assert.Equal("User!", new NonNullTypeNode((INullableTypeNode)new NamedTypeNode("User")).ToString());
        ISelectionNode selection = new FieldNode("field");
        Assert.Same(selection, new SelectionSetNode(new ISelectionNode[] { selection }).Selections[0]);
        Assert.Same(selection, new SelectionSetNode(location, new ISelectionNode[] { selection }).Selections[0]);

        var scalar = new ScalarTypeDefinitionNode(location, new NameNode("Date"), null, []);
        Assert.Equal("Date", scalar.Name.Value);
        Assert.Equal("Date", new ScalarTypeDefinitionNode(new NameNode("Date"), null, []).Name.Value);
    }
}
