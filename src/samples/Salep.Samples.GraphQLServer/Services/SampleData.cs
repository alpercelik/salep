using System.Globalization;
using Salep.Samples.GraphQLServer.Models;
using Salep.Samples.GraphQLServer.Models.Inputs;
using SampleNode = Salep.Samples.GraphQLServer.Models.INode;

namespace Salep.Samples.GraphQLServer.Services;

public sealed class SampleData
{
    private readonly List<User> _users =
    [
        new("1", "Ada", Role.ADMIN, "ada@example.com", DateTimeOffset.Parse("2025-01-01T00:00:00Z", CultureInfo.InvariantCulture)),
        new("2", "Grace", Role.USER, "grace@example.com", DateTimeOffset.Parse("2025-02-01T00:00:00Z", CultureInfo.InvariantCulture))
    ];

    private readonly List<Post> _posts =
    [
        new("1", "Welcome", "A sample post", "1", ["sample"], DateTimeOffset.Parse("2025-03-01T00:00:00Z", CultureInfo.InvariantCulture))
    ];

    public IReadOnlyList<User> Users => _users;
    public IReadOnlyList<Post> Posts => _posts;

    public SampleNode? Node(string id) =>
        (SampleNode?)_users.FirstOrDefault(user => user.Id == id)
        ?? _posts.FirstOrDefault(post => post.Id == id);

    public Post CreatePost(CreatePostInput input)
    {
        var post = new Post((_posts.Count + 1).ToString(CultureInfo.InvariantCulture), input.Title, input.Content, "1", input.Tags ?? [], DateTimeOffset.UtcNow);
        _posts.Add(post);
        return post;
    }

    public Post? UpdatePost(string id, CreatePostInput input)
    {
        var index = _posts.FindIndex(post => post.Id == id);
        if (index < 0) return null;
        var updated = _posts[index] with { Title = input.Title, Content = input.Content, Tags = input.Tags ?? [] };
        _posts[index] = updated;
        return updated;
    }

    public IReadOnlyList<Post> DeletePosts(IEnumerable<string> ids)
    {
        var idSet = ids.ToHashSet(StringComparer.Ordinal);
        var selected = _posts.Where(post => idSet.Contains(post.Id)).ToArray();
        _posts.RemoveAll(post => idSet.Contains(post.Id));
        return selected;
    }
}
