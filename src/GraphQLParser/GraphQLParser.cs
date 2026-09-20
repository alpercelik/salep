namespace GraphQLParser;

/// <summary>Parses GraphQL source text into an immutable syntax tree.</summary>
public sealed class GraphQLParser
{
    /// <summary>Maximum diagnostics returned by one recovery parse.</summary>
    public const int MaximumDiagnosticCount = 100;

    private static readonly HashSet<string> ValidDirectiveLocations = new(StringComparer.Ordinal)
    {
        "QUERY", "MUTATION", "SUBSCRIPTION", "FIELD", "FRAGMENT_DEFINITION", "FRAGMENT_SPREAD", "INLINE_FRAGMENT", "VARIABLE_DEFINITION",
        "SCHEMA", "SCALAR", "OBJECT", "FIELD_DEFINITION", "ARGUMENT_DEFINITION", "INTERFACE", "UNION", "ENUM", "ENUM_VALUE", "INPUT_OBJECT", "INPUT_FIELD_DEFINITION",
    };

    private readonly SourceText _source;
    private readonly GraphQLParserOptions _options;
    private readonly int[] _lineStarts;
    private Source? _sourceInfo;
    private readonly List<Token> _tokens = [];
    private int _index;
    private int _braceDepth;
    private int _parenthesisDepth;
    private int _bracketDepth;
    private GraphQLLexicalException? _lexicalError;
    private List<GraphQLDiagnostic>? _activeDiagnostics;
    private bool _diagnosticsTruncated;

    /// <summary>Creates a parser that snapshots the supplied source into immutable storage.</summary>
    public GraphQLParser(SourceText source)
        : this(source, GraphQLParserOptions.Default, copySource: true)
    {
    }

    /// <summary>Creates a parser over a named source body.</summary>
    public GraphQLParser(Source source)
        : this(new SourceText((source ?? throw new ArgumentNullException(nameof(source))).Body.AsMemory()), GraphQLParserOptions.Default, copySource: false)
    {
        _sourceInfo = source;
    }

    /// <summary>Creates a parser with explicit input and work limits, snapshotting source into immutable storage.</summary>
    public GraphQLParser(SourceText source, GraphQLParserOptions options)
        : this(source, options, copySource: true)
    {
    }

    private GraphQLParser(SourceText source, GraphQLParserOptions options, bool copySource)
    {
        ArgumentNullException.ThrowIfNull(options);
        if (source.Length > options.MaximumSourceLength)
        {
            throw new GraphQLResourceLimitException("source length", options.MaximumSourceLength, source.Length,
                new SourceLocation(options.MaximumSourceLength, source.Length));
        }

        _source = copySource ? source.ToOwned() : source;
        _options = options;
        _lineStarts = BuildLineStarts(_source.Content.Span);
        var lexer = new GraphQLLexer(_source);
        var tokenCount = 0;
        var nestingDepth = 0;
        try
        {
            Token token;
            do
            {
                token = lexer.NextToken();
                if (token.Kind != TokenKind.EndOfFile)
                {
                    tokenCount++;
                    if (tokenCount > options.MaximumTokenCount)
                    {
                        throw new GraphQLResourceLimitException("token count", options.MaximumTokenCount, tokenCount, new SourceLocation(token.Start, token.End));
                    }

                    if (token.Kind is TokenKind.BraceLeft or TokenKind.ParenthesisLeft or TokenKind.BracketLeft)
                    {
                        nestingDepth++;
                        if (nestingDepth > options.MaximumNestingDepth)
                        {
                            throw new GraphQLResourceLimitException("nesting depth", options.MaximumNestingDepth, nestingDepth, new SourceLocation(token.Start, token.End));
                        }
                    }
                    else if (token.Kind is TokenKind.BraceRight or TokenKind.ParenthesisRight or TokenKind.BracketRight)
                    {
                        nestingDepth = Math.Max(0, nestingDepth - 1);
                    }
                }

                _tokens.Add(token);
            } while (token.Kind != TokenKind.EndOfFile);
        }
        catch (GraphQLResourceLimitException)
        {
            throw;
        }
        catch (GraphQLLexicalException exception)
        {
            _lexicalError = exception;
                    _tokens.Add(new Token(TokenKind.EndOfFile, exception.Position, exception.Position, _source.Slice(exception.Position, 0)));
        }
    }

    /// <summary>Creates a parser over a named source body with explicit options.</summary>
    public GraphQLParser(Source source, GraphQLParserOptions options)
        : this(new SourceText((source ?? throw new ArgumentNullException(nameof(source))).Body.AsMemory()), options, copySource: false)
    {
        _sourceInfo = source;
    }

    /// <summary>Parses one non-empty GraphQL document.</summary>
    public static DocumentNode Parse(SourceText source) => Parse(source, GraphQLParserOptions.Default);

    /// <summary>Parses one non-empty document from a named source.</summary>
    public static DocumentNode Parse(Source source) => Parse(source, GraphQLParserOptions.Default);

    /// <summary>Parses one non-empty document from a named source with explicit options.</summary>
    public static DocumentNode Parse(Source source, GraphQLParserOptions options) => new GraphQLParser(source, options).ParseDocument();

    /// <summary>Parses one non-empty GraphQL document with explicit resource limits.</summary>
    public static DocumentNode Parse(SourceText source, GraphQLParserOptions options) => new GraphQLParser(source, options).ParseDocument();

