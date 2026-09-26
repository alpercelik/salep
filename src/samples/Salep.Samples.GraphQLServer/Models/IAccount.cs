using HotChocolate.Types;

namespace Salep.Samples.GraphQLServer.Models;

[InterfaceType("Account")]
public interface IAccount : INode, INamed
{
    string Email { get; }
}
