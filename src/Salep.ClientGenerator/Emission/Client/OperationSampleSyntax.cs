using SyntaxKind = Microsoft.CodeAnalysis.CSharp.SyntaxKind;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Salep.GraphQLParser;
using Salep.ClientGenerator.Model;
using Salep.ClientGenerator.Utilities;
using static Salep.ClientGenerator.Emission.Syntax.Cs;

namespace Salep.ClientGenerator.Emission.Client;

internal sealed class OperationSampleSyntax(ClientNames names, SchemaModel schema)
{
    public MemberDeclarationSyntax Generate(IEnumerable<OperationDefinitionNode> operations)
    {
        var body = new List<StatementSyntax>
        {
            Local("client", New(names.Client, Name("httpClient"), Name("endpoint"))),
            Local("api", New(names.OperationsClass, Name("client")), type: names.OperationsInterface)
        };
        foreach (var operation in operations.Where(operation => !string.IsNullOrWhiteSpace(operation.Name?.Value)))
        {
            var name = CSharpNaming.ToTypeName(operation.Name!.Value);
            var arguments = new List<ExpressionSyntax>();
            if (operation.VariableDefinitions.Count > 0)
            {
                body.Add(Local($"{name}Vars", NewObject($"{name}Variables", operation.VariableDefinitions.Select(variable =>
                    Assign(CSharpNaming.ToPropertyName(variable.Variable.Name.Value), Value(variable.Type, 0))))));
                arguments.Add(Name($"{name}Vars"));
            }
            body.Add(Local($"response{name}", Await(Call($"api.{name}Async", arguments.ToArray()), configureAwait: false)));
        }
        return Class("OperationsSample", [Method("Task", "RunAsync", [Parameter("HttpClient", "httpClient"), Parameter("Uri", "endpoint")], body,
            Modifiers(SyntaxKind.PublicKeyword, SyntaxKind.StaticKeyword, SyntaxKind.AsyncKeyword))], PublicStatic);
    }

    private ExpressionSyntax Value(ITypeNode type, int depth) => new Testing.SampleValues(schema).Input(type, depth);
}
