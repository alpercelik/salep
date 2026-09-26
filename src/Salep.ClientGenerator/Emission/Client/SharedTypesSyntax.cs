using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using static Salep.ClientGenerator.Emission.Syntax.Cs;

namespace Salep.ClientGenerator.Emission.Client;

internal static class SharedTypesSyntax
{
    public static IEnumerable<MemberDeclarationSyntax> Generate(bool operationInterface, bool coreTypes)
    {
        if (operationInterface)
            yield return SyntaxFactory.InterfaceDeclaration("IGraphQLOperation").WithModifiers(Public)
                .WithTypeParameterList(TypeParameters("TResponse", "TVariables")).WithMembers(SyntaxFactory.List<MemberDeclarationSyntax>([
                    ReadOnlyProperty("string", "OperationName", isPublic: false), ReadOnlyProperty("string", "Query", isPublic: false),
                    ReadOnlyProperty("TVariables?", "Variables", isPublic: false)]));
        if (!coreTypes) yield break;
        yield return PositionalRecord("GraphQLRequest", [Parameter("string", "Query"), Parameter("string", "OperationName"), Parameter("TVariables?", "Variables")])
            .WithTypeParameterList(TypeParameters("TVariables"));
        yield return Record("GraphQLResponse", [Property("TResponse?", "Data"), Property("GraphQLError[]?", "Errors")]).WithTypeParameterList(TypeParameters("TResponse"));
        yield return Record("GraphQLError", [Property("string", "Message", initializer: Name("string.Empty")), Property("GraphQLErrorLocation[]?", "Locations"),
            Property("JsonElement[]?", "Path"), Property("JsonElement?", "Extensions")]);
        yield return Record("GraphQLErrorLocation", [Property("int", "Line"), Property("int", "Column")]);
    }

    public static RecordDeclarationSyntax PositionalRecord(string name, IEnumerable<ParameterSyntax> parameters, SyntaxTokenList? modifiers = null)
        => SyntaxFactory.RecordDeclaration(SyntaxKind.RecordDeclaration, SyntaxFactory.Token(SyntaxKind.RecordKeyword), Identifier(name))
            .WithModifiers(modifiers ?? PublicSealed).WithParameterList(SyntaxFactory.ParameterList(SyntaxFactory.SeparatedList(parameters)))
            .WithSemicolonToken(SyntaxFactory.Token(SyntaxKind.SemicolonToken));
}