    /// <summary>Parses a document while retaining the caller's source memory without copying.</summary>
    /// <remarks>The caller must keep the backing memory alive and unchanged while the returned document is in use.</remarks>
    public static DocumentNode ParseBorrowed(SourceText source) => ParseBorrowed(source, GraphQLParserOptions.Default);

    /// <summary>Parses a document with explicit limits while retaining the caller's source memory without copying.</summary>
    /// <remarks>The caller must keep the backing memory alive and unchanged while the returned document is in use.</remarks>
    public static DocumentNode ParseBorrowed(SourceText source, GraphQLParserOptions options) => new GraphQLParser(source, options, copySource: false).ParseDocument();

    /// <summary>Parses a document with language API options while retaining the caller's source memory without copying.</summary>
    /// <remarks>The caller must keep the backing memory alive and unchanged while the returned document is in use.</remarks>
    public static DocumentNode ParseBorrowed(SourceText source, ParserOptions options)
    {
        ArgumentNullException.ThrowIfNull(options);
        var document = new GraphQLParser(source, options.ToGraphQLParserOptions(), copySource: false).ParseDocument();
        options.ValidateTree(document);
        return document;
    }

    /// <summary>Parses one non-empty GraphQL document using language API parser options.</summary>
    public static DocumentNode Parse(SourceText source, ParserOptions options)
    {
        ArgumentNullException.ThrowIfNull(options);
        var document = new GraphQLParser(source, options.ToGraphQLParserOptions()).ParseDocument();
        options.ValidateTree(document);
        return document;
    }

    /// <summary>Parses a named source using language API parser options.</summary>
    public static DocumentNode Parse(Source source, ParserOptions options)
    {
        ArgumentNullException.ThrowIfNull(options);
        var document = new GraphQLParser(source, options.ToGraphQLParserOptions()).ParseDocument();
        options.ValidateTree(document);
        return document;
    }

    /// <summary>Parses a document while collecting diagnostics and recovering at later definitions.</summary>
    public static GraphQLParseResult ParseWithDiagnostics(SourceText source) => ParseWithDiagnostics(source, GraphQLParserOptions.Default);

    /// <summary>Parses a named source while collecting diagnostics.</summary>
    public static GraphQLParseResult ParseWithDiagnostics(Source source) => ParseWithDiagnostics(source, GraphQLParserOptions.Default);

    /// <summary>Parses a named source with explicit options and diagnostic recovery.</summary>
    public static GraphQLParseResult ParseWithDiagnostics(Source source, GraphQLParserOptions options) => new GraphQLParser(source, options).ParseDocumentWithDiagnostics();

    /// <summary>Parses a document with explicit resource limits and diagnostic recovery.</summary>
    public static GraphQLParseResult ParseWithDiagnostics(SourceText source, GraphQLParserOptions options) => new GraphQLParser(source, options).ParseDocumentWithDiagnostics();

    /// <summary>Parses with diagnostics while retaining the caller's source memory without copying.</summary>
    /// <remarks>The caller must keep the backing memory alive and unchanged while the returned result is in use.</remarks>
    public static GraphQLParseResult ParseWithDiagnosticsBorrowed(SourceText source) => ParseWithDiagnosticsBorrowed(source, GraphQLParserOptions.Default);

    /// <summary>Parses with explicit limits and diagnostics while retaining the caller's source memory without copying.</summary>
    /// <remarks>The caller must keep the backing memory alive and unchanged while the returned result is in use.</remarks>
    public static GraphQLParseResult ParseWithDiagnosticsBorrowed(SourceText source, GraphQLParserOptions options) => new GraphQLParser(source, options, copySource: false).ParseDocumentWithDiagnostics();

    /// <summary>Parses one strict GraphQL schema coordinate.</summary>
    public static SchemaCoordinateNode ParseSchemaCoordinate(SourceText source) => new SchemaCoordinateParser(source.ToOwned()).Parse();

    /// <summary>Parses a schema coordinate while retaining the caller's source memory without copying.</summary>
    /// <remarks>The caller must keep the backing memory alive and unchanged while the returned coordinate is in use.</remarks>
    public static SchemaCoordinateNode ParseSchemaCoordinateBorrowed(SourceText source) => new SchemaCoordinateParser(source).Parse();

    /// <summary>Parses one strict GraphQL schema coordinate from a named source body.</summary>
    public static SchemaCoordinateNode ParseSchemaCoordinate(Source source)
    {
        ArgumentNullException.ThrowIfNull(source);
        return ParseSchemaCoordinate(new SourceText(source.Body.AsMemory()));
    }

    /// <summary>Parses one strict GraphQL schema coordinate from a string.</summary>
    public static SchemaCoordinateNode ParseSchemaCoordinate(string source)
    {
        ArgumentNullException.ThrowIfNull(source);
        return ParseSchemaCoordinate(new SourceText(source.AsMemory()));
    }

