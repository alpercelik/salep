using HotChocolate.Language;
using HotChocolate.Types;

namespace Salep.Samples.GraphQLServer.Models;

public sealed class RoleType : EnumType<Role>
{
    protected override void Configure(IEnumTypeDescriptor<Role> descriptor)
    {
        descriptor.Directive("tag", new ArgumentNode("name", new StringValueNode("enum")));
        descriptor.Value(Role.GUEST).Deprecated("Use USER");
        descriptor.Value(Role.SUPERADMIN)
            .Directive("tag", new ArgumentNode("name", new StringValueNode("enumValue")));
    }
}
