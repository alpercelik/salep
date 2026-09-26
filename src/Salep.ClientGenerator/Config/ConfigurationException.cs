namespace Salep.ClientGenerator.Config;

public sealed record ConfigurationDiagnostic(string Code, string ConfigurationPath, string Property, string Message, string Guidance);

public sealed class ConfigurationException(ConfigurationDiagnostic diagnostic)
    : InvalidOperationException($"{diagnostic.Code}: {diagnostic.ConfigurationPath} [{diagnostic.Property}]: {diagnostic.Message} {diagnostic.Guidance}")
{
    public ConfigurationDiagnostic Diagnostic { get; } = diagnostic;
}
