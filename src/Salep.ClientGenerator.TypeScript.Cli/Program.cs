using Salep.ClientGenerator.Generation;
using Salep.ClientGenerator.TypeScript.Generation;
using Salep.ClientGenerator.TypeScript.Templates;

if (args is ["notices"])
{
    var assembly = System.Reflection.Assembly.GetExecutingAssembly();
    foreach (var name in new[] { "Salep.LICENSE", "Salep.ThirdPartyNotices", "Salep.RuntimeNotices" })
    {
        using var stream = assembly.GetManifestResourceStream(name)!;
        using var reader = new StreamReader(stream);
        Console.WriteLine(reader.ReadToEnd());
    }
    return 0;
}

if (args.Length == 0 || args.Contains("--help", StringComparer.Ordinal))
{
    Console.WriteLine("salep-typescript generate|validate --config salep.typescript.json [--solution-directory path] [--read-root path]\nsalep-typescript templates --output-directory folder\nsalep-typescript template --template-key key --output file\nsalep-typescript notices");
    return 0;
}
try
{
    string? config = null, solution = null, output = null;
    string templateKey = "client";
    var roots = new List<string>();
    for (var i = 1; i < args.Length; i++)
    {
        if (i + 1 >= args.Length) throw new ArgumentException($"Missing value for '{args[i]}'.");
        var value = args[++i];
        switch (args[i - 1])
        {
            case "--config": config = value; break;
            case "--solution-directory": solution = Path.GetFullPath(value); break;
            case "--read-root": roots.Add(Path.GetFullPath(value)); break;
            case "--output": case "--output-directory": output = value; break;
            case "--template-key": templateKey = value; break;
            default: throw new ArgumentException($"Unknown argument '{args[i - 1]}'.");
        }
    }
    if (args[0] is "template" or "templates")
    {
        if (output is null) throw new ArgumentException("--output is required.");
        var path = Path.GetFullPath(output);
        var policy = new GeneratorPathPolicy(solution ?? GeneratorPathPolicy.FindSolutionRoot(Environment.CurrentDirectory));
        if (args[0] == "templates")
        {
            policy.Write(path, false);
            foreach (var entry in TypeScriptTemplateCatalog.Files)
            {
                var destination = policy.Write(Path.Combine(path, entry.Value));
                Directory.CreateDirectory(path);
                File.WriteAllText(destination, TypeScriptGenerator.EmbeddedTemplate(entry.Key));
                Console.WriteLine(GeneratorPathPolicy.Normalize(destination));
            }
        }
        else
        {
            policy.Write(path, false);
            Directory.CreateDirectory(Path.GetDirectoryName(path)!);
            File.WriteAllText(path, TypeScriptGenerator.EmbeddedTemplate(templateKey));
            Console.WriteLine(GeneratorPathPolicy.Normalize(path));
        }
    }
    else
    {
        if (args[0] is not ("generate" or "validate") || config is null) throw new ArgumentException("Select generate or validate and --config.");
        foreach (var file in TypeScriptGenerator.Generate(new(config, solution, roots), args[0] == "validate")) Console.WriteLine(file);
    }
    return 0;
}
catch (Exception exception)
{
    Console.Error.WriteLine(exception.Message);
    return 1;
}
