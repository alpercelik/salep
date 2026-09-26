using Salep.GraphQLParser;
using Xunit;

namespace Salep.GraphQLParser.Tests;

public sealed class ExecutableAstTests
{
    [Fact]
    public void NestedOperationAndFragmentNodesPreserveRequiredChildrenAndOrder()
    {
        const string sourceText = "query Get($id: ID!) { person: user(id: $id) { name ...User ... on Admin { level } } } fragment User on User { id }";
        var source = sourceText.AsMemory();
        var rootOpen = sourceText.IndexOf('{', sourceText.IndexOf("ID!", StringComparison.Ordinal));
        var closingSequence = sourceText.IndexOf("} } } fragment", StringComparison.Ordinal);
        var inlineStart = sourceText.IndexOf("... on Admin", StringComparison.Ordinal);
        var inlineOpen = sourceText.IndexOf('{', inlineStart);
        var inlineClose = closingSequence;
        var nestedOpen = sourceText.IndexOf('{', sourceText.IndexOf("user(", StringComparison.Ordinal));
        var nestedClose = closingSequence + 2;
        var rootClose = closingSequence + 4;

        var nameField = new FieldNode(
            CreateName(sourceText, "name"),
            alias: null,
            arguments: [],
            directives: [],
            selectionSet: null,
            LocationOf(sourceText, "name"));
        var levelField = new FieldNode(
            CreateName(sourceText, "level"),
            alias: null,
            arguments: [],
            directives: [],
            selectionSet: null,
            LocationOf(sourceText, "level"));
        var inlineSelection = new SelectionSetNode(
            [levelField],
            new SourceLocation(inlineOpen, inlineClose + 1));
        var adminType = new NamedTypeNode(CreateName(sourceText, "Admin"), LocationOf(sourceText, "Admin"));
        var inline = new InlineFragmentNode(
            adminType,
            [],
            inlineSelection,
            new SourceLocation(inlineStart, inlineClose + 1));

        var spreadStart = sourceText.IndexOf("...User", StringComparison.Ordinal);
        var spreadName = CreateName(sourceText, "User", spreadStart + 3);
        var spread = new FragmentSpreadNode(
            spreadName,
            [],
            new SourceLocation(spreadStart, spreadStart + 7));
        var nestedSelection = new SelectionSetNode(
            [nameField, spread, inline],
            new SourceLocation(nestedOpen, nestedClose + 1));

        var variableStart = sourceText.IndexOf("$id", StringComparison.Ordinal);
        var variableName = CreateName(sourceText, "id", variableStart + 1);
        var variable = new VariableNode(
            variableName,
            new SourceLocation(variableStart, variableStart + 3));
        var argumentStart = sourceText.IndexOf("id: $id", sourceText.IndexOf("user(", StringComparison.Ordinal), StringComparison.Ordinal);
        var argumentVariableStart = argumentStart + "id: ".Length;
        var argumentVariable = new VariableNode(
            CreateName(sourceText, "id", argumentVariableStart + 1),
            new SourceLocation(argumentVariableStart, argumentVariableStart + 3));
        var argument = new ArgumentNode(
            CreateName(sourceText, "id", argumentStart),
            argumentVariable,
            new SourceLocation(argumentStart, argumentStart + "id: $id".Length));
        var userStart = sourceText.IndexOf("user(", StringComparison.Ordinal);
        var userName = CreateName(sourceText, "user", userStart);
        var alias = CreateName(sourceText, "person");
        var userField = new FieldNode(
            userName,
            alias,
            [argument],
            [],
            nestedSelection,
            new SourceLocation(alias.Location.Start, nestedClose + 1));
        var operationSelection = new SelectionSetNode(
            [userField],
            new SourceLocation(rootOpen, rootClose + 1));

        var typeStart = sourceText.IndexOf("ID!", StringComparison.Ordinal);
        var namedId = new NamedTypeNode(CreateName(sourceText, "ID", typeStart), new SourceLocation(typeStart, typeStart + 2));
        var nonNullId = new NonNullTypeNode(namedId, new SourceLocation(typeStart, typeStart + 3));
        var variableDefinition = new VariableDefinitionNode(
            variable,
            nonNullId,
            defaultValue: null,
            directives: [],
            new SourceLocation(variableStart, typeStart + 3));
        var operation = new OperationDefinitionNode(
            OperationType.Query,
            CreateName(sourceText, "Get"),
            [variableDefinition],
            [],
            operationSelection,
            new SourceLocation(0, rootClose + 1));

        var fragmentStart = sourceText.IndexOf("fragment", StringComparison.Ordinal);
        var fragmentName = CreateName(sourceText, "User", fragmentStart + "fragment ".Length);
        var fragmentTypeStart = sourceText.IndexOf("on User", fragmentStart, StringComparison.Ordinal) + "on ".Length;
        var fragmentType = new NamedTypeNode(CreateName(sourceText, "User", fragmentTypeStart), new SourceLocation(fragmentTypeStart, fragmentTypeStart + 4));
        var fragmentSelectionStart = sourceText.IndexOf('{', fragmentStart);
        var fragmentSelectionEnd = sourceText.IndexOf('}', fragmentSelectionStart);
        var fragmentField = new FieldNode(
            CreateName(sourceText, "id", fragmentSelectionStart),
            alias: null,
            arguments: [],
            directives: [],
            selectionSet: null,
            new SourceLocation(sourceText.LastIndexOf("id", StringComparison.Ordinal), sourceText.LastIndexOf("id", StringComparison.Ordinal) + 2));
        var fragmentSelection = new SelectionSetNode(
            [fragmentField],
            new SourceLocation(fragmentSelectionStart, fragmentSelectionEnd + 1));
        var fragment = new FragmentDefinitionNode(
            fragmentName,
            fragmentType,
            [],
            fragmentSelection,
            new SourceLocation(fragmentStart, sourceText.Length));

        var document = new DocumentNode(
            new SourceText(source),
            [operation, fragment],
            new SourceLocation(0, sourceText.Length));

        Assert.Equal(AstNodeKind.Document, document.AstKind);
        Assert.Equal(source, document.Source.Content);
        Assert.Equal(2, document.Definitions.Count);
        Assert.Same(operation, document.Definitions[0]);
        Assert.Same(fragment, document.Definitions[1]);
        Assert.Equal(OperationType.Query, operation.Operation);
        Assert.Equal("Get", operation.Name!.Value.ToString());
        Assert.Equal("ID", ((NonNullTypeNode)operation.VariableDefinitions[0].Type).Type is NamedTypeNode operationId
            ? operationId.Name.Value.ToString()
            : null);
        Assert.Equal("person", userField.Alias!.Value.ToString());
        Assert.Equal("user", userField.Name.Value.ToString());
        Assert.Equal("name", nestedSelection.Selections[0] is FieldNode firstField ? firstField.Name.Value.ToString() : null);
        Assert.IsType<FragmentSpreadNode>(nestedSelection.Selections[1]);
        Assert.Equal("Admin", inline.TypeCondition!.Name.Value.ToString());
        Assert.Equal("User", fragment.Name.Value.ToString());
        Assert.Equal("User", fragment.TypeCondition.Name.Value.ToString());
        Assert.Empty(fragment.VariableDefinitions);
        Assert.Null(nameField.SelectionSet);
    }

