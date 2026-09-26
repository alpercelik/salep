using Salep.GraphQLParser;
using Microsoft.Extensions.FileSystemGlobbing;
using Microsoft.Extensions.FileSystemGlobbing.Abstractions;

namespace Salep.ClientGenerator.Operations;

internal sealed record LoadedOperations(
    IReadOnlyList<OperationDefinitionNode> Operations,
    IReadOnlyList<FragmentDefinitionNode> Fragments);

internal static class OperationLoader
{
    public static LoadedOperations Load(string operationsPath)
    {
        var operationFiles = ResolveOperationFiles(operationsPath);

        var operations = new List<OperationDefinitionNode>();
        var fragments = new List<FragmentDefinitionNode>();
        foreach (var file in operationFiles)
        {
            var text = File.ReadAllText(file);
            var opDocument = Utf8GraphQLParser.Parse(text, new ParserOptions(maxAllowedDirectives: 10_000));
            foreach (var definition in opDocument.Definitions)
            {
                if (definition is OperationDefinitionNode operationDef)
                {
                    operations.Add(operationDef);
                }
                else if (definition is FragmentDefinitionNode fragmentDef)
                {
                    fragments.Add(fragmentDef);
                }
            }
        }

        return new LoadedOperations(operations, fragments);
    }

    internal static IReadOnlyList<string> ResolveOperationFiles(string operationsPath)
    {
        if (File.Exists(operationsPath))
        {
            return [Path.GetFullPath(operationsPath)];
        }

        if (Directory.Exists(operationsPath))
        {
            return
            [
                .. Directory.GetFiles(operationsPath, "*.graphql", SearchOption.TopDirectoryOnly)
                    .OrderBy(file => file, StringComparer.Ordinal)
            ];
        }

        if (!LooksLikeGlobPattern(operationsPath))
        {
            return [];
        }

        var (baseDirectory, pattern) = SplitGlobPattern(operationsPath);
        if (string.IsNullOrWhiteSpace(baseDirectory) || string.IsNullOrWhiteSpace(pattern))
        {
            return [];
        }

        var matcher = new Matcher(StringComparison.OrdinalIgnoreCase);
        matcher.AddInclude(pattern);
        var result = matcher.Execute(new DirectoryInfoWrapper(new(baseDirectory)));

        return
        [
            .. result.Files
                .Select(match => Path.GetFullPath(Path.Combine(baseDirectory, match.Path)))
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .OrderBy(file => file, StringComparer.Ordinal)
        ];
    }

    private static bool LooksLikeGlobPattern(string path)
    {
        return path.IndexOfAny(['*', '?', '[', ']']) >= 0;
    }

    private static (string BaseDirectory, string Pattern) SplitGlobPattern(string path)
    {
        var wildcardIndex = path.IndexOfAny(['*', '?', '[', ']']);
        if (wildcardIndex < 0)
        {
            return (string.Empty, string.Empty);
        }

        var separatorBeforeWildcard = path.LastIndexOfAny([Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar], wildcardIndex);
        var baseDirectory = separatorBeforeWildcard >= 0
            ? path[..separatorBeforeWildcard]
            : Environment.CurrentDirectory;
        var pattern = separatorBeforeWildcard >= 0
            ? path[(separatorBeforeWildcard + 1)..]
            : path;

        baseDirectory = string.IsNullOrWhiteSpace(baseDirectory)
            ? Environment.CurrentDirectory
            : baseDirectory;

        return (baseDirectory, pattern);
    }
}
