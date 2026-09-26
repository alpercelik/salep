using HotChocolate.Types;
using Salep.Samples.GraphQLServer.Models;

namespace Salep.Samples.GraphQLServer.Directives;

public sealed class AuthDirectiveType : DirectiveType
{
    protected override void Configure(IDirectiveTypeDescriptor descriptor)
    {
        descriptor.Name("auth");
        descriptor.Argument("role").Type<RoleType>().DefaultValue(Role.USER);
        descriptor.Location(DirectiveLocation.Object | DirectiveLocation.FieldDefinition);
    }
}
