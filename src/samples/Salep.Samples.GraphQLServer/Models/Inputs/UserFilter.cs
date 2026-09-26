using HotChocolate;
using HotChocolate.Types;
using HotChocolate.Types.Relay;
using Salep.Samples.GraphQLServer.Models;

namespace Salep.Samples.GraphQLServer.Models.Inputs;

[GraphQLName("UserFilter")]
public sealed class UserFilter
{
    public Role[] Roles { get; set; } = [Role.USER];
    [ID]
    public string[] Ids { get; set; } = ["1", "2"];
}
