using HotChocolate.Types;

namespace Salep.Samples.GraphQLServer.Directives;

public sealed class UpperDirectiveType : DirectiveType
{
    protected override void Configure(IDirectiveTypeDescriptor descriptor)
    {
        descriptor.Name("upper");
        descriptor.Location(DirectiveLocation.FieldDefinition);
    }
}
