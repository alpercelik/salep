using HotChocolate.Types;

namespace Salep.Samples.GraphQLServer.Models.Inputs;

public sealed class UserFilterInputType : InputObjectType<UserFilter>
{
    protected override void Configure(IInputObjectTypeDescriptor<UserFilter> descriptor) => descriptor.Name("UserFilter");
}
