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
            definitions.Add(ParseOperationDefinition());
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
        var selection = ParseSelectionSet();
        return new OperationDefinitionNode(operation, name, variables, [], selection, new SourceLocation(start, selection.Location.End));
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

            variables.Add(new VariableDefinitionNode(variable, type, defaultValue, [], new SourceLocation(start, (defaultValue ?? (AstNode)type).Location.End)));
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
            var start = type.Location.Start;
            var bang = Advance();
            type = new NonNullTypeNode(type, new SourceLocation(start, bang.End));
        }

        return type;
    }

    private ValueNode ParseConstantValue()
    {
        var token = Current;
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
            var name = ReadRequiredName("Expected a field name.");
            selections.Add(new FieldNode(name, null, [], [], null, name.Location));
        }

        if (selections.Count == 0) throw Error("A selection set cannot be empty.");
        var close = Advance();
        return new SelectionSetNode(selections, new SourceLocation(open.Start, close.End));
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
