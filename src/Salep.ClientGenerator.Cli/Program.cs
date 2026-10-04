using System.Reflection;
using System.Text.Json;
using Salep.ClientGenerator.Generation;
using Salep.ClientGenerator.Templates;

namespace Salep.ClientGenerator.Cli;

public static class Program
{
    public static int Main(string[] args) => Run(args, Console.Out, Console.Error);

    public static int Run(string[] args, TextWriter stdout, TextWriter stderr)
    {
        var parsed = ScribanCliArguments.Parse(args);
        if (parsed.Help)
        {
            stdout.WriteLine("Salep Scriban C# generator");
            stdout.WriteLine("Usage: salep [generate|validate|inputs] --config <salep.json> [--working-directory <directory>]");
            stdout.WriteLine("       salep templates --output-directory <directory> [--working-directory <directory>]");
            stdout.WriteLine("Options: -c, --config; -w, --working-directory; --output-directory; --target-framework; --language-version; --reference-config (repeatable); --read-root (repeatable); --solution-directory; --sources-file; --inputs-file; --stamp-file; -h, --help; -v, --version");
            return 0;
        }
        if (parsed.Version)
        {
            stdout.WriteLine(typeof(Program).Assembly.GetCustomAttribute<AssemblyInformationalVersionAttribute>()?.InformationalVersion ?? "unknown");
            return 0;
        }
        if (parsed.Errors.Count > 0)
        {
            foreach (var message in parsed.Errors)
                stderr.WriteLine(JsonSerializer.Serialize(new ScribanDiagnostic("SALEPS0001", parsed.Config ?? "salep.json", "arguments", message, "Use salep --help for supported options.")));
            return 1;
        }
        try
        {
            var selectedSolution = parsed.SolutionDirectory is null ? null : Path.GetFullPath(GeneratorPathPolicy.Normalize(parsed.SolutionDirectory), Path.GetFullPath(parsed.WorkingDirectory ?? Environment.CurrentDirectory));
            if (parsed.ExportTemplates)
            {
                var working = Path.GetFullPath(parsed.WorkingDirectory ?? Environment.CurrentDirectory);
                var outputDirectory = new GeneratorPathPolicy(selectedSolution ?? GeneratorPathPolicy.FindSolutionRoot(working)).Write(Path.GetFullPath(GeneratorPathPolicy.Normalize(parsed.TemplatesOutputDirectory ?? "templates"), working), allowRoot: false);
                var written = ScribanTemplateCatalog.WriteDefaults(outputDirectory);
                stdout.WriteLine($"[Salep.Scriban] Exported {written.Count} default templates to '{GeneratorPathPolicy.Normalize(outputDirectory)}'.");
                return 0;
            }

            var options = new ScribanGeneratorOptions(parsed.Config, parsed.WorkingDirectory,
                parsed.TargetFramework is null && parsed.LanguageVersion is null && parsed.ReferenceConfigurations.Count == 0
                    ? null
                    : new(parsed.TargetFramework, parsed.LanguageVersion, parsed.ReferenceConfigurations), parsed.ReadRoots.Select(root => Path.GetFullPath(GeneratorPathPolicy.Normalize(root), Path.GetFullPath(parsed.WorkingDirectory ?? Environment.CurrentDirectory))).ToArray(), selectedSolution ?? (parsed.WorkingDirectory is null ? null : GeneratorPathPolicy.FindSolutionRoot(parsed.WorkingDirectory)));
            var projectDirectory = parsed.WorkingDirectory ?? Path.GetDirectoryName(Path.GetFullPath(GeneratorPathPolicy.Normalize(parsed.Config ?? "salep.json"), Environment.CurrentDirectory))!;
            var outputPaths = new GeneratorPathPolicy(selectedSolution ?? GeneratorPathPolicy.FindSolutionRoot(projectDirectory));
            var cliWorking = Path.GetFullPath(parsed.WorkingDirectory ?? Environment.CurrentDirectory);
            foreach (var file in new[] { parsed.InputsFile, parsed.SourcesFile, parsed.StampFile }.OfType<string>())
                outputPaths.Write(Path.GetFullPath(GeneratorPathPolicy.Normalize(file), cliWorking));
            if (parsed.ListInputs)
            {
                var working = Path.GetFullPath(parsed.WorkingDirectory ?? Environment.CurrentDirectory);
                var inputPath = outputPaths.Write(Path.GetFullPath(GeneratorPathPolicy.Normalize(parsed.InputsFile ?? throw new InvalidOperationException("inputs requires --inputs-file.")), working));
                Directory.CreateDirectory(Path.GetDirectoryName(inputPath)!);
                var inputs = ScribanGenerator.GetInputFiles(options);
                var contents = string.Join(Environment.NewLine, inputs) + Environment.NewLine;
                if (!File.Exists(inputPath) || File.ReadAllText(inputPath) != contents) File.WriteAllText(inputPath, contents);
                stdout.WriteLine($"[Salep.Scriban] Wrote input list '{GeneratorPathPolicy.Normalize(inputPath)}'.");
                return 0;
            }
            var result = parsed.ValidateOnly ? ScribanGenerator.Validate(options) : ScribanGenerator.Generate(options);
            foreach (var message in result.Logs) stdout.WriteLine("[Salep.Scriban] " + message);
            if (!parsed.ValidateOnly && parsed.SourcesFile is { } sourcesFile)
            {
                var working = Path.GetFullPath(parsed.WorkingDirectory ?? Environment.CurrentDirectory);
                var path = outputPaths.Write(Path.GetFullPath(GeneratorPathPolicy.Normalize(sourcesFile), working));
                Directory.CreateDirectory(Path.GetDirectoryName(path)!);
                File.WriteAllLines(path, result.GeneratedFiles.Where(file => file.EndsWith(".cs", StringComparison.Ordinal)));
            }
            if (!parsed.ValidateOnly && parsed.StampFile is { } stampFile)
            {
                var working = Path.GetFullPath(parsed.WorkingDirectory ?? Environment.CurrentDirectory);
                var path = outputPaths.Write(Path.GetFullPath(GeneratorPathPolicy.Normalize(stampFile), working));
                Directory.CreateDirectory(Path.GetDirectoryName(path)!);
                File.WriteAllText(path, DateTimeOffset.UtcNow.ToString("O", System.Globalization.CultureInfo.InvariantCulture));
            }
            stdout.WriteLine(parsed.ValidateOnly ? "[Salep.Scriban] Validation completed successfully." : $"[Salep.Scriban] Generation completed successfully. Generated {result.GeneratedFiles.Count} files.");
            return 0;
        }
        catch (ScribanConfigurationException exception)
        {
            stderr.WriteLine(JsonSerializer.Serialize(exception.Diagnostic));
            return 1;
        }
        catch (Exception exception)
        {
            stderr.WriteLine(JsonSerializer.Serialize(new ScribanDiagnostic("SALEPS1006", parsed.Config ?? "salep.json", "$", exception.Message, "Correct the reported input or output error and rerun salep validate.")));
            return 1;
        }
    }
}

