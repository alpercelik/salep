namespace Salep.ClientGenerator.Targets;

/// <summary>Configures optional C# test and agent-guidance output.</summary>
public sealed record CSharpTestGenerationOptions
{
    /// <summary>Destination namespace for generated test classes.</summary>
    public string? Namespace { get; init; }

    /// <summary>Selects transport, operations, unions, and sample test suites.</summary>
    public IReadOnlySet<string> Suites { get; init; } = new HashSet<string>(["transport", "operations", "unions"], StringComparer.Ordinal);

    /// <summary>Emits JSON test data as C# raw string literals.</summary>
    public bool UseRawJsonLiterals { get; init; }

    /// <summary>Emits generated agent guidance alongside tests.</summary>
    public bool EmitAgentInstructions { get; init; }

    /// <summary>Additional namespaces that own shared client types used by generated tests.</summary>
    public IReadOnlyList<string> AdditionalImports { get; init; } = [];
}
