using HotChocolate;
using HotChocolate.Types;

namespace Salep.Samples.GraphQLServer.Models;

[InterfaceType("Named")]
public interface INamed
{
    string Name { get; }
    [GraphQLDeprecated("Use name")]
    string? LegacyName { get; }
}
