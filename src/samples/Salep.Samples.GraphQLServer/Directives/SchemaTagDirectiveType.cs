using HotChocolate.Types;

namespace Salep.Samples.GraphQLServer.Directives;

public sealed class SchemaTagDirectiveType : DirectiveType
{
    protected override void Configure(IDirectiveTypeDescriptor descriptor)
    {
        descriptor.Name("schemaTag");
        descriptor.Argument("name").Type<NonNullType<StringType>>();
        descriptor.Location(DirectiveLocation.Schema);
        descriptor.Repeatable();
    }
}
