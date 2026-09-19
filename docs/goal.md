# GraphQL C# Parser Implementation Plan

This document outlines a step-by-step implementation plan for an AI agent to build a custom, spec-compliant GraphQL lexer and recursive-descent parser in C#. The target implementation must support all current GraphQL specification features (executable documents and SDL), provide feature parity with the GraphQL reference implementation without copying source code, and maintain low memory allocations using modern C# features (`ReadOnlySpan<char>`, `ReadOnlyMemory<char>`, `ref struct`).

The intended result is a production-ready, battle-tested parser. Correctness, predictable diagnostics, bounded resource use, and maintainability take priority over an allocation target when they conflict. Pin the supported GraphQL specification edition and document any remaining limitations before claiming conformance.

## Continuous Test Harness

Build a runnable test harness with the initial C# solution, before implementing lexer or parser behavior. Keep it working throughout every milestone: each implementation slice adds positive, negative, and boundary cases relevant to its grammar or API change, runs the focused tests and full current suite, and records the results. A passing empty or placeholder suite is not evidence.

Seed a version-pinned `graphql-js` oracle and canonical AST comparison early, then expand its valid and invalid fixture corpus as lexer, executable, and SDL support arrive. Keep fixture generation reproducible and retain regression cases for every discovered defect. Measure allocations and throughput against recorded baselines without weakening conformance tests. A milestone is complete only when its behavior and the full harness pass from a clean build.

---

## Architectural Guiding Principles

1. **Zero-Copy Lexing:** The lexer must operate on a `ReadOnlySpan<char>` source and return stack-allocated `ref struct Token` instances representing slices of the source document.
2. **Span-Friendly AST:** AST nodes are immutable `record` types storing identifiers and values using `ReadOnlyMemory<char>` to preserve source slices on the heap without string allocations.
3. **Spec-Driven Grammar:** Parsing rules map 1:1 to the official GraphQL Specification EBNF grammar productions.
4. **Black-Box Validation:** Compliance is validated by parsing test fixtures and comparing serialized JSON outputs against the official JavaScript reference implementation (`graphql-js`).

---

## Milestone 1: Zero-Allocation Lexer (`GraphQLReader` & `Token`)

### 1.1 Core Types Definition
Define the token representation and token classification enumeration.

```csharp
public enum TokenKind : byte
{
    Eof,
    Bang,          // !
    Dollar,        // $
    Ampersand,     // &
    ParenL,        // (
    ParenR,        // )
    Spread,        // ...
    Colon,         // :
    Equals,        // =
    At,            // @
    BracketL,      // [
    BracketR,      // ]
    BraceL,        // {
    Pipe,          // |
    BraceR,        // }
    Name,          // Identifier
    Int,           // Integer literal
    Float,         // Float literal
    String,        // "..."
    BlockString    // """..."""
}

public readonly ref struct Token
{
    public TokenKind Kind { get; }
    public int Start { get; }
    public int End { get; }
    public ReadOnlySpan<char> Value { get; }

    public Token(TokenKind kind, int start, int end, ReadOnlySpan<char> value)
    {
        Kind = kind;
        Start = start;
        End = end;
        Value = value;
    }
}
```

### 1.2 Lexer Implementation Tasks
Create a `ref struct GraphQLLexer` that moves a pointer/index through `ReadOnlySpan<char>`.

* **Ignored Characters:**
  * Automatically skip Unicode Byte Order Mark (BOM: `\uFEFF`), Whitespace (`\u0009`, `\u0020`), Line terminators (`\u000A`, `\u000D`), Commas (`,`), and Comments (`#...` to end of line).
* **Punctuators:**
  * Lex single-character punctuators directly into their corresponding `TokenKind`.
  * Handle three consecutive period characters `...` as `TokenKind.Spread`.
* **Names/Identifiers:**
  * Match regex `[_A-Za-z][_0-9A-Za-z]*`.
* **Numbers:**
  * Handle `Int` and `Float` per spec rules (optional sign `-`, integer component, optional fractional component `.123`, optional exponent `e+10`).
