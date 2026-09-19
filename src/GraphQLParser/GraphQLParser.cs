namespace GraphQLParser;

/// <summary>Parses GraphQL source text into an immutable syntax tree.</summary>
public sealed class GraphQLParser
{
    private readonly SourceText _source;
    private readonly List<Token> _tokens = [];
    private int _index;

    /// <summary>Creates a parser over caller-owned source memory.</summary>
    public GraphQLParser(SourceText source)
    {
        _source = source;
        var lexer = new GraphQLLexer(source);
        Token token;
        do
        {
            token = lexer.NextToken();
            _tokens.Add(token);
        } while (token.Kind != TokenKind.EndOfFile);
    }

    /// <summary>Parses one non-empty GraphQL document.</summary>
    public static DocumentNode Parse(SourceText source) => new GraphQLParser(source).ParseDocument();

    /// <summary>Parses one non-empty GraphQL document.</summary>
    public DocumentNode ParseDocument()
    {
        if (Current.Kind == TokenKind.EndOfFile)
        {
            throw Error("A document must contain at least one definition.");
        }

        var definitions = new List<DefinitionNode>();
        while (Current.Kind != TokenKind.EndOfFile)
        {
            definitions.Add(IsName("fragment") ? ParseFragmentDefinition() : ParseOperationDefinition());
        }

        return new DocumentNode(_source, definitions, new SourceLocation(0, _source.Length));
    }

    private OperationDefinitionNode ParseOperationDefinition()
    {
        var start = Current.Start;
        if (Current.Kind == TokenKind.BraceLeft)
        {
            var selectionSet = ParseSelectionSet();
            return new OperationDefinitionNode(OperationType.Query, null, [], [], selectionSet, new SourceLocation(start, selectionSet.Location.End));
        }

        var operation = ParseOperationType();
        NameNode? name = Current.Kind == TokenKind.Name ? ReadName() : null;
        var variables = Current.Kind == TokenKind.ParenthesisLeft ? ParseVariableDefinitions() : [];
        var directives = ParseDirectives();
        var selection = ParseSelectionSet();
        return new OperationDefinitionNode(operation, name, variables, directives, selection, new SourceLocation(start, selection.Location.End));
    }

    private FragmentDefinitionNode ParseFragmentDefinition()
    {
        var start = ExpectName("fragment", "Expected 'fragment'.").Start;
        var name = ReadRequiredName("Expected a fragment name.");
        if (name.Value.Span.SequenceEqual("on")) throw Error("A fragment name cannot be 'on'.");
        ExpectName("on", "Expected 'on' after the fragment name.");
        var typeName = ReadRequiredName("Expected a fragment type condition.");
        var type = new NamedTypeNode(typeName, typeName.Location);
        var directives = ParseDirectives();
        var selection = ParseSelectionSet();
        return new FragmentDefinitionNode(name, type, directives, selection, new SourceLocation(start, selection.Location.End));
    }

    private OperationType ParseOperationType()
    {
        if (Current.Kind != TokenKind.Name) throw Error("Expected an operation type or selection set.");
        var value = Current.Value.Span;
        var operation = value.SequenceEqual("query") ? OperationType.Query
            : value.SequenceEqual("mutation") ? OperationType.Mutation
            : value.SequenceEqual("subscription") ? OperationType.Subscription
            : throw Error("Expected query, mutation, or subscription.");
        Advance();
        return operation;
    }

    private List<VariableDefinitionNode> ParseVariableDefinitions()
    {
        Expect(TokenKind.ParenthesisLeft, "Expected '('.");
        var variables = new List<VariableDefinitionNode>();
        while (Current.Kind != TokenKind.ParenthesisRight)
        {
            if (Current.Kind == TokenKind.EndOfFile) throw Error("Unterminated variable definitions.");
            var start = Current.Start;
            Expect(TokenKind.Dollar, "Expected '$' before a variable name.");
            var variableName = ReadRequiredName("Expected a variable name.");
            var variable = new VariableNode(variableName, new SourceLocation(start, variableName.Location.End));
            Expect(TokenKind.Colon, "Expected ':' after the variable name.");
            var type = ParseTypeReference();
            ValueNode? defaultValue = null;
            if (Current.Kind == TokenKind.Equals)
            {
                Advance();
                defaultValue = ParseConstantValue();
            }

            var directives = ParseDirectives(constantArguments: true);
            var end = directives.Count > 0 ? directives[^1].Location.End : (defaultValue ?? (AstNode)type).Location.End;
            variables.Add(new VariableDefinitionNode(variable, type, defaultValue, directives, new SourceLocation(start, end)));
        }

        if (variables.Count == 0) throw Error("Variable definitions cannot be empty.");
        Advance();
        return variables;
    }

