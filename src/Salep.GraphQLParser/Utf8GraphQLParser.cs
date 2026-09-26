using System.Buffers;
using System.Text;

namespace Salep.GraphQLParser;

/// <summary>Reports invalid UTF-8 input supplied to a parser entry point.</summary>
public class Utf8EncodingException : Exception
{
    /// <summary>Creates a Utf8EncodingException value.</summary>
    public Utf8EncodingException() { }
    /// <summary>Creates a Utf8EncodingException value.</summary>
    public Utf8EncodingException(string message) : base(message) { }
    /// <summary>Creates a Utf8EncodingException value.</summary>
    public Utf8EncodingException(string message, Exception inner) : base(message, inner) { }
}

/// <summary>Parses UTF-8 GraphQL input through the project-owned syntax API.</summary>
public ref partial struct Utf8GraphQLParser
{
    private static readonly UTF8Encoding StrictUtf8 = new(false, true);
    private readonly ReadOnlySpan<byte> _sourceText;
    private readonly ReadOnlySequence<byte> _sourceSequence;
    private readonly bool _usesSequence;
    private readonly ParserOptions _options;
    private bool _isEndOfFile;
    private int _parsedSyntaxNodes;

    /// <summary>Creates a Utf8GraphQLParser value.</summary>
    public Utf8GraphQLParser(ReadOnlySpan<byte> sourceText, ParserOptions? options = null)
    {
        _sourceText = sourceText;
        _sourceSequence = default;
        _usesSequence = false;
        _options = options ?? ParserOptions.Default;
        _isEndOfFile = false;
        _parsedSyntaxNodes = 0;
    }

    /// <summary>Creates a Utf8GraphQLParser value.</summary>
    public Utf8GraphQLParser(ReadOnlySequence<byte> sourceText, ParserOptions? options = null)
    {
        _sourceText = default;
        _sourceSequence = sourceText;
        _usesSequence = true;
        _options = options ?? ParserOptions.Default;
        _isEndOfFile = false;
        _parsedSyntaxNodes = 0;
    }

    /// <summary>Represents a parser API member.</summary>
    public bool IsEndOfFile => _isEndOfFile;
    /// <summary>Represents a parser API member.</summary>
    public int ParsedSyntaxNodes => _parsedSyntaxNodes;

    /// <summary>Parses a GraphQL document from the supplied input.</summary>
    public DocumentNode Parse()
    {
        var utf8 = _usesSequence ? _sourceSequence.ToArray() : _sourceText.ToArray();
        string source;
        try
        {
            source = StrictUtf8.GetString(utf8);
        }
        catch (DecoderFallbackException exception)
        {
            throw new Utf8EncodingException("The GraphQL source contains invalid UTF-8.", exception);
        }

        var document = GraphQLParser.Parse(new SourceText(source.AsMemory()), _options);
        var parsedSyntaxNodes = 0;
        GraphQLAstVisitor.Visit(document, _ =>
        {
            parsedSyntaxNodes++;
            return GraphQLVisitControl.Continue;
        });
        _parsedSyntaxNodes = parsedSyntaxNodes;
        _isEndOfFile = true;
        return document;
    }

    /// <summary>Parses a GraphQL document from the supplied input.</summary>
    public static DocumentNode Parse(ReadOnlySpan<byte> sourceText) => Parse(sourceText, ParserOptions.Default);
    /// <summary>Parses a GraphQL document from the supplied input.</summary>
    public static DocumentNode Parse(ReadOnlySpan<byte> sourceText, ParserOptions options) => new Utf8GraphQLParser(sourceText, options).Parse();
    /// <summary>Parses a GraphQL document from the supplied input.</summary>
    public static DocumentNode Parse(ReadOnlySequence<byte> sourceText) => Parse(sourceText, ParserOptions.Default);
    /// <summary>Parses a GraphQL document from the supplied input.</summary>
    public static DocumentNode Parse(ReadOnlySequence<byte> sourceText, ParserOptions options) => new Utf8GraphQLParser(sourceText, options).Parse();
    /// <summary>Parses a GraphQL document from the supplied input.</summary>
    public static DocumentNode Parse(string sourceText) => Parse(sourceText, ParserOptions.Default);
    /// <summary>Parses a GraphQL document from the supplied input.</summary>
    public static DocumentNode Parse(string sourceText, ParserOptions options)
    {
        ArgumentNullException.ThrowIfNull(sourceText);
        ArgumentNullException.ThrowIfNull(options);
        var document = GraphQLParser.Parse(new SourceText(sourceText.AsMemory()), options);
        return document;
    }
}
