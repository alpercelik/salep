namespace Salep.GraphQLParser.Benchmarks;

internal static class BenchmarkOperations
{
    public static int Execute(string operation, string source) => operation switch
    {
        "lexer" => Lex(source),
        "strict-parse" => GraphQLParser.Parse(new SourceText(source.AsMemory())).Definitions.Count,
        "diagnostic-parse" => DiagnosticChecksum(GraphQLParser.ParseWithDiagnostics(new SourceText(source.AsMemory()))),
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