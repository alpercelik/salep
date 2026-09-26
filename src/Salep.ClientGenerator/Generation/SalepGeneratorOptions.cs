namespace Salep.ClientGenerator.Generation;

public sealed record GenerationEnvironment(string? TargetFramework, string? LanguageVersion, IReadOnlyList<string> ReferencedConfigurations);
public sealed record SalepGeneratorOptions(string? ConfigPath = null, string? WorkingDirectory = null, GenerationEnvironment? Environment = null);
