using HotChocolate.Types;

namespace Salep.Samples.GraphQLServer.Models;

[InterfaceType("Timestamped")]
public interface ITimestamped : INode { DateTimeOffset CreatedAt { get; } }