    [Fact]
    public void ValueDirectiveAndTypeNodesRepresentNestedGrammarShapes()
    {
        const string source = "value";
        var name = CreateName(source, "value");
        var integer = new IntValueNode("12".AsMemory(), new SourceLocation(0, 2));
        var floating = new FloatValueNode("-2.5E+6".AsMemory(), new SourceLocation(0, 7));
        var text = new StringValueNode("decoded\nvalue".AsMemory(), isBlock: false, new SourceLocation(0, 7));
        var enumValue = new EnumValueNode("READY".AsMemory(), new SourceLocation(0, 5));
        var boolean = new BooleanValueNode(true, new SourceLocation(0, 4));
        var nullValue = new NullValueNode(new SourceLocation(0, 4));
        var variable = new VariableNode(name, new SourceLocation(0, 6));
        var list = new ListValueNode([integer, boolean, enumValue], new SourceLocation(0, 10));
        var field = new ObjectFieldNode(name, list, new SourceLocation(0, 10));
        var objectValue = new ObjectValueNode([field], new SourceLocation(0, 12));
        var type = new NonNullTypeNode(
            new ListTypeNode(new NamedTypeNode(name, new SourceLocation(0, 5)), new SourceLocation(0, 7)),
            new SourceLocation(0, 8));
        var argument = new ArgumentNode(name, objectValue, new SourceLocation(0, 12));
        var directive = new DirectiveNode(name, [argument], new SourceLocation(0, 13));
        var definition = new VariableDefinitionNode(variable, type, defaultValue: null, [directive], new SourceLocation(0, 13));

        Assert.Equal(AstNodeKind.IntValue, integer.AstKind);
        Assert.Equal("12", integer.Value.ToString());
        Assert.Equal("-2.5E+6", floating.Value.ToString());
        Assert.Equal("decoded\nvalue", text.Value.ToString());
        Assert.False(text.IsBlock);
        Assert.Equal("READY", enumValue.Value.ToString());
        Assert.True(boolean.Value);
        Assert.Equal(AstNodeKind.NullValue, nullValue.AstKind);
        Assert.Same(variable, definition.Variable);
        Assert.Equal(AstNodeKind.NonNullType, ((AstNode)definition.Type).AstKind);
        Assert.Equal(AstNodeKind.ListType, ((AstNode)((NonNullTypeNode)definition.Type).Type).AstKind);
        Assert.Same(directive, definition.Directives[0]);
        Assert.Same(argument, directive.Arguments[0]);
        Assert.Same(objectValue, argument.Value);
        Assert.Same(field, objectValue.Fields[0]);
        Assert.Equal(3, list.Values.Count);
    }

