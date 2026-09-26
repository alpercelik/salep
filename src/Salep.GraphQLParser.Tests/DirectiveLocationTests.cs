using Xunit;

namespace Salep.GraphQLParser.Tests;

public sealed class DirectiveLocationTests
{
    [Fact]
    public void StandardLocationsHaveSpecNamesAndParseByExactName()
    {
        var values = new[]
        {
            DirectiveLocation.ArgumentDefinition, DirectiveLocation.DirectiveDefinition, DirectiveLocation.Enum,
            DirectiveLocation.EnumValue, DirectiveLocation.Field, DirectiveLocation.FieldDefinition,
            DirectiveLocation.FragmentDefinition, DirectiveLocation.FragmentSpread, DirectiveLocation.InlineFragment,
            DirectiveLocation.InputFieldDefinition, DirectiveLocation.InputObject, DirectiveLocation.Interface,
            DirectiveLocation.Mutation, DirectiveLocation.Object, DirectiveLocation.Query, DirectiveLocation.Scalar,
            DirectiveLocation.Schema, DirectiveLocation.Subscription, DirectiveLocation.Union,
            DirectiveLocation.VariableDefinition,
        };

        Assert.Equal(20, values.Select(value => value.Value).Distinct(StringComparer.Ordinal).Count());
        Assert.Contains("QUERY", values.Select(value => value.Value));
        Assert.Contains("INPUT_FIELD_DEFINITION", values.Select(value => value.Value));
        Assert.All(values, value =>
        {
            Assert.True(DirectiveLocation.IsValidName(value.Value));
            Assert.True(DirectiveLocation.TryParse(value.Value, out var parsed));
            Assert.Equal(value, parsed);
            Assert.Equal(value.Value, value.ToString());
        });
    }

    [Theory]
    [InlineData("query")]
    [InlineData("UNKNOWN")]
    [InlineData("")]
    public void LocationNamesAreCaseSensitiveAndRejectUnknownValues(string value)
    {
        Assert.False(DirectiveLocation.IsValidName(value));
        Assert.False(DirectiveLocation.TryParse(value, out var location));
        Assert.Null(location);
    }
}
