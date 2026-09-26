using HotChocolate.Language;
using HotChocolate.Types;

namespace Salep.Samples.GraphQLServer.Resolvers;

public sealed class MutationType : ObjectType<MutationResolvers>
{
    protected override void Configure(IObjectTypeDescriptor<MutationResolvers> descriptor)
    {
        descriptor.Name("Mutation");
        descriptor.Field(mutation => mutation.CreatePost(null!))
            .Directive("auth", new ArgumentNode("role", new EnumValueNode("ADMIN")));
    }
}