    /// <summary>Parses one non-empty GraphQL document.</summary>
    public DocumentNode ParseDocument()
    {
        if (Current.Kind == TokenKind.EndOfFile)
        {
            if (_lexicalError is not null) throw _lexicalError;
            throw Error("A document must contain at least one definition.");
        }

        var definitions = new List<DefinitionNode>();
        while (Current.Kind != TokenKind.EndOfFile)
        {
            definitions.Add(ShouldParseTypeSystemDefinition()
                ? ParseTypeSystemDefinition()
                : IsName("fragment") || IsDescriptionFollowedBy("fragment") ? ParseFragmentDefinition() : ParseOperationDefinition());
        }

        if (_lexicalError is not null) throw _lexicalError;

        return new DocumentNode(_source, definitions, AstLocation(0, _source.Length), _sourceInfo);
    }

    /// <summary>Parses a document, returning valid definitions and source-ordered diagnostics.</summary>
    public GraphQLParseResult ParseDocumentWithDiagnostics()
    {
        var definitions = new List<DefinitionNode>();
        var diagnostics = new List<GraphQLDiagnostic>();
        _activeDiagnostics = diagnostics;
        var lexicalDiagnosticAdded = false;
        while (true)
        {
            try
            {
                if (Current.Kind == TokenKind.EndOfFile) break;
                definitions.Add(ShouldParseTypeSystemDefinition()
                    ? ParseTypeSystemDefinition()
                    : IsName("fragment") || IsDescriptionFollowedBy("fragment") ? ParseFragmentDefinition() : ParseOperationDefinition());
                if (_diagnosticsTruncated) break;
            }
            catch (DiagnosticLimitReachedException)
            {
                break;
            }
            catch (GraphQLSyntaxException exception)
            {
                if (!AddDiagnostic(new GraphQLDiagnostic("syntax", exception.Message, exception.Expected, exception.Actual,
                    new SourceLocation(exception.Position, exception.Position + exception.Length)))) break;
                if (_diagnosticsTruncated) break;
                RecoverToNextDefinition();
            }
            catch (GraphQLLexicalException exception)
            {
                _ = AddDiagnostic(CreateLexicalDiagnostic(exception));
                lexicalDiagnosticAdded = true;
                break;
            }
        }

        if (_lexicalError is not null && !lexicalDiagnosticAdded)
        {
            _ = AddDiagnostic(CreateLexicalDiagnostic(_lexicalError));
        }
        else if (definitions.Count == 0 && diagnostics.Count == 0)
        {
            var emptyDocumentError = Error("A document must contain at least one definition.");
            if (emptyDocumentError is GraphQLSyntaxException syntaxError)
            {
                AddDiagnostic(new GraphQLDiagnostic("syntax", syntaxError.Message, syntaxError.Expected, syntaxError.Actual,
                    new SourceLocation(syntaxError.Position, syntaxError.Position + syntaxError.Length)));
            }
        }

        DocumentNode? document = definitions.Count == 0 ? null : new DocumentNode(_source, definitions, AstLocation(0, _source.Length), _sourceInfo);
        _activeDiagnostics = null;
        return new GraphQLParseResult(document, diagnostics, _diagnosticsTruncated);
    }

    private bool AddDiagnostic(GraphQLDiagnostic diagnostic)
    {
        var diagnostics = _activeDiagnostics ?? throw new InvalidOperationException("Diagnostic collection is not active.");
        if (diagnostics.Count >= _options.MaximumDiagnosticCount)
        {
            _diagnosticsTruncated = true;
            return false;
        }

        diagnostics.Add(diagnostic);
        return true;
    }

    private GraphQLDiagnostic CreateLexicalDiagnostic(GraphQLLexicalException exception)
    {
        var position = exception.Position;
        var length = exception.Length;
        var actual = position + length <= _source.Length ? _source.Slice(position, length).ToString() : string.Empty;
        return new GraphQLDiagnostic("lexical", exception.Message, "a valid GraphQL token", actual,
            new SourceLocation(position, position + length));
    }

    private void RecoverToNextDefinition()
    {
        var consumed = false;
        while (Current.Kind != TokenKind.EndOfFile)
        {
            if (consumed && AtTopLevel && IsDefinitionStart(Current)) return;
            Advance();
            consumed = true;
        }
    }

    private bool AtTopLevel => _braceDepth == 0 && _parenthesisDepth == 0 && _bracketDepth == 0;

    private static bool IsDefinitionStart(Token token)
    {
        if (token.Kind is TokenKind.BraceLeft or TokenKind.String or TokenKind.BlockString) return true;
        if (token.Kind != TokenKind.Name) return false;
        var value = token.Value.Span;
        return value.SequenceEqual("query") || value.SequenceEqual("mutation") || value.SequenceEqual("subscription")
            || value.SequenceEqual("fragment") || value.SequenceEqual("schema") || value.SequenceEqual("scalar")
            || value.SequenceEqual("type") || value.SequenceEqual("interface") || value.SequenceEqual("union")
            || value.SequenceEqual("enum") || value.SequenceEqual("input") || value.SequenceEqual("directive") || value.SequenceEqual("extend");
    }

    private OperationDefinitionNode ParseOperationDefinition()
    {
        var description = ParseDescription();
        var start = description?.Location.Start ?? Current.Start;
        if (Current.Kind == TokenKind.BraceLeft)
        {
            if (description is not null) throw Error("Descriptions cannot be applied to shorthand operations.", description.Location);
            var selectionSet = ParseSelectionSet();
            return new OperationDefinitionNode(OperationType.Query, null, [], [], selectionSet, AstLocation(start, selectionSet.Location.End));
        }

        var operation = ParseOperationType();
        NameNode? name = Current.Kind == TokenKind.Name ? ReadName() : null;
        var variables = Current.Kind == TokenKind.ParenthesisLeft ? ParseVariableDefinitions() : [];
        var directives = ParseDirectives();
        var selection = ParseSelectionSet();
        return new OperationDefinitionNode(operation, name, variables, directives, selection, AstLocation(start, selection.Location.End), description);
    }

