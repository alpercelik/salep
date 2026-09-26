using HotChocolate.Types;

namespace Salep.Samples.GraphQLServer.Directives;

public sealed class OpTagDirectiveType : DirectiveType
{
    protected override void Configure(IDirectiveTypeDescriptor descriptor)
    {
        descriptor.Name("opTag");
        descriptor.Argument("name").Type<StringType>().DefaultValue("op");
        descriptor.Location(
            DirectiveLocation.Query
            | DirectiveLocation.Mutation
            | DirectiveLocation.Subscription
            | DirectiveLocation.Field
            | DirectiveLocation.FragmentDefinition
            | DirectiveLocation.FragmentSpread
            | DirectiveLocation.InlineFragment);
        descriptor.Repeatable();
    }
}
