using Salep.GraphQLParser;
using Salep.ClientGenerator.Config;
using Salep.ClientGenerator.Model;
using Salep.ClientGenerator.Emission.Client;

namespace Salep.ClientGenerator.Emission;

// File-level orchestration only. Each component owns a coherent part of the generated API.
internal sealed class GraphQlClientEmitter
{
    private readonly GeneratorConfig _config;
    private readonly ClientNames _names;
    private readonly OperationVariablePolicies _variables;
    private readonly bool _registerConverters;

    public GraphQlClientEmitter(GeneratorConfig config, string operationInterfaceTypeName = "IGraphQLOperation",
        IReadOnlyList<FragmentDefinitionNode>? fragments = null, bool? registerUnionConverters = null)
    {
        _config = config;
        _names = ClientNames.From(config, operationInterfaceTypeName);
        _variables = new(config, fragments ?? []);
        _registerConverters = registerUnionConverters ?? true;
    }

    public string GenerateClient(IReadOnlyList<OperationDefinitionNode> operations)
    {
        List<string> usings = ["System", "System.Collections.Generic", "System.IO", "System.Linq", "System.Net.Http", "System.Net",
            "System.Runtime.CompilerServices", "System.Text", "System.Text.Json", "System.Text.Json.Serialization", "System.Threading", "System.Threading.Tasks"];
        if (_config.RequiresNodaTime()) usings.AddRange(["NodaTime", "NodaTime.Serialization.SystemTextJson"]);
        return RoslynEmitter.EmitFile(_config, _config.GeneratedNamespace, usings,
            [new HttpTransportSyntax(_config, _names, _registerConverters).Generate(),
                .. new OperationApiSyntax(_names, operations.Select(_variables.Apply).ToArray()).Generate()]);
    }

    public string GenerateSharedTypes(bool includeOperationInterface, bool includeCoreSharedTypes)
        => RoslynEmitter.EmitFile(_config, _config.GeneratedNamespace, ["System.Text.Json"],
            SharedTypesSyntax.Generate(includeOperationInterface, includeCoreSharedTypes));

    public string GenerateSample(IReadOnlyList<OperationDefinitionNode> operations, SchemaModel schema)
        => RoslynEmitter.EmitFile(_config, _config.GeneratedNamespace, ["System", "System.Collections.Generic", "System.Net.Http", "System.Threading.Tasks"],
            [new OperationSampleSyntax(_names, schema).Generate(operations.Select(_variables.Apply))]);
}