    private FragmentDefinitionNode ParseFragmentDefinition()
    {
        var description = ParseDescription();
        var fragmentStart = ExpectName("fragment", "Expected 'fragment'.").Start;
        var start = description?.Location.Start ?? fragmentStart;
        var name = ReadRequiredName("Expected a fragment name.");
        if (name.SourceValue.Span.SequenceEqual("on")) throw Error("A fragment name cannot be 'on'.", name.Location);
        var variables = _options.AllowLegacyFragmentVariables && Current.Kind == TokenKind.ParenthesisLeft ? ParseVariableDefinitions() : [];
        ExpectName("on", "Expected 'on' after the fragment name.");
        var typeName = ReadRequiredName("Expected a fragment type condition.");
        var type = new NamedTypeNode(typeName, typeName.Location);
        var directives = ParseDirectives();
        var selection = ParseSelectionSet();
        return new FragmentDefinitionNode(name, type, directives, selection, AstLocation(start, selection.Location.End), description, variables);
    }

    private bool ShouldParseTypeSystemDefinition()
    {
        if (Current.Kind is TokenKind.String or TokenKind.BlockString)
        {
            if (_index + 1 >= _tokens.Count) return false;
            var next = _tokens[_index + 1];
            if (next.Kind != TokenKind.Name) return false;
            var nextName = next.Value.Span;
            return nextName.SequenceEqual("schema") || nextName.SequenceEqual("scalar") || nextName.SequenceEqual("type")
                || nextName.SequenceEqual("interface") || nextName.SequenceEqual("union") || nextName.SequenceEqual("enum")
                || nextName.SequenceEqual("input") || nextName.SequenceEqual("directive") || nextName.SequenceEqual("extend");
        }
        return IsName("schema") || IsName("scalar") || IsName("type") || IsName("interface")
            || IsName("union") || IsName("enum") || IsName("input") || IsName("directive") || IsName("extend");
    }

    private DefinitionNode ParseTypeSystemDefinition()
    {
        var description = ParseDescription();
        var start = description?.Location.Start ?? Current.Start;
        if (IsName("schema")) return ParseSchemaDefinition(start, description);
        if (IsName("scalar")) return ParseScalarTypeDefinition(start, description);
        if (IsName("type")) return ParseObjectTypeDefinition(start, description);
        if (IsName("interface")) return ParseInterfaceTypeDefinition(start, description);
        if (IsName("union")) return ParseUnionTypeDefinition(start, description);
        if (IsName("enum")) return ParseEnumTypeDefinition(start, description);
        if (IsName("input")) return ParseInputObjectTypeDefinition(start, description);
        if (IsName("directive")) return ParseDirectiveDefinition(start, description);
        if (IsName("extend"))
        {
            if (description is not null) throw Error("Descriptions cannot be applied to extensions.", description.Location);
            return ParseTypeSystemExtension();
        }
        throw Error("Expected a schema or type-system definition.");
    }

    private StringValueNode? ParseDescription()
    {
        if (Current.Kind is not (TokenKind.String or TokenKind.BlockString)) return null;
        var token = Advance();
        return new StringValueNode(token.Value, token.Kind == TokenKind.BlockString, Span(token));
    }

    private SchemaDefinitionNode ParseSchemaDefinition(int start, StringValueNode? description)
    {
        Advance();
        var directives = ParseDirectives(constantArguments: true);
        var open = Expect(TokenKind.BraceLeft, "Expected root operation mappings in schema definition.");
        var operations = new List<OperationTypeDefinitionNode>();
        while (Current.Kind != TokenKind.BraceRight)
        {
            if (Current.Kind == TokenKind.EndOfFile) throw Error("Unterminated schema definition.");
            var opStart = Current.Start;
            var operation = ParseOperationType();
            Expect(TokenKind.Colon, "Expected ':' after root operation kind.");
            var typeName = ReadRequiredName("Expected a root operation type.");
            operations.Add(new OperationTypeDefinitionNode(operation, new NamedTypeNode(typeName, typeName.Location), AstLocation(opStart, typeName.Location.End)));
        }

        if (operations.Count == 0) throw Error("A schema definition requires at least one root operation mapping.");
        var close = Advance();
        _ = open;
        return new SchemaDefinitionNode(operations, directives, AstLocation(start, close.End), description);
    }

    private ScalarTypeDefinitionNode ParseScalarTypeDefinition(int start, StringValueNode? description)
    {
        Advance();
        var name = ReadRequiredName("Expected a scalar type name.");
        var directives = ParseDirectives(constantArguments: true);
        return new ScalarTypeDefinitionNode(name, directives, AstLocation(start, LastConsumedEnd(name.Location.End)), description);
    }

    private ObjectTypeDefinitionNode ParseObjectTypeDefinition(int start, StringValueNode? description)
    {
        Advance();
        var name = ReadRequiredName("Expected an object type name.");
        var interfaces = ParseImplementsInterfaces();
        var directives = ParseDirectives(constantArguments: true);
        var fields = ParseFieldDefinitions(required: false);
        return new ObjectTypeDefinitionNode(name, interfaces, directives, fields, AstLocation(start, LastConsumedEnd(name.Location.End)), description);
    }

