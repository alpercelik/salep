using HotChocolate.Language;
using HotChocolate.Types;

namespace Salep.Samples.GraphQLServer.Models.Inputs;

public sealed class MetadataInputType : InputObjectType<MetadataInput>
{
    protected override void Configure(IInputObjectTypeDescriptor<MetadataInput> descriptor)
    {
        descriptor.Directive("tag", new ArgumentNode("name", new StringValueNode("inputObj")));
    }
}
