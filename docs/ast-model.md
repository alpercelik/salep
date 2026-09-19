# AST core and source ownership

AST locations use validated zero-based, half-open offsets in UTF-16 code units, matching `SourceText`, lexer tokens, JavaScript string indexing, and the canonical oracle format. Every node exposes an `AstNodeKind` and immutable `SourceLocation`.

## Source-backed values

Unchanged names and other values that exactly match a source range are represented by `ReadOnlyMemory<char>` slices over the caller's original source. `NameNode` stores such a slice without copying. The caller must keep the input memory alive and must not mutate its backing storage while the AST is in use.

Quoted strings and block strings retain their full raw lexeme on the token for locations and diagnostics. Their `Value` contains the evaluated text: an unescaped quoted string can point into the original source; a string with escapes points to the decoder-owned string; a block string points to the normalized string. AST value nodes will store the corresponding `ReadOnlyMemory<char>` directly, so they retain the backing source or decoded string without another copy. This ownership is safe because `ReadOnlyMemory<char>` holds the backing object alive, while mutation of caller-owned memory remains prohibited.

## Child collections

AST child sequences use `AstNodeList<TNode>`. Its constructor copies the input sequence once, preserves order, and exposes only indexed reads and enumeration. It does not expose the backing array or a mutable collection interface. Nodes held by the list are themselves immutable.
