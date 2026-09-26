using HotChocolate;
using HotChocolate.Types.Relay;

namespace Salep.Samples.GraphQLServer.Models;

public sealed record Comment([property: ID] string Id, string Body, [property: GraphQLIgnore] string AuthorId, DateTimeOffset CreatedAt)
    : INode, ITimestamped, ISearchResult
{
    public User Author => new(AuthorId, "Ada", Role.ADMIN, "ada@example.com", CreatedAt);
}