* **Strings & Block Strings:**
  * Differentiate standard double-quoted strings (`"..."`) from block strings (`"""..."""`).
  * Process escape sequences (`\n`, `\r`, `\t`, `\\`, `\/`, `\"`, `\uXXXX`).
  * Implement the spec-mandated **Block String Indentation Trimming Algorithm**:
    1. Split raw text into lines on `\r\n`, `\r`, or `\n`.
    2. Determine common indent (count leading spaces/tabs on non-empty lines, ignoring line 0).
    3. Remove common indent from each line (except line 0).
    4. Remove leading and trailing blank lines.

---

## Milestone 2: Immutable AST Representation

### 2.1 AST Core Infrastructure
All AST nodes must inherit from a common base record and capture location tracking.

```csharp
public readonly record struct SourceLocation(int Start, int End);

public abstract record AstNode
{
    public SourceLocation Location { get; init; }
}

public record NameNode(ReadOnlyMemory<char> Value) : AstNode;
```

### 2.2 Complete Node Taxonomy
Implement standard AST node record definitions matching the spec:

1. **Document & Top-Level Definitions:**
   * `DocumentNode(IReadOnlyList<IDefinitionNode> Definitions)`
   * `OperationDefinitionNode`, `FragmentDefinitionNode`, `SchemaDefinitionNode`, `TypeDefinitionNode`, `DirectiveDefinitionNode`, `SchemaExtensionNode`, `TypeExtensionNode`
2. **Selections:**
   * `SelectionSetNode(IReadOnlyList<ISelectionNode> Selections)`
   * `FieldNode(NameNode? Alias, NameNode Name, IReadOnlyList<ArgumentNode> Arguments, IReadOnlyList<DirectiveNode> Directives, SelectionSetNode? SelectionSet)`
   * `FragmentSpreadNode(NameNode Name, IReadOnlyList<DirectiveNode> Directives)`
   * `InlineFragmentNode(TypeConditionNode? TypeCondition, IReadOnlyList<DirectiveNode> Directives, SelectionSetNode SelectionSet)`
3. **Values:**
   * `VariableNode`, `IntValueNode`, `FloatValueNode`, `StringValueNode`, `BooleanValueNode`, `NullValueNode`, `EnumValueNode`, `ListValueNode`, `ObjectValueNode`
4. **Type References:**
   * `NamedTypeNode`, `ListTypeNode`, `NonNullTypeNode`
5. **Schema Definition Language (SDL):**
   * `ObjectTypeDefinitionNode`, `InterfaceTypeDefinitionNode` (including implemented interfaces), `UnionTypeDefinitionNode`, `EnumTypeDefinitionNode`, `InputObjectTypeDefinitionNode`, `ScalarTypeDefinitionNode`

---

## Milestone 3: Fail-Fast Recursive Descent Parser

Implement `GraphQLParser`, consuming the lexer's token stream to construct AST nodes. In this phase, throw `GraphQLSyntaxException` immediately on any token mismatch.

```csharp
public ref struct GraphQLParser
{
    private GraphQLLexer _lexer;
    private Token _currentToken;
    private readonly ReadOnlyMemory<char> _sourceMemory;

    public GraphQLParser(ReadOnlyMemory<char> source)
    {
        _sourceMemory = source;
        _lexer = new GraphQLLexer(source.Span);
        _currentToken = _lexer.NextToken();
    }

    public DocumentNode ParseDocument()
    {
        var definitions = new List<IDefinitionNode>();
        var start = _currentToken.Start;

        while (_currentToken.Kind != TokenKind.Eof)
        {
            definitions.Add(ParseDefinition());
        }

        return new DocumentNode(definitions)
        {
            Location = new SourceLocation(start, _currentToken.End)
        };
    }
}
```

### 3.1 Key Grammar Production Methods

* `ParseDefinition()`: Route to `ParseExecutableDefinition()` or `ParseTypeSystemDefinition()` based on keywords (`query`, `mutation`, `subscription`, `fragment`, `schema`, `type`, `interface`, `union`, `enum`, `input`, `scalar`, `directive`, `extend`).
* `ParseSelectionSet()`: Expect `{`, parse zero or more selections (`Field`, `FragmentSpread`, `InlineFragment`), expect `}`.
* `ParseField()`: Lookahead to determine whether first `Name` is an alias (`alias: fieldName`) or the field name itself. Parse arguments `(...)`, directives `@...`, and sub-selection sets `{...}`.
* `ParseValueLiteral()`: Parse primitives, variables (`$var`), lists (`[...]`), or input objects (`{ key: value }`).
* `ParseTypeReference()`: Parse nested named, list, or non-null types (e.g., `[String!]!`).
* **Spec Feature Parity Rules:**
  * Support interfaces implementing interfaces (`type A implements B & C`).
  * Support `@oneOf` directives on input objects.
  * Support custom scalar definitions and `@specifiedBy`.
  * Support schema/type extensions (`extend type ...`).

