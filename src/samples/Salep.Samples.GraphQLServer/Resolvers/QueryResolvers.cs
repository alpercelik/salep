using HotChocolate.Types.Relay;
using Salep.Samples.GraphQLServer.Models;
using Salep.Samples.GraphQLServer.Models.Inputs;
using Salep.Samples.GraphQLServer.Services;

namespace Salep.Samples.GraphQLServer.Resolvers;

public sealed class QueryResolvers(SampleData data)
{
    public Salep.Samples.GraphQLServer.Models.INode? Node([ID] string id) => data.Node(id);

    public IReadOnlyList<User> Users(Role role = Role.USER, UserFilter? filter = null)
    {
        IEnumerable<User> users = data.Users.Where(user => user.Role == role);
        if (filter is not null)
        {
            users = users.Where(user => filter.Roles.Contains(user.Role) && filter.Ids.Contains(user.Id));
        }
        return [.. users];
    }

    public IReadOnlyList<ISearchResult> Search(string text, SearchInput? options = null)
    {
        var searchText = options?.Text ?? text;
        return
        [
            .. data.Users.Where(user => user.Name.Contains(searchText, StringComparison.OrdinalIgnoreCase))
                .Cast<ISearchResult>(),

            .. data.Posts.Where(post => post.Title.Contains(searchText, StringComparison.OrdinalIgnoreCase))
        ];
    }

    public IReadOnlyList<Post?> Posts([ID] string[] ids, bool allowMissing = false) =>
    [
        .. ids.Select(id => data.Posts.FirstOrDefault(post => post.Id == id))
            .Where(post => allowMissing || post is not null)
    ];

    public string[] EchoList(string[]? values = null) => values ?? ["a", "b"];
}
