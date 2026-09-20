using Xunit;

namespace GraphQLParser.Tests;

public sealed class SyntaxCompatibilityTests
{
    [Fact]
    public void Syntax_contract_exposes_ordered_children_printing_and_one_based_locations()
    {
        var document = GraphQLParser.Parse(new SourceText("query Q {\r\n  field\r\n}".AsMemory()));
        ISyntaxNode syntax = document;

        Assert.Equal(SyntaxKind.Document, document.Kind);
        Assert.Equal(SyntaxKind.Document, syntax.Kind);
        Assert.Equal(new Location(0, 21, 1, 1), syntax.Location);
        Assert.Equal("query Q {\n  field\n}", syntax.ToString(true));
        Assert.Equal("query Q {\n  field\n}", syntax.ToString());

        var firstChild = Assert.Single(syntax.GetNodes());
        Assert.Equal(SyntaxKind.OperationDefinition, firstChild.Kind);
        var operationChildren = firstChild.GetNodes().ToArray();
        Assert.Equal(2, operationChildren.Length);
        var operationChild = operationChildren[0];
        Assert.Equal(SyntaxKind.Name, operationChild.Kind);
        Assert.Equal(new Location(6, 7, 1, 7), operationChild.Location);

        var parsedOperation = Assert.IsType<OperationDefinitionNode>(document.Definitions[0]);
        var field = Assert.IsType<FieldNode>(parsedOperation.SelectionSet.Selections[0]);
        Assert.Equal(2, ((ISyntaxNode)field).Location.Line);
        Assert.Equal(3, ((ISyntaxNode)field).Location.Column);
        Assert.Equal(SyntaxKind.Field, field.Kind);
    }

    [Fact]
    public void Source_location_equality_preserves_range_semantics_when_line_metadata_is_present()
    {
        Assert.Equal(new SourceLocation(4, 9), new SourceLocation(4, 9));
        Assert.NotEqual(new Location(4, 9, 1, 5), new Location(4, 9, 3, 2));

        Location publicLocation = new(4, 9, 3, 2);
        SourceLocation range = publicLocation;
        Assert.Equal(new Location(4, 9, 3, 2), publicLocation);
        Assert.Equal(new SourceLocation(4, 9), range);
    }

    [Fact]
    public void Literal_interfaces_expose_typed_values_and_checked_numeric_conversions()
    {
        var document = GraphQLParser.Parse(new SourceText("{ field(arg: 42.5) }".AsMemory()));
        var operation = Assert.IsType<OperationDefinitionNode>(document.Definitions[0]);
        var field = Assert.IsType<FieldNode>(operation.SelectionSet.Selections[0]);
        var floatingPoint = Assert.IsAssignableFrom<IFloatValueLiteral>(field.Arguments[0].Value);
        Assert.Equal(42.5, floatingPoint.ToDouble());
        Assert.Equal(42.5m, floatingPoint.ToDecimal());
        Assert.Equal("42.5", Assert.IsAssignableFrom<IValueNode<string>>(field.Arguments[0].Value).Value);
        Assert.Equal("42.5", System.Text.Encoding.UTF8.GetString(floatingPoint.AsSpan()));

        var integer = Assert.IsAssignableFrom<IIntValueLiteral>(GraphQLParser.Parse(new SourceText("{ f(arg: 300) }".AsMemory()))
            .Definitions.OfType<OperationDefinitionNode>().Single().SelectionSet.Selections.OfType<FieldNode>().Single().Arguments[0].Value);
        Assert.Equal(300, integer.ToInt32());
        Assert.Throws<OverflowException>(() => integer.ToByte());
    }

    [Fact]
    public void Role_interfaces_expose_common_definition_selection_and_type_members()
    {
        var document = GraphQLParser.Parse(new SourceText("type Query @oneOf { field(arg: String): [Int] }".AsMemory()));
        var type = Assert.IsAssignableFrom<ITypeDefinitionNode>(document.Definitions[0]);
        Assert.Equal("Query", type.Name.Value.ToString());
        Assert.Equal("Query", Assert.IsAssignableFrom<IHasName>(type).Name.Value.ToString());
        Assert.Equal("oneOf", Assert.Single(Assert.IsAssignableFrom<IHasDirectives>(type).Directives).Name.Value.ToString());

        var field = Assert.IsAssignableFrom<INamedSyntaxNode>(type.GetNodes().OfType<FieldDefinitionNode>().FirstOrDefault()
            ?? Assert.IsType<ObjectTypeDefinitionNode>(document.Definitions[0]).Fields[0]);
        Assert.Equal("field", field.Name.Value.ToString());
        var typeReference = Assert.IsAssignableFrom<INullableTypeNode>(Assert.IsType<FieldDefinitionNode>(
            Assert.IsType<ObjectTypeDefinitionNode>(document.Definitions[0]).Fields[0]).Type is ListTypeNode list ? list : null);
        Assert.Equal(SyntaxKind.ListType, typeReference.Kind);
    }

    [Fact]
    public void Name_nodes_expose_immutable_string_rewrites_and_keep_backing_memory()
    {
        var original = new NameNode(new Location(4, 8, 2, 3), "name");
        var renamed = original.WithValue("longerName");
        var relocated = original.WithLocation(new Location(10, 14, 4, 1));

        Assert.Equal("name", original.Value);
        Assert.Equal("longerName", renamed.Value);
        Assert.Equal((SourceLocation)original.Location, (SourceLocation)renamed.Location);
        Assert.Equal((SourceLocation)new Location(10, 14, 4, 1), (SourceLocation)relocated.Location);
        Assert.Equal("name", original.SourceValue.ToString());
        Assert.Equal(new NameNode("name", original.Location), original);
    }

    [Fact]
    public void Compatibility_token_aliases_preserve_the_contract_numeric_values()
    {
        Assert.Equal((byte)0, (byte)TokenKind.StartOfFile);
        Assert.Equal((byte)1, (byte)TokenKind.EndOfFile);
        Assert.Equal((byte)2, (byte)TokenKind.Bang);
        Assert.Equal((byte)3, (byte)TokenKind.QuestionMark);
        Assert.Equal((byte)6, (byte)TokenKind.ParenthesisLeft);
        Assert.Equal((byte)6, (byte)TokenKind.LeftParenthesis);
        Assert.Equal((byte)10, (byte)TokenKind.Equal);
        Assert.Equal((byte)10, (byte)TokenKind.Equals);
        Assert.Equal((byte)22, (byte)TokenKind.Comment);
        Assert.Equal((byte)23, (byte)TokenKind.Dot);
    }
}
