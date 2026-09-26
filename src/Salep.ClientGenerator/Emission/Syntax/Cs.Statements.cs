using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;

namespace Salep.ClientGenerator.Emission.Syntax;

internal static partial class Cs
{
    public static StatementSyntax Statement(ExpressionSyntax expression) => (StatementSyntax)Generator.ExpressionStatement(expression);
    public static StatementSyntax Set(string target, ExpressionSyntax value) => Statement(Assign(target, value));
    public static LocalDeclarationStatementSyntax Local(string name, ExpressionSyntax value, bool disposable = false, string type = "var")
    {
        var statement = (LocalDeclarationStatementSyntax)Generator.LocalDeclarationStatement(Type(type), name, value);
        return disposable ? statement.WithUsingKeyword(SyntaxFactory.Token(SyntaxKind.UsingKeyword)) : statement;
    }
    public static StatementSyntax Return(ExpressionSyntax? value = null) => (StatementSyntax)Generator.ReturnStatement(value);
    public static StatementSyntax Throw(string message, string type = "InvalidOperationException") => (StatementSyntax)Generator.ThrowStatement(New(type, String(message)));
    public static IfStatementSyntax If(ExpressionSyntax condition, params StatementSyntax[] body) => (IfStatementSyntax)Generator.IfStatement(condition, body);
    public static StatementSyntax ForEach(string variable, ExpressionSyntax collection, params StatementSyntax[] body)
        => SyntaxFactory.ForEachStatement(Type("var"), Identifier(variable), collection, SyntaxFactory.Block(body));
    public static StatementSyntax Yield(ExpressionSyntax value) => SyntaxFactory.YieldStatement(SyntaxKind.YieldReturnStatement, value);
}
