using Salep.ClientGenerator.Model;
using Salep.GraphQLParser;

namespace Salep.ClientGenerator.TypeScript.Generation;

/// <summary>Maps parser/model locations back to the original source, including decoded literal escapes.</summary>
public sealed record TypeScriptSourceDocument(string Name, string Original, string Text, IReadOnlyList<int>? Offsets = null)
{
    public GraphQlSourceSpan Map(GraphQlSourceSpan span)
    {
        var start = Offset(span.Start);
        var end = Offset(span.End);
        var (line, column) = Coordinates(start);
        return span with { Start = start, End = end, Line = line, Column = column, SourceName = Name };
    }
    public DocumentNode Parse(ParserOptions options)
    {
        try { return global::Salep.GraphQLParser.GraphQLParser.Parse(new SourceText(Text.AsMemory()), options); }
        catch (GraphQLSyntaxException error) { throw Syntax(error.Message, error.Position, error); }
        catch (GraphQLLexicalException error) { throw Syntax(error.Message, error.Position, error); }
    }
    private InvalidDataException Syntax(string message, int offset, Exception inner)
    {
        var (line, column) = Coordinates(Offset(offset));
        return new($"{Name}({line},{column}): {message}", inner);
    }
    private int Offset(int index) => Offsets is null ? Math.Clamp(index, 0, Original.Length) : Offsets[Math.Clamp(index, 0, Offsets.Count - 1)];
    public (int Line, int Column) Coordinates(int index)
    {
        var line = 1; var column = 1;
        for (var cursor = 0; cursor < index; cursor++)
        {
            if (Original[cursor] == '\r') { line++; column = 1; if (cursor + 1 < index && Original[cursor + 1] == '\n') cursor++; }
            else if (Original[cursor] == '\n') { line++; column = 1; }
            else column++;
        }
        return (line, column);
    }
}
