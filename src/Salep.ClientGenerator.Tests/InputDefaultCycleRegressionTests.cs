using Salep.ClientGenerator.Model;
using Salep.GraphQLParser;
using Xunit;

namespace Salep.ClientGenerator.Tests;

public sealed class InputDefaultCycleRegressionTests
{
    [Theory]
    [InlineData("input A { a: A = {} }")]
    [InlineData("input A { a: [A] = [{}] }")]
    [InlineData("input A { a: [A] = {} }")]
    [InlineData("input A { a: [[A]] = [[{}]] }")]
    [InlineData("input A { b: B = {} } input B { a: A = {} }")]
    [InlineData("input A { b: B = { a: {} } } input B { a: A }")]
    [InlineData("input A { a: [A] = [null, {}] }")]
    [InlineData("input A { leaf: A = null branch: A = { branch: {} } }")]
    [InlineData("input A { value: String } extend input A { a: A = {} }")]
    public void Recursive_default_expansion_is_rejected(string inputDefinitions)
    {
        var error = Assert.Throws<InvalidDataException>(() => Validate(inputDefinitions));
        Assert.Contains("Input default cycle", error.Message);
    }

    [Theory]
    [InlineData("input A { a: A }")]
    [InlineData("input A { a: [A!]! }")]
    [InlineData("input A { a: A = null }")]
    [InlineData("input A { a: A = { a: null } }")]
    [InlineData("input A { a: [A] = [] }")]
    [InlineData("input A { a: [A] = [null] }")]
    [InlineData("input A { a: [A] = [{ a: [] }, null] }")]
    [InlineData("input A { a: [[A]] = [[{ a: [[]] }]] }")]
    [InlineData("input A { b: B = { a: null } } input B { a: A = null }")]
    [InlineData("input A { left: B = {} right: B = {} } input B { a: A = null }")]
    [InlineData("input A { value: String } extend input A { a: A = { a: null } }")]
    public void Finite_or_absent_defaults_are_allowed(string inputDefinitions) => Validate(inputDefinitions);

    private static void Validate(string inputDefinitions)
    {
        var schema = GraphQlModelFactory.CreateSchema(global::Salep.GraphQLParser.GraphQLParser.Parse(new SourceText((inputDefinitions + " type Query { value: String }").AsMemory())));
        var document = GraphQlModelFactory.CreateExecutable(schema, [global::Salep.GraphQLParser.GraphQLParser.Parse(new SourceText("{ value }".AsMemory()))]);
        new GraphQlDocumentValidator(schema, document).Validate();
    }
}
