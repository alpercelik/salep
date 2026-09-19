using GraphQLParser;
using Xunit;

namespace GraphQLParser.Tests;

public sealed class GraphQLParserTests
{
    [Fact]
    public void ParsesShorthandAndNamedOperationsWithPreciseSpans()
    {
        const string source = "  { viewer } query Find($id: [ID!]! = 1) { user }  ";
        var document = GraphQLParser.Parse(new SourceText(source.AsMemory()));

        Assert.Equal(new SourceLocation(0, source.Length), document.Location);
        Assert.Equal(2, document.Definitions.Count);
        var shorthand = Assert.IsType<OperationDefinitionNode>(document.Definitions[0]);
        Assert.Equal(OperationType.Query, shorthand.Operation);
        Assert.Null(shorthand.Name);
        Assert.Equal(new SourceLocation(2, 12), shorthand.Location);

        var named = Assert.IsType<OperationDefinitionNode>(document.Definitions[1]);
        Assert.Equal("Find", named.Name!.Value.ToString());
        Assert.Equal(new SourceLocation(13, source.Length - 2), named.Location);
        var variable = Assert.Single(named.VariableDefinitions);
        Assert.Equal("id", variable.Variable.Name.Value.ToString());
        Assert.IsType<NonNullTypeNode>(variable.Type);
        Assert.IsType<IntValueNode>(variable.DefaultValue);
    }

    [Theory]
    [InlineData("")]
    [InlineData("query Q() { field }")]
    [InlineData("query Q($x:) { field }")]
    [InlineData("query Q($x: Int) { }")]
    [InlineData("query Q { field")]
    [InlineData("{ field } garbage")]
    public void RejectsEmptyOrMalformedRequiredGrammar(string source)
    {
        Assert.Throws<GraphQLSyntaxException>(() => GraphQLParser.Parse(new SourceText(source.AsMemory())));
    }

    [Fact]
    public void UnexpectedTokenReportsItsExactSourceLocation()
    {
        const string source = "query Q($x: Int) { field } @";
        var error = Assert.Throws<GraphQLSyntaxException>(() => GraphQLParser.Parse(new SourceText(source.AsMemory())));

        Assert.Equal(source.Length - 1, error.Position);
        Assert.Equal(1, error.Length);
    }

    [Fact]
    public void ParsesAliasesNestedFieldsArgumentsDirectivesAndAllFragmentSelections()
    {
        const string source = "query Named($id: ID!) @tag { alias: user(id: $id) @include(if: true) { ...UserParts ... on Admin @flag { role } ... @skip(if: false) { active } } } fragment UserParts on User { id name }";
        var document = GraphQLParser.Parse(new SourceText(source.AsMemory()));
        var operation = Assert.IsType<OperationDefinitionNode>(document.Definitions[0]);
        var field = Assert.IsType<FieldNode>(Assert.Single(operation.SelectionSet.Selections));

        Assert.Equal("alias", field.Alias!.Value.ToString());
        Assert.Equal("user", field.Name.Value.ToString());
        Assert.Equal("$id", field.Arguments.Single().Value is VariableNode variable ? "$" + variable.Name.Value.ToString() : "");
        Assert.Equal("include", Assert.Single(field.Directives).Name.Value.ToString());
        Assert.Equal(new SourceLocation(source.IndexOf("alias", StringComparison.Ordinal), source.IndexOf("} } fragment", StringComparison.Ordinal) + 1), field.Location);

        var nested = field.SelectionSet!.Selections;
        Assert.IsType<FragmentSpreadNode>(nested[0]);
        Assert.IsType<InlineFragmentNode>(nested[1]);
        Assert.Null(Assert.IsType<InlineFragmentNode>(nested[2]).TypeCondition);
        var fragment = Assert.IsType<FragmentDefinitionNode>(document.Definitions[1]);
        Assert.Equal("User", fragment.TypeCondition.Name.Value.ToString());
        Assert.Equal(2, fragment.SelectionSet.Selections.Count);
    }

