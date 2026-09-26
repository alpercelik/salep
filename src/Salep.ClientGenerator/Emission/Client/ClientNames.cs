using Salep.ClientGenerator.Config;

namespace Salep.ClientGenerator.Emission.Client;

internal sealed record ClientNames(string Client, string OperationContract)
{
    public string OperationsBase => Client.EndsWith("Client", StringComparison.Ordinal) ? Client[..^"Client".Length] : Client;
    public string OperationsInterface => $"I{OperationsBase}Operations";
    public string OperationsClass => $"{OperationsBase}Operations";
    public static ClientNames From(GeneratorConfig config, string operationContract) => new(config.ClientClassName, operationContract);
}
