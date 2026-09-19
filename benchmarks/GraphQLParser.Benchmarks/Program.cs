using System.Diagnostics;
using System.Globalization;
using System.Runtime;
using GraphQLParser;
using Parser = global::GraphQLParser.GraphQLParser;

CultureInfo.CurrentCulture = CultureInfo.InvariantCulture;
CultureInfo.CurrentUICulture = CultureInfo.InvariantCulture;

const string executable = "query ProductSearch($filter: Filter = { text: \"tea\", tags: [HOT, NEW] }) @trace { products(filter: $filter) { id name price { amount currency } ... on FeaturedProduct { badge } } } fragment ProductParts on Product { id name }";
const string sdl = "schema { query: Query mutation: Mutation } scalar Date @specifiedBy(url: \"https://example.test/date\") interface Node { id: ID! } type Query implements Node { id: ID! products(filter: Filter): [Product!]! } type Product implements Node { id: ID! name: String! price: Money } union SearchResult = Product | FeaturedProduct enum Sort { RELEVANCE PRICE } input Filter @oneOf { text: String tags: [Tag!] } directive @trace(label: String = \"parse\") repeatable on FIELD | QUERY";
const string malformed = "query Broken { user(id: ) { name } } query Later { field }";
var iterations = args.Length > 0 && int.TryParse(args[0], out var configured) && configured > 0 ? configured : 5000;
var warmup = Math.Min(500, Math.Max(25, iterations / 10));

Console.WriteLine($"Runtime: {Environment.Version}; {System.Runtime.InteropServices.RuntimeInformation.FrameworkDescription}");
Console.WriteLine($"OS: {System.Runtime.InteropServices.RuntimeInformation.OSDescription}");
Console.WriteLine($"Iterations: {iterations}; warmup: {warmup}; configuration: Release; GC: {GCSettings.IsServerGC switch { true => "Server", false => "Workstation" }}");
Console.WriteLine("Case,Mode,Characters,Bytes/op,Ops/sec,Checksum");

Run("executable", "lexer", executable, Lex);
Run("sdl", "lexer", sdl, Lex);
Run("executable", "strict-parse", executable, source => Parser.Parse(new SourceText(source.AsMemory())).Definitions.Count);
Run("sdl", "strict-parse", sdl, source => Parser.Parse(new SourceText(source.AsMemory())).Definitions.Count);
Run("malformed", "diagnostic-parse", malformed, source => Parser.ParseWithDiagnostics(new SourceText(source.AsMemory())).Diagnostics.Count);

void Run(string name, string mode, string source, Func<string, int> action)
{
    var checksum = 0;
    for (var index = 0; index < warmup; index++) checksum += action(source);
    GC.Collect();
    GC.WaitForPendingFinalizers();
    GC.Collect();
    var allocatedBefore = GC.GetAllocatedBytesForCurrentThread();
    var startedAt = Stopwatch.GetTimestamp();
    for (var index = 0; index < iterations; index++) checksum += action(source);
    var elapsedSeconds = Stopwatch.GetElapsedTime(startedAt).TotalSeconds;
    var allocated = GC.GetAllocatedBytesForCurrentThread() - allocatedBefore;
    var bytesPerOperation = (double)allocated / iterations;
    var operationsPerSecond = iterations / elapsedSeconds;
    Console.WriteLine($"{name},{mode},{source.Length},{bytesPerOperation:F1},{operationsPerSecond:F0},{checksum}");
}

static int Lex(string source)
{
    var lexer = new GraphQLLexer(new SourceText(source.AsMemory()));
    var count = 0;
    while (lexer.NextToken().Kind != TokenKind.EndOfFile) count++;
    return count;
}
