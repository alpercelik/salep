using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Salep.GraphQLParser;
using Salep.ClientGenerator.Utilities;
using static Salep.ClientGenerator.Emission.Syntax.Cs;

namespace Salep.ClientGenerator.Emission.Client;

internal sealed class OperationApiSyntax(ClientNames names, IReadOnlyList<OperationDefinitionNode> operations)
{
    public IEnumerable<MemberDeclarationSyntax> Generate()
    {
        var contracts = new List<MemberDeclarationSyntax>();
        var implementations = new List<MemberDeclarationSyntax>
        {
            Field(names.Client, "_client"), Constructor(names.OperationsClass, [Parameter(names.Client, "client")], Set("_client", Name("client")))
        };
        foreach (var operation in operations.Where(operation => !string.IsNullOrWhiteSpace(operation.Name?.Value)))
        {
            var name = CSharpNaming.ToTypeName(operation.Name!.Value);
            var parameters = new List<ParameterSyntax>();
            if (operation.VariableDefinitions.Count > 0) parameters.Add(Parameter($"{name}Variables", "variables"));
            parameters.Add(Cancellation());
            var returnType = $"Task<GraphQLResponse<{name}Response>>";
            contracts.Add(Method(returnType, $"{name}Async", parameters, null, default(SyntaxTokenList)));
            implementations.Add(Method(returnType, $"{name}Async", parameters,
            [
                Local("operation", New($"{name}Operation", operation.VariableDefinitions.Count > 0 ? [Name("variables")] : [])),
                Return(Call("_client.ExecuteAsync", Name("operation"), Name("cancellationToken")))
            ]));
        }
        return [SyntaxFactory.InterfaceDeclaration(Identifier(names.OperationsInterface)).WithModifiers(Public).WithMembers(SyntaxFactory.List(contracts)),
            Class(names.OperationsClass, implementations, bases: [names.OperationsInterface])];
    }
}
