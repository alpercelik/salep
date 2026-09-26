# AST core and source ownership

AST locations use validated zero-based, half-open offsets in UTF-16 code units, matching `SourceText`, lexer tokens, JavaScript string indexing, and the canonical oracle format. Every node exposes an `AstNodeKind` and immutable `SourceLocation`.

## Source-backed values

Default `GraphQLParser.Parse`, diagnostic parsing, and schema-coordinate parsing snapshot `SourceText` into an immutable string before creating source slices. The caller may release or mutate the original memory after parsing without changing the document, names, values, or locations. `DocumentNode.Source` retains the private immutable snapshot used by its AST.

`ParseBorrowed`, `ParseWithDiagnosticsBorrowed`, and `ParseSchemaCoordinateBorrowed` are explicit zero-copy alternatives. Their AST values and source retain the supplied memory; callers must keep its backing storage alive and must not mutate it while parsing or while any returned node is in use. Direct `GraphQLLexer` use and manually constructed nodes over `SourceText` are also borrowed-memory APIs.

Quoted strings and block strings retain their full raw lexeme on the token for locations and diagnostics. Their `Value` contains evaluated text: unescaped quoted strings may point into the immutable source snapshot; escaped strings point to decoder-owned strings; block strings point to normalized strings. AST value nodes retain the backing string through `ReadOnlyMemory<char>`.

## Child collections

AST child sequences use `AstNodeList<TNode>`. Its constructor copies the input sequence once, preserves order, and exposes only indexed reads and enumeration. It does not expose the backing array or a mutable collection interface. Nodes held by the list are themselves immutable.

## Executable node shapes

`DocumentNode` retains its `SourceText` and an ordered, non-empty `Definitions` list. `OperationDefinitionNode` requires a selection set and always has variable-definition and directive lists; its name and description are optional. `FragmentDefinitionNode` requires a name, named type condition, and selection set; its description is optional. The pinned `graphql-js` 16.x AST shape includes an empty fragment `variableDefinitions` array, so this property is always empty for the September 2025 grammar.

Selection sets are non-empty. Fields require a name and have optional alias and nested selection set, with ordered argument and directive lists. Fragment spreads require a name; inline fragments require a selection set and may omit the type condition. Arguments, directive applications, object fields, variables, and variable definitions hold their required children as immutable node properties.

## SDL node shapes

Schema and named type definitions preserve optional descriptions and ordered directives and children. The model covers schema root mappings; scalar, object, interface, union, enum, and input-object definitions and extensions; field and input-value definitions; enum-value definitions; and directive definitions. Object and interface nodes preserve implemented interfaces. Union nodes preserve member types. Directive definitions retain their argument definitions, repeatable flag, and ordered source locations. Syntax-required extension content and schema root mappings are enforced by constructors; schema validity and cross-definition rules remain parser-independent semantic validation.

## Parser diagnostics

`GraphQLParser.Parse(SourceText)` is the strict document entry point. A parsed document spans the complete source, including ignored leading and trailing input; definition and child spans cover their syntax tokens. `GraphQLSyntaxException` reports the zero-based UTF-16 start and length of the unexpected token, with a zero-length position at end of input. Lexical failures remain `GraphQLLexicalException` instances from the lexer.

Call `GraphQLParser.ParseWithDiagnostics(SourceText)` for opt-in recovery. It returns `GraphQLParseResult`, with valid recovered definitions (or a null document), plus immutable source-ordered diagnostics that include category, expected context, actual token, and location. The strict entry point continues to throw on the first failure.

Diagnostic mode synchronizes at selection boundaries and top-level definition starts while tracking nested braces, parentheses, and brackets. The default is 100 diagnostics; `GraphQLParseResult.DiagnosticsTruncated` indicates that more errors were found. Set `GraphQLParserOptions.MaximumDiagnosticCount` for a different bound (1 through 10,000). Recovery always advances or stops at a closing grammar boundary or end of input.

`GraphQLParserOptions.Default` limits source length to 1,048,576 UTF-16 code units, non-EOF tokens to 250,000, and combined brace/parenthesis/bracket nesting to 128. The parser checks source length before lexing and token count and nesting while materializing tokens, before recursive descent. Call `Parse(source, options)` or `ParseWithDiagnostics(source, options)` to choose smaller or larger limits. Exceeding a limit throws `GraphQLResourceLimitException` in either mode; it includes the resource, configured limit, observed value, and source span. `GraphQLParserOptions` rejects non-positive limits and diagnostic bounds above 10,000.

Executable parsing preserves operation and fragment definition order, aliases, argument and directive order, nested selection sets, fragment spreads, and typed or type-less inline fragments. Empty argument and selection sets are rejected according to the grammar.

SDL parsing preserves descriptions and ordered definitions, schema root mappings, type directives, object/interface implementation lists, field arguments, union members, enum values, and input-object fields. GraphQL directive uses and SDL defaults parse constant values, so variable nodes cannot appear in those positions.

Directive definitions preserve repeatability, argument definitions, and syntactically valid executable or type-system locations. Schema, scalar, object, interface, union, enum, and input-object extensions enforce their grammar-specific added-content requirements. Directive names such as `@oneOf` and `@specifiedBy` remain ordinary names in the parser.

Value nodes cover variable references, integer and float source spellings, evaluated strings with a block-string flag, booleans, null, enum text, ordered list values, and ordered object fields. List and object values may be empty. Type references are named types wrapped by list or non-null nodes; a non-null node cannot directly wrap another non-null node. Optional syntax is represented by nullable node properties, while child lists are always present and empty when the syntax has no children, matching the canonical comparison contract.