    private InterfaceTypeDefinitionNode ParseInterfaceTypeDefinition(int start, StringValueNode? description)
    {
        Advance();
        var name = ReadRequiredName("Expected an interface type name.");
        var interfaces = ParseImplementsInterfaces();
        var directives = ParseDirectives(constantArguments: true);
        var fields = ParseFieldDefinitions(required: false);
        return new InterfaceTypeDefinitionNode(name, interfaces, directives, fields, AstLocation(start, LastConsumedEnd(name.Location.End)), description);
    }

    private List<NamedTypeNode> ParseImplementsInterfaces()
    {
        var interfaces = new List<NamedTypeNode>();
        if (!IsName("implements")) return interfaces;
        Advance();
        if (Current.Kind == TokenKind.Ampersand) Advance();
        interfaces.Add(ParseNamedType());
        while (Current.Kind == TokenKind.Ampersand)
        {
            Advance();
            interfaces.Add(ParseNamedType());
        }

        return interfaces;
    }

    private List<FieldDefinitionNode> ParseFieldDefinitions(bool required)
    {
        var fields = new List<FieldDefinitionNode>();
        if (Current.Kind != TokenKind.BraceLeft)
        {
            if (required) throw Error("Expected a non-empty field definition block.");
            return fields;
        }

        Advance();
        while (Current.Kind != TokenKind.BraceRight)
        {
            if (Current.Kind == TokenKind.EndOfFile) throw Error("Unterminated field definition block.");
            var description = ParseDescription();
            var start = description?.Location.Start ?? Current.Start;
            var name = ReadRequiredName("Expected a field definition name.");
            var arguments = ParseInputValueDefinitions();
            Expect(TokenKind.Colon, "Expected ':' after field definition name.");
            var type = ParseTypeReference();
            var directives = ParseDirectives(constantArguments: true);
            fields.Add(new FieldDefinitionNode(name, arguments, type, directives, AstLocation(start, LastConsumedEnd(type.Location.End)), description));
        }

        Advance();
        return fields;
    }

    private List<InputValueDefinitionNode> ParseInputValueDefinitions()
    {
        var values = new List<InputValueDefinitionNode>();
        if (Current.Kind != TokenKind.ParenthesisLeft) return values;
        Advance();
        while (Current.Kind != TokenKind.ParenthesisRight)
        {
            if (Current.Kind == TokenKind.EndOfFile) throw Error("Unterminated input-value definitions.");
            values.Add(ParseInputValueDefinition());
        }

        if (values.Count == 0) throw Error("Input-value definitions cannot be empty.");
        Advance();
        return values;
    }

    private InputValueDefinitionNode ParseInputValueDefinition()
    {
        var description = ParseDescription();
        var start = description?.Location.Start ?? Current.Start;
        var name = ReadRequiredName("Expected an input-value definition name.");
        Expect(TokenKind.Colon, "Expected ':' after input-value definition name.");
        var type = ParseTypeReference();
        ValueNode? defaultValue = null;
        if (Current.Kind == TokenKind.Equals)
        {
            Advance();
            defaultValue = ParseConstantValue();
        }

        var directives = ParseDirectives(constantArguments: true);
        var end = directives.Count > 0 ? directives[^1].Location.End : (defaultValue ?? (AstNode)type).Location.End;
        return new InputValueDefinitionNode(name, type, defaultValue, directives, AstLocation(start, end), description);
    }

    private UnionTypeDefinitionNode ParseUnionTypeDefinition(int start, StringValueNode? description)
    {
        Advance();
        var name = ReadRequiredName("Expected a union type name.");
        var directives = ParseDirectives(constantArguments: true);
        var types = new List<NamedTypeNode>();
        if (Current.Kind == TokenKind.Equals)
        {
            Advance();
            if (Current.Kind == TokenKind.Pipe) Advance();
            types.Add(ParseNamedType());
            while (Current.Kind == TokenKind.Pipe)
            {
                Advance();
                types.Add(ParseNamedType());
            }
        }

        return new UnionTypeDefinitionNode(name, directives, types, AstLocation(start, LastConsumedEnd(name.Location.End)), description);
    }

    private EnumTypeDefinitionNode ParseEnumTypeDefinition(int start, StringValueNode? description)
    {
        Advance();
        var name = ReadRequiredName("Expected an enum type name.");
        var directives = ParseDirectives(constantArguments: true);
        var values = new List<EnumValueDefinitionNode>();
        if (Current.Kind == TokenKind.BraceLeft)
        {
            Advance();
            while (Current.Kind != TokenKind.BraceRight)
            {
                if (Current.Kind == TokenKind.EndOfFile) throw Error("Unterminated enum value block.");
                var valueDescription = ParseDescription();
                var valueStart = valueDescription?.Location.Start ?? Current.Start;
                if (Current.Kind != TokenKind.Name || IsName("true") || IsName("false") || IsName("null")) throw Error("Expected an enum value name other than true, false, or null.");
                var valueName = ReadName();
                var valueDirectives = ParseDirectives(constantArguments: true);
                values.Add(new EnumValueDefinitionNode(valueName, valueDirectives, AstLocation(valueStart, LastConsumedEnd(valueName.Location.End)), valueDescription));
            }

            if (values.Count == 0) throw Error("An enum value block cannot be empty.");
            Advance();
        }

        return new EnumTypeDefinitionNode(name, directives, values, AstLocation(start, LastConsumedEnd(name.Location.End)), description);
    }

