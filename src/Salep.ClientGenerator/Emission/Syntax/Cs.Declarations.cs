using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;

namespace Salep.ClientGenerator.Emission.Syntax;

internal static partial class Cs
{
    public static SyntaxToken Identifier(string name)
    {
        if (!SyntaxFacts.IsValidIdentifier(name.TrimStart('@')) || name.Length == 0)
            throw new InvalidOperationException($"Invalid generated C# identifier '{name}'.");
        return name.StartsWith('@')
            ? SyntaxFactory.Identifier(default, SyntaxKind.IdentifierToken, name, name[1..], default)
            : SyntaxFactory.Identifier(name);
    }

    // Type specifications come from the schema's type mapping and user configuration.
    public static TypeSyntax Type(string specification)
    {
        if (specification == "void") return SyntaxFactory.PredefinedType(SyntaxFactory.Token(SyntaxKind.VoidKeyword));
        var type = SyntaxFactory.ParseTypeName(specification, consumeFullText: true);
        EnsureValid(type, $"type '{specification}'");
        return type;
    }

    public static ExpressionSyntax ConfiguredExpression(string expression)
    {
        var syntax = SyntaxFactory.ParseExpression(expression, consumeFullText: true);
        EnsureValid(syntax, $"configured sample expression '{expression}'");
        return syntax;
    }

    private static void EnsureValid(SyntaxNode node, string context)
    {
        var errors = node.GetDiagnostics().Where(d => d.Severity == DiagnosticSeverity.Error).ToArray();
        if (errors.Length != 0) throw new InvalidOperationException($"Invalid C# {context}: {string.Join("; ", errors.Select(d => d.ToString()))}");
    }

