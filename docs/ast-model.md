# AST core and source ownership

AST locations use validated zero-based, half-open offsets in UTF-16 code units, matching `SourceText`, lexer tokens, JavaScript string indexing, and the canonical oracle format. Every node exposes an `AstNodeKind` and immutable `SourceLocation`.

## Source-backed values

Unchanged names and other values that exactly match a source range are represented by `ReadOnlyMemory<char>` slices over the caller's original source. `NameNode` stores such a slice without copying. The caller must keep the input memory alive and must not mutate its backing storage while the AST is in use.

Quoted strings and block strings retain their full raw lexeme on the token for locations and diagnostics. Their `Value` contains the evaluated text: an unescaped quoted string can point into the original source; a string with escapes points to the decoder-owned string; a block string points to the normalized string. AST value nodes will store the corresponding `ReadOnlyMemory<char>` directly, so they retain the backing source or decoded string without another copy. This ownership is safe because `ReadOnlyMemory<char>` holds the backing object alive, while mutation of caller-owned memory remains prohibited.

## Child collections

AST child sequences use `AstNodeList<TNode>`. Its constructor copies the input sequence once, preserves order, and exposes only indexed reads and enumeration. It does not expose the backing array or a mutable collection interface. Nodes held by the list are themselves immutable.

## Executable node shapes

`DocumentNode` retains its `SourceText` and an ordered, non-empty `Definitions` list. `OperationDefinitionNode` requires a selection set and always has variable-definition and directive lists; its name and description are optional. `FragmentDefinitionNode` requires a name, named type condition, and selection set; its description is optional. The pinned `graphql-js` 16.x AST shape includes an empty fragment `variableDefinitions` array, so this property is always empty for the September 2025 grammar.

Selection sets are non-empty. Fields require a name and have optional alias and nested selection set, with ordered argument and directive lists. Fragment spreads require a name; inline fragments require a selection set and may omit the type condition. Arguments, directive applications, object fields, variables, and variable definitions hold their required children as immutable node properties.

## SDL node shapes

Schema and named type definitions preserve optional descriptions and ordered directives and children. The model covers schema root mappings; scalar, object, interface, union, enum, and input-object definitions and extensions; field and input-value definitions; enum-value definitions; and directive definitions. Object and interface nodes preserve implemented interfaces. Union nodes preserve member types. Directive definitions retain their argument definitions, repeatable flag, and ordered source locations. Syntax-required extension content and schema root mappings are enforced by constructors; schema validity and cross-definition rules remain parser-independent semantic validation.

## Parser diagnostics

`GraphQLParser.Parse(SourceText)` is the strict document entry point. A parsed document spans the complete source, including ignored leading and trailing input; definition and child spans cover their syntax tokens. `GraphQLSyntaxException` reports the zero-based UTF-16 start and length of the unexpected token, with a zero-length position at end of input. Lexical failures remain `GraphQLLexicalException` instances from the lexer.

Executable parsing preserves operation and fragment definition order, aliases, argument and directive order, nested selection sets, fragment spreads, and typed or type-less inline fragments. Empty argument and selection sets are rejected according to the grammar.

Value nodes cover variable references, integer and float source spellings, evaluated strings with a block-string flag, booleans, null, enum text, ordered list values, and ordered object fields. List and object values may be empty. Type references are named types wrapped by list or non-null nodes; a non-null node cannot directly wrap another non-null node. Optional syntax is represented by nullable node properties, while child lists are always present and empty when the syntax has no children, matching the canonical comparison contract.