    private InputObjectTypeDefinitionNode ParseInputObjectTypeDefinition(int start, StringValueNode? description)
    {
        Advance();
        var name = ReadRequiredName("Expected an input-object type name.");
        var directives = ParseDirectives(constantArguments: true);
        var fields = new List<InputValueDefinitionNode>();
        if (Current.Kind == TokenKind.BraceLeft)
        {
            Advance();
            while (Current.Kind != TokenKind.BraceRight)
            {
                if (Current.Kind == TokenKind.EndOfFile) throw Error("Unterminated input-object field block.");
                fields.Add(ParseInputValueDefinition());
            }

            if (fields.Count == 0) throw Error("An input-object field block cannot be empty.");
            Advance();
        }

        return new InputObjectTypeDefinitionNode(name, directives, fields, AstLocation(start, LastConsumedEnd(name.Location.End)), description);
    }

    private NamedTypeNode ParseNamedType()
    {
        var name = ReadRequiredName("Expected a named type.");
        return new NamedTypeNode(name, name.Location);
    }

    private int LastConsumedEnd(int fallback) => _index == 0 ? fallback : Math.Max(fallback, _tokens[_index - 1].End);

    private DirectiveDefinitionNode ParseDirectiveDefinition(int start, StringValueNode? description)
    {
        Advance();
        Expect(TokenKind.At, "Expected '@' before directive definition name.");
        var name = ReadRequiredName("Expected a directive definition name.");
        var arguments = ParseInputValueDefinitions();
        var repeatable = IsName("repeatable");
        if (repeatable) Advance();
        ExpectName("on", "Expected 'on' before directive locations.");
        var locations = new List<NameNode>();
        if (Current.Kind == TokenKind.Pipe) Advance();
        locations.Add(ParseDirectiveLocation());
        while (Current.Kind == TokenKind.Pipe)
        {
            Advance();
            locations.Add(ParseDirectiveLocation());
        }

        return new DirectiveDefinitionNode(name, arguments, repeatable, locations,
            AstLocation(start, LastConsumedEnd(name.Location.End)), description);
    }

    private NameNode ParseDirectiveLocation()
    {
        var name = ReadRequiredName("Expected a directive location.");
        if (!ValidDirectiveLocations.Contains(name.Value.ToString())) throw Error("Expected a valid directive location.", name.Location);
        return name;
    }

    private DefinitionNode ParseTypeSystemExtension()
    {
        var start = ExpectName("extend", "Expected 'extend'.").Start;
        if (IsName("schema"))
        {
            Advance();
            var directives = ParseDirectives(constantArguments: true);
            var operations = new List<OperationTypeDefinitionNode>();
            if (Current.Kind == TokenKind.BraceLeft)
            {
                Advance();
                while (Current.Kind != TokenKind.BraceRight)
                {
                    if (Current.Kind == TokenKind.EndOfFile) throw Error("Unterminated schema extension.");
                    var opStart = Current.Start;
                    var operation = ParseOperationType();
                    Expect(TokenKind.Colon, "Expected ':' after root operation kind.");
                    var typeName = ReadRequiredName("Expected a root operation type.");
                    operations.Add(new OperationTypeDefinitionNode(operation, new NamedTypeNode(typeName, typeName.Location), AstLocation(opStart, typeName.Location.End)));
                }

                if (operations.Count == 0) throw Error("A schema extension root-operation block cannot be empty.");
                Advance();
            }

            if (operations.Count == 0 && directives.Count == 0) throw Error("A schema extension must add a root operation mapping or directive.");
            return new SchemaExtensionNode(operations, directives, AstLocation(start, LastConsumedEnd(start)));
        }

        if (IsName("scalar"))
        {
            Advance();
            var name = ReadRequiredName("Expected a scalar type name.");
            var directives = ParseDirectives(constantArguments: true);
            if (directives.Count == 0) throw Error("A scalar extension must add at least one directive.");
            return new ScalarTypeExtensionNode(name, directives, AstLocation(start, LastConsumedEnd(name.Location.End)));
        }

        if (IsName("type"))
        {
            Advance();
            var name = ReadRequiredName("Expected an object type name.");
            var interfaces = ParseImplementsInterfaces();
            var directives = ParseDirectives(constantArguments: true);
            var fields = ParseFieldDefinitionsIfPresent();
            if (interfaces.Count == 0 && directives.Count == 0 && fields.Count == 0) throw Error("An object type extension must add content.");
            return new ObjectTypeExtensionNode(name, interfaces, directives, fields, AstLocation(start, LastConsumedEnd(name.Location.End)));
        }

        if (IsName("interface"))
        {
            Advance();
            var name = ReadRequiredName("Expected an interface type name.");
            var interfaces = ParseImplementsInterfaces();
            var directives = ParseDirectives(constantArguments: true);
            var fields = ParseFieldDefinitionsIfPresent();
            if (interfaces.Count == 0 && directives.Count == 0 && fields.Count == 0) throw Error("An interface type extension must add content.");
            return new InterfaceTypeExtensionNode(name, interfaces, directives, fields, AstLocation(start, LastConsumedEnd(name.Location.End)));
        }

        if (IsName("union"))
        {
            Advance();
            var name = ReadRequiredName("Expected a union type name.");
            var directives = ParseDirectives(constantArguments: true);
            var types = ParseUnionMembersIfPresent();
            if (directives.Count == 0 && types.Count == 0) throw Error("A union extension must add content.");
            return new UnionTypeExtensionNode(name, directives, types, AstLocation(start, LastConsumedEnd(name.Location.End)));
        }

        if (IsName("enum"))
        {
            Advance();
            var name = ReadRequiredName("Expected an enum type name.");
            var directives = ParseDirectives(constantArguments: true);
            var values = ParseEnumValuesIfPresent();
            if (directives.Count == 0 && values.Count == 0) throw Error("An enum extension must add content.");
            return new EnumTypeExtensionNode(name, directives, values, AstLocation(start, LastConsumedEnd(name.Location.End)));
        }

        if (IsName("input"))
        {
            Advance();
            var name = ReadRequiredName("Expected an input-object type name.");
            var directives = ParseDirectives(constantArguments: true);
            var fields = ParseInputFieldsIfPresent();
            if (directives.Count == 0 && fields.Count == 0) throw Error("An input-object extension must add content.");
            return new InputObjectTypeExtensionNode(name, directives, fields, AstLocation(start, LastConsumedEnd(name.Location.End)));
        }

        throw Error("Expected a schema, scalar, object, interface, union, enum, or input extension.");
    }

