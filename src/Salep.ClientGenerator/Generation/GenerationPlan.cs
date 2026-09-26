using System.Collections.Immutable;
using Salep.ClientGenerator.Config;

namespace Salep.ClientGenerator.Generation;

internal sealed record GenerationPlan(ResolvedConfiguration Configuration, GenerationManifest Manifest,
    ImmutableSortedDictionary<string, string> Outputs, ImmutableArray<string> Warnings)
{
    public void VerifyExisting() => GenerationManifest.Read(Configuration.Output, Configuration.Path).Verify(Manifest);

    public void CheckOwnership()
    {
        var path = Path.Combine(Configuration.Output, GenerationManifest.FileName);
        if (File.Exists(path))
        {
            var previous = GenerationManifest.Read(Configuration.Output, Configuration.Path);
            foreach (var name in Outputs.Keys)
                if (!previous.Files.ContainsKey(name) && File.Exists(Path.Combine(Configuration.Output, name)))
                    throw ConfigurationResolver.Error("SALEP2003", Configuration.Path, "output", $"Planned output '{name}' already exists without ownership.", "Move the unowned file or choose a separate output directory.");
        }
        else if (Directory.Exists(Configuration.Output) && Directory.EnumerateFiles(Configuration.Output, "*.cs").Any())
            throw ConfigurationResolver.Error("SALEP2003", Configuration.Path, "output", "Output contains C# files without a verified ownership manifest.", "Choose an empty output directory or clear obsolete generated artifacts during migration.");
    }

    public void Publish()
    {
        CheckOwnership();
        Directory.CreateDirectory(Configuration.Output);
        using var guard = AcquireLock(Path.Combine(Configuration.Output, ".salep.lock"));
        CheckOwnership();
        var manifestPath = Path.Combine(Configuration.Output, GenerationManifest.FileName);
        var previous = File.Exists(manifestPath) ? GenerationManifest.Read(Configuration.Output, Configuration.Path) : null;
        var stage = Path.Combine(Configuration.Output, ".salep-stage-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(stage);
        try
        {
            foreach (var file in Outputs) File.WriteAllText(Path.Combine(stage, file.Key), file.Value);
            File.WriteAllText(Path.Combine(stage, GenerationManifest.FileName), Manifest.Serialize());
            foreach (var file in Outputs)
            {
                var destination = Path.Combine(Configuration.Output, file.Key);
                if (!File.Exists(destination) || File.ReadAllText(destination) != file.Value) File.Move(Path.Combine(stage, file.Key), destination, true);
            }
            if (previous is not null)
                foreach (var old in previous.Files.Keys.Where(name => !Outputs.ContainsKey(name))) File.Delete(Path.Combine(Configuration.Output, old));
            var serialized = Manifest.Serialize();
            if (!File.Exists(manifestPath) || File.ReadAllText(manifestPath) != serialized) File.Move(Path.Combine(stage, GenerationManifest.FileName), manifestPath, true);
        }
        finally { Directory.Delete(stage, true); }
    }

    private static FileStream AcquireLock(string path)
    {
        var deadline = DateTime.UtcNow.AddSeconds(30);
        while (true)
        {
            try { return new FileStream(path, FileMode.OpenOrCreate, FileAccess.ReadWrite, FileShare.None); }
            catch (IOException) when (DateTime.UtcNow < deadline) { Thread.Sleep(25); }
        }
    }
}
