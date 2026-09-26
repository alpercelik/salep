using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Editing;

namespace Salep.ClientGenerator.Emission.Syntax;

// Typed adapters over Roslyn. SyntaxGenerator owns common construction;
// SyntaxFactory handles CSharp-specific syntax absent from the language-neutral API.
internal static partial class Cs
{
    // The language service is stateless. No documents, project state or configuration
    // are retained, so concurrent generation shares only Roslyn services.
    private static readonly SyntaxGenerator Generator = CreateGenerator();

    private static SyntaxGenerator CreateGenerator()
    {
        using var workspace = new AdhocWorkspace();
        return SyntaxGenerator.GetGenerator(workspace, LanguageNames.CSharp);
    }

    public static ExpressionSyntax Name(string name)
    {
        var global = name.StartsWith("global::", StringComparison.Ordinal);
        var parts = (global ? name[8..] : name).Split('.');
        ExpressionSyntax expression = global
            ? SyntaxFactory.AliasQualifiedName(SyntaxFactory.IdentifierName("global"), SyntaxFactory.IdentifierName(Identifier(parts[0])))
            : SyntaxFactory.IdentifierName(Identifier(parts[0]));
        foreach (var part in parts.Skip(1)) expression = Member(expression, part);
        return expression;
    }

    public static MemberAccessExpressionSyntax Member(ExpressionSyntax expression, string name)
        => SyntaxFactory.MemberAccessExpression(SyntaxKind.SimpleMemberAccessExpression, expression, SyntaxFactory.IdentifierName(Identifier(name)));

    public static ExpressionSyntax GenericMember(ExpressionSyntax expression, string name, params TypeSyntax[] types)
        => SyntaxFactory.MemberAccessExpression(SyntaxKind.SimpleMemberAccessExpression, expression,
            SyntaxFactory.GenericName(Identifier(name)).WithTypeArgumentList(SyntaxFactory.TypeArgumentList(SyntaxFactory.SeparatedList(types))));

