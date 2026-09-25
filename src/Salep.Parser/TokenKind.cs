namespace Salep.Parser;

/// <summary>Identifies a GraphQL lexical token.</summary>
public enum TokenKind : byte
{
    /// <summary>The start-of-file sentinel.</summary>
    StartOfFile = 0,
    /// <summary>The stable end-of-file sentinel.</summary>
    EndOfFile = 1,
    /// <summary>The <c>!</c> punctuator.</summary>
    Bang = 2,
    /// <summary>The <c>?</c> punctuator.</summary>
    QuestionMark = 3,
    /// <summary>The <c>$</c> punctuator.</summary>
    Dollar = 4,
    /// <summary>The <c>&amp;</c> punctuator.</summary>
    Ampersand = 5,
    /// <summary>The <c>(</c> punctuator.</summary>
    ParenthesisLeft = 6,
    /// <summary>Compatibility alias for <see cref="ParenthesisLeft"/>.</summary>
    LeftParenthesis = ParenthesisLeft,
    /// <summary>The <c>)</c> punctuator.</summary>
    ParenthesisRight = 7,
    /// <summary>Compatibility alias for <see cref="ParenthesisRight"/>.</summary>
    RightParenthesis = ParenthesisRight,
    /// <summary>The <c>...</c> spread token.</summary>
    Spread = 8,
    /// <summary>The <c>:</c> punctuator.</summary>
    Colon = 9,
    /// <summary>The <c>=</c> punctuator.</summary>
    Equals = 10,
    /// <summary>Compatibility alias for <see cref="Equals"/>.</summary>
    Equal = Equals,
    /// <summary>The <c>@</c> punctuator.</summary>
    At = 11,
    /// <summary>The <c>[</c> punctuator.</summary>
    BracketLeft = 12,
    /// <summary>Compatibility alias for <see cref="BracketLeft"/>.</summary>
    LeftBracket = BracketLeft,
    /// <summary>The <c>]</c> punctuator.</summary>
    BracketRight = 13,
    /// <summary>Compatibility alias for <see cref="BracketRight"/>.</summary>
    RightBracket = BracketRight,
    /// <summary>The <c>{</c> punctuator.</summary>
    BraceLeft = 14,
    /// <summary>Compatibility alias for <see cref="BraceLeft"/>.</summary>
    LeftBrace = BraceLeft,
    /// <summary>The <c>|</c> punctuator.</summary>
    Pipe = 16,
    /// <summary>The <c>}</c> punctuator.</summary>
    BraceRight = 15,
    /// <summary>Compatibility alias for <see cref="BraceRight"/>.</summary>
    RightBrace = BraceRight,
    /// <summary>A GraphQL name.</summary>
    Name = 17,
    /// <summary>An integer literal.</summary>
    Integer = 18,
    /// <summary>A floating-point literal.</summary>
    Float = 19,
    /// <summary>A quoted string literal.</summary>
    String = 20,
    /// <summary>A triple-quoted block string literal.</summary>
    BlockString = 21,
    /// <summary>A GraphQL comment.</summary>
    Comment = 22,
    /// <summary>The dot separator used in schema coordinates.</summary>
    Dot = 23,
}