    private TypeNode ParseTypeReference()
    {
        TypeNode type;
        if (Current.Kind == TokenKind.BracketLeft)
        {
            var start = Current.Start;
            Advance();
            var inner = ParseTypeReference();
            var close = Expect(TokenKind.BracketRight, "Expected ']' in list type.");
            type = new ListTypeNode(inner, new SourceLocation(start, close.End));
        }
        else
        {
            var name = ReadRequiredName("Expected a named type.");
            type = new NamedTypeNode(name, name.Location);
        }

        if (Current.Kind == TokenKind.Bang)
        {
            if (type is NonNullTypeNode) throw Error("A non-null type cannot wrap another non-null type.");
            var start = type.Location.Start;
            var bang = Advance();
            type = new NonNullTypeNode(type, new SourceLocation(start, bang.End));
        }

        return type;
    }

    private ValueNode ParseConstantValue()
    {
        var token = Current;
        if (token.Kind == TokenKind.Dollar) throw Error("Variables are not allowed in constant values.");
        if (token.Kind == TokenKind.BracketLeft)
        {
            var start = Advance().Start;
            var values = new List<ValueNode>();
            while (Current.Kind != TokenKind.BracketRight)
            {
                if (Current.Kind == TokenKind.EndOfFile) throw Error("Unterminated constant list value.");
                values.Add(ParseConstantValue());
            }

            return new ListValueNode(values, new SourceLocation(start, Advance().End));
        }

        if (token.Kind == TokenKind.BraceLeft)
        {
            var start = Advance().Start;
            var fields = new List<ObjectFieldNode>();
            while (Current.Kind != TokenKind.BraceRight)
            {
                if (Current.Kind == TokenKind.EndOfFile) throw Error("Unterminated constant object value.");
                var fieldName = ReadRequiredName("Expected a constant input-object field name.");
                Expect(TokenKind.Colon, "Expected ':' after the constant input-object field name.");
                var value = ParseConstantValue();
                fields.Add(new ObjectFieldNode(fieldName, value, new SourceLocation(fieldName.Location.Start, value.Location.End)));
            }

            return new ObjectValueNode(fields, new SourceLocation(start, Advance().End));
        }

        if (token.Kind is TokenKind.Integer or TokenKind.Float or TokenKind.String or TokenKind.BlockString)
        {
            Advance();
            return token.Kind switch
            {
                TokenKind.Integer => new IntValueNode(token.Value, Span(token)),
                TokenKind.Float => new FloatValueNode(token.Value, Span(token)),
                _ => new StringValueNode(token.Value, token.Kind == TokenKind.BlockString, Span(token)),
            };
        }

        if (token.Kind == TokenKind.Name)
        {
            Advance();
            if (token.Value.Span.SequenceEqual("true")) return new BooleanValueNode(true, Span(token));
            if (token.Value.Span.SequenceEqual("false")) return new BooleanValueNode(false, Span(token));
            if (token.Value.Span.SequenceEqual("null")) return new NullValueNode(Span(token));
            return new EnumValueNode(token.Value, Span(token));
        }

        throw Error("Expected a constant value.");
    }

    private SelectionSetNode ParseSelectionSet()
    {
        var open = Expect(TokenKind.BraceLeft, "Expected a selection set.");
        var selections = new List<SelectionNode>();
        while (Current.Kind != TokenKind.BraceRight)
        {
            if (Current.Kind == TokenKind.EndOfFile) throw Error("Unterminated selection set.");
            selections.Add(Current.Kind == TokenKind.Spread ? ParseFragmentSelection() : ParseField());
        }

        if (selections.Count == 0) throw Error("A selection set cannot be empty.");
        var close = Advance();
        return new SelectionSetNode(selections, new SourceLocation(open.Start, close.End));
    }

    private FieldNode ParseField()
    {
        var first = ReadRequiredName("Expected a field name.");
        NameNode? alias = null;
        var name = first;
        if (Current.Kind == TokenKind.Colon)
        {
            alias = first;
            Advance();
            name = ReadRequiredName("Expected a field name after the alias.");
        }

        var arguments = ParseArguments();
        var directives = ParseDirectives();
        var selection = Current.Kind == TokenKind.BraceLeft ? ParseSelectionSet() : null;
        var end = selection?.Location.End ?? _tokens[_index - 1].End;
        return new FieldNode(name, alias, arguments, directives, selection, new SourceLocation(first.Location.Start, end));
    }

