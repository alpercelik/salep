namespace Salep.ClientGenerator.Generation;

/// <summary>Caller-controlled filesystem boundaries for generator inputs and outputs.</summary>
public sealed class GeneratorPathPolicy
{
    private readonly string boundaryRoot;
    public IReadOnlyList<string> ReadRoots { get; }

    public GeneratorPathPolicy(string boundaryRoot, IReadOnlyList<string>? allowedReadRoots = null)
    {
        this.boundaryRoot = Path.TrimEndingDirectorySeparator(Path.GetFullPath(Normalize(boundaryRoot)));
        ReadRoots = new[] { this.boundaryRoot }.Concat(allowedReadRoots ?? [])
            .Select(root => Path.TrimEndingDirectorySeparator(Path.GetFullPath(Normalize(root), this.boundaryRoot)))
            .Distinct(StringComparer.Ordinal).ToArray();
        foreach (var root in ReadRoots) RejectLink(root);
    }

    /// <summary>Finds the nearest containing solution folder, falling back to the selected project folder.</summary>
    public static string FindSolutionRoot(string projectDirectory)
    {
        var project = Path.GetFullPath(Normalize(projectDirectory));
        for (var directory = new DirectoryInfo(project); directory is not null; directory = directory.Parent)
        {
            if (!directory.Exists) continue;
            if (directory.EnumerateFiles("*", SearchOption.TopDirectoryOnly).Any(file =>
                file.Extension.Equals(".sln", StringComparison.OrdinalIgnoreCase) ||
                file.Extension.Equals(".slnx", StringComparison.OrdinalIgnoreCase)))
                return directory.FullName;
        }
        return project;
    }

    public static string Normalize(string path) => path.Replace('\\', '/');

    /// <summary>Rejects portable path aliases before the host filesystem can normalize them away.</summary>
    public static string ResolvePath(string path, string baseDirectory)
    {
        var normalized = Normalize(path);
        foreach (var segment in normalized.Split('/'))
        {
            if (segment is "." or "..") continue;
            if (segment.EndsWith('.') || segment.EndsWith(' '))
                throw new InvalidDataException($"Invalid portable path component '{segment}'.");
        }
        return Path.GetFullPath(normalized, baseDirectory);
    }

    /// <summary>Resolves physical path identity for ownership comparisons without changing read/write boundaries.</summary>
    public static string PhysicalIdentity(string path) => PhysicalIdentity(path, new HashSet<string>(StringComparer.OrdinalIgnoreCase), 0);

    private static string PhysicalIdentity(string path, HashSet<string> activeLinks, int linkDepth)
    {
        // Keep the path policy lexical boundary intact. Identity comparisons also
        // resolve aliases above that boundary, such as macOS /var and /tmp.
        var full = Path.GetFullPath(Normalize(path));
        var root = Path.GetPathRoot(full)!;
        var current = root;
        foreach (var component in full[root.Length..].Split(Path.DirectorySeparatorChar, StringSplitOptions.RemoveEmptyEntries))
        {
            current = Path.Combine(current, component);
            FileSystemInfo entry = Directory.Exists(current) ? new DirectoryInfo(current) : new FileInfo(current);
            if (entry.LinkTarget is null) continue;
            var link = current;
            if (linkDepth >= 64 || !activeLinks.Add(link))
                throw new InvalidDataException($"Generator path identity contains a symbolic-link cycle or exceeds 64 links: '{Normalize(path)}'.");
            try
            {
                // A target can itself contain ancestor aliases. Resolve its entire
                // path rather than accepting the lexical target returned by .NET.
                var target = entry.ResolveLinkTarget(returnFinalTarget: false)
                    ?? throw new InvalidDataException($"Cannot resolve generator path identity: '{Normalize(link)}'.");
                current = PhysicalIdentity(target.FullName, activeLinks, linkDepth + 1);
            }
            finally { activeLinks.Remove(link); }
        }
        return Path.GetFullPath(current);
    }

    public string Read(string path) => Check(path, ReadRoots, true);
    public string Write(string path, bool allowRoot = true) => Check(path, [boundaryRoot], allowRoot);

    public void ReadPattern(string path)
    {
        var normalized = Normalize(path);
        var wildcard = normalized.IndexOfAny(['*', '?', '[', ']']);
        if (wildcard < 0) { Read(normalized); return; }
        var separator = normalized.LastIndexOf('/', wildcard);
        Read(separator < 0 ? boundaryRoot : normalized[..separator]);
        if (normalized[(separator + 1)..].Split('/').Any(segment => segment == ".."))
            throw new InvalidDataException("Operation patterns cannot contain parent traversal after a wildcard.");
    }

    public static bool IsOutputName(string name) =>
        !string.IsNullOrWhiteSpace(name) && name is not "." and not ".." &&
        !name.StartsWith(".salep", StringComparison.OrdinalIgnoreCase) &&
        !name.EndsWith('.') && !name.EndsWith(' ') &&
        !name.Any(character => character < 32 || "\\/:*?\"<>|".Contains(character)) &&
        !IsDeviceName(name.Split('.')[0]);

    private static bool IsDeviceName(string stem) =>
        stem.Equals("CON", StringComparison.OrdinalIgnoreCase) || stem.Equals("PRN", StringComparison.OrdinalIgnoreCase) ||
        stem.Equals("AUX", StringComparison.OrdinalIgnoreCase) || stem.Equals("NUL", StringComparison.OrdinalIgnoreCase) ||
        (stem.Length == 4 && (stem.StartsWith("COM", StringComparison.OrdinalIgnoreCase) || stem.StartsWith("LPT", StringComparison.OrdinalIgnoreCase)) && (stem[3] is >= '1' and <= '9' or '¹' or '²' or '³'));

    private static void RejectLink(string path)
    {
        FileSystemInfo entry = Directory.Exists(path) ? new DirectoryInfo(path) : new FileInfo(path);
        if (entry.LinkTarget is not null || (entry.Exists && (entry.Attributes & FileAttributes.ReparsePoint) != 0))
            throw new InvalidDataException($"Generator paths cannot traverse symbolic links or junctions: '{Normalize(path)}'.");
    }

    private static string Check(string path, IReadOnlyList<string> roots, bool allowRoot)
    {
        var full = ResolvePath(path, Environment.CurrentDirectory);
        // Ordinal comparison deliberately fails closed on case-sensitive volumes, including macOS.
        foreach (var root in roots)
        {
            var comparison = StringComparison.Ordinal;
            if (!full.Equals(root, comparison) && !full.StartsWith(root + Path.DirectorySeparatorChar, comparison)) continue;
            var relative = Path.GetRelativePath(root, full);
            if (Path.IsPathRooted(relative) || relative == ".." || relative.StartsWith(".." + Path.DirectorySeparatorChar, StringComparison.Ordinal)) continue;
            if (!allowRoot && relative == ".") break;
            var current = root;
            foreach (var segment in relative.Split(Path.DirectorySeparatorChar))
            {
                if (segment == ".") continue;
                if (segment.EndsWith('.') || segment.EndsWith(' ') || IsDeviceName(segment.Split('.')[0]) ||
                    segment.Any(character => character < 32 || "\\/:*?\"<>|".Contains(character)))
                    throw new InvalidDataException($"Invalid portable path component '{segment}'.");
                current = Path.Combine(current, segment);
                RejectLink(current);
            }
            return full;
        }
        throw new InvalidDataException($"Path '{Normalize(full)}' is outside the permitted generator folders.");
    }
}
