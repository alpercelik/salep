using Microsoft.CodeAnalysis.CSharp.Syntax;
using Salep.GraphQLParser;
using Salep.ClientGenerator.Config;
using Salep.ClientGenerator.Model;
using Salep.ClientGenerator.Emission.Client;
using Salep.ClientGenerator.Emission.Testing;

namespace Salep.ClientGenerator.Emission;

internal sealed class TestsEmitter
{
    private readonly GeneratorConfig _config;
    private readonly ClientNames _names;
    private readonly OperationTestSyntax _operations;

    public TestsEmitter(GeneratorConfig config, string operationInterfaceTypeName = "IGraphQLOperation", IReadOnlyList<FragmentDefinitionNode>? fragments = null)
    {
        _config = config;
        _names = ClientNames.From(config, operationInterfaceTypeName);
        _operations = new(config, _names, new OperationVariablePolicies(config, fragments ?? []));
    }
    public string GenerateHttpHandler() => Emit(HttpHandlerTestSyntax.Generate(), ["System", "System.Net", "System.Net.Http", "System.Threading", "System.Threading.Tasks"]);
    public string GeneratePayloadTests() => Emit(new PayloadTestSyntax(_config, _names).Generate(),
        ["System", "System.Collections.Generic", "System.Net", "System.Net.Http", "System.Net.Http.Headers", "System.Text.Json", "System.Text", "System.Threading.Tasks", _config.GeneratedNamespace, "NSubstitute", "Shouldly", "Xunit"]);
    public string GenerateOperationsResponseTests(IReadOnlyList<OperationDefinitionNode> operations, SchemaModel schema) => Emit(_operations.Responses(operations, schema),
        ["System", "System.Collections.Generic", "System.Net.Http", "System.Text.Json", "System.Threading.Tasks", _config.GeneratedNamespace, "Shouldly", "Xunit"]);
    public string GenerateOperationsMetadataTests(IReadOnlyList<OperationDefinitionNode> operations) => Emit(_operations.Metadata(operations), [_config.GeneratedNamespace, "Shouldly", "Xunit"]);
    public string GenerateOperationsSampleTests() => Emit(_operations.Sample(), ["System", "System.Net.Http", "System.Threading.Tasks", _config.GeneratedNamespace, "Xunit"]);
    public string GenerateUnionConverterTests(SchemaModel schema)
    {
        List<string> usings = ["System", "System.Collections.Generic", "System.Text.Json", "System.Text.Json.Nodes", "System.Text.Json.Serialization", _config.GeneratedNamespace, "Shouldly", "Xunit"];
        if (_config.RequiresNodaTime()) usings.AddRange(["NodaTime", "NodaTime.Serialization.SystemTextJson"]);
        return Emit(new UnionTestSyntax(schema).Generate(), usings);
    }
    private string Emit(MemberDeclarationSyntax member, IEnumerable<string> usings) => RoslynEmitter.EmitFile(_config, _config.TestsNamespace, usings, [member]);
}
