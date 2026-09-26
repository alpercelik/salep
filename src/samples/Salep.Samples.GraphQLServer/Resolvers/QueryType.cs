using HotChocolate.Language;
using HotChocolate.Types;
using Salep.Samples.GraphQLServer.Models;

namespace Salep.Samples.GraphQLServer.Resolvers;

public sealed class QueryType : ObjectType<QueryResolvers>
{
    protected override void Configure(IObjectTypeDescriptor<QueryResolvers> descriptor)
    {
        descriptor.Name("Query");
        descriptor.Field(query => query.Users(Role.USER, null!))
            .Directive("tag", new ArgumentNode("name", new StringValueNode("users")));
    }
}
