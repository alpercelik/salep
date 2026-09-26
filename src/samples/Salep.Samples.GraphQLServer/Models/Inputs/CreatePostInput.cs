namespace Salep.Samples.GraphQLServer.Models.Inputs;

public sealed class CreatePostInput
{
    public required string Title { get; set; }
    public string? Content { get; set; } = "default";
    public string?[]? Tags { get; set; } = ["a", "b"];
    public MetadataInput? Meta { get; set; } = new() { Rating = 4, Flags = ["m"] };
    public bool? Notify { get; set; } = true;
}
