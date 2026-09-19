namespace GraphQLParser;

/// <summary>Identifies a GraphQL lexical token.</summary>
public enum TokenKind : byte
{
    /// <summary>The stable end-of-file sentinel.</summary>
    EndOfFile,
    /// <summary>The <c>!</c> punctuator.</summary>
    Bang,
    /// <summary>The <c>$</c> punctuator.</summary>
    Dollar,
    /// <summary>The <c>&amp;</c> punctuator.</summary>
    Ampersand,
    /// <summary>The <c>(</c> punctuator.</summary>
    ParenthesisLeft,
    /// <summary>The <c>)</c> punctuator.</summary>
    ParenthesisRight,
    /// <summary>The <c>...</c> spread token.</summary>
    Spread,
    /// <summary>The <c>:</c> punctuator.</summary>
    Colon,
    /// <summary>The <c>=</c> punctuator.</summary>
    Equals,
    /// <summary>The <c>@</c> punctuator.</summary>
    At,
    /// <summary>The <c>[</c> punctuator.</summary>
    BracketLeft,
    /// <summary>The <c>]</c> punctuator.</summary>
    BracketRight,
    /// <summary>The <c>{</c> punctuator.</summary>
    BraceLeft,
    /// <summary>The <c>|</c> punctuator.</summary>
    Pipe,
    /// <summary>The <c>}</c> punctuator.</summary>
    BraceRight,
}