    [Theory]
    [InlineData("{ field { nested }")]
    [InlineData("{ alias: }")]
    [InlineData("{ field(arg:) }")]
    [InlineData("{ field() }")]
    [InlineData("{ ... }")]
    [InlineData("fragment F Type { field }")]
    [InlineData("fragment F on { field }")]
    [InlineData("{ ... on Type }")]
    public void RejectsMissingSelectionAndFragmentComponents(string source)
    {
        Assert.Throws<GraphQLSyntaxException>(() => GraphQLParser.Parse(new SourceText(source.AsMemory())));
    }

    [Fact]
    public void ParsesAnonymousInlineFragmentsAndOperationDirectives()
    {
        const string source = "query @cache { ... @include(if: true) { field } }";
        var operation = Assert.IsType<OperationDefinitionNode>(Assert.Single(GraphQLParser.Parse(new SourceText(source.AsMemory())).Definitions));

        Assert.Null(operation.Name);
        Assert.Equal("cache", Assert.Single(operation.Directives).Name.Value.ToString());
        var inline = Assert.IsType<InlineFragmentNode>(Assert.Single(operation.SelectionSet.Selections));
        Assert.Null(inline.TypeCondition);
    }

    [Fact]
    public void ParsesNestedTypeReferencesValuesAndConstantDefaults()
    {
        const string source = "query Q($data: [[Int!]!]! = [[1], []]) { field(value: { a: [true, null, ENUM], text: \"ok\" }) }";
        var operation = Assert.IsType<OperationDefinitionNode>(Assert.Single(GraphQLParser.Parse(new SourceText(source.AsMemory())).Definitions));
        var variable = Assert.Single(operation.VariableDefinitions);
        var outerNonNull = Assert.IsType<NonNullTypeNode>(variable.Type);
        var list = Assert.IsType<ListTypeNode>(outerNonNull.Type);
        Assert.IsType<ListTypeNode>(Assert.IsType<NonNullTypeNode>(list.Type).Type);
        var defaultList = Assert.IsType<ListValueNode>(variable.DefaultValue);
        Assert.Equal(2, defaultList.Values.Count);
        Assert.Single(Assert.IsType<ListValueNode>(defaultList.Values[0]).Values);
        Assert.Empty(Assert.IsType<ListValueNode>(defaultList.Values[1]).Values);

        var field = Assert.IsType<FieldNode>(Assert.Single(operation.SelectionSet.Selections));
        var objectValue = Assert.IsType<ObjectValueNode>(field.Arguments[0].Value);
        var values = Assert.IsType<ListValueNode>(objectValue.Fields[0].Value).Values;
        Assert.IsType<BooleanValueNode>(values[0]);
        Assert.IsType<NullValueNode>(values[1]);
        Assert.IsType<EnumValueNode>(values[2]);
    }

    [Theory]
    [InlineData("query Q($x: Int = $other) { field }")]
    [InlineData("query Q($x: Int = [1, $other]) { field }")]
    [InlineData("query Q($x: Int!!) { field }")]
    [InlineData("{ field(value: [1, 2) }")]
    [InlineData("{ field(value: { a 1 }) }")]
    [InlineData("query Q($x: [Int) { field }")]
    public void RejectsVariablesInConstantsAndMalformedValuesOrTypes(string source)
    {
        var error = Assert.Throws<GraphQLSyntaxException>(() => GraphQLParser.Parse(new SourceText(source.AsMemory())));
        Assert.InRange(error.Position, 0, source.Length);
    }

    [Fact]
    public void AllowsVariablesOnlyInExecutableValues()
    {
        const string source = "query Q($x: Int) { field(values: [$x, { nested: $x }]) }";
        var operation = Assert.IsType<OperationDefinitionNode>(Assert.Single(GraphQLParser.Parse(new SourceText(source.AsMemory())).Definitions));
        var field = Assert.IsType<FieldNode>(Assert.Single(operation.SelectionSet.Selections));
        var list = Assert.IsType<ListValueNode>(field.Arguments[0].Value);
        Assert.IsType<VariableNode>(list.Values[0]);
        Assert.IsType<VariableNode>(Assert.IsType<ObjectValueNode>(list.Values[1]).Fields[0].Value);
    }
}
