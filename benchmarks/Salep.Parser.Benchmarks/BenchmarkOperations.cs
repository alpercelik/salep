using Salep.Parser;
using Parser = Salep.Parser.GraphQLParser;

internal static class BenchmarkOperations
{
    public static int Execute(string operation, string source) => operation switch
    {
        "lexer" => Lex(source),
        "strict-parse" => Parser.Parse(new SourceText(source.AsMemory())).Definitions.Count,
        "diagnostic-parse" => DiagnosticChecksum(Parser.ParseWithDiagnostics(new SourceText(source.AsMemory()))),
        _ => throw new InvalidOperationException($"Unknown benchmark operation '{operation}'."),
    };

    public static int DiagnosticChecksum(GraphQLParseResult result) => result.Diagnostics.Count + (result.Document?.Definitions.Count ?? 0);

    public static int Lex(string source)
    {
        var lexer = new GraphQLLexer(new SourceText(source.AsMemory()));
        var count = 0;
        while (lexer.NextToken().Kind != TokenKind.EndOfFile)
            count++;
        return count;
    }
}