    private SelectionNode ParseFragmentSelection()
    {
        var start = Expect(TokenKind.Spread, "Expected '...'.").Start;
        if (IsName("on"))
        {
            Advance();
            var typeName = ReadRequiredName("Expected a type condition after 'on'.");
            var type = new NamedTypeNode(typeName, typeName.Location);
            var directives = ParseDirectives();
            var selection = ParseSelectionSet();
            return new InlineFragmentNode(type, directives, selection, new SourceLocation(start, selection.Location.End));
        }

        if (Current.Kind == TokenKind.At)
        {
            var directives = ParseDirectives();
            var selection = ParseSelectionSet();
            return new InlineFragmentNode(null, directives, selection, new SourceLocation(start, selection.Location.End));
        }

        var name = ReadRequiredName("Expected a fragment name or 'on' after '...'.");
        var spreadDirectives = ParseDirectives();
        var end = spreadDirectives.Count > 0 ? spreadDirectives[^1].Location.End : name.Location.End;
        return new FragmentSpreadNode(name, spreadDirectives, new SourceLocation(start, end));
    }

    private List<ArgumentNode> ParseArguments(bool constantValues = false)
    {
        var arguments = new List<ArgumentNode>();
        if (Current.Kind != TokenKind.ParenthesisLeft) return arguments;
        Advance();
        while (Current.Kind != TokenKind.ParenthesisRight)
        {
            if (Current.Kind == TokenKind.EndOfFile) throw Error("Unterminated arguments.");
            var start = Current.Start;
            var name = ReadRequiredName("Expected an argument name.");
            Expect(TokenKind.Colon, "Expected ':' after the argument name.");
            var value = constantValues ? ParseConstantValue() : ParseValue();
            arguments.Add(new ArgumentNode(name, value, new SourceLocation(start, value.Location.End)));
        }

        if (arguments.Count == 0) throw Error("Arguments cannot be empty.");
        Advance();
        return arguments;
    }

    private List<DirectiveNode> ParseDirectives(bool constantArguments = false)
    {
        var directives = new List<DirectiveNode>();
        while (Current.Kind == TokenKind.At)
        {
            var start = Advance().Start;
            var name = ReadRequiredName("Expected a directive name after '@'.");
            var arguments = ParseArguments(constantArguments);
            var end = arguments.Count > 0 ? _tokens[_index - 1].End : name.Location.End;
            directives.Add(new DirectiveNode(name, arguments, new SourceLocation(start, end)));
        }

        return directives;
    }

    private ValueNode ParseValue()
    {
        if (Current.Kind == TokenKind.Dollar)
        {
            var start = Advance().Start;
            var name = ReadRequiredName("Expected a variable name after '$'.");
            return new VariableNode(name, new SourceLocation(start, name.Location.End));
        }

        if (Current.Kind == TokenKind.BracketLeft)
        {
            var start = Advance().Start;
            var values = new List<ValueNode>();
            while (Current.Kind != TokenKind.BracketRight)
            {
                if (Current.Kind == TokenKind.EndOfFile) throw Error("Unterminated list value.");
                values.Add(ParseValue());
            }

            var end = Advance().End;
            return new ListValueNode(values, new SourceLocation(start, end));
        }

        if (Current.Kind == TokenKind.BraceLeft)
        {
            var start = Advance().Start;
            var fields = new List<ObjectFieldNode>();
            while (Current.Kind != TokenKind.BraceRight)
            {
                if (Current.Kind == TokenKind.EndOfFile) throw Error("Unterminated object value.");
                var fieldName = ReadRequiredName("Expected an input-object field name.");
                Expect(TokenKind.Colon, "Expected ':' after the input-object field name.");
                var fieldValue = ParseValue();
                fields.Add(new ObjectFieldNode(fieldName, fieldValue, new SourceLocation(fieldName.Location.Start, fieldValue.Location.End)));
            }

            var end = Advance().End;
            return new ObjectValueNode(fields, new SourceLocation(start, end));
        }

        return ParseConstantValue();
    }

    private bool IsName(string value) => Current.Kind == TokenKind.Name && Current.Value.Span.SequenceEqual(value);

    private Token ExpectName(string value, string message)
    {
        if (!IsName(value)) throw Error(message);
        return Advance();
    }

    private NameNode ReadRequiredName(string message)
    {
        if (Current.Kind != TokenKind.Name) throw Error(message);
        return ReadName();
    }

    private NameNode ReadName()
    {
        var token = Advance();
        return new NameNode(token.Value, Span(token));
    }

    private Token Expect(TokenKind kind, string message)
    {
        if (Current.Kind != kind) throw Error(message);
        return Advance();
    }

    private Token Advance() => _tokens[_index++];
    private Token Current => _tokens[_index];
    private SourceLocation Span(Token token) => new(token.Start, token.End);
    private GraphQLSyntaxException Error(string message) => new(message, Current.Start, Current.End - Current.Start);
}
