using System.Diagnostics;
using System.IO.Compression;
using System.Reflection.Metadata;
using System.Text.Json;
using System.Xml.Linq;
using Shouldly;
using Xunit;

namespace Salep.ClientGenerator.MSBuild.Tests;

public class PackageLayoutTests
{
    [Fact]
    public async Task SalepPackageContainsBuildAssetsAndPrivateCliTools()
    {
        var repoRoot = FindRepoRoot();
        var msbuildProject = Path.Combine(repoRoot, "src", "Salep.ClientGenerator.MSBuild", "Salep.ClientGenerator.MSBuild.csproj");
        var tempOutDir = Path.Combine(Path.GetTempPath(), "salep_pkg_test_" + Guid.NewGuid().ToString("N"));

        try
        {
            Directory.CreateDirectory(tempOutDir);

            // Framework runners are separate processes. Exercise overlapping packs explicitly,
            // with independent restore/build/publish roots rather than a process-local lock.
            var concurrentOutput = Path.Combine(tempOutDir, "concurrent");
            await Task.WhenAll(PackAsync(msbuildProject, tempOutDir), PackAsync(msbuildProject, concurrentOutput));

            foreach (var outputRoot in new[] { tempOutDir, concurrentOutput })
            {
                var publishDirectories = Directory.GetDirectories(Path.Combine(outputRoot, "artifacts"), "cli_publish", SearchOption.AllDirectories);
                publishDirectories.Length.ShouldBe(2, "Each pack must stage both CLI frameworks inside its own artifacts root");
                foreach (var publishDirectory in publishDirectories)
                    File.Exists(Path.Combine(publishDirectory, "Salep.ClientGenerator.Cli.dll")).ShouldBeTrue();
            }

            var packagePath = Path.Combine(tempOutDir, "Salep.ClientGenerator.9.9.9-test.nupkg");
            File.Exists(packagePath).ShouldBeTrue("Nupkg was not created");

            await using var archive = await ZipFile.OpenReadAsync(packagePath, TestContext.Current.CancellationToken);
            var entryNames = archive.Entries.Select(e => e.FullName).ToList();
            using var concurrentArchive = ZipFile.OpenRead(Path.Combine(concurrentOutput, "Salep.ClientGenerator.9.9.9-test.nupkg"));
            concurrentArchive.Entries.Select(entry => entry.FullName).Order(StringComparer.Ordinal)
                .ShouldBe(entryNames.Order(StringComparer.Ordinal));
            foreach (var entry in concurrentArchive.Entries)
            {
                using var contents = entry.Open();
                using var bytes = new MemoryStream();
                await contents.CopyToAsync(bytes, TestContext.Current.CancellationToken);
                bytes.Length.ShouldBe(entry.Length, $"Incomplete archive entry: {entry.FullName}");
            }

            // Verify build/ assets exist
            entryNames.ShouldContain("build/Salep.ClientGenerator.props");
            entryNames.ShouldContain("build/Salep.ClientGenerator.targets");

            entryNames.ShouldContain("package-readme.md");
            entryNames.ShouldContain("LICENSE");
            entryNames.ShouldContain("third-party-notices.md");
            entryNames.ShouldContain("licenses/roslyn-ThirdPartyNotices.rtf");
            entryNames.ShouldContain("licenses/runtime-THIRD-PARTY-NOTICES.txt");
            entryNames.ShouldContain("licenses/humanizer-LICENSE.txt");
            entryNames.ShouldContain("licenses/composition-THIRD-PARTY-NOTICES.txt");
            entryNames.ShouldNotContain(e => e.StartsWith("lib/", StringComparison.Ordinal));
            // The archive must be portable: MSBuild uses the managed CLI, never a host-specific apphost.
            entryNames.ShouldNotContain(e => e.EndsWith("/Salep.ClientGenerator.Cli", StringComparison.Ordinal)
                || e.EndsWith("/Salep.ClientGenerator.Cli.exe", StringComparison.Ordinal));

            // Verify no buildTransitive/ assets
            entryNames.ShouldNotContain(e => e.StartsWith("buildTransitive", StringComparison.OrdinalIgnoreCase));

            // Verify private CLI tools are included for each supported target framework.
            foreach (var framework in new[] { "net10.0", "net11.0" })
            {
                entryNames.ShouldContain($"tools/{framework}/any/Salep.ClientGenerator.Cli.dll");
                entryNames.ShouldContain($"tools/{framework}/any/Salep.ClientGenerator.dll");
                entryNames.ShouldContain($"tools/{framework}/any/Salep.ClientGenerator.Cli.runtimeconfig.json");
                entryNames.ShouldContain($"tools/{framework}/any/Salep.ClientGenerator.Cli.deps.json");
                entryNames.ShouldContain($"tools/{framework}/any/Salep.GraphQLParser.dll");
                entryNames.ShouldContain($"tools/{framework}/any/Microsoft.CodeAnalysis.CSharp.dll");
                foreach (var dependency in new[] { "Microsoft.CodeAnalysis.Workspaces", "Microsoft.CodeAnalysis.CSharp.Workspaces",
                    "Humanizer", "System.Composition.AttributedModel", "System.Composition.Convention", "System.Composition.Hosting",
                    "System.Composition.Runtime", "System.Composition.TypedParts" })
                    entryNames.ShouldContain($"tools/{framework}/any/{dependency}.dll");
            }

            // Verify nuspec contains no dependencies
            var nuspecEntry = archive.Entries.FirstOrDefault(e => e.FullName == "Salep.ClientGenerator.nuspec");
            nuspecEntry.ShouldNotBeNull();

            using var stream = nuspecEntry.Open();
            var doc = XDocument.Load(stream);
            var ns = doc.Root?.GetDefaultNamespace() ?? XNamespace.None;
            var metadata = doc.Root?.Element(ns + "metadata");
            metadata.ShouldNotBeNull();

            metadata.Element(ns + "id")?.Value.ShouldBe("Salep.ClientGenerator");
            metadata.Element(ns + "developmentDependency")?.Value.ShouldBe("true");
            metadata.Element(ns + "version")?.Value.ShouldBe("9.9.9-test");
            metadata.Element(ns + "authors")?.Value.ShouldBe("Alper Çelik");
            metadata.Element(ns + "license")?.Value.ShouldBe("MIT");
            metadata.Element(ns + "license")?.Attribute("type")?.Value.ShouldBe("expression");
            metadata.Element(ns + "readme")?.Value.ShouldBe("package-readme.md");
            metadata.Element(ns + "repository")?.Attribute("url")?.Value.ShouldBe("https://github.com/alpercelik/salep");
            metadata.Element(ns + "repository")?.Attribute("commit")?.Value.ShouldNotBeNullOrWhiteSpace();
            metadata.Element(ns + "description")?.Value.ShouldNotBeNullOrWhiteSpace();

            var dependencies = metadata.Element(ns + "dependencies");
            if (dependencies != null)
            {
                dependencies.Elements().ShouldBeEmpty();
            }
        }
        finally
        {
            if (Directory.Exists(tempOutDir))
            {
                try { Directory.Delete(tempOutDir, true); } catch { }
            }
        }
    }

