using System.Text.Json;
using System.Text.Json.Nodes;
using Salep.ClientGenerator.Config;
using Salep.ClientGenerator.Generation;

namespace Salep.ClientGenerator.Tests;

internal sealed class ContractFixture : IDisposable
{
    public string Root { get; } = Path.Combine(Path.GetTempPath(), "salep_contract_" + Guid.NewGuid().ToString("N"));
    public string Schema => Path.Combine(Root, "schema.graphql");
    public ContractFixture() { Directory.CreateDirectory(Root); File.WriteAllText(Schema, "type User { id: ID! } type Query { user: User }"); }
    public string Write(string name, object value)
    {
        var directory = Path.Combine(Root, name); Directory.CreateDirectory(directory);
        var path = Path.Combine(directory, "salep.json"); File.WriteAllText(path, JsonSerializer.Serialize(value)); return path;
    }
    public string Client(string name = "Client", string? parent = null, string? operations = null, bool native = false)
    {
        var node = new JsonObject { ["version"] = 1, ["kind"] = "client", ["schema"] = Schema, ["namespace"] = "Generated." + name,
            ["clientName"] = name + "Api", ["emitAgentInstructions"] = false, ["unionRepresentation"] = native ? "native" : "dunet" };
        if (parent is not null) node["baseClient"] = parent;
        var path = Write(name, node);
        if (operations is not null)
        { var dir = Path.Combine(Path.GetDirectoryName(path)!, "graphql"); Directory.CreateDirectory(dir); File.WriteAllText(Path.Combine(dir, "query.graphql"), operations); }
        return path;
    }
    public string Tests(string client, object? overrides = null)
    {
        var node = new JsonObject { ["version"] = 1, ["kind"] = "tests", ["client"] = client, ["emitAgentInstructions"] = false };
        if (overrides is not null) foreach (var pair in JsonSerializer.SerializeToNode(overrides)!.AsObject()) node[pair.Key] = pair.Value?.DeepClone();
        return Write(Path.GetFileName(Path.GetDirectoryName(client)) + "Tests", node);
    }

    public static void Change(string path, string key, object value)
    {
        var node = JsonNode.Parse(File.ReadAllText(path))!; node[key] = JsonSerializer.SerializeToNode(value); File.WriteAllText(path, node.ToJsonString());
    }

    public static string Output(string config) => ConfigurationResolver.Resolve(config).Output;
    public static SalepGeneratorResult Generate(string config) => SalepGenerator.Generate(new(config));
    public static GenerationManifest Manifest(string config) => GenerationManifest.Read(Output(config), config);
    public void Dispose() { if (Directory.Exists(Root)) Directory.Delete(Root, true); }
}