    private List<FieldDefinitionNode> ParseFieldDefinitionsIfPresent() => Current.Kind == TokenKind.BraceLeft ? ParseFieldDefinitions(required: false) : [];

    private List<NamedTypeNode> ParseUnionMembersIfPresent()
    {
        if (Current.Kind != TokenKind.Equals) return [];
        Advance();
        if (Current.Kind == TokenKind.Pipe) Advance();
        var types = new List<NamedTypeNode> { ParseNamedType() };
        while (Current.Kind == TokenKind.Pipe)
        {
            Advance();
            types.Add(ParseNamedType());
        }

        return types;
    }

    private List<EnumValueDefinitionNode> ParseEnumValuesIfPresent()
    {
        if (Current.Kind != TokenKind.BraceLeft) return [];
        Advance();
        var values = new List<EnumValueDefinitionNode>();
        while (Current.Kind != TokenKind.BraceRight)
        {
            if (Current.Kind == TokenKind.EndOfFile) throw Error("Unterminated enum value block.");
            var description = ParseDescription();
            var start = description?.Location.Start ?? Current.Start;
            if (Current.Kind != TokenKind.Name || IsName("true") || IsName("false") || IsName("null")) throw Error("Expected an enum value name other than true, false, or null.");
            var name = ReadName();
            var directives = ParseDirectives(constantArguments: true);
            values.Add(new EnumValueDefinitionNode(name, directives, AstLocation(start, LastConsumedEnd(name.Location.End)), description));
        }

        if (values.Count == 0) throw Error("An enum value block cannot be empty.");
        Advance();
        return values;
    }

    private List<InputValueDefinitionNode> ParseInputFieldsIfPresent()
    {
        if (Current.Kind != TokenKind.BraceLeft) return [];
        Advance();
        var fields = new List<InputValueDefinitionNode>();
        while (Current.Kind != TokenKind.BraceRight)
        {
            if (Current.Kind == TokenKind.EndOfFile) throw Error("Unterminated input-object field block.");
            fields.Add(ParseInputValueDefinition());
        }

        if (fields.Count == 0) throw Error("An input-object field block cannot be empty.");
        Advance();
        return fields;
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
            var description = ParseDescription();
            var start = description?.Location.Start ?? Current.Start;
            Expect(TokenKind.Dollar, "Expected '$' before a variable name.");
            var variableStart = _tokens[_index - 1].Start;
            var variableName = ReadRequiredName("Expected a variable name.");
            var variable = new VariableNode(variableName, AstLocation(variableStart, variableName.Location.End));
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
            variables.Add(new VariableDefinitionNode(variable, type, defaultValue, directives, AstLocation(start, end), description));
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
            type = new ListTypeNode(inner, AstLocation(start, close.End));
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
            type = new NonNullTypeNode(type, AstLocation(start, bang.End));
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

            return new ListValueNode(values, AstLocation(start, Advance().End));
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
                fields.Add(new ObjectFieldNode(fieldName, value, AstLocation(fieldName.Location.Start, value.Location.End)));
            }

            return new ObjectValueNode(fields, AstLocation(start, Advance().End));
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
        var setBraceDepth = _braceDepth;
        var setParenthesisDepth = _parenthesisDepth;
        var setBracketDepth = _bracketDepth;
        var selections = new List<SelectionNode>();
        while (Current.Kind != TokenKind.BraceRight)
        {
            if (Current.Kind == TokenKind.EndOfFile) throw Error("Unterminated selection set.");
            try
            {
                selections.Add(Current.Kind == TokenKind.Spread ? ParseFragmentSelection() : ParseField());
            }
            catch (GraphQLSyntaxException exception) when (_activeDiagnostics is not null)
            {
                if (!AddDiagnostic(new GraphQLDiagnostic("syntax", exception.Message, exception.Expected, exception.Actual,
                    new SourceLocation(exception.Position, exception.Position + exception.Length)))) throw new DiagnosticLimitReachedException();
                RecoverToNextSelection(setBraceDepth, setParenthesisDepth, setBracketDepth);
            }
        }

