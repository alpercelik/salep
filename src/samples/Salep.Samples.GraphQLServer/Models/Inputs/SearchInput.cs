namespace Salep.Samples.GraphQLServer.Models.Inputs;

public sealed class SearchInput
{
    public required string Text { get; set; }
    public string[] Tags { get; set; } = ["x"];
    public MetadataInput? Meta { get; set; } = new() { Rating = 3, Flags = ["z"] };
}