    [Fact]
    public async Task ParserPackageContainsPublicLibraryMetadataAndPortableSymbols()
    {
        var repoRoot = FindRepoRoot();
        var output = Path.Combine(Path.GetTempPath(), "salep_parser_pkg_test_" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(output);
        try
        {
            await PackAsync(Path.Combine(repoRoot, "src", "Salep.GraphQLParser", "Salep.GraphQLParser.csproj"), output);
            using var package = ZipFile.OpenRead(Path.Combine(output, "Salep.GraphQLParser.9.9.9-test.nupkg"));
            var names = package.Entries.Select(entry => entry.FullName).ToList();
            names.ShouldContain("package-readme.md");
            names.ShouldContain("LICENSE");
            names.ShouldNotContain(name => name.StartsWith("tools/", StringComparison.Ordinal));
            using var manifest = package.GetEntry("Salep.GraphQLParser.nuspec")!.Open();
            var doc = XDocument.Load(manifest);
            var ns = doc.Root!.GetDefaultNamespace();
            var metadata = doc.Root.Element(ns + "metadata")!;
            metadata.Element(ns + "id")!.Value.ShouldBe("Salep.GraphQLParser");
            metadata.Element(ns + "version")!.Value.ShouldBe("9.9.9-test");
            metadata.Element(ns + "authors")!.Value.ShouldBe("Alper Çelik");
            metadata.Element(ns + "license")!.Value.ShouldBe("MIT");
            metadata.Element(ns + "license")!.Attribute("type")!.Value.ShouldBe("expression");
            metadata.Element(ns + "readme")!.Value.ShouldBe("package-readme.md");
            metadata.Element(ns + "description")!.Value.ShouldNotBeNullOrWhiteSpace();
            metadata.Element(ns + "repository")!.Attribute("url")!.Value.ShouldBe("https://github.com/alpercelik/salep");
            metadata.Element(ns + "repository")!.Attribute("commit")!.Value.ShouldNotBeNullOrWhiteSpace();
            metadata.Descendants(ns + "dependency").ShouldBeEmpty();

            using var symbols = ZipFile.OpenRead(Path.Combine(output, "Salep.GraphQLParser.9.9.9-test.snupkg"));
            foreach (var framework in new[] { "net10.0", "net11.0" })
            {
                names.ShouldContain($"lib/{framework}/Salep.GraphQLParser.dll");
                names.ShouldContain($"lib/{framework}/Salep.GraphQLParser.xml");
                names.Where(name => name.StartsWith($"lib/{framework}/", StringComparison.Ordinal)
                    && name.EndsWith(".dll", StringComparison.Ordinal)).ShouldHaveSingleItem();
                using var pdb = symbols.GetEntry($"lib/{framework}/Salep.GraphQLParser.pdb")!.Open();
                using var pdbBytes = new MemoryStream();
                pdb.CopyTo(pdbBytes);
                pdbBytes.Position = 0;
                using var provider = MetadataReaderProvider.FromPortablePdbStream(pdbBytes);
                var pdbReader = provider.GetMetadataReader();
                // Source Link custom debug information, as defined by the portable PDB specification.
                var sourceLinkKind = new Guid("CC110556-A091-4D38-9FEC-25AB9A351A6A");
                var sourceLinks = pdbReader.CustomDebugInformation
                    .Select(handle => pdbReader.GetCustomDebugInformation(handle))
                    .Where(info => pdbReader.GetGuid(info.Kind) == sourceLinkKind).ToArray();
                sourceLinks.ShouldHaveSingleItem();
                using var sourceLink = JsonDocument.Parse(pdbReader.GetBlobBytes(sourceLinks[0].Value));
                var mappings = sourceLink.RootElement.GetProperty("documents").EnumerateObject().ToArray();
                mappings.ShouldNotBeEmpty();
                foreach (var mapping in mappings)
                {
                    mapping.Value.GetString()!.ShouldStartWith("https://raw.githubusercontent.com/alpercelik/salep/");
                }
            }
        }
        finally
        {
            Directory.Delete(output, recursive: true);
        }
    }

    private static async Task PackAsync(string msbuildProject, string tempOutDir)
    {
        var startInfo = new ProcessStartInfo("dotnet")
        {
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            UseShellExecute = false,
            CreateNoWindow = true
        };
        // Output isolation alone is insufficient: restore, compilation and publish staging
        // must also be private to this invocation, across every referenced project and TFM.
        foreach (var argument in new[]
        {
            "pack", msbuildProject, "--artifacts-path", Path.Combine(tempOutDir, "artifacts"),
            "--disable-build-servers", "-m:1", "-c", "Release", "-p:Version=9.9.9-test", "-o", tempOutDir,
            "-p:EnforceCodeStyleInBuild=false", "-p:TreatWarningsAsErrors=false"
        })
        {
            startInfo.ArgumentList.Add(argument);
        }
        startInfo.Environment["DOTNET_CLI_USE_MSBUILD_SERVER"] = "0";

        using var process = Process.Start(startInfo);
        process.ShouldNotBeNull();
        var cancellationToken = TestContext.Current.CancellationToken;
        var standardOutput = process.StandardOutput.ReadToEndAsync(cancellationToken);
        var standardError = process.StandardError.ReadToEndAsync(cancellationToken);
        try
        {
            await process.WaitForExitAsync(cancellationToken).WaitAsync(TimeSpan.FromMinutes(3), cancellationToken);
        }
        catch (TimeoutException)
        {
            if (!process.HasExited) process.Kill(entireProcessTree: true);
            await process.WaitForExitAsync(CancellationToken.None);
            throw new TimeoutException($"dotnet pack timed out:\n{await standardOutput}\n{await standardError}");
        }
        catch (OperationCanceledException)
        {
            if (!process.HasExited) process.Kill(entireProcessTree: true);
            await process.WaitForExitAsync(CancellationToken.None);
            throw;
        }

        var output = await standardOutput;
        var error = await standardError;
        process.ExitCode.ShouldBe(0, $"dotnet pack failed:\n{output}\n{error}");
    }

    private static string FindRepoRoot()
    {
        var current = AppContext.BaseDirectory;
        while (current != null && !File.Exists(Path.Combine(current, "src", "Salep.slnx")))
        {
            current = Directory.GetParent(current)?.FullName;
        }

        return current ?? throw new InvalidOperationException("Could not find repository root");
    }
}
