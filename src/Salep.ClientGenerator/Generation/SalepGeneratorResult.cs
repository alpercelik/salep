namespace Salep.ClientGenerator.Generation;

public sealed record SalepGeneratorResult(
    IReadOnlyList<string> GeneratedFiles,
    IReadOnlyList<string> Warnings,
    IReadOnlyList<string> Logs);