---

## Milestone 4: Expand the Black-Box Compliance Test Harness

The harness starts with the initial solution and grows with each implementation slice. This milestone expands its corpus and differential coverage using `graphql-js` without copying reference parser code.

### 4.1 Reference Artifact Generation Strategy
1. **Fixture Extraction:** Pull `.graphql` query and schema fixture files directly from the `graphql/graphql-js` GitHub repository (`src/language/__tests__`).
2. **Node.js Generator Script:** Write a small script using `graphql-js` that parses each `.graphql` file into an AST and outputs a standard `.json` AST representation:
   ```javascript
   const fs = require('fs');
   const { parse } = require('graphql');

   const source = fs.readFileSync(process.argv[2], 'utf8');
   const ast = parse(source, { noLocation: false });
   console.log(JSON.stringify(ast, null, 2));
   ```

### 4.2 C# Integration Test Suite
Write an xUnit test suite that:
1. Reads a target `.graphql` fixture file.
2. Parses it using `GraphQLParser`.
3. Serializes the generated C# `DocumentNode` to JSON using `System.Text.Json`.
4. Compares the generated AST against the reference JSON output.

---

## Milestone 5: Error Accumulation & Parser Synchronization

Transition from fail-fast throwing to collecting syntax errors and resuming parsing.

### 5.1 Error Collection State
Update `GraphQLParser` to accept a `List<GraphQLError>` accumulator.

```csharp
public record GraphQLError(string Message, SourceLocation Location);
```

### 5.2 Synchronization & Recovery Algorithm
When encountering an unexpected token:
1. Log a `GraphQLError` detailing expected vs. actual tokens at current `SourceLocation`.
2. Trigger the `Synchronize()` routine to skip tokens until hitting a safe structural boundary:
   * **Selection Level:** Skip until finding `}`, `...`, or top-level field names.
   * **Definition Level:** Skip until finding `query`, `mutation`, `subscription`, `fragment`, `type`, `interface`, `union`, `enum`, `input`, `scalar`, `schema`, or `Eof`.
3. Resume recursive descent parsing from the synchronized state.

---

## Milestone 6: Allocation-Free Optimization Pass

Refine the implementation to eliminate unnecessary heap allocations during lexing and parsing.

### 6.1 Memory Optimization Checklist

* [ ] **String Deduplication:** Ensure field names, operation keywords, and directives reuse static singletons or string tables where appropriate.
* [ ] **Zero-Allocation Slicing:** Verify `NameNode` and `StringValueNode` slice source text via `ReadOnlyMemory<char>.Slice()` without allocating new strings.
* [ ] **Custom List Allocations:** Evaluate replacing standard `List<T>` allocations in parser loops with a temporary array pool (`ArrayPool<T>.Shared`) before finalizing node arrays.
* [ ] **Struct Lexer Validation:** Confirm `GraphQLLexer` remains a `ref struct` that generates zero GC pressure on hot tokenization paths.

---

## Final Verification Checklist

1. **Continuous Evidence:** The harness has run throughout implementation, each milestone has positive, negative, boundary, and regression coverage, and the final clean build runs the full suite.
2. **Spec Parity:** Parses all standard executable documents, schema definitions, extensions, custom directives, `@oneOf` input objects, and interfaces implementing interfaces.
3. **Edge Cases:** Correctly processes Unicode escape sequences, block string indentation trimming rules, omitted optional tokens, and ignored whitespace/commas.
4. **Error Reporting:** Reports full multi-error lists with precise source locations when fed malformed GraphQL input files.
5. **Compliance Validation:** Passes 100% of the fixture tests generated by the reference JavaScript parser test runner.
6. **Production Stress:** Seeded fuzzing and resource-bound suites pass without crashes, hangs, or undocumented failure behavior.
