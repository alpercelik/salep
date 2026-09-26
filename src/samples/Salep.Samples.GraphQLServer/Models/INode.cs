using HotChocolate.Types;
using HotChocolate.Types.Relay;

namespace Salep.Samples.GraphQLServer.Models;

[InterfaceType("Node")]
public interface INode { [ID] string Id { get; } }
