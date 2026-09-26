using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Salep.GraphQLParser;
using Salep.ClientGenerator.Model;
using Salep.ClientGenerator.Config;
using Salep.ClientGenerator.Emission.Client;
using Salep.ClientGenerator.Utilities;
using static Salep.ClientGenerator.Emission.Syntax.Cs;
using static Salep.ClientGenerator.Emission.Testing.TestSyntax;
using SyntaxKind = Microsoft.CodeAnalysis.CSharp.SyntaxKind;

namespace Salep.ClientGenerator.Emission.Testing;

internal sealed class OperationTestSyntax(GeneratorConfig config, ClientNames names, OperationVariablePolicies variables)
{
    private readonly TestSyntax _test = new(config, names);
    public MemberDeclarationSyntax Responses(IReadOnlyList<OperationDefinitionNode> operations, SchemaModel schema)
    {
        var values = new SampleValues(schema);
        var methods = new List<MemberDeclarationSyntax>();
        foreach (var operation in operations.Where(operation => !string.IsNullOrWhiteSpace(operation.Name?.Value)))
        {
            var effective = variables.Apply(operation);
            var name = CSharpNaming.ToTypeName(operation.Name!.Value);
            var arguments = new List<ExpressionSyntax>();
            if (effective.VariableDefinitions.Count > 0)
                arguments.Add(NewObject($"{name}Variables", effective.VariableDefinitions.Select(variable => Assign(CSharpNaming.ToPropertyName(variable.Variable.Name.Value), values.Input(variable.Type)))));
            arguments.Add(TestCancellation);
            var properties = operation.SelectionSet.Selections.OfType<FieldNode>().Select(field =>
            {
                var type = schema.GetFieldTypeNode(schema.GetRootTypeName(operation.Operation), field.Name.Value);
                var json = type is null ? "null" : SampleJson.BuildSampleJson(type, schema, 0, []);
                return $"\"{field.Alias?.Value ?? field.Name.Value}\":{json}";
            });
            var data = "{" + string.Join(",", properties) + "}";
            methods.Add(Fact($"{name}_Deserializes_Response",
            [
                Local("json", Call("WrapData", _test.Json(data))),
                Local("response", Await(Call("ExecuteOperationAsync", Lambda("api", Call($"api.{name}Async", arguments.ToArray())), Name("json")), configureAwait: false)),
                Assert("response", "ShouldNotBeNull")
            ], async: true));
        }
        methods.Add(Method("Task<GraphQLResponse<TResponse>>", "ExecuteOperationAsync",
            [Parameter($"Func<{names.OperationsInterface}, Task<GraphQLResponse<TResponse>>>", "call"), Parameter("string", "json")],
            [.. _test.Client(Name("json")), Local("api", New(names.OperationsClass, Name("client"))),
                Local("response", Await(Call("call", Name("api")), configureAwait: false)), Assert("response", "ShouldNotBeNull"), Return(Name("response"))],
            Modifiers(SyntaxKind.PrivateKeyword, SyntaxKind.StaticKeyword, SyntaxKind.AsyncKeyword), "TResponse"));
        methods.Add(Method("string", "WrapData", [Parameter("string", "dataJson")], [Return(Add(Add(String("{\"data\":"), Name("dataJson")), String("}")))], Modifiers(SyntaxKind.PrivateKeyword, SyntaxKind.StaticKeyword)));
        return Class("OperationsResponseTests", methods);
    }

    public MemberDeclarationSyntax Metadata(IReadOnlyList<OperationDefinitionNode> operations)
    {
        var methods = new List<MemberDeclarationSyntax>();
        foreach (var operation in operations.Where(operation => !string.IsNullOrWhiteSpace(operation.Name?.Value)))
        {
            var name = CSharpNaming.ToTypeName(operation.Name!.Value);
            var assertions = new List<StatementSyntax>
            {
                Local("variables", New($"{name}Variables")), Local("response", New($"{name}Response")), Local("operation", New($"{name}Operation", Name("variables"))),
                Assert("operation.OperationName", "ShouldBe", String(operation.Name.Value)), Assert("operation.Query", "ShouldContain", String(operation.Name.Value)),
                Assert("operation.Variables", "ShouldBeSameAs", Name("variables")), Assert("response", "ShouldNotBeNull")
            };
            // Test the actual policy outcome for every operation, without schema-specific names.
            var retained = variables.Apply(operation).VariableDefinitions.Select(variable => variable.Variable.Name.Value).ToHashSet(StringComparer.Ordinal);
            var removedVariables = operation.VariableDefinitions.Where(variable => !retained.Contains(variable.Variable.Name.Value)).ToArray();
            methods.Add(Fact($"{name}_Operation_Metadata", assertions));
            if (removedVariables.Length > 0)
                methods.Add(Fact($"{name}_Omits_Variable_Definition",
                [
                    Local("operation", New($"{name}Operation")),
                    // Match complete GraphQL variable names: removing $id must not reject retained $id2.
                    .. removedVariables.Select(variable => Assert(Call("System.Text.RegularExpressions.Regex.IsMatch",
                        Name("operation.Query"), String(@"\$" + variable.Variable.Name.Value + "(?![_0-9A-Za-z])")), "ShouldBeFalse"))
                ]));
        }
        return Class("OperationsMetadataTests", methods);
    }

    public MemberDeclarationSyntax Sample() => Class("OperationsSampleTests", [Fact("OperationsSample_RunAsync_Completes",
    [
        Local("handler", New("TestHttpMessageHandler", Lambda("_", Call("TestHttpMessageHandler.JsonResponse", _test.Json("{\"data\":null}"))))),
        Local("httpClient", New("HttpClient", Name("handler"))),
        Statement(Await(Call("OperationsSample.RunAsync", Name("httpClient"), New("Uri", String("https://example.com/graphql"))), configureAwait: false))
    ], async: true)]);
}