        if (selections.Count == 0) throw Error("A selection set cannot be empty.");
        var close = Advance();
        return new SelectionSetNode(selections, AstLocation(open.Start, close.End));
    }

    private void RecoverToNextSelection(int braceDepth, int parenthesisDepth, int bracketDepth)
    {
        var consumed = false;
        while (Current.Kind != TokenKind.EndOfFile)
        {
            if (_braceDepth == braceDepth && _parenthesisDepth == parenthesisDepth && _bracketDepth == bracketDepth)
            {
                if (Current.Kind == TokenKind.BraceRight) return;
                if (consumed && Current.Kind is TokenKind.Name or TokenKind.Spread) return;
            }

            Advance();
            consumed = true;
        }
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
        return new FieldNode(name, alias, arguments, directives, selection, AstLocation(first.Location.Start, end));
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
            return new InlineFragmentNode(type, directives, selection, AstLocation(start, selection.Location.End));
        }

        if (Current.Kind is TokenKind.At or TokenKind.BraceLeft)
        {
            var directives = ParseDirectives();
            var selection = ParseSelectionSet();
            return new InlineFragmentNode(null, directives, selection, AstLocation(start, selection.Location.End));
        }

        var name = ReadRequiredName("Expected a fragment name or 'on' after '...'.");
        var spreadDirectives = ParseDirectives();
        var end = spreadDirectives.Count > 0 ? spreadDirectives[^1].Location.End : name.Location.End;
        return new FragmentSpreadNode(name, spreadDirectives, AstLocation(start, end));
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
            arguments.Add(new ArgumentNode(name, value, AstLocation(start, value.Location.End)));
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
            directives.Add(new DirectiveNode(name, arguments, AstLocation(start, end)));
        }

        return directives;
    }

    private ValueNode ParseValue()
    {
        if (Current.Kind == TokenKind.Dollar)
        {
            var start = Advance().Start;
            var name = ReadRequiredName("Expected a variable name after '$'.");
            return new VariableNode(name, AstLocation(start, name.Location.End));
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
            return new ListValueNode(values, AstLocation(start, end));
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
                fields.Add(new ObjectFieldNode(fieldName, fieldValue, AstLocation(fieldName.Location.Start, fieldValue.Location.End)));
            }

            var end = Advance().End;
            return new ObjectValueNode(fields, AstLocation(start, end));
        }

        return ParseConstantValue();
    }

    private bool IsName(string value) => Current.Kind == TokenKind.Name && Current.Value.Span.SequenceEqual(value);

    private bool IsDescriptionFollowedBy(string value) =>
        Current.Kind is TokenKind.String or TokenKind.BlockString
        && _index + 1 < _tokens.Count
        && _tokens[_index + 1].Kind == TokenKind.Name
        && _tokens[_index + 1].Value.Span.SequenceEqual(value);

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

    private Token Advance()
    {
        var token = _tokens[_index++];
        switch (token.Kind)
        {
            case TokenKind.BraceLeft: _braceDepth++; break;
            case TokenKind.BraceRight: _braceDepth = Math.Max(0, _braceDepth - 1); break;
            case TokenKind.ParenthesisLeft: _parenthesisDepth++; break;
            case TokenKind.ParenthesisRight: _parenthesisDepth = Math.Max(0, _parenthesisDepth - 1); break;
            case TokenKind.BracketLeft: _bracketDepth++; break;
            case TokenKind.BracketRight: _bracketDepth = Math.Max(0, _bracketDepth - 1); break;
        }

        return token;
    }
    private Token Current => _tokens[_index];
    private SourceLocation Span(Token token) => AstLocation(token.Start, token.End);
    private SourceLocation AstLocation(int start, int end)
    {
        var lineIndex = Array.BinarySearch(_lineStarts, start);
        if (lineIndex < 0) lineIndex = ~lineIndex - 1;
        return new SourceLocation(start, end, lineIndex + 1, start - _lineStarts[lineIndex] + 1, !_options.NoLocation);
    }

    private static int[] BuildLineStarts(ReadOnlySpan<char> source)
    {
        var starts = new List<int> { 0 };
        for (var index = 0; index < source.Length; index++)
        {
            if (source[index] == '\r')
            {
                if (index + 1 < source.Length && source[index + 1] == '\n') index++;
                starts.Add(index + 1);
            }
            else if (source[index] == '\n') starts.Add(index + 1);
        }
        return starts.ToArray();
    }
    private Exception Error(string message)
    {
        if (_lexicalError is not null && _index == _tokens.Count - 1) return _lexicalError;
        return new GraphQLSyntaxException(message, Current.Start, Current.End - Current.Start, message, Describe(Current));
    }
    private static GraphQLSyntaxException Error(string message, SourceLocation location) => new(message, location.Start, location.Length, message, $"source span [{location.Start}, {location.End})");
    private static string Describe(Token token) => token.Kind == TokenKind.EndOfFile
        ? "EndOfFile"
        : $"{token.Kind} '{token.RawValue.ToString()}'";

    private sealed class DiagnosticLimitReachedException : Exception { }
}
