using HotChocolate;
using HotChocolate.Types;
using HotChocolate.Types.Relay;

namespace Salep.Samples.GraphQLServer.Models;

public sealed record User([property: ID] string Id, string Name, Role Role, string Email, DateTimeOffset CreatedAt)
    : INode, INamed, IAccount, ITimestamped, ISearchResult
{
    public string? LegacyName => Name;
    public string? Nickname => Name;
    [GraphQLDeprecated("legacy")]
    public string? OldField => null;
    public object? Metadata => new { origin = "sample" };
    public IReadOnlyList<Post> Posts => [];
}
