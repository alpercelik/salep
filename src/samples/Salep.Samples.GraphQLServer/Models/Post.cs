using HotChocolate;
using HotChocolate.Types.Relay;

namespace Salep.Samples.GraphQLServer.Models;

public sealed record Post([property: ID] string Id, string Title, string? Content, [property: GraphQLIgnore] string AuthorId, string?[]? Tags, DateTimeOffset CreatedAt)
    : INode, ITimestamped, ISearchResult
{
    public User Author => new(AuthorId, "Ada", Role.ADMIN, "ada@example.com", CreatedAt);
    public object? Metadata => new { origin = "sample" };
}
