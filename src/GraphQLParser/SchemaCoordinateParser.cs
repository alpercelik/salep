namespace GraphQLParser;

internal ref struct SchemaCoordinateParser
{
    private readonly SourceText _source;
    private readonly ReadOnlySpan<char> _text;
    private int _position;

    public SchemaCoordinateParser(SourceText source)
    {
        _source = source;
        _text = source.Content.Span;
        _position = 0;
    }

    public SchemaCoordinateNode Parse()
    {
        var start = _position;
        var directive = Take('@');
        var name = ReadName(directive ? "Expected a directive name." : "Expected a type name.");
        if (directive)
        {
            if (Take('('))
            {
                var argument = ReadName("Expected a directive argument name.");
                Expect(':');
                Expect(')');
                EnsureEnd();
                return new DirectiveArgumentCoordinateNode(name, argument, new SourceLocation(start, _position));
            }

            EnsureEnd();
            return new DirectiveCoordinateNode(name, new SourceLocation(start, _position));
        }

        if (!Take('.'))
        {
            EnsureEnd();
            return new TypeCoordinateNode(name, new SourceLocation(start, _position));
        }

        var field = ReadName("Expected a field name after '.'.");
        if (Take('('))
        {
            var argument = ReadName("Expected an argument name.");
            Expect(':');
            Expect(')');
            EnsureEnd();
            return new ArgumentCoordinateNode(name, field, argument, new SourceLocation(start, _position));
        }

        EnsureEnd();
        return new MemberCoordinateNode(name, field, new SourceLocation(start, _position));
    }

    private NameNode ReadName(string message)
    {
        var start = _position;
        if (_position >= _text.Length || !IsNameStart(_text[_position])) Fail(message);
        _position++;
        while (_position < _text.Length && IsNameContinue(_text[_position])) _position++;
        return new NameNode(_source.Slice(start, _position - start), new SourceLocation(start, _position));
    }

    private bool Take(char character)
    {
        if (_position >= _text.Length || _text[_position] != character) return false;
        _position++;
        return true;
    }

    private void Expect(char character)
    {
        if (!Take(character)) Fail($"Expected '{character}'.");
    }

    private void EnsureEnd()
    {
        if (_position != _text.Length) Fail("Unexpected characters after schema coordinate.");
    }

    private void Fail(string message)
    {
        var position = _position;
        var length = position < _text.Length ? 1 : 0;
        var actual = length == 0 ? "EndOfFile" : $"'{_text[position]}'";
        throw new GraphQLSyntaxException(message, position, length, message, actual);
    }

    private static bool IsNameStart(char c) => c is '_' or >= 'A' and <= 'Z' or >= 'a' and <= 'z';
    private static bool IsNameContinue(char c) => IsNameStart(c) || c is >= '0' and <= '9';
}