public sealed record ScribanCliParseResult(string? Config, string? WorkingDirectory, string? TargetFramework,
    string? LanguageVersion, IReadOnlyList<string> ReferenceConfigurations, string? SourcesFile, string? InputsFile, string? StampFile,
    string? TemplatesOutputDirectory, bool ValidateOnly, bool ListInputs, bool ExportTemplates, bool Help, bool Version, IReadOnlyList<string> Errors, IReadOnlyList<string> ReadRoots, string? SolutionDirectory);

public static class ScribanCliArguments
{
    public static ScribanCliParseResult Parse(string[] args)
    {
        string? config = null, workingDirectory = null, targetFramework = null, languageVersion = null, sourcesFile = null, inputsFile = null, stampFile = null, templatesOutputDirectory = null;
        var referenceConfigs = new List<string>();
        var readRoots = new List<string>();
        string? solutionDirectory = null;
        var errors = new List<string>();
        var validate = false;
        var listInputs = false;
        var exportTemplates = false;
        var help = false;
        var version = false;
        for (var index = 0; index < args.Length; index++)
        {
            var argument = args[index];
            if (index == 0 && argument is "generate" or "validate" or "inputs" or "templates") { validate = argument == "validate"; listInputs = argument == "inputs"; exportTemplates = argument == "templates"; continue; }
            if (argument is "-h" or "--help") { help = true; continue; }
            if (argument is "-v" or "--version") { version = true; continue; }
            var split = argument.IndexOfAny(['=', ':']);
            var key = split < 0 ? argument : argument[..split];
            if (key is not ("-c" or "--config" or "-w" or "--working-directory" or "--target-framework" or "--language-version" or "--reference-config" or "--read-root" or "--solution-directory" or "--sources-file" or "--inputs-file" or "--stamp-file" or "--output-directory"))
            { errors.Add($"Unrecognized argument '{argument}'."); continue; }
            var value = split >= 0 ? argument[(split + 1)..] : index + 1 < args.Length && !args[index + 1].StartsWith('-') ? args[++index] : null;
            if (string.IsNullOrWhiteSpace(value)) { errors.Add($"Missing value for '{key}'."); continue; }
            switch (key)
            {
                case "-c": case "--config": config = value; break;
                case "-w": case "--working-directory": workingDirectory = value; break;
                case "--target-framework": targetFramework = value; break;
                case "--language-version": languageVersion = value; break;
                case "--reference-config": referenceConfigs.Add(value); break;
                case "--read-root": readRoots.Add(value); break;
                case "--solution-directory": solutionDirectory = value; break;
                case "--sources-file": sourcesFile = value; break;
                case "--inputs-file": inputsFile = value; break;
                case "--stamp-file": stampFile = value; break;
                case "--output-directory": templatesOutputDirectory = value; break;
            }
        }
        if (validate && sourcesFile is not null) errors.Add("validate does not write source lists; remove --sources-file.");
        if (validate && stampFile is not null) errors.Add("validate does not write generation stamps; remove --stamp-file.");
        if (listInputs && (sourcesFile is not null || stampFile is not null)) errors.Add("inputs only writes --inputs-file.");
        if (inputsFile is not null && !listInputs) errors.Add("--inputs-file is only valid with the inputs command.");
        if (templatesOutputDirectory is not null && !exportTemplates) errors.Add("--output-directory is only valid with the templates command.");
        if (exportTemplates && (config is not null || sourcesFile is not null || inputsFile is not null || stampFile is not null)) errors.Add("templates only writes default template files; remove generation or input-list options.");
        return new(config, workingDirectory, targetFramework, languageVersion, referenceConfigs, sourcesFile, inputsFile, stampFile,
            templatesOutputDirectory, validate, listInputs, exportTemplates, help, version, errors, readRoots, solutionDirectory);
    }
}