    [Fact]
    public void GrammarRequiredChildrenAndNonEmptyDefinitionsAreEnforced()
    {
        var location = new SourceLocation(0, 1);
        var name = CreateName("x", "x");
        var field = new FieldNode(name, null, [], [], null, location);
        var selectionSet = new SelectionSetNode([field], location);
        var type = new NamedTypeNode(name, location);
        var operation = new OperationDefinitionNode(OperationType.Query, null, [], [], selectionSet, location);

        Assert.Throws<ArgumentException>(() => new SelectionSetNode([], location));
        Assert.Throws<ArgumentException>(() => new SelectionSetNode([field], new SourceLocation(0, 0)));
        Assert.Throws<ArgumentException>(() => new DocumentNode(new SourceText("x".AsMemory()), [], location));
        Assert.Throws<ArgumentOutOfRangeException>(() => new DocumentNode(
            new SourceText("x".AsMemory()), [operation], new SourceLocation(0, 2)));
        Assert.Throws<ArgumentException>(() => new NonNullTypeNode(new NonNullTypeNode(type, location), location));
        Assert.Throws<ArgumentNullException>(() => new ArgumentNode(name, null!, location));
        Assert.Throws<ArgumentNullException>(() => new FieldNode(null!, null, [], [], selectionSet, location));
        Assert.Throws<ArgumentOutOfRangeException>(() => new OperationDefinitionNode(
            (OperationType)255, null, [], [], selectionSet, location));

        var spread = new FragmentSpreadNode(name, [], location);
        var inlineWithoutCondition = new InlineFragmentNode(null, [], selectionSet, location);
        Assert.Null(inlineWithoutCondition.TypeCondition);
        Assert.Empty(spread.Directives);
        Assert.Empty(field.Arguments);
    }

    private static NameNode CreateName(string source, string value, int searchStart = 0)
    {
        var start = source.IndexOf(value, searchStart, StringComparison.Ordinal);
        Assert.True(start >= 0, $"Could not find '{value}' in the AST construction source.");
        return new NameNode(source.AsMemory(start, value.Length), new SourceLocation(start, start + value.Length));
    }

    private static SourceLocation LocationOf(string source, string value)
    {
        var start = source.IndexOf(value, StringComparison.Ordinal);
        Assert.True(start >= 0, $"Could not find '{value}' in the AST construction source.");
        return new SourceLocation(start, start + value.Length);
    }
}
