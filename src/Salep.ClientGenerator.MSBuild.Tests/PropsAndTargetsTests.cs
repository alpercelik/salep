using System.Xml.Linq;
using Shouldly;
using Xunit;

namespace Salep.ClientGenerator.MSBuild.Tests;

public class PropsAndTargetsTests
{
    [Fact]
    public void Salep_Props_Defines_Expected_Defaults_And_Tool_Paths()
    {
        var repoRoot = FindRepoRoot();
        var propsPath = Path.Combine(repoRoot, "src", "Salep.ClientGenerator.MSBuild", "Salep.ClientGenerator.props");
        File.Exists(propsPath).ShouldBeTrue();

        var text = File.ReadAllText(propsPath);
        var doc = XDocument.Parse(text);
        var root = doc.Root;
        root.ShouldNotBeNull();

        // Check property values
        var propertyGroups = root.Elements("PropertyGroup").ToList();
        propertyGroups.ShouldNotBeEmpty();

        var salepConfigFile = propertyGroups.Elements("SalepConfigFile").FirstOrDefault();
        salepConfigFile.ShouldNotBeNull();
        salepConfigFile.Value.Trim().ShouldBe("salep.json");

        var salepEnabled = propertyGroups.Elements("SalepEnabled").FirstOrDefault();
        salepEnabled.ShouldNotBeNull();
        salepEnabled.Value.Trim().ShouldBe("true");

        var toolPaths = propertyGroups.Elements("SalepToolPath").ToList();
        toolPaths.Count.ShouldBeGreaterThanOrEqualTo(2);
        toolPaths.Any(tp => tp.Value.Contains("tools/$(TargetFramework)/any")).ShouldBeTrue();
    }

    [Fact]
    public void Salep_Targets_Defines_Generation_Target_And_Cli_Invocation()
    {
        var repoRoot = FindRepoRoot();
        var targetsPath = Path.Combine(repoRoot, "src", "Salep.ClientGenerator.MSBuild", "Salep.ClientGenerator.targets");
        File.Exists(targetsPath).ShouldBeTrue();

        var text = File.ReadAllText(targetsPath);
        var doc = XDocument.Parse(text);
        var root = doc.Root;
        root.ShouldNotBeNull();

        var targets = root.Elements("Target").ToList();
        var generateTarget = targets.FirstOrDefault(t => t.Attribute("Name")?.Value == "SalepGenerate");
        generateTarget.ShouldNotBeNull();

        generateTarget.Attribute("BeforeTargets")?.Value.ShouldContain("CoreCompile");
        generateTarget.Attribute("Inputs").ShouldBeNull("Generation must not be skipped based on timestamps");
        generateTarget.Attribute("Outputs").ShouldBeNull("Generation must not be skipped based on a stamp file");

        var exec = generateTarget.Elements("Exec").FirstOrDefault();
        exec.ShouldNotBeNull();
        exec.Attribute("Command")?.Value.ShouldContain("dotnet exec");
        exec.Attribute("Command")?.Value.ShouldContain("$(_SalepCliDll)");

    }

    [Fact]
    public void Salep_Targets_Defines_IncludeGeneratedFiles_Target_With_Deduplication()
    {
        var repoRoot = FindRepoRoot();
        var targetsPath = Path.Combine(repoRoot, "src", "Salep.ClientGenerator.MSBuild", "Salep.ClientGenerator.targets");
        File.Exists(targetsPath).ShouldBeTrue();

        var text = File.ReadAllText(targetsPath);
        var doc = XDocument.Parse(text);
        var root = doc.Root;
        root.ShouldNotBeNull();

        var targets = root.Elements("Target").ToList();
        var includeTarget = targets.FirstOrDefault(t => t.Attribute("Name")?.Value == "_SalepIncludeGeneratedFiles");
        includeTarget.ShouldNotBeNull();

        includeTarget.Attribute("BeforeTargets")?.Value.ShouldContain("CoreCompile");
        includeTarget.Attribute("DependsOnTargets")?.Value.ShouldContain("SalepGenerate");

        // Verify Compile Remove is present before Compile Include to prevent duplicate CS2002 warnings
        var compileRemoves = includeTarget.Elements("ItemGroup").Elements("Compile").Where(c => c.Attribute("Remove") != null).ToList();
        compileRemoves.ShouldNotBeEmpty();
        compileRemoves.Any(c => c.Attribute("Remove")?.Value.Contains("@(_SalepGeneratedCsDistinct)") == true).ShouldBeTrue();

        var compileIncludes = includeTarget.Elements("ItemGroup").Elements("Compile").Where(c => c.Attribute("Include") != null).ToList();
        compileIncludes.ShouldNotBeEmpty();
        compileIncludes.Any(c => c.Attribute("Include")?.Value.Contains("@(_SalepGeneratedCsDistinct)") == true).ShouldBeTrue();
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
