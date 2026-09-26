using System.Reflection;
using System.Text.Json;
using Salep.ClientGenerator.Config;
using Salep.ClientGenerator.Generation;

namespace Salep.ClientGenerator.Cli;

public static class Program
{
    public static int Main(string[] args) => Run(args, Console.Out, Console.Error);
    public static int Run(string[] args, TextWriter? stdout = null, TextWriter? stderr = null)
    {
        var output = stdout ?? Console.Out;
        var error = stderr ?? Console.Error;
        var parsed = SalepCliParser.Parse(args);
        if (parsed.ShowHelp)
        {
            output.WriteLine("Salep - C# GraphQL Client and Test Generator");
            output.WriteLine("Usage: salep [generate|validate] --config <salep.json> [--working-directory <directory>]");
            output.WriteLine("Options: -c, --config; -w, --working-directory; -h, --help; -v, --version");
            output.WriteLine("MSBuild context: --target-framework, --language-version, --reference-config (repeatable), --sources-file");
            return 0;
        }
        if (parsed.ShowVersion)
        {
            output.WriteLine(typeof(Program).Assembly.GetCustomAttribute<AssemblyInformationalVersionAttribute>()?.InformationalVersion ?? "unknown");
            return 0;
        }
        if (!parsed.IsSuccess)
        {
            foreach (var message in parsed.Errors) error.WriteLine(JsonSerializer.Serialize(new ConfigurationDiagnostic("SALEP0001", parsed.Options.ConfigPath ?? "salep.json", "arguments", message, "Use salep --help for supported configuration selection options.")));
            return 1;
        }
        try
        {
            var result = parsed.ValidateOnly ? SalepGenerator.Validate(parsed.Options) : SalepGenerator.Generate(parsed.Options);
            foreach (var message in result.Logs) output.WriteLine("[Salep] " + message);
            foreach (var warning in result.Warnings) output.WriteLine("[Salep] Warning: " + warning);
            if (!parsed.ValidateOnly && parsed.SourcesFile is { } sourcesFile)
            {
                var path = Path.GetFullPath(sourcesFile, parsed.Options.WorkingDirectory ?? Environment.CurrentDirectory);
                Directory.CreateDirectory(Path.GetDirectoryName(path)!);
                File.WriteAllLines(path, result.GeneratedFiles.Where(file => file.EndsWith(".cs", StringComparison.Ordinal)));
            }
            output.WriteLine(parsed.ValidateOnly ? "[Salep] Validation completed successfully." : $"[Salep] Generation completed successfully. Generated {result.GeneratedFiles.Count} files.");
            return 0;
        }
        catch (ConfigurationException e)
        {
            error.WriteLine(JsonSerializer.Serialize(e.Diagnostic));
            return 1;
        }
        catch (Exception e)
        {
            error.WriteLine(JsonSerializer.Serialize(new ConfigurationDiagnostic("SALEP1006", parsed.Options.ConfigPath ?? "salep.json", "$", e.Message, "Correct the reported input or output error and run salep validate.")));
            return 1;
        }
    }
}

public sealed record SalepCliParseResult(SalepGeneratorOptions Options, bool ShowHelp, bool ShowVersion,
    IReadOnlyList<string> Errors, bool ValidateOnly = false, string? SourcesFile = null)
{
    public bool IsSuccess => Errors.Count == 0;
}

public static class SalepCliParser
{
    public static SalepCliParseResult Parse(string[] args)
    {
        string? config = null, working = null, framework = null, language = null, sources = null;
        var references = new List<string>();
        var errors = new List<string>();
        var help = false; var version = false; var validate = false;
        for (var i = 0; i < args.Length; i++)
        {
            var arg = args[i];
            if (i == 0 && arg is "generate" or "validate") { validate = arg == "validate"; continue; }
            if (arg is "-h" or "--help") { help = true; continue; }
            if (arg is "-v" or "--version") { version = true; continue; }
            var split = arg.IndexOfAny(['=', ':']);
            var key = split >= 0 ? arg[..split] : arg;
            if (key is not ("-c" or "--config" or "-w" or "--working-directory" or "--target-framework" or "--language-version" or "--reference-config" or "--sources-file"))
            { errors.Add($"Unrecognized argument '{arg}'."); continue; }
            string? value = split >= 0 ? arg[(split + 1)..] : i + 1 < args.Length && !args[i + 1].StartsWith('-') ? args[++i] : null;
            if (string.IsNullOrWhiteSpace(value)) { errors.Add($"Missing value for '{key}'."); continue; }
            switch (key)
            {
                case "-c": case "--config": config = value; break;
                case "-w": case "--working-directory": working = value; break;
                case "--target-framework": framework = value; break;
                case "--language-version": language = value; break;
                case "--reference-config": references.Add(value); break;
                case "--sources-file": sources = value; break;
            }
        }
        if (validate && sources is not null) errors.Add("validate does not write source lists; remove --sources-file.");
        var context = framework is null && language is null && references.Count == 0 ? null : new GenerationEnvironment(framework, language, references);
        return new(new(config, working, context), help, version, errors, validate, sources);
    }
}
