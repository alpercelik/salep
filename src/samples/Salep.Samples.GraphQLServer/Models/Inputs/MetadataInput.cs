namespace Salep.Samples.GraphQLServer.Models.Inputs;

public sealed class MetadataInput
{
    public int Rating { get; set; } = 5;
    public string[] Flags { get; set; } = ["x", "y"];
}
