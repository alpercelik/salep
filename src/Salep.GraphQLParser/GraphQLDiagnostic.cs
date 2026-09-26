namespace Salep.GraphQLParser;

/// <summary>A recoverable parser diagnostic with source and token context.</summary>
public sealed class GraphQLDiagnostic
{
    internal GraphQLDiagnostic(string category, string message, string expected, string actual, SourceLocation location)
    {
        Category = category;
        Message = message;
        Expected = expected;
        Actual = actual;
        Location = location;
    }

    /// <summary>Gets the failure category, either <c>lexical</c> or <c>syntax</c>.</summary>
    public string Category { get; }
    /// <summary>Gets the diagnostic message.</summary>
    public string Message { get; }
    /// <summary>Gets the expected token or grammar context.</summary>
    public string Expected { get; }
    /// <summary>Gets the actual token kind and spelling.</summary>
    public string Actual { get; }
    /// <summary>Gets the half-open UTF-16 source location.</summary>
    public SourceLocation Location { get; }
}

/// <summary>The document and diagnostics produced by an opt-in diagnostic parse.</summary>
public sealed class GraphQLParseResult
{
    internal GraphQLParseResult(DocumentNode? document, IEnumerable<GraphQLDiagnostic> diagnostics, bool diagnosticsTruncated)
    {
        Document = document;
        Diagnostics = Array.AsReadOnly(diagnostics.ToArray());
        DiagnosticsTruncated = diagnosticsTruncated;
    }

    /// <summary>Gets the parsed document, or null when no valid definitions were recovered.</summary>
    public DocumentNode? Document { get; }
    /// <summary>Gets immutable diagnostics in source order.</summary>
    public IReadOnlyList<GraphQLDiagnostic> Diagnostics { get; }
    /// <summary>Gets whether parsing stopped because the diagnostic limit was reached.</summary>
    public bool DiagnosticsTruncated { get; }
    /// <summary>Gets whether parsing completed without diagnostics.</summary>
    public bool Success => Diagnostics.Count == 0 && Document is not null;
}
