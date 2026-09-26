using HotChocolate;
using HotChocolate.Types;
using Salep.Samples.GraphQLServer.Models;

namespace Salep.Samples.GraphQLServer.Resolvers;

public sealed class SubscriptionResolvers
{
    [Subscribe]
    public Post PostCreated([EventMessage] Post post, Role role = Role.USER) => post;

    [Subscribe]
    public Comment CommentAdded([EventMessage] Comment comment) => comment;
}