    public static SyntaxTokenList Modifiers(params SyntaxKind[] kinds) => SyntaxFactory.TokenList(kinds.Select(SyntaxFactory.Token));
    public static SyntaxTokenList Public => Modifiers(SyntaxKind.PublicKeyword);
    public static SyntaxTokenList PublicSealed => Modifiers(SyntaxKind.PublicKeyword, SyntaxKind.SealedKeyword);
    public static SyntaxTokenList PublicStatic => Modifiers(SyntaxKind.PublicKeyword, SyntaxKind.StaticKeyword);
    public static ParameterSyntax Parameter(string type, string name, ExpressionSyntax? defaultValue = null)
    {
        return ((ParameterSyntax)Generator.ParameterDeclaration(name, Type(type), defaultValue)).WithIdentifier(Identifier(name));
    }
    public static ParameterSyntax Cancellation(bool enumerator = false, bool optional = true)
    {
        var parameter = Parameter("CancellationToken", "cancellationToken", optional ? Default : null);
        return enumerator ? parameter.AddAttributeLists(Attribute("EnumeratorCancellation")) : parameter;
    }
    public static AttributeListSyntax Attribute(string name, params ExpressionSyntax[] arguments)
        => SyntaxFactory.AttributeList(SyntaxFactory.SingletonSeparatedList(SyntaxFactory.Attribute((NameSyntax)Type(name))
            .WithArgumentList(arguments.Length == 0 ? null : SyntaxFactory.AttributeArgumentList(SyntaxFactory.SeparatedList(arguments.Select(SyntaxFactory.AttributeArgument))))));
    public static PropertyDeclarationSyntax Property(string type, string name, bool init = true, ExpressionSyntax? initializer = null, SyntaxTokenList? modifiers = null)
    {
        var property = SyntaxFactory.PropertyDeclaration(Type(type), Identifier(name)).WithModifiers(modifiers ?? Public)
            .WithAccessorList(SyntaxFactory.AccessorList(SyntaxFactory.List(new[]
            {
                SyntaxFactory.AccessorDeclaration(SyntaxKind.GetAccessorDeclaration).WithSemicolonToken(SyntaxFactory.Token(SyntaxKind.SemicolonToken)),
                SyntaxFactory.AccessorDeclaration(init ? SyntaxKind.InitAccessorDeclaration : SyntaxKind.SetAccessorDeclaration).WithSemicolonToken(SyntaxFactory.Token(SyntaxKind.SemicolonToken))
            })));
        return initializer is null ? property : property.WithInitializer(SyntaxFactory.EqualsValueClause(initializer)).WithSemicolonToken(SyntaxFactory.Token(SyntaxKind.SemicolonToken));
    }
    public static PropertyDeclarationSyntax ReadOnlyProperty(string type, string name, ExpressionSyntax? value = null, bool isPublic = true)
    {
        var property = SyntaxFactory.PropertyDeclaration(Type(type), Identifier(name)).WithModifiers(isPublic ? Public : default);
        return value is null ? property.WithAccessorList(SyntaxFactory.AccessorList(SyntaxFactory.SingletonList(
            SyntaxFactory.AccessorDeclaration(SyntaxKind.GetAccessorDeclaration).WithSemicolonToken(SyntaxFactory.Token(SyntaxKind.SemicolonToken)))))
            : property.WithExpressionBody(SyntaxFactory.ArrowExpressionClause(value)).WithSemicolonToken(SyntaxFactory.Token(SyntaxKind.SemicolonToken));
    }
    public static FieldDeclarationSyntax Field(string type, string name, ExpressionSyntax? value = null, SyntaxTokenList? modifiers = null)
        => SyntaxFactory.FieldDeclaration(SyntaxFactory.VariableDeclaration(Type(type), SyntaxFactory.SingletonSeparatedList(
            SyntaxFactory.VariableDeclarator(Identifier(name)).WithInitializer(value is null ? null : SyntaxFactory.EqualsValueClause(value)))))
            .WithModifiers(modifiers ?? Modifiers(SyntaxKind.PrivateKeyword, SyntaxKind.ReadOnlyKeyword));
    public static ClassDeclarationSyntax Class(string name, IEnumerable<MemberDeclarationSyntax> members, SyntaxTokenList? modifiers = null, params string[] bases)
        => ((ClassDeclarationSyntax)Generator.ClassDeclaration(name, members: members)).WithIdentifier(Identifier(name)).WithModifiers(modifiers ?? PublicSealed).WithBaseList(Bases(bases));
    public static RecordDeclarationSyntax Record(string name, IEnumerable<MemberDeclarationSyntax> members, SyntaxTokenList? modifiers = null, params string[] bases)
        => SyntaxFactory.RecordDeclaration(SyntaxKind.RecordDeclaration, SyntaxFactory.Token(SyntaxKind.RecordKeyword), Identifier(name)).WithModifiers(modifiers ?? PublicSealed)
            .WithOpenBraceToken(SyntaxFactory.Token(SyntaxKind.OpenBraceToken)).WithCloseBraceToken(SyntaxFactory.Token(SyntaxKind.CloseBraceToken))
            .WithMembers(SyntaxFactory.List(members)).WithBaseList(Bases(bases));
    public static BaseListSyntax? Bases(IEnumerable<string> bases)
    {
        var list = bases.Select(name => (BaseTypeSyntax)SyntaxFactory.SimpleBaseType(Type(name))).ToArray();
        return list.Length == 0 ? null : SyntaxFactory.BaseList(SyntaxFactory.SeparatedList(list));
    }
    public static MethodDeclarationSyntax Method(string returnType, string name, IEnumerable<ParameterSyntax> parameters, IEnumerable<StatementSyntax>? body, SyntaxTokenList? modifiers = null, params string[] typeParameters)
    {
        var method = ((MethodDeclarationSyntax)Generator.MethodDeclaration(name, parameters: parameters, returnType: Type(returnType)))
            .WithIdentifier(Identifier(name)).WithModifiers(modifiers ?? Public);
        if (typeParameters.Length > 0) method = method.WithTypeParameterList(TypeParameters(typeParameters));
        return body is null ? method.WithBody(null).WithSemicolonToken(SyntaxFactory.Token(SyntaxKind.SemicolonToken)) : method.WithSemicolonToken(default).WithBody(SyntaxFactory.Block(body));
    }
    public static TypeParameterListSyntax TypeParameters(params string[] names)
        => SyntaxFactory.TypeParameterList(SyntaxFactory.SeparatedList(names.Select(name => SyntaxFactory.TypeParameter(Identifier(name)))));
    public static ConstructorDeclarationSyntax Constructor(string name, IEnumerable<ParameterSyntax> parameters, params StatementSyntax[] body)
        => ((ConstructorDeclarationSyntax)Generator.ConstructorDeclaration(name, parameters: parameters, statements: body))
            .WithIdentifier(Identifier(name)).WithModifiers(Public);
}