    public static InvocationExpressionSyntax Call(ExpressionSyntax expression, params ExpressionSyntax[] arguments)
        => Invoke(expression, arguments.Select(SyntaxFactory.Argument).ToArray());
    public static InvocationExpressionSyntax Call(string name, params ExpressionSyntax[] arguments) => Call(Name(name), arguments);
    public static InvocationExpressionSyntax Invoke(ExpressionSyntax expression, params ArgumentSyntax[] arguments)
        => (InvocationExpressionSyntax)Generator.InvocationExpression(expression, arguments);
    public static ExpressionSyntax GenericCall(string receiver, string name, IEnumerable<TypeSyntax> types, params ExpressionSyntax[] arguments)
        => Call(GenericMember(Name(receiver), name, types.ToArray()), arguments);
    public static ExpressionSyntax New(string type, params ExpressionSyntax[] arguments)
        => (ExpressionSyntax)Generator.ObjectCreationExpression(Type(type), arguments);
    public static ObjectCreationExpressionSyntax NewObject(string type, IEnumerable<ExpressionSyntax> assignments)
        => SyntaxFactory.ObjectCreationExpression(Type(type)).WithInitializer(SyntaxFactory.InitializerExpression(SyntaxKind.ObjectInitializerExpression, SyntaxFactory.SeparatedList(assignments)));
    public static ObjectCreationExpressionSyntax NewCollection(string type, params ExpressionSyntax[] values)
        => SyntaxFactory.ObjectCreationExpression(Type(type)).WithInitializer(SyntaxFactory.InitializerExpression(SyntaxKind.CollectionInitializerExpression, SyntaxFactory.SeparatedList(values)));
    public static ExpressionSyntax Array(params ExpressionSyntax[] values)
        => SyntaxFactory.ImplicitArrayCreationExpression(SyntaxFactory.InitializerExpression(SyntaxKind.ArrayInitializerExpression, SyntaxFactory.SeparatedList(values)));
    public static ExpressionSyntax String(string value) => (ExpressionSyntax)Generator.LiteralExpression(value);
    public static ExpressionSyntax Char(char value) => (ExpressionSyntax)Generator.LiteralExpression(value);
    public static ExpressionSyntax Number(int value) => (ExpressionSyntax)Generator.LiteralExpression(value);
    public static ExpressionSyntax Bool(bool value) => (ExpressionSyntax)Generator.LiteralExpression(value);
    public static ExpressionSyntax Null => (ExpressionSyntax)Generator.NullLiteralExpression();
    public static ExpressionSyntax Default => SyntaxFactory.LiteralExpression(SyntaxKind.DefaultLiteralExpression);
    public static ExpressionSyntax Suppress(ExpressionSyntax value) => SyntaxFactory.PostfixUnaryExpression(SyntaxKind.SuppressNullableWarningExpression, value);
    public static ExpressionSyntax Not(ExpressionSyntax value) => SyntaxFactory.PrefixUnaryExpression(SyntaxKind.LogicalNotExpression, value);
    public static ExpressionSyntax Binary(SyntaxKind kind, ExpressionSyntax left, ExpressionSyntax right) => SyntaxFactory.BinaryExpression(kind, left, right);
    public static ExpressionSyntax Equal(ExpressionSyntax left, ExpressionSyntax right) => Binary(SyntaxKind.EqualsExpression, left, right);
    public static ExpressionSyntax Add(ExpressionSyntax left, ExpressionSyntax right) => Binary(SyntaxKind.AddExpression, left, right);
    public static ExpressionSyntax Coalesce(ExpressionSyntax left, ExpressionSyntax right) => Binary(SyntaxKind.CoalesceExpression, left, right);
    public static ExpressionSyntax And(ExpressionSyntax left, ExpressionSyntax right) => Binary(SyntaxKind.LogicalAndExpression, left, right);
    public static ExpressionSyntax IsNull(ExpressionSyntax value, bool negated = false)
        => SyntaxFactory.IsPatternExpression(value, negated
            ? SyntaxFactory.UnaryPattern(SyntaxFactory.Token(SyntaxKind.NotKeyword), SyntaxFactory.ConstantPattern(Null))
            : SyntaxFactory.ConstantPattern(Null));
    public static ExpressionSyntax Assign(string name, ExpressionSyntax value) => Assign(Name(name), value);
    public static ExpressionSyntax Assign(ExpressionSyntax target, ExpressionSyntax value) => SyntaxFactory.AssignmentExpression(SyntaxKind.SimpleAssignmentExpression, target, value);
    public static ExpressionSyntax Await(ExpressionSyntax value, bool configureAwait = true)
        => SyntaxFactory.AwaitExpression(configureAwait ? Call(Member(value, "ConfigureAwait"), Bool(false)) : value);
    public static ExpressionSyntax Lambda(string parameter, ExpressionSyntax body)
        => SyntaxFactory.SimpleLambdaExpression(SyntaxFactory.Parameter(Identifier(parameter)), body);
    public static ExpressionSyntax Conditional(ExpressionSyntax condition, ExpressionSyntax whenTrue, ExpressionSyntax whenFalse)
        => SyntaxFactory.ConditionalExpression(condition, whenTrue, whenFalse);
    public static ArgumentSyntax Ref(string name) => SyntaxFactory.Argument(Name(name)).WithRefKindKeyword(SyntaxFactory.Token(SyntaxKind.RefKeyword));
    public static ArgumentSyntax OutVar(string name) => SyntaxFactory.Argument(SyntaxFactory.DeclarationExpression(Type("var"), SyntaxFactory.SingleVariableDesignation(Identifier(name))))
        .WithRefKindKeyword(SyntaxFactory.Token(SyntaxKind.OutKeyword));
    public static ExpressionSyntax ThrowExpression(string type, ExpressionSyntax message) => SyntaxFactory.ThrowExpression(New(type, message));
}
