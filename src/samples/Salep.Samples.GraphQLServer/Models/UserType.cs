using HotChocolate.Language;
using HotChocolate.Types;

namespace Salep.Samples.GraphQLServer.Models;

public sealed class UserType : ObjectType<User>
{
    protected override void Configure(IObjectTypeDescriptor<User> descriptor)
    {
        descriptor.Directive("tag", new ArgumentNode("name", new StringValueNode("user")));
        descriptor.Directive("auth", new ArgumentNode("role", new EnumValueNode(nameof(Role.ADMIN))));
        descriptor.Field(user => user.Name)
            .Directive("upper");
        descriptor.Field(user => user.Email)
            .Directive("auth", new ArgumentNode("role", new EnumValueNode(nameof(Role.ADMIN))));
    }
}
