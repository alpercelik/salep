using System.Security.Cryptography;
using System.Text.Json;
using Salep.ClientGenerator.Generation;
using Xunit;

namespace Salep.ClientGenerator.Tests;

public sealed class ScribanSampleIsolationTests
{
    [Theory]
    [InlineData("MinimalDependencies")]
    [InlineData("Opinionated")]
    public void Samples_generate_without_a_Roslyn_checkout_and_manifests_resolve_from_their_directory(string profile)
    {
        var repository = new DirectoryInfo(AppContext.BaseDirectory);
        while (repository is not null && !File.Exists(Path.Combine(repository.FullName, "Directory.Build.props"))) repository = repository.Parent;
        Assert.NotNull(repository);
        var root = Path.Combine(Path.GetTempPath(), "salep-detached-scriban-" + Guid.NewGuid().ToString("N"));
        var samples = Path.Combine(root, "src", "samples");
        try
        {
            var schemaDirectory = Path.Combine(samples, "Salep.Samples.GraphQLServer", "Generated");
            Directory.CreateDirectory(schemaDirectory);
            File.Copy(Path.Combine(repository.FullName, "src", "samples", "Salep.Samples.GraphQLServer", "Generated", "schema.graphql"), Path.Combine(schemaDirectory, "schema.graphql"));
            foreach (var project in new[] { "Client", "Module", "Client.Tests", "Module.Tests" })
            {
                var original = Path.Combine(repository.FullName, "src", "samples", profile, project);
                var detached = Path.Combine(samples, profile, project);
                Directory.CreateDirectory(detached);
                File.Copy(Path.Combine(original, "salep.json"), Path.Combine(detached, "salep.json"));
                if (Directory.Exists(Path.Combine(original, "graphql")))
                {
                    Directory.CreateDirectory(Path.Combine(detached, "graphql"));
                    foreach (var file in Directory.GetFiles(Path.Combine(original, "graphql"), "*.graphql"))
                        File.Copy(file, Path.Combine(detached, "graphql", Path.GetFileName(file)));
                }
            }
            Assert.False(Directory.Exists(Path.Combine(samples, "Roslyn")));
            foreach (var project in new[] { "Client", "Module", "Client.Tests", "Module.Tests" })
            {
                var directory = Path.Combine(samples, profile, project);
                ScribanGenerator.Generate(new("salep.json", directory, new("net11.0", "preview"), [samples]));
                var output = Path.Combine(directory, project.EndsWith(".Tests", StringComparison.Ordinal) ? "GeneratedTests" : "Generated");
                using var manifest = JsonDocument.Parse(File.ReadAllText(Path.Combine(output, ".salep.manifest.json")));
                var inputs = manifest.RootElement.GetProperty("Inputs").EnumerateObject().ToArray();
                Assert.Contains(inputs, input => input.Name.EndsWith("graphql/query.graphql", StringComparison.Ordinal));
                foreach (var input in inputs)
                {
                    Assert.False(Path.IsPathRooted(input.Name));
                    Assert.DoesNotContain("Roslyn", input.Name, StringComparison.Ordinal);
                    var path = Path.GetFullPath(input.Name, output);
                    Assert.True(File.Exists(path), path);
                    Assert.Equal(input.Value.GetString(), Convert.ToHexString(SHA256.HashData(File.ReadAllBytes(path))));
                }
                if (project == "Client") Assert.True(manifest.RootElement.GetProperty("Inputs").TryGetProperty("../graphql/query.graphql", out _));
            }
        }
        finally { if (Directory.Exists(root)) Directory.Delete(root, true); }
    }
}
