using System.Buffers;
using System.Text;

namespace Salep.Parser;

/// <summary>Reads GraphQL tokens from UTF-8 source memory.</summary>
/// <remarks>The span constructor retains caller-owned memory for the reader lifetime.</remarks>
public ref struct Utf8GraphQLReader
{
    private static readonly UTF8Encoding StrictUtf8 = new(false, true);
    private readonly ReadOnlySpan<byte> _sourceText;
    private readonly string _decodedSource;
    private readonly SourceText _characterSource;
    private GraphQLLexer _lexer;
    private readonly int _maximumTokenCount;
    private Token _current;
    private byte[] _currentValue;
    private string _comment;
    private bool _hasCurrent;
    private bool _atEnd;
    private bool _disposed;
    private int _tokenCount;

    /// <summary>Creates a reader over UTF-8 source memory.</summary>
    public Utf8GraphQLReader(ReadOnlySpan<byte> sourceText, int maxAllowedTokens = int.MaxValue)
    {
        ValidateTokenLimit(maxAllowedTokens);
        _sourceText = sourceText;
        _decodedSource = Decode(sourceText);
        _characterSource = new SourceText(_decodedSource.AsMemory());
        _lexer = new GraphQLLexer(_characterSource);
        _maximumTokenCount = maxAllowedTokens;
        _current = default;
        _currentValue = [];
        _comment = string.Empty;
        _hasCurrent = false;
        _atEnd = false;
        _disposed = false;
        _tokenCount = 0;
    }

    /// <summary>Creates a reader over a possibly segmented UTF-8 sequence.</summary>
    public Utf8GraphQLReader(ReadOnlySequence<byte> sourceText, int maxAllowedTokens = int.MaxValue)
        : this(sourceText.ToArray().AsSpan(), maxAllowedTokens)
    {
    }

    /// <summary>Gets the current token kind, or the start-of-file kind before the first read.</summary>
    public TokenKind Kind => _hasCurrent ? _current.Kind : TokenKind.StartOfFile;
    /// <summary>Gets the current token's UTF-8 start offset.</summary>
    public int Start => _hasCurrent ? ByteOffset(_current.Start) : 0;
    /// <summary>Gets the current token's UTF-8 end offset.</summary>
    public int End => _hasCurrent ? ByteOffset(_current.End) : 0;
    /// <summary>Gets the UTF-8 source offset of the next read.</summary>
    public int Position => _hasCurrent ? End : 0;
    /// <summary>Gets the one-based source line of the current token.</summary>
    public int Line => GetLineAndColumn(_hasCurrent ? _current.Start : 0).Line;
    /// <summary>Gets the one-based byte column of the current token.</summary>
    public int Column => GetLineAndColumn(_hasCurrent ? _current.Start : 0).Column;
    /// <summary>Gets the UTF-8 source offset at the start of the current line.</summary>
    public int LineStart => GetLineAndColumn(_hasCurrent ? _current.Start : 0).LineStart;
    /// <summary>Gets the current token's floating-point notation, when it is a float.</summary>
    public FloatFormat? FloatFormat => _hasCurrent && _current.Kind == TokenKind.Float
        ? _current.RawValue.Span.IndexOfAny('e', 'E') >= 0 ? global::Salep.Parser.FloatFormat.Exponential : global::Salep.Parser.FloatFormat.FixedPoint
        : null;
    /// <summary>Gets the original UTF-8 source span.</summary>
    public ReadOnlySpan<byte> SourceText => _sourceText;
    /// <summary>Gets the current token's UTF-8 value span.</summary>
    public ReadOnlySpan<byte> Value => _currentValue;

    /// <summary>Reads the next token. Returns false repeatedly after end-of-file.</summary>
    public bool Read()
    {
        ThrowIfDisposed();
        if (_atEnd) return false;

        var previousEnd = _hasCurrent ? _current.End : 0;
        _current = _lexer.NextToken();
        _hasCurrent = true;
        _comment = FindComment(previousEnd, _current.Start);
        if (_current.Kind == TokenKind.EndOfFile)
        {
            _atEnd = true;
            _currentValue = [];
            return false;
        }

        _tokenCount++;
        if (_tokenCount > _maximumTokenCount)
            throw new GraphQLResourceLimitException("token count", _maximumTokenCount, _tokenCount, new SourceLocation(_current.Start, _current.End));
        _currentValue = StrictUtf8.GetBytes(_current.Value.ToString());
        return true;
    }

    /// <summary>Counts the remaining non-EOF tokens without changing this reader.</summary>
    public int Count()
    {
        ThrowIfDisposed();
        var copy = this;
        var count = 0;
        while (copy.Read()) count++;
        return count;
    }

    /// <summary>Gets the current name token's decoded text.</summary>
    public string GetName()
    {
        RequireKind(TokenKind.Name);
        return StrictUtf8.GetString(_currentValue);
    }

    /// <summary>Gets the comment preceding the current token, without its leading hash.</summary>
    public string GetComment()
    {
        ThrowIfDisposed();
        return _comment;
    }

    /// <summary>Gets the current string token's decoded value.</summary>
    public string GetString()
    {
        if (!_hasCurrent || _current.Kind is not (TokenKind.String or TokenKind.BlockString))
            throw new InvalidOperationException("The current token is not a string.");
        return StrictUtf8.GetString(_currentValue);
    }

    /// <summary>Gets the current scalar token's text.</summary>
    public string GetScalarValue()
    {
        if (!_hasCurrent || _current.Kind is not (TokenKind.Name or TokenKind.Integer or TokenKind.Float or TokenKind.String or TokenKind.BlockString))
            throw new InvalidOperationException("The current token is not a scalar value.");
        return StrictUtf8.GetString(_currentValue);
    }

    /// <summary>Writes the current token's raw string spelling to a byte writer.</summary>
    public int GetRawString(IBufferWriter<byte> writer)
    {
        ArgumentNullException.ThrowIfNull(writer);
        if (!_hasCurrent || _current.Kind is not (TokenKind.String or TokenKind.BlockString))
            throw new InvalidOperationException("The current token is not a string.");
        return WriteUtf8(_current.RawValue.Span, writer);
    }

    /// <summary>Encodes an escaped string value as a GraphQL string literal.</summary>
    public static int GetRawString(ReadOnlySpan<byte> escapedValue, bool isBlockString, IBufferWriter<byte> writer)
    {
        ArgumentNullException.ThrowIfNull(writer);
        var value = GetString(escapedValue, isBlockString);
        var literal = GraphQLPrinter.Print(new StringValueNode(value.AsMemory(), isBlockString, default));
        return WriteUtf8(literal.AsSpan(), writer);
    }

    /// <summary>Decodes an escaped UTF-8 GraphQL string value.</summary>
    public static string GetString(ReadOnlySpan<byte> escapedValue, bool isBlockString)
    {
        var source = Decode(escapedValue);
        var literal = isBlockString ? "\"\"\"" + source + "\"\"\"" : "\"" + source + "\"";
        try
        {
            var wrapped = GraphQLParser.Parse(new SourceText($"query {{ value(input: {literal}) }}".AsMemory()));
            var operation = (OperationDefinitionNode)wrapped.Definitions[0];
            var field = (FieldNode)operation.SelectionSet.Selections[0];
            return field.Arguments[0].Value is StringValueNode value
                ? value.Value
                : throw new FormatException("The supplied UTF-8 bytes are not a GraphQL string value.");
        }
        catch (GraphQLSyntaxException exception)
        {
            throw new FormatException("The supplied UTF-8 bytes are not a valid GraphQL string value.", exception);
        }
    }

    /// <summary>Decodes an unescaped UTF-8 string value.</summary>
    public static string GetString(ReadOnlySpan<byte> unescapedValue) => Decode(unescapedValue);

    /// <summary>Unescapes a UTF-8 GraphQL string payload in place and shortens the supplied span.</summary>
    public void UnescapeValue(scoped ref Span<byte> unescapedValue)
    {
        ThrowIfDisposed();
        var decoded = GetString(unescapedValue, isBlockString: false);
        var bytes = StrictUtf8.GetBytes(decoded);
        bytes.CopyTo(unescapedValue);
        unescapedValue = bytes;
    }

    /// <summary>Releases reader resources and invalidates subsequent operations.</summary>
    public void Dispose()
    {
        _disposed = true;
        _currentValue = [];
        _comment = string.Empty;
    }

    internal readonly string GetDecodedSource()
    {
        ThrowIfDisposed();
        return _decodedSource;
    }

    private readonly int ByteOffset(int charOffset) => StrictUtf8.GetByteCount(_decodedSource.AsSpan(0, charOffset));

    private readonly (int Line, int Column, int LineStart) GetLineAndColumn(int charOffset)
    {
        var line = 1;
        var lineStartChars = 0;
        var source = _decodedSource.AsSpan(0, charOffset);
        for (var index = 0; index < source.Length; index++)
        {
            if (source[index] == '\r')
            {
                if (index + 1 < source.Length && source[index + 1] == '\n') index++;
                line++;
                lineStartChars = index + 1;
            }
            else if (source[index] == '\n')
            {
                line++;
                lineStartChars = index + 1;
            }
        }
        var column = StrictUtf8.GetByteCount(_decodedSource.AsSpan(lineStartChars, charOffset - lineStartChars)) + 1;
        return (line, column, ByteOffset(lineStartChars));
    }

    private readonly string FindComment(int start, int end)
    {
        var span = _decodedSource.AsSpan(start, end - start);
        var hash = span.LastIndexOf('#');
        if (hash < 0) return string.Empty;
        var comment = span[(hash + 1)..];
        return comment.ToString().TrimEnd('\r', '\n');
    }

    private readonly void RequireKind(TokenKind kind)
    {
        ThrowIfDisposed();
        if (!_hasCurrent || _current.Kind != kind) throw new InvalidOperationException($"Expected a {kind} token.");
    }

    private readonly void ThrowIfDisposed()
    {
        if (_disposed) throw new ObjectDisposedException(nameof(Utf8GraphQLReader));
    }

    private static string Decode(ReadOnlySpan<byte> sourceText)
    {
        try { return StrictUtf8.GetString(sourceText); }
        catch (DecoderFallbackException exception) { throw new Utf8EncodingException("The GraphQL source contains invalid UTF-8.", exception); }
    }

    private static void ValidateTokenLimit(int maxAllowedTokens)
    {
        if (maxAllowedTokens < 0) throw new ArgumentOutOfRangeException(nameof(maxAllowedTokens));
    }

    private static int WriteUtf8(ReadOnlySpan<char> text, IBufferWriter<byte> writer)
    {
        var length = StrictUtf8.GetByteCount(text);
        var destination = writer.GetSpan(length);
        var written = StrictUtf8.GetBytes(text, destination);
        writer.Advance(written);
        return written;
    }
}
