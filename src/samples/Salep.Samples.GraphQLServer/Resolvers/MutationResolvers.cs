using HotChocolate.Subscriptions;
using HotChocolate.Types.Relay;
using Salep.Samples.GraphQLServer.Models;
using Salep.Samples.GraphQLServer.Models.Inputs;
using Salep.Samples.GraphQLServer.Services;

namespace Salep.Samples.GraphQLServer.Resolvers;

public sealed class MutationResolvers(SampleData data, ITopicEventSender eventSender)
{
    public async Task<Post> CreatePost(CreatePostInput input)
    {
        var post = data.CreatePost(input);
        await eventSender.SendAsync(nameof(SubscriptionResolvers.PostCreated), post);
        return post;
    }

    public Post? UpdatePost([ID] string id, CreatePostInput input) => data.UpdatePost(id, input);

    public IReadOnlyList<Post> DeletePosts([ID] string[] ids) => data.DeletePosts(ids);
}